using MarkBackend.Data;
using MarkBackend.DTOs;
using MarkBackend.Models;
using MarkBackend.Models.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MarkBackend.Controllers
{
    /// <summary>
    /// Restricted to SuperAdmin. Manages assignment of elevated roles (Admin, Seller).
    /// Regular admins cannot promote other users — only the superadmin account can.
    /// </summary>
    [ApiController]
    [Route("api/superadmin")]
    [Authorize(Roles = "SuperAdmin")]
    [Produces("application/json")]
    public class SuperAdminController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly ApplicationContext _context;

        // Roles that SuperAdmin is allowed to grant or revoke.
        // "SuperAdmin" is intentionally absent — it cannot be self-replicated via API.
        private static readonly HashSet<string> ManageableRoles =
            new(StringComparer.OrdinalIgnoreCase) { "Admin", "Seller" };

        public SuperAdminController(UserManager<User> userManager, ApplicationContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        /// <summary>
        /// Returns all users along with their current roles.
        /// Paginated endpoint.
        /// </summary>
        [HttpGet("users")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllUsers([FromQuery] QueryOptions options)
        {
            var paged = await PagedList<User>.CreateAsync(_userManager.Users, options);

            var userIds = paged.Items.Select(u => u.Id).ToList();

            var flatUserRoles = await _context.UserRoles
                .Where(ur => userIds.Contains(ur.UserId))
                .Join(_context.Roles,
                    ur => ur.RoleId,
                    r => r.Id,
                    (ur, r) => new { ur.UserId, RoleName = r.Name! })
                .ToListAsync();

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
        /// Grants a role (Admin or Seller) to a user.
        /// Returns 400 if the role is not in the manageable set.
        /// </summary>
        [HttpPost("users/{userId}/roles/{role}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GrantRole(string userId, string role)
        {
            if (!ManageableRoles.Contains(role))
                return BadRequest(new { message = $"Role '{role}' cannot be assigned via this endpoint. Allowed: Admin, Seller." });

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            if (await _userManager.IsInRoleAsync(user, role))
                return BadRequest(new { message = $"User already has the '{role}' role." });

            await _userManager.AddToRoleAsync(user, role);
            return NoContent();
        }

        /// <summary>
        /// Revokes a role (Admin or Seller) from a user.
        /// </summary>
        [HttpDelete("users/{userId}/roles/{role}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RevokeRole(string userId, string role)
        {
            if (!ManageableRoles.Contains(role))
                return BadRequest(new { message = $"Role '{role}' cannot be revoked via this endpoint. Allowed: Admin, Seller." });

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            if (!await _userManager.IsInRoleAsync(user, role))
                return BadRequest(new { message = $"User does not have the '{role}' role." });

            await _userManager.RemoveFromRoleAsync(user, role);
            return NoContent();
        }

        /// <summary>
        /// Replaces a user's full role set in one call.
        /// Only roles within the manageable set (Admin, Seller) are affected.
        /// The User role and SuperAdmin role are never touched by this endpoint.
        /// </summary>
        [HttpPut("users/{userId}/roles")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetRoles(string userId, [FromBody] ChangeRolesDto dto)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            // Silently ignore any roles outside the manageable set in the request body
            var requestedManageable = dto.Roles
                .Where(r => ManageableRoles.Contains(r))
                .ToList();

            var currentRoles = await _userManager.GetRolesAsync(user);

            // Only operate within manageable roles — never touch User or SuperAdmin
            var currentManageable = currentRoles
                .Where(r => ManageableRoles.Contains(r))
                .ToList();

            var toAdd = requestedManageable.Except(currentManageable);
            var toRemove = currentManageable.Except(requestedManageable);

            await _userManager.AddToRolesAsync(user, toAdd);
            await _userManager.RemoveFromRolesAsync(user, toRemove);

            return NoContent();
        }
    }
}