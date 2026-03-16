using System.ComponentModel.DataAnnotations;

namespace MarkBackend.DTOs
{
    public class ForgotPasswordDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        /// <summary>
        /// Frontend page that handles the reset form.
        /// Example: https://example.com/reset-password
        /// </summary>
        [Required]
        public string ClientUri { get; set; } = null!;
    }

    public class ResetPasswordDto
    {
        [Required]
        public string Email { get; set; } = null!;

        [Required]
        public string EncodedToken { get; set; } = null!;

        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; } = null!;

        [Required]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = null!;
    }
}