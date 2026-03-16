using System.ComponentModel.DataAnnotations;

namespace MarkBackend.Models
{
    /// <summary>
    /// Generic marketplace product. Specific technical attributes (RAM, storage, etc.)
    /// are intentionally excluded to keep this model universal.
    /// </summary>
    public class Product
    {
        public int Id { get; set; }

        [MaxLength(100)]
        public string Name { get; set; } = null!;

        /// <summary>
        /// Rich-text HTML description. Hard-capped at 10 000 characters.
        /// May contain up to 10 embedded images referenced as /api/images/{guid}.
        /// </summary>
        [MaxLength(10000)]
        public string Description { get; set; } = null!;

        public decimal Price { get; set; }

        public DateTime DateOfCreation { get; set; }

        // === Preview image ===
        /// <summary>
        /// FK to ProductImage where IsPreview = true.
        /// Null until a preview is uploaded.
        /// </summary>
        public Guid? PreviewImageId { get; set; }

        public int CategoryId { get; set; }
        public Category Category { get; set; } = null!;

        public int BrandId { get; set; }
        public Brand Brand { get; set; } = null!;

        // === Stock ===
        /// <summary>
        /// Available unit count. Frontend shows "In Stock" / "Out of Stock" based on whether this is > 0.
        /// Admin panel shows the exact number.
        /// </summary>
        public int StockQuantity { get; set; } = 0;

        // === Rating ===
        // Denormalized per-star counters.
        // Frontend computes the average rating and total count from these for display.
        public int Rating1Count { get; set; } = 0;
        public int Rating2Count { get; set; } = 0;
        public int Rating3Count { get; set; } = 0;
        public int Rating4Count { get; set; } = 0;
        public int Rating5Count { get; set; } = 0;
    }
}