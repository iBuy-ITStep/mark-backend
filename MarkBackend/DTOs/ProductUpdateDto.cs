using System.ComponentModel.DataAnnotations;

namespace MarkBackend.DTOs
{
    public class ProductUpdateDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = null!;

        [StringLength(10000, ErrorMessage = "Description cannot exceed 10 000 characters.")]
        public string? Description { get; set; }

        [Required]
        [Range(0.01, (double)decimal.MaxValue, ErrorMessage = "Price must be greater than zero.")]
        public decimal Price { get; set; }

        [Required]
        public int CategoryId { get; set; }

        [Required]
        public int BrandId { get; set; }

        /// <summary>
        /// Pass a new preview image ID to replace the existing one.
        /// Omit (null) to keep the current preview.
        /// </summary>
        public Guid? NewPreviewImageId { get; set; }
    }
}