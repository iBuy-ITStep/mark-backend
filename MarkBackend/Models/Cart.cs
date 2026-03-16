using System.ComponentModel.DataAnnotations;

namespace MarkBackend.Models
{
    public class Cart
    {
        [Key]
        public string CartId { get; set; } = Guid.NewGuid().ToString();

        public string OwnerId { get; set; } = null!;

        public DateTime TimestampLastUpdate { get; set; }

        public ICollection<CartEntry> CartEntries { get; set; } = new List<CartEntry>();

        public bool IsOrder { get; set; }

        public int StatusId { get; set; } = (int)OrderStatus.Processing;
        public CartOrderStatus? Status { get; set; }

        /// <summary>
        /// Cached count of distinct product lines in this cart.
        /// Maintained transactionally on every add/remove operation.
        /// Never exceeds <see cref="MaxDistinctProducts"/>.
        /// </summary>
        public int DistinctProductCount { get; set; }

        public const int MaxDistinctProducts = 1024;
    }
}