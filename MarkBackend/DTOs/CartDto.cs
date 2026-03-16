using System.ComponentModel.DataAnnotations;

namespace MarkBackend.DTOs
{
    public class CartDto
    {
        public string CartId { get; set; } = null!;
        public DateTime TimestampLastUpdate { get; set; }
        public int DistinctProductCount { get; set; }
        public int MaxDistinctProducts { get; set; } = 1024;
        public bool IsOrder { get; set; }
        public string Status { get; set; } = null!;
        public List<CartEntryDto> Items { get; set; } = new();
        public decimal TotalPrice { get; set; }
    }

    public class CartEntryDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = null!;
        public decimal ProductPrice { get; set; }
        public Guid? ProductPreviewImageId { get; set; }
        public int Quantity { get; set; }
        public decimal Subtotal => ProductPrice * Quantity;
    }

    public class AddToCartDto
    {
        [Required]
        public int ProductId { get; set; }

        [Range(1, 1024)]
        public int Quantity { get; set; } = 1;
    }

    public class SetCartItemQuantityDto
    {
        /// <summary>
        /// Target quantity. Send 0 to remove the product line entirely.
        /// </summary>
        [Range(0, 1024)]
        public int Quantity { get; set; }
    }
}