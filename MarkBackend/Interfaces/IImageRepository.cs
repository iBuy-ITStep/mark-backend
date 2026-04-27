using MarkBackend.Models;

namespace MarkBackend.Interfaces
{
    public interface IImageRepository
    {
        Task<ProductImage?> GetByIdAsync(Guid id);
        Task AddAsync(ProductImage image);
        Task DeleteAsync(Guid id);
        Task DeleteManyAsync(IEnumerable<Guid> ids);

        /// <summary>
        /// Returns all description images (IsPreview = false) linked to a product,
        /// ordered by upload time ascending (oldest first).
        /// </summary>
        Task<List<ProductImage>> GetDescriptionImagesByProductAsync(int productId);

        /// <summary>
        /// Returns all images (both preview and description) for a product.
        /// Includes preview image if linked and all description images.
        /// </summary>
        Task<List<ProductImage>> GetAllImagesByProductAsync(int productId);

        /// <summary>
        /// Returns all images uploaded by the current user.
        /// </summary>
        Task<List<ProductImage>> GetImagesByUserAsync(string userId);
    }
}