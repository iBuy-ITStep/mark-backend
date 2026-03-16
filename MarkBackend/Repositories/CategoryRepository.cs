using Microsoft.EntityFrameworkCore;
using MarkBackend.Data;
using MarkBackend.Interfaces;
using MarkBackend.Models;
using MarkBackend.Models.Pages;

namespace MarkBackend.Repositories
{
    public class CategoryRepository : ICategoryRepository
    {
        private readonly ApplicationContext _context;

        public CategoryRepository(ApplicationContext context)
        {
            _context = context;
        }

        public async Task<PagedList<Category>> GetAllCategoriesAsync(QueryOptions options)
        {
            return await PagedList<Category>.CreateAsync(_context.Categories.AsNoTracking(), options);
        }

        public async Task<IEnumerable<Category>> GetAllCategoriesListAsync()
        {
            return await _context.Categories.AsNoTracking().ToListAsync();
        }

        public async Task<Category?> GetCategoryByIdAsync(int categoryId)
        {
            return await _context.Categories.FindAsync(categoryId);
        }

        public async Task AddCategoryAsync(Category category)
        {
            await _context.Categories.AddAsync(category);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateCategoryAsync(Category category)
        {
            _context.Categories.Update(category);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteCategoryAsync(int categoryId)
        {
            var category = await GetCategoryByIdAsync(categoryId);
            if (category != null)
            {
                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
            }
        }
    }
}