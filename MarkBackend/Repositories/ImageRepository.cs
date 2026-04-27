using Microsoft.EntityFrameworkCore;
using MarkBackend.Data;
using MarkBackend.Interfaces;
using MarkBackend.Models;

namespace MarkBackend.Repositories
{
    public class ImageRepository : IImageRepository
    {
        private readonly ApplicationContext _context;

        public ImageRepository(ApplicationContext context) => _context = context;

        public async Task<ProductImage?> GetByIdAsync(Guid id) =>
            await _context.ProductImages.FindAsync(id);

        public async Task AddAsync(ProductImage image)
        {
            await _context.ProductImages.AddAsync(image);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var image = await _context.ProductImages.FindAsync(id);
            if (image != null)
            {
                _context.ProductImages.Remove(image);
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteManyAsync(IEnumerable<Guid> ids)
        {
            var images = await _context.ProductImages
                .Where(i => ids.Contains(i.Id))
                .ToListAsync();

            if (images.Count > 0)
            {
                _context.ProductImages.RemoveRange(images);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<ProductImage>> GetDescriptionImagesByProductAsync(int productId) =>
            await _context.ProductImages
                .Where(i => i.ProductId == productId && !i.IsPreview)
                .OrderBy(i => i.UploadedAt)
                .ToListAsync();

        public async Task<List<ProductImage>> GetAllImagesByProductAsync(int productId) =>
            await _context.ProductImages
                .Where(i => i.ProductId == productId)
                .OrderBy(i => i.UploadedAt)
                .ToListAsync();

        public async Task<List<ProductImage>> GetImagesByUserAsync(string userId) =>
            await _context.ProductImages
                .Where(i => i.UploadedById == userId)
                .OrderByDescending(i => i.UploadedAt)
                .ToListAsync();
    }
}