using MarkBackend.Data;
using MarkBackend.Interfaces;
using MarkBackend.Models;
using MarkBackend.Models.Pages;
using Microsoft.EntityFrameworkCore;

namespace MarkBackend.Repositories
{
    public class CartRepository : ICartRepository
    {
        private readonly ApplicationContext _context;

        public CartRepository(ApplicationContext context) => _context = context;

        public async Task<Cart?> GetActiveCartAsync(string userId) =>
            await _context.Carts
                .Include(c => c.CartEntries)
                    .ThenInclude(e => e.Product)
                .Where(c => c.OwnerId == userId && !c.IsOrder)
                .FirstOrDefaultAsync();

        public async Task<Cart> GetOrCreateActiveCartAsync(string userId)
        {
            var cart = await GetActiveCartAsync(userId);
            if (cart != null) return cart;

            cart = new Cart
            {
                CartId = Guid.NewGuid().ToString(),
                OwnerId = userId,
                TimestampLastUpdate = DateTime.UtcNow
            };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();
            return cart;
        }

        public async Task<Cart?> GetCartByIdAsync(string cartId) =>
            await _context.Carts
                .Include(c => c.CartEntries)
                    .ThenInclude(e => e.Product)
                .FirstOrDefaultAsync(c => c.CartId == cartId);



        public async Task<PagedList<Cart>> GetOrdersByUserAsync(string userId, QueryOptions options)
        {
            var query = _context.Carts
                .Include(c => c.CartEntries)
                    .ThenInclude(e => e.Product)
                    .Include(c => c.Status)
                .Where(c => c.OwnerId == userId && c.IsOrder)
                .OrderByDescending(c => c.TimestampLastUpdate)
                .AsQueryable();

            return await PagedList<Cart>.CreateAsync(query, options);
        }

        public async Task<IEnumerable<Cart>> GetAllOrdersAsync() =>
            await _context.Carts
                .Include(c => c.CartEntries)
                    .ThenInclude(e => e.Product)
                    .Include(c => c.Status)
                .Where(c => c.IsOrder)
                .OrderByDescending(c => c.TimestampLastUpdate)
                .ToListAsync();

        public async Task<(bool Success, string? Error)> AddToCartAsync(
            string userId, int productId, int quantity = 1)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // Lock the cart row for transaction
                var cart = await _context.Carts
                    .Include(c => c.CartEntries)
                    .Where(c => c.OwnerId == userId && !c.IsOrder)
                    .FirstOrDefaultAsync();

                if (cart == null)
                {
                    cart = new Cart
                    {
                        CartId = Guid.NewGuid().ToString(),
                        OwnerId = userId,
                        TimestampLastUpdate = DateTime.UtcNow
                    };
                    _context.Carts.Add(cart);
                    await _context.SaveChangesAsync();
                }

                var entry = cart.CartEntries.FirstOrDefault(e => e.ProductId == productId);

                if (entry == null)
                {
                    if (cart.DistinctProductCount >= Cart.MaxDistinctProducts)
                    {
                        await tx.RollbackAsync();
                        return (false, $"Cart is full. Maximum {Cart.MaxDistinctProducts} distinct products allowed.");
                    }

                    _context.CartEntries.Add(new CartEntry
                    {
                        CartId = cart.CartId,
                        ProductId = productId,
                        Quantity = quantity
                    });
                    cart.DistinctProductCount++;
                }
                else
                {
                    entry.Quantity += quantity;
                }

                cart.TimestampLastUpdate = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return (true, null);
            }
            catch
            {
                await tx.RollbackAsync();
                return (false, "An error occurred while updating the cart.");
            }
        }

        public async Task<(bool Success, string? Error)> DecrementFromCartAsync(
            string userId, int productId)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var cart = await _context.Carts
                    .Include(c => c.CartEntries)
                    .Where(c => c.OwnerId == userId && !c.IsOrder)
                    .FirstOrDefaultAsync();

                if (cart == null) return (false, "Cart not found.");

                var entry = cart.CartEntries.FirstOrDefault(e => e.ProductId == productId);
                if (entry == null) return (false, "Product not in cart.");

                if (entry.Quantity > 1)
                {
                    entry.Quantity--;
                }
                else
                {
                    _context.CartEntries.Remove(entry);
                    cart.DistinctProductCount = Math.Max(0, cart.DistinctProductCount - 1);
                }

                cart.TimestampLastUpdate = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return (true, null);
            }
            catch
            {
                await tx.RollbackAsync();
                return (false, "An error occurred while updating the cart.");
            }
        }

        public async Task<(bool Success, string? Error)> RemoveProductAsync(
            string userId, int productId)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var cart = await _context.Carts
                    .Include(c => c.CartEntries)
                    .Where(c => c.OwnerId == userId && !c.IsOrder)
                    .FirstOrDefaultAsync();

                if (cart == null) return (false, "Cart not found.");

                var entry = cart.CartEntries.FirstOrDefault(e => e.ProductId == productId);
                if (entry == null) return (false, "Product not in cart.");

                _context.CartEntries.Remove(entry);
                cart.DistinctProductCount = Math.Max(0, cart.DistinctProductCount - 1);
                cart.TimestampLastUpdate = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return (true, null);
            }
            catch
            {
                await tx.RollbackAsync();
                return (false, "An error occurred while updating the cart.");
            }
        }

        public async Task<(bool Success, string? Error)> SetItemQuantityAsync(
            string userId, int productId, int quantity)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var cart = await _context.Carts
                    .Include(c => c.CartEntries)
                    .Where(c => c.OwnerId == userId && !c.IsOrder)
                    .FirstOrDefaultAsync();

                if (cart == null)
                {
                    if (quantity == 0) return (true, null); // Nothing to do

                    cart = new Cart
                    {
                        CartId = Guid.NewGuid().ToString(),
                        OwnerId = userId,
                        TimestampLastUpdate = DateTime.UtcNow
                    };
                    _context.Carts.Add(cart);
                    await _context.SaveChangesAsync();
                }

                var entry = cart.CartEntries.FirstOrDefault(e => e.ProductId == productId);

                if (quantity == 0)
                {
                    if (entry != null)
                    {
                        _context.CartEntries.Remove(entry);
                        cart.DistinctProductCount = Math.Max(0, cart.DistinctProductCount - 1);
                    }
                }
                else if (entry == null)
                {
                    if (cart.DistinctProductCount >= Cart.MaxDistinctProducts)
                    {
                        await tx.RollbackAsync();
                        return (false, $"Cart is full. Maximum {Cart.MaxDistinctProducts} distinct products allowed.");
                    }

                    _context.CartEntries.Add(new CartEntry
                    {
                        CartId = cart.CartId,
                        ProductId = productId,
                        Quantity = quantity
                    });
                    cart.DistinctProductCount++;
                }
                else
                {
                    entry.Quantity = quantity;
                }

                cart.TimestampLastUpdate = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return (true, null);
            }
            catch
            {
                await tx.RollbackAsync();
                return (false, "An error occurred while updating the cart.");
            }
        }

        public async Task<bool> ConvertToOrderAsync(string userId)
        {
            var cart = await _context.Carts
                .Where(c => c.OwnerId == userId && !c.IsOrder)
                .FirstOrDefaultAsync();

            if (cart == null || !cart.CartEntries.Any()) return false;

            cart.IsOrder = true;
            cart.TimestampLastUpdate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task UpdateOrderStatusAsync(string cartId, OrderStatus status)
        {
            var cart = await _context.Carts.FindAsync(cartId);
            if (cart != null && cart.IsOrder)
            {
                cart.StatusId = (int)status;
                await _context.SaveChangesAsync();
            }
        }
    }
}