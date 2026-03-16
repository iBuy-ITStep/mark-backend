using MarkBackend.Models;
using MarkBackend.Models.Pages;

namespace MarkBackend.Interfaces
{
    public interface IProductRepository
    {
        Task<Product?> GetProductByIdAsync(int productId);
        Task AddProductAsync(Product product);
        Task UpdateProductAsync(Product product);
        Task DeleteProductAsync(int productId);

        Task<PagedList<Product>> GetAllProductsAsync(QueryOptions options);
        Task<PagedList<Product>> GetProductsByCategoryAsync(int categoryId, QueryOptions options);
        Task<PagedList<Product>> GetProductsByBrandAsync(int brandId, QueryOptions options);
    }
}