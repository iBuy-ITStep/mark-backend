using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MarkBackend.Models;
using MarkBackend.DTOs;
using MarkBackend.ViewModels;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using MarkBackend.Helpers;

namespace MarkBackend.Controllers
{
    /// <summary>
    /// Handles user authentication, registration, and token management.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class AccountController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IConfiguration _config;
        private readonly EmailHelper _emailHelper;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccountController"/> class.
        /// </summary>
        /// <param name="userManager">The ASP.NET Core Identity user manager.</param>
        /// <param name="signInManager">The ASP.NET Core Identity sign-in manager.</param>
        /// <param name="config">The application configuration properties.</param>
        /// <param name="emailHelper">The helper service for dispatching emails.</param>
        public AccountController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IConfiguration config,
            EmailHelper emailHelper)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _config = config;
            _emailHelper = emailHelper;
        }

        /// <summary>
        /// Registers a new user account and dispatches a confirmation email.
        /// </summary>
        /// <param name="model">The registration credentials and client callback URI.</param>
        /// <returns>A status message indicating success or validation errors.</returns>
        /// <response code="200">Registration was successful and email was dispatched.</response>
        /// <response code="400">Validation failed or user already exists.</response>
        /// <response code="500">User was created but the email dispatch failed.</response>
        [HttpPost("register")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Register([FromBody] RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = new User { Email = model.Email, UserName = model.Email };
            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "User");

                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var encodedToken = Base64UrlEncoder.Encode(token);

                var confirmationLink = $"{model.ClientUri}?encodedToken={encodedToken}&email={Uri.EscapeDataString(user.Email)}";

                var emailResult = await _emailHelper.SendEmailRegistrationConfirm(user.Email, confirmationLink);

                if (!emailResult)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, new { message = "User created, but failed to send confirmation email." });
                }

                return Ok(new { message = "Registration successful. Please check your email to confirm." });
            }

            return BadRequest(result.Errors);
        }

        /// <summary>
        /// Authenticates a user and issues a JWT access token and refresh token.
        /// </summary>
        /// <param name="model">The user login credentials.</param>
        /// <returns>An object containing the JWT, refresh token, and expiry details.</returns>
        /// <response code="200">Authentication successful. Tokens returned.</response>
        /// <response code="401">Invalid credentials provided.</response>[HttpPost("login")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userManager.FindByEmailAsync(model.Email!);
            if (user == null)
                return Unauthorized(new { message = "Invalid email or password." });

            var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password!, false);
            if (!result.Succeeded)
                return Unauthorized(new { message = "Invalid email or password." });

            var jwtToken = await GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

            await _userManager.UpdateAsync(user);

            return Ok(new
            {
                token = jwtToken,
                refreshToken = refreshToken,
                expiration = DateTime.UtcNow.AddMinutes(30)
            });
        }

        /// <summary>
        /// Issues a new JWT access token using a valid refresh token.
        /// </summary>
        /// <param name="tokenModel">An object containing the expired access token and the active refresh token.</param>
        /// <returns>A new pair of access and refresh tokens.</returns>
        /// <response code="200">Tokens refreshed successfully.</response>
        /// <response code="400">Invalid or expired tokens provided.</response>
        [HttpPost("refresh-token")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RefreshToken([FromBody] TokenApiModel tokenModel)
        {
            if (tokenModel is null)
                return BadRequest("Invalid client request");

            var principal = GetPrincipalFromExpiredToken(tokenModel.AccessToken);
            if (principal == null)
                return BadRequest("Invalid access token or refresh token");

            var username = principal.Identity?.Name;
            var user = await _userManager.FindByNameAsync(username!);

            if (user == null || user.RefreshToken != tokenModel.RefreshToken || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            {
                return BadRequest("Invalid or expired refresh token");
            }

            var newAccessToken = await GenerateJwtToken(user);
            var newRefreshToken = GenerateRefreshToken();

            user.RefreshToken = newRefreshToken;
            await _userManager.UpdateAsync(user);

            return Ok(new
            {
                token = newAccessToken,
                refreshToken = newRefreshToken
            });
        }

        /// <summary>
        /// Revokes the current refresh token, effectively logging the user out across sessions.
        /// </summary>
        /// <returns>A success status message.</returns>
        /// <response code="200">Logout successful and token revoked.</response>
        /// <response code="401">The request lacks valid authentication credentials.</response>[HttpPost("logout")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Logout()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
                return BadRequest();

            var user = await _userManager.FindByNameAsync(username);
            if (user == null)
                return BadRequest();

            user.RefreshToken = null;
            await _userManager.UpdateAsync(user);

            return Ok(new { message = "Logged out successfully" });
        }

        /// <summary>
        /// Confirms a user's email address using the token provided via email.
        /// </summary>
        /// <param name="encodedToken">The Base64Url encoded confirmation token.</param>
        /// <param name="email">The email address of the user.</param>
        /// <returns>A status indicating whether the email was confirmed successfully.</returns>
        /// <response code="200">Email confirmed successfully.</response>
        /// <response code="400">Invalid token or email provided.</response>[HttpPost("confirm-email")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ConfirmEmail([FromQuery] string encodedToken, [FromQuery] string email)
        {
            if (string.IsNullOrEmpty(encodedToken) || string.IsNullOrEmpty(email))
                return BadRequest("Invalid confirmation request.");

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return BadRequest("Invalid user.");

            var decodedToken = Base64UrlEncoder.Decode(encodedToken);
            var result = await _userManager.ConfirmEmailAsync(user, decodedToken);

            if (result.Succeeded)
                return Ok(new { message = "Email confirmed successfully." });

            return BadRequest(result.Errors);
        }

        /// <summary>
        /// Sends a password reset link to the provided email address.
        /// Always returns 200 even if the email does not exist — prevents user enumeration.
        /// </summary>
        [HttpPost("forgot-password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // Always return 200 — never confirm whether an email exists in the system
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return Ok(new { message = "If that email exists, a reset link has been sent." });

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = Base64UrlEncoder.Encode(token);
            var resetLink = $"{model.ClientUri}?encodedToken={encodedToken}&email={Uri.EscapeDataString(user.Email!)}";

            await _emailHelper.SendEmailPasswordReset(user.Email!, resetLink);

            return Ok(new { message = "If that email exists, a reset link has been sent." });
        }

        /// <summary>
        /// Resets a user's password using the token received via email.
        /// </summary>
        [HttpPost("reset-password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return BadRequest(new { message = "Invalid request." });

            var decodedToken = Base64UrlEncoder.Decode(model.EncodedToken);
            var result = await _userManager.ResetPasswordAsync(user, decodedToken, model.NewPassword);

            if (result.Succeeded)
                return Ok(new { message = "Password reset successfully." });

            return BadRequest(result.Errors);
        }

        private async Task<string> GenerateJwtToken(User user)
        {
            var jwtSecret = _config.GetValue<string>("JwtSecret") ?? throw new InvalidOperationException("JwtSecret is missing");
            var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtSecret));
            var userRoles = await _userManager.GetRolesAsync(user);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.UserName!),
                new Claim(ClaimTypes.Email, user.Email!),
                new Claim(ClaimTypes.NameIdentifier, user.Id)
            };

            foreach (var role in userRoles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(30),
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }

        private static string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            var jwtSecret = _config.GetValue<string>("JwtSecret");

            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtSecret!)),
                ValidateLifetime = false
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);

            if (securityToken is not JwtSecurityToken jwtSecurityToken || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                throw new SecurityTokenException("Invalid token");

            return principal;
        }
    }

    /// <summary>
    /// Data Transfer Object utilized for requesting a new JWT via a refresh token.
    /// </summary>
    public class TokenApiModel
    {
        /// <summary>
        /// The expired JSON Web Token.
        /// </summary>
        public string AccessToken { get; set; } = null!;

        /// <summary>
        /// The active refresh token associated with the user session.
        /// </summary>
        public string RefreshToken { get; set; } = null!;
    }
}