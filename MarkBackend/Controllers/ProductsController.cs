using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MarkBackend.DTOs;
using MarkBackend.Interfaces;
using MarkBackend.Models;
using MarkBackend.Models.Pages;

namespace MarkBackend.Controllers
{
    /// <summary>
    /// Product catalogue. All read endpoints are public.
    /// Write endpoints require Admin or Seller role.
    /// </summary>
    [ApiController]
    [Route("api/products")]
    [Produces("application/json")]
    public class ProductsController : ControllerBase
    {
        private const int MaxDescriptionImages = 10;

        private readonly IProductRepository _products;
        private readonly IImageRepository _images;
        private readonly IServiceScopeFactory _scopeFactory;

        public ProductsController(
            IProductRepository products,
            IImageRepository images,
            IServiceScopeFactory scopeFactory)
        {
            _products = products;
            _images = images;
            _scopeFactory = scopeFactory;
        }

        // ── Read (public) ─────────────────────────────────────────────────────

        /// <summary>
        /// Paginated, searchable product list.
        /// Filter by category or brand via query string.
        /// Searchable fields: Name, Description, Category.Name, Brand.Name
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(
            [FromQuery] QueryOptions options,
            [FromQuery] int? categoryId,
            [FromQuery] int? brandId)
        {
            PagedList<Product> paged;

            if (categoryId.HasValue)
                paged = await _products.GetProductsByCategoryAsync(categoryId.Value, options);
            else if (brandId.HasValue)
                paged = await _products.GetProductsByBrandAsync(brandId.Value, options);
            else
                paged = await _products.GetAllProductsAsync(options);

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

        /// <summary>Full product details for a product page.</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var product = await _products.GetProductByIdAsync(id);
            if (product == null) return NotFound();
            return Ok(MapToDto(product));
        }

        /// <summary>
        /// Lightweight preview for product cards and hover tooltips.
        /// Returns image ID, truncated name, price, category, brand, stock flag, and rating counters.
        /// </summary>
        [HttpGet("{id:int}/tooltip")]
        [ProducesResponseType(typeof(ProductTooltipDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTooltip(int id)
        {
            var product = await _products.GetProductByIdAsync(id);
            if (product == null) return NotFound();

            return Ok(new ProductTooltipDto
            {
                Id = product.Id,
                Name = product.Name.Length > 60 ? product.Name[..60] + "…" : product.Name,
                Price = product.Price,
                PreviewImageId = product.PreviewImageId,
                CategoryName = product.Category.Name,
                BrandName = product.Brand.Name,
                InStock = product.StockQuantity > 0,
                Rating1Count = product.Rating1Count,
                Rating2Count = product.Rating2Count,
                Rating3Count = product.Rating3Count,
                Rating4Count = product.Rating4Count,
                Rating5Count = product.Rating5Count
            });
        }

        // ── Write (Admin / Seller) ────────────────────────────────────────────

        /// <summary>
        /// Creates a new product. Upload the preview image first via POST /api/images/upload/preview,
        /// then pass the returned GUID as PreviewImageId.
        /// Description images are embedded as &lt;img src="/api/images/{guid}"&gt; in the HTML.
        /// If more than 10 description images are detected, extras are deleted asynchronously.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin,Seller")]
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] ProductCreateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var product = new Product
            {
                Name = dto.Name,
                Description = dto.Description ?? string.Empty,
                Price = dto.Price,
                CategoryId = dto.CategoryId,
                BrandId = dto.BrandId,
                PreviewImageId = dto.PreviewImageId,
                DateOfCreation = DateTime.UtcNow
            };

            await _products.AddProductAsync(product);
            EnqueueDescriptionImageCleanup(product.Id, product.Description);

            var created = await _products.GetProductByIdAsync(product.Id);
            return CreatedAtAction(nameof(GetById), new { id = product.Id }, MapToDto(created!));
        }

        /// <summary>
        /// Updates a product. Pass NewPreviewImageId only when replacing the preview image.
        /// Description image cap is re-enforced asynchronously after each update.
        /// </summary>
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,Seller")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int id, [FromBody] ProductUpdateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var product = await _products.GetProductByIdAsync(id);
            if (product == null) return NotFound();

            product.Name = dto.Name;
            product.Description = dto.Description ?? string.Empty;
            product.Price = dto.Price;
            product.CategoryId = dto.CategoryId;
            product.BrandId = dto.BrandId;

            if (dto.NewPreviewImageId.HasValue)
            {
                // Delete the old preview from DB before linking the new one
                if (product.PreviewImageId.HasValue)
                    await _images.DeleteAsync(product.PreviewImageId.Value);

                product.PreviewImageId = dto.NewPreviewImageId;
            }

            await _products.UpdateProductAsync(product);
            EnqueueDescriptionImageCleanup(product.Id, product.Description);

            return NoContent();
        }

        /// <summary>
        /// Deletes a product, its preview image, and all description images.
        /// </summary>
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _products.GetProductByIdAsync(id);
            if (product == null) return NotFound();

            // Delete preview image
            if (product.PreviewImageId.HasValue)
                await _images.DeleteAsync(product.PreviewImageId.Value);

            // Delete all description images linked to this product
            var descriptionImages = await _images.GetDescriptionImagesByProductAsync(id);
            if (descriptionImages.Count > 0)
                await _images.DeleteManyAsync(descriptionImages.Select(i => i.Id));

            await _products.DeleteProductAsync(id);
            return NoContent();
        }

        /// <summary>
        /// Sets the available stock quantity for a product.
        /// Users only see "In Stock / Out of Stock". Admins see the exact number via GET /api/products/{id}.
        /// </summary>
        [HttpPut("{id:int}/stock")]
        [Authorize(Roles = "Admin,Seller")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SetStock(int id, [FromBody] SetStockDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var product = await _products.GetProductByIdAsync(id);
            if (product == null) return NotFound();

            product.StockQuantity = dto.Quantity;
            await _products.UpdateProductAsync(product);
            return NoContent();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static ProductDto MapToDto(Product p) => new()
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price,
            PreviewImageId = p.PreviewImageId,
            DateOfCreation = p.DateOfCreation,
            CategoryId = p.CategoryId,
            CategoryName = p.Category.Name,
            BrandId = p.BrandId,
            BrandName = p.Brand.Name,
            InStock = p.StockQuantity > 0,
            StockQuantity = p.StockQuantity,
            Rating1Count = p.Rating1Count,
            Rating2Count = p.Rating2Count,
            Rating3Count = p.Rating3Count,
            Rating4Count = p.Rating4Count,
            Rating5Count = p.Rating5Count
        };

        /// <summary>
        /// Fires an async task to delete description images beyond the 10-image cap.
        /// Uses a dedicated DI scope because the request's DbContext will be disposed
        /// before this lambda completes.
        /// </summary>
        private void EnqueueDescriptionImageCleanup(int productId, string description)
        {
            var allIds = ImagesController
                .ExtractDescriptionImageIds(description)
                .ToList();

            if (allIds.Count <= MaxDescriptionImages) return;

            var excessIds = allIds.Skip(MaxDescriptionImages).ToList();

            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var images = scope.ServiceProvider.GetRequiredService<IImageRepository>();
                    await images.DeleteManyAsync(excessIds);
                }
                catch
                {
                    // TODO: Replace with proper logging via ILogger once logging is configured.
                    // The orphan-cleanup SQL job will catch anything that slips through here.
                }
            });
        }
    }
}