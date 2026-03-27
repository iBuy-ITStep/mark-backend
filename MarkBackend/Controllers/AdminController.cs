using MarkBackend.Data;
using MarkBackend.DTOs;
using MarkBackend.Models;
using MarkBackend.Models.Pages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

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
        private readonly ApplicationContext _context;

        public AdminController(UserManager<User> userManager, RoleManager<IdentityRole> roleManager, ApplicationContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        /// <summary>
        /// Returns a paginated list of all registered users.
        /// </summary>
        [HttpGet("users")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUsers([FromQuery] QueryOptions options)
        {
            var paged = await PagedList<User>.CreateAsync(_userManager.Users, options);

            var userIds = paged.Items.Select(u => u.Id).ToList();
            
            // 1. Fetch the flat data from the database into memory first
            var flatUserRoles = await _context.UserRoles
                .Where(ur => userIds.Contains(ur.UserId))
                .Join(_context.Roles,
                    ur => ur.RoleId,
                    r => r.Id,
                    (ur, r) => new { ur.UserId, RoleName = r.Name! })
                .ToListAsync();

            // 2. Perform the GroupBy and ToDictionary in memory using standard LINQ
            var rolesByUser = flatUserRoles
                .GroupBy(x => x.UserId)
                .ToDictionary(
                    g => g.Key,
                    g => (IList<string>)g.Select(x => x.RoleName).ToList()
                );
            
            var userDtos = paged.Items.Select(u => new UserDto
            {
                Id = u.Id,
                Email = u.Email!,
                EmailConfirmed = u.EmailConfirmed,
                Roles = rolesByUser.TryGetValue(u.Id, out var roles) ? roles : new List<string>()
            }).ToList();

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
        /// Creates a new verified user and assigns the default "User" role.
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
        /// roles absent from the list are removed, roles present are added if missing. Assignment of the "SuperAdmin" role is prohibited via this endpoint for security reasons.
        /// </summary>
        /// <param name="userId">The ID of the user whose roles are being updated.</param>
        /// <param name="dto">A DTO containing the list of roles to assign to the user.</param>
        /// <returns>
        /// No content on success, 404 if the user does not exist, or 400 if the request is invalid.
        /// </returns>
        [HttpPut("users/{userId}/roles")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetUserRoles(string userId, [FromBody] ChangeRolesDto dto)
        {
            if (dto.Roles.Contains("SuperAdmin", StringComparer.OrdinalIgnoreCase))
                return BadRequest(new { message = "SuperAdmin role cannot be assigned via this endpoint." });

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