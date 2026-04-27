using System.Collections.Generic;
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

        /// <summary>
        /// Gets all descendant category IDs for a given parent category (including the parent itself).
        /// Recursively finds all children to support hierarchical filtering.
        /// </summary>
        public async Task<List<int>> GetCategoryAndDescendantsAsync(int categoryId)
        {
            var allCategories = await _context.Categories.AsNoTracking().ToListAsync();
            var result = new List<int> { categoryId };
            var queue = new Queue<int>();
            queue.Enqueue(categoryId);

            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();
                var children = allCategories.Where(c => c.ParentId == currentId).Select(c => c.Id).ToList();

                foreach (var childId in children)
                {
                    result.Add(childId);
                    queue.Enqueue(childId);
                }
            }

            return result;
        }
    }
}