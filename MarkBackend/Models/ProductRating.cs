namespace MarkBackend.Models
{
    /// <summary>
    /// 5-star rating system.
    /// A user may only rate a product they have previously purchased.
    /// Enforcement of the purchase requirement is handled in RatingsController.
    /// </summary>
    public class ProductRating
    {
        /// <summary>
        /// Unique user identifier.
        /// </summary>
        public string UserId { get; set; } = null!;

        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;

        /// <summary>
        /// Score from 1 to 5 inclusive.
        /// </summary>
        public int Score { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Set when the user updates a previously submitted rating.
        /// </summary>
        public DateTime? UpdatedAt { get; set; }
    }
}