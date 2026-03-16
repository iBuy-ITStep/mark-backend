namespace MarkBackend.Models
{
    /// <summary>
    /// Lookup table mapping order status IDs to display strings.
    /// Edit the Name here to change what the frontend sees — no code changes needed.
    /// </summary>
    public class CartOrderStatus
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
    }

    /// <summary>
    /// Mirrors the CartOrderStatus table. Use this enum in code instead of magic numbers.
    /// The integer values must always match the seeded rows in ApplicationContext.
    /// </summary>
    public enum OrderStatus
    {
        Processing = 1,
        InTransit = 2,
        Delivered = 3
    }
}