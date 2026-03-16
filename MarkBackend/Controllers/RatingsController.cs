using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MarkBackend.Data;
using MarkBackend.Models;

namespace MarkBackend.Controllers
{
    /// <summary>
    /// Handles the 5-star product rating system.
    /// Only users who have a completed order containing the product may submit a rating.
    /// </summary>
    [ApiController]
    [Route("api/ratings")]
    [Produces("application/json")]
    public class RatingsController : ControllerBase
    {
        private readonly ApplicationContext _context;

        public RatingsController(ApplicationContext context) => _context = context;

        /// <summary>
        /// Submits or updates a rating for a product (1–5 stars).
        /// Requires the user to have purchased the product in a completed order.
        /// On update, the previous star counter is decremented and the new one incremented.
        /// </summary>
        [HttpPost("{productId:int}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Rate(int productId, [FromBody] RateDto dto)
        {
            if (dto.Score < 1 || dto.Score > 5)
                return BadRequest(new { message = "Score must be between 1 and 5." });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var product = await _context.Products.FindAsync(productId);
            if (product == null) return NotFound();

            // Only verified buyers may rate
            var hasPurchased = await _context.Carts
                .Where(c => c.OwnerId == userId && c.IsOrder)
                .AnyAsync(c => c.CartEntries.Any(e => e.ProductId == productId));

            if (!hasPurchased)
                return Forbid();

            var existing = await _context.ProductRatings
                .FirstOrDefaultAsync(r => r.UserId == userId && r.ProductId == productId);

            if (existing == null)
            {
                _context.ProductRatings.Add(new ProductRating
                {
                    UserId = userId,
                    ProductId = productId,
                    Score = dto.Score,
                    CreatedAt = DateTime.UtcNow
                });
                AdjustStarCount(product, dto.Score, +1);
            }
            else
            {
                // Swap counters: undo old score, apply new score
                AdjustStarCount(product, existing.Score, -1);
                AdjustStarCount(product, dto.Score, +1);
                existing.Score = dto.Score;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                product.Rating1Count,
                product.Rating2Count,
                product.Rating3Count,
                product.Rating4Count,
                product.Rating5Count
            });
        }

        /// <summary>Returns the authenticated user's existing rating for a product, if any.</summary>
        [HttpGet("{productId:int}/my")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMyRating(int productId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var rating = await _context.ProductRatings
                .FirstOrDefaultAsync(r => r.UserId == userId && r.ProductId == productId);

            if (rating == null) return NotFound();
            return Ok(new { rating.Score, rating.CreatedAt, rating.UpdatedAt });
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void AdjustStarCount(Product product, int score, int delta)
        {
            switch (score)
            {
                case 1: product.Rating1Count = Math.Max(0, product.Rating1Count + delta); break;
                case 2: product.Rating2Count = Math.Max(0, product.Rating2Count + delta); break;
                case 3: product.Rating3Count = Math.Max(0, product.Rating3Count + delta); break;
                case 4: product.Rating4Count = Math.Max(0, product.Rating4Count + delta); break;
                case 5: product.Rating5Count = Math.Max(0, product.Rating5Count + delta); break;
            }
        }
    }

    public class RateDto
    {
        /// <summary>Rating score from 1 to 5.</summary>
        public int Score { get; set; }
    }
}