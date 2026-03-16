using System.ComponentModel.DataAnnotations;

namespace MarkBackend.DTOs
{
    /// <summary>
    /// Payload for creating a new product. Image is uploaded separately via /api/images/upload.
    /// </summary>
    public class ProductCreateDto
    {
        [Required]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
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
        /// ID returned by POST /api/images/upload/preview.
        /// Upload the preview first, then pass its ID here.
        /// </summary>
        public Guid? PreviewImageId { get; set; }
    }
}