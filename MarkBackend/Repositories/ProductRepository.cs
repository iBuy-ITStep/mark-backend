using Microsoft.EntityFrameworkCore;
using MarkBackend.Data;
using MarkBackend.Interfaces;
using MarkBackend.Models;
using MarkBackend.Models.Pages;

namespace MarkBackend.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly ApplicationContext _context;

        public ProductRepository(ApplicationContext context)
        {
            _context = context;
        }

        public async Task<PagedList<Product>> GetAllProductsAsync(QueryOptions options)
        {
            IQueryable<Product> query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .AsNoTracking();

            return await PagedList<Product>.CreateAsync(query, options);
        }

        public async Task<PagedList<Product>> GetProductsByCategoryAsync(int categoryId, QueryOptions options)
        {
            IQueryable<Product> query = _context.Products
                .Where(p => p.CategoryId == categoryId)
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .AsNoTracking();

            return await PagedList<Product>.CreateAsync(query, options);
        }

        public async Task<PagedList<Product>> GetProductsByBrandAsync(int brandId, QueryOptions options)
        {
            IQueryable<Product> query = _context.Products
                .Where(p => p.BrandId == brandId)
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .AsNoTracking();

            return await PagedList<Product>.CreateAsync(query, options);
        }

        public async Task<Product?> GetProductByIdAsync(int productId)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .FirstOrDefaultAsync(p => p.Id == productId);
        }

        public async Task AddProductAsync(Product product)
        {
            await _context.Products.AddAsync(product);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateProductAsync(Product product)
        {
            _context.Products.Update(product);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteProductAsync(int productId)
        {
            var product = await GetProductByIdAsync(productId);
            if (product != null)
            {
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }
        }
    }
}