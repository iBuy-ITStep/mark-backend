namespace MarkBackend.Models
{
    public class CartEntry
    {
        public string CartId { get; set; } = null!;
        public Cart Cart { get; set; } = null!;

        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public int Quantity { get; set; }
    }
}