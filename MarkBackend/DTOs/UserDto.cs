namespace MarkBackend.DTOs
{
    /// <summary>
    /// User representation returned by admin endpoints. No credentials.
    /// </summary>
    public class UserDto
    {
        public string Id { get; set; } = null!;
        public string Email { get; set; } = null!;
        public bool EmailConfirmed { get; set; }
        public IList<string> Roles { get; set; } = new List<string>();
    }

    public class UserCreateDto
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.EmailAddress]
        public string Email { get; set; } = null!;

        [System.ComponentModel.DataAnnotations.Required]
        public string Password { get; set; } = null!;
    }

    public class UserUpdateEmailDto
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.EmailAddress]
        public string NewEmail { get; set; } = null!;
    }

    public class ChangeRolesDto
    {
        /// <summary>
        /// The complete desired role list. Roles absent from this list will be removed!
        /// </summary>
        public List<string> Roles { get; set; } = new();
    }
}