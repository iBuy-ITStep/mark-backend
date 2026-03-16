namespace MarkBackend.DTOs
{
    /// <summary>
    /// Lightweight product data for rendering a product card or hover tooltip.
    /// Contains only the fields needed for a list view.
    /// </summary>
    public class ProductTooltipDto
    {
        public int Id { get; set; }

        /// <summary>
        /// Truncated to 60 characters by the controller for compact display.
        /// </summary>
        public string Name { get; set; } = null!;
        public decimal Price { get; set; }
        
        /// <summary>
        /// Fetch the actual image via GET /api/images/{PreviewImageId}
        /// </summary>
        public Guid? PreviewImageId { get; set; }
        public string CategoryName { get; set; } = null!;
        public string BrandName { get; set; } = null!;
        public bool InStock { get; set; }
        public int Rating1Count { get; set; }
        public int Rating2Count { get; set; }
        public int Rating3Count { get; set; }
        public int Rating4Count { get; set; }
        public int Rating5Count { get; set; }
    }
}