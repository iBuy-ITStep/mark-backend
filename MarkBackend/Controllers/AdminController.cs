using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MarkBackend.DTOs;
using MarkBackend.Models;
using MarkBackend.Models.Pages;

namespace MarkBackend.Controllers
{
    /// <summary>
    /// Administrative endpoints for user management. Restricted to the Admin role.
    /// </summary>
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "Admin")]
    [Produces("application/json")]
    public class AdminController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(UserManager<User> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        /// <summary>
        /// Returns a paginated list of all registered users.
        /// </summary>
        [HttpGet("users")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUsers([FromQuery] QueryOptions options)
        {
            // PagedList works on IQueryable, so we pass the EF-backed Users queryable
            var paged = await PagedList<User>.CreateAsync(_userManager.Users, options);

            // Map to DTO and resolve roles per user
            var userDtos = new List<UserDto>();
            foreach (var user in paged.Items)
            {
                userDtos.Add(new UserDto
                {
                    Id = user.Id,
                    Email = user.Email!,
                    EmailConfirmed = user.EmailConfirmed,
                    Roles = await _userManager.GetRolesAsync(user)
                });
            }

            return Ok(new
            {
                paged.CurrentPage,
                paged.TotalPages,
                paged.PageSize,
                paged.HasPreviousPage,
                paged.HasNextPage,
                Items = userDtos
            });
        }

        /// <summary>
        /// Returns a single user by ID including their roles.
        /// </summary>
        [HttpGet("users/{userId}")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            return Ok(new UserDto
            {
                Id = user.Id,
                Email = user.Email!,
                EmailConfirmed = user.EmailConfirmed,
                Roles = await _userManager.GetRolesAsync(user)
            });
        }

        /// <summary>
        /// Creates a new user and assigns the default "User" role.
        /// </summary>
        [HttpPost("users")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateUser([FromBody] UserCreateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var existing = await _userManager.FindByEmailAsync(dto.Email);
            if (existing != null)
                return BadRequest(new { message = "A user with this email already exists." });

            var user = new User { Email = dto.Email, UserName = dto.Email };
            var result = await _userManager.CreateAsync(user, dto.Password);

            if (!result.Succeeded) return BadRequest(result.Errors);

            await _userManager.AddToRoleAsync(user, "User");

            return CreatedAtAction(nameof(GetUser), new { userId = user.Id }, new UserDto
            {
                Id = user.Id,
                Email = user.Email!,
                EmailConfirmed = user.EmailConfirmed,
                Roles = new List<string> { "User" }
            });
        }

        /// <summary>
        /// Updates a user's email address.
        /// </summary>
        [HttpPut("users/{userId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateUser(string userId, [FromBody] UserUpdateEmailDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            user.Email = dto.NewEmail;
            user.UserName = dto.NewEmail;
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded) return BadRequest(result.Errors);
            return NoContent();
        }

        /// <summary>
        /// Deletes a user account permanently.
        /// </summary>
        [HttpDelete("users/{userId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            await _userManager.DeleteAsync(user);
            return NoContent();
        }

        /// <summary>
        /// Returns all available roles in the system.
        /// </summary>
        [HttpGet("roles")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetAllRoles()
        {
            var roles = _roleManager.Roles.Select(r => new { r.Id, r.Name }).ToList();
            return Ok(roles);
        }

        /// <summary>
        /// Sets a user's roles. The provided list is treated as the complete desired state —
        /// roles absent from the list are removed, roles present are added if missing.
        /// </summary>
        [HttpPut("users/{userId}/roles")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetUserRoles(string userId, [FromBody] ChangeRolesDto dto)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var currentRoles = await _userManager.GetRolesAsync(user);
            var toAdd = dto.Roles.Except(currentRoles);
            var toRemove = currentRoles.Except(dto.Roles);

            await _userManager.AddToRolesAsync(user, toAdd);
            await _userManager.RemoveFromRolesAsync(user, toRemove);

            return NoContent();
        }
    }
}