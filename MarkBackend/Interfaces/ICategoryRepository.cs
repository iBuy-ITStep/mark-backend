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
    }
}