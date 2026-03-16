using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MarkBackend.DTOs;
using MarkBackend.Interfaces;
using MarkBackend.Models;
using MarkBackend.Models.Pages;

namespace MarkBackend.Controllers
{
    /// <summary>
    /// Manages product brands.
    /// </summary>
    [ApiController]
    [Route("api/brands")]
    [Produces("application/json")]
    public class BrandsController : ControllerBase
    {
        private readonly IBrandRepository _brands;

        public BrandsController(IBrandRepository brands)
        {
            _brands = brands;
        }

        /// <summary>
        /// Returns a paginated list of all brands.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(PagedList<BrandDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] QueryOptions options)
        {
            var paged = await _brands.GetAllBrandsAsync(options);

            // Map Model → DTO so we never accidentally expose internal fields
            var dto = new
            {
                paged.CurrentPage,
                paged.TotalPages,
                paged.PageSize,
                paged.HasPreviousPage,
                paged.HasNextPage,
                Items = paged.Items.Select(b => new BrandDto { Id = b.Id, Name = b.Name })
            };

            return Ok(dto);
        }

        /// <summary>
        /// Returns a flat list of all brands — useful for populating dropdowns on the frontend.
        /// </summary>
        [HttpGet("list")]
        [ProducesResponseType(typeof(IEnumerable<BrandDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetList()
        {
            var brands = await _brands.GetAllBrandsListAsync();
            return Ok(brands.Select(b => new BrandDto { Id = b.Id, Name = b.Name }));
        }

        /// <summary>
        /// Returns a single brand by ID.
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(BrandDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var brand = await _brands.GetBrandByIdAsync(id);
            if (brand == null) return NotFound();
            return Ok(new BrandDto { Id = brand.Id, Name = brand.Name });
        }

        /// <summary>
        /// Creates a new brand.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin,Seller")]
        [ProducesResponseType(typeof(BrandDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] BrandWriteDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var brand = new Brand { Name = dto.Name };
            await _brands.AddBrandAsync(brand);

            var result = new BrandDto { Id = brand.Id, Name = brand.Name };
            return CreatedAtAction(nameof(GetById), new { id = brand.Id }, result);
        }

        /// <summary>
        /// Updates an existing brand.
        /// </summary>
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,Seller")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int id, [FromBody] BrandWriteDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var brand = await _brands.GetBrandByIdAsync(id);
            if (brand == null) return NotFound();

            brand.Name = dto.Name;
            await _brands.UpdateBrandAsync(brand);
            return NoContent();
        }

        /// <summary>
        /// Deletes a brand by ID.
        /// </summary>
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id)
        {
            var brand = await _brands.GetBrandByIdAsync(id);
            if (brand == null) return NotFound();

            await _brands.DeleteBrandAsync(id);
            return NoContent();
        }
    }
}