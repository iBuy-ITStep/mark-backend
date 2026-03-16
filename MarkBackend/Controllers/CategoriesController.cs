using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MarkBackend.DTOs;
using MarkBackend.Interfaces;
using MarkBackend.Models;
using MarkBackend.Models.Pages;

namespace MarkBackend.Controllers
{
    /// <summary>
    /// Manages product categories. Categories support a parent/child hierarchy via ParentId.
    /// </summary>
    [ApiController]
    [Route("api/categories")]
    [Produces("application/json")]
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryRepository _categories;

        public CategoriesController(ICategoryRepository categories)
        {
            _categories = categories;
        }

        /// <summary>
        /// Returns a paginated list of all categories.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] QueryOptions options)
        {
            var paged = await _categories.GetAllCategoriesAsync(options);

            var dto = new
            {
                paged.CurrentPage,
                paged.TotalPages,
                paged.PageSize,
                paged.HasPreviousPage,
                paged.HasNextPage,
                Items = paged.Items.Select(c => new CategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    ParentId = c.ParentId
                })
            };

            return Ok(dto);
        }

        /// <summary>
        /// Returns a flat list of all categories — for dropdown menus on the frontend.
        /// </summary>
        [HttpGet("list")]
        [ProducesResponseType(typeof(IEnumerable<CategoryDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetList()
        {
            var categories = await _categories.GetAllCategoriesListAsync();
            return Ok(categories.Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                ParentId = c.ParentId
            }));
        }

        /// <summary>
        /// Returns a single category by ID.
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var category = await _categories.GetCategoryByIdAsync(id);
            if (category == null) return NotFound();

            return Ok(new CategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                ParentId = category.ParentId
            });
        }

        /// <summary>
        /// Creates a new category. Set ParentId to 0 for a top-level category.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin,Seller")]
        [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CategoryWriteDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var category = new Category { Name = dto.Name, ParentId = dto.ParentId };
            await _categories.AddCategoryAsync(category);

            var result = new CategoryDto { Id = category.Id, Name = category.Name, ParentId = category.ParentId };
            return CreatedAtAction(nameof(GetById), new { id = category.Id }, result);
        }

        /// <summary>
        /// Updates an existing category.
        /// </summary>
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,Seller")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int id, [FromBody] CategoryWriteDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var category = await _categories.GetCategoryByIdAsync(id);
            if (category == null) return NotFound();

            category.Name = dto.Name;
            category.ParentId = dto.ParentId;
            await _categories.UpdateCategoryAsync(category);
            return NoContent();
        }

        /// <summary>
        /// Deletes a category by ID.
        /// </summary>
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _categories.GetCategoryByIdAsync(id);
            if (category == null) return NotFound();

            await _categories.DeleteCategoryAsync(id);
            return NoContent();
        }
    }
}