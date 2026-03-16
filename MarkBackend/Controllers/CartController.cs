using MarkBackend.DTOs;
using MarkBackend.Interfaces;
using MarkBackend.Models;
using MarkBackend.Models.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MarkBackend.Controllers
{
    /// <summary>
    /// Manages the user's shopping cart and order history.
    /// All endpoints require authentication.
    /// </summary>
    [ApiController]
    [Route("api/cart")]
    [Authorize]
    [Produces("application/json")]
    public class CartController : ControllerBase
    {
        private readonly ICartRepository _carts;

        public CartController(ICartRepository carts) => _carts = carts;

        /// <summary>Returns the current user's active cart.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCart()
        {
            var userId = GetUserId();
            var cart = await _carts.GetOrCreateActiveCartAsync(userId);
            return Ok(MapToDto(cart));
        }

        /// <summary>
        /// Adds a product to the cart or increments its quantity.
        /// Returns 400 if the 1024-product cap would be exceeded.
        /// </summary>
        [HttpPost("items")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AddItem([FromBody] AddToCartDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var (success, error) = await _carts.AddToCartAsync(GetUserId(), dto.ProductId, dto.Quantity);
            if (!success) return BadRequest(new { message = error });

            return Ok(new { message = "Item added to cart." });
        }

        /// <summary>Decrements a product's quantity by 1. Removes the line at 0.</summary>
        [HttpDelete("items/{productId:int}/one")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DecrementItem(int productId)
        {
            var (success, error) = await _carts.DecrementFromCartAsync(GetUserId(), productId);
            if (!success) return BadRequest(new { message = error });

            return Ok(new { message = "Item quantity decremented." });
        }

        /// <summary>Removes an entire product line from the cart.</summary>
        [HttpDelete("items/{productId:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RemoveItem(int productId)
        {
            var (success, error) = await _carts.RemoveProductAsync(GetUserId(), productId);
            if (!success) return BadRequest(new { message = error });

            return Ok(new { message = "Item removed from cart." });
        }

        /// <summary>
        /// Converts the active cart into an order.
        /// </summary>
        [HttpPost("checkout")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Checkout()
        {
            var success = await _carts.ConvertToOrderAsync(GetUserId());
            if (!success) return BadRequest(new { message = "Cart is empty or already an order." });

            return Ok(new { message = "Order placed successfully." });
        }

        /// <summary>
        /// Sets a product's quantity in the cart. Send quantity 0 to remove the line.
        /// Returns 400 if adding a new line would exceed the 1024-product cap.
        /// </summary>
        [HttpPut("items/{productId:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SetItemQuantity(int productId, [FromBody] SetCartItemQuantityDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var (success, error) = await _carts.SetItemQuantityAsync(GetUserId(), productId, dto.Quantity);
            if (!success) return BadRequest(new { message = error });

            return Ok(new { message = dto.Quantity == 0 ? "Item removed." : "Quantity updated." });
        }

        /// <summary>
        /// Returns the current user's order history, paginated.
        /// </summary>
        [HttpGet("orders")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetOrders([FromQuery] QueryOptions options)
        {
            var paged = await _carts.GetOrdersByUserAsync(GetUserId(), options);

            return Ok(new
            {
                paged.CurrentPage,
                paged.TotalPages,
                paged.PageSize,
                paged.HasPreviousPage,
                paged.HasNextPage,
                Items = paged.Items.Select(MapToDto)
            });
        }

        // ── Admin / Seller ────────────────────────────────────────────────────

        /// <summary>
        /// Returns all orders placed on the platform.
        /// </summary>
        [HttpGet("orders/all")]
        [Authorize(Roles = "Admin,Seller")]
        [ProducesResponseType(typeof(IEnumerable<CartDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllOrders()
        {
            var orders = await _carts.GetAllOrdersAsync();
            return Ok(orders.Select(MapToDto));
        }

        /// <summary>
        /// Advances an order's status through: В обработке → В пути → Отправлен
        /// </summary>
        [HttpPut("orders/{cartId}/status")]
        [Authorize(Roles = "Admin,Seller")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AdvanceStatus(string cartId)
        {
            var cart = await _carts.GetCartByIdAsync(cartId);
            if (cart == null || !cart.IsOrder) return NotFound();

            var next = (OrderStatus)cart.StatusId switch
            {
                OrderStatus.Processing => OrderStatus.InTransit,
                OrderStatus.InTransit => OrderStatus.Delivered,
                _ => OrderStatus.Delivered
            };

            await _carts.UpdateOrderStatusAsync(cartId, next);
            return NoContent();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private string GetUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException();

        private static CartDto MapToDto(Cart cart) => new()
        {
            CartId = cart.CartId,
            TimestampLastUpdate = cart.TimestampLastUpdate,
            DistinctProductCount = cart.DistinctProductCount,
            MaxDistinctProducts = Cart.MaxDistinctProducts,
            IsOrder = cart.IsOrder,
            Status = cart.Status?.Name ?? "Unknown",
            Items = cart.CartEntries.Select(e => new CartEntryDto
            {
                ProductId = e.ProductId,
                ProductName = e.Product?.Name ?? string.Empty,
                ProductPrice = e.Product?.Price ?? 0,
                ProductPreviewImageId = e.Product?.PreviewImageId,
                Quantity = e.Quantity
            }).ToList(),
            TotalPrice = cart.CartEntries.Sum(e => (e.Product?.Price ?? 0) * e.Quantity)
        };
    }
}