namespace MarkBackend.DTOs
{
    public class ProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Description { get; set; } = null!;
        public decimal Price { get; set; }

        /// <summary>
        /// Fetch the actual image via GET /api/images/{PreviewImageId}
        /// </summary>
        public Guid? PreviewImageId { get; set; }

        public DateTime DateOfCreation { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = null!;
        public int BrandId { get; set; }
        public string BrandName { get; set; } = null!;

        // Stock
        /// <summary>
        /// True if StockQuantity > 0. Admins receive the raw number separately.
        /// </summary>
        public bool InStock { get; set; }
        public int StockQuantity { get; set; }

        // Rating — frontend computes average and total from these
        public int Rating1Count { get; set; }
        public int Rating2Count { get; set; }
        public int Rating3Count { get; set; }
        public int Rating4Count { get; set; }
        public int Rating5Count { get; set; }
    }
}