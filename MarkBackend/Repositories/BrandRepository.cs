using Microsoft.EntityFrameworkCore;
using MarkBackend.Data;
using MarkBackend.Interfaces;
using MarkBackend.Models;
using MarkBackend.Models.Pages;

namespace MarkBackend.Repositories
{
    public class BrandRepository : IBrandRepository
    {
        private readonly ApplicationContext _context;

        public BrandRepository(ApplicationContext context)
        {
            _context = context;
        }

        public async Task<PagedList<Brand>> GetAllBrandsAsync(QueryOptions options)
        {
            return await PagedList<Brand>.CreateAsync(_context.Brands.AsNoTracking(), options);
        }

        public async Task<IEnumerable<Brand>> GetAllBrandsListAsync()
        {
            return await _context.Brands.AsNoTracking().ToListAsync();
        }

        public async Task<Brand?> GetBrandByIdAsync(int brandId)
        {
            return await _context.Brands.FindAsync(brandId);
        }

        public async Task AddBrandAsync(Brand brand)
        {
            await _context.Brands.AddAsync(brand);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateBrandAsync(Brand brand)
        {
            _context.Brands.Update(brand);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteBrandAsync(int brandId)
        {
            var brand = await GetBrandByIdAsync(brandId);
            if (brand != null)
            {
                _context.Brands.Remove(brand);
                await _context.SaveChangesAsync();
            }
        }
    }
}