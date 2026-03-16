using System.ComponentModel.DataAnnotations;

namespace MarkBackend.ViewModels
{
    /// <summary>
    /// Model used for registering a new user.
    /// </summary>
    public class RegisterViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!; [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = null!;

        [Required]
        [Compare("Password", ErrorMessage = "The passwords do not match.")]
        [DataType(DataType.Password)]
        public string PasswordConfirm { get; set; } = null!;

        /// <summary>
        /// The URI of the client application where the user should be redirected after clicking the email link.
        /// Example: https://mywebsite.com/confirm-email
        /// </summary>
        [Required]
        public string ClientUri { get; set; } = null!;
    }
}
