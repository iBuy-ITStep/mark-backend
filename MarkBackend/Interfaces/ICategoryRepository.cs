using MarkBackend.Models;
using MarkBackend.Models.Pages;

namespace MarkBackend.Interfaces
{
    public interface ICategoryRepository
    {
        Task<PagedList<Category>> GetAllCategoriesAsync(QueryOptions options);
        Task<IEnumerable<Category>> GetAllCategoriesListAsync();
        Task<Category?> GetCategoryByIdAsync(int categoryId);
        Task AddCategoryAsync(Category category);
        Task UpdateCategoryAsync(Category category);
        Task DeleteCategoryAsync(int categoryId);

        /// <summary>
        /// Gets all descendant category IDs for a given parent category (including the parent itself).
        /// Used for hierarchical product filtering.
        /// </summary>
        Task<List<int>> GetCategoryAndDescendantsAsync(int categoryId);
    }
}