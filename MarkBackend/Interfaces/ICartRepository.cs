using MarkBackend.Models;
using MarkBackend.Models.Pages;

namespace MarkBackend.Interfaces
{
    public interface ICartRepository
    {
        Task<Cart?> GetActiveCartAsync(string userId);
        Task<Cart> GetOrCreateActiveCartAsync(string userId);
        Task<Cart?> GetCartByIdAsync(string cartId);
        Task<PagedList<Cart>> GetOrdersByUserAsync(string userId, QueryOptions options);
        Task<IEnumerable<Cart>> GetAllOrdersAsync();

        /// <summary>
        /// Transactionally adds quantity to a product line, creating it if absent.
        /// </summary>
        Task<(bool Success, string? Error)> AddToCartAsync(string userId, int productId, int quantity = 1);

        /// <summary>
        /// Transactionally decrements quantity by 1. Removes the line if quantity reaches 0.
        /// </summary>
        Task<(bool Success, string? Error)> DecrementFromCartAsync(string userId, int productId);

        /// <summary>
        /// Transactionally removes an entire product line regardless of quantity.
        /// </summary>
        Task<(bool Success, string? Error)> RemoveProductAsync(string userId, int productId);

        /// <summary>
        /// Sets a product line to an exact quantity. Removes the line if quantity is 0.
        /// Rejects if adding a new line would exceed the 1024-product cap.
        /// </summary>
        Task<(bool Success, string? Error)> SetItemQuantityAsync(string userId, int productId, int quantity);

        Task<bool> ConvertToOrderAsync(string userId);
        Task UpdateOrderStatusAsync(string cartId, OrderStatus status);
    }
}