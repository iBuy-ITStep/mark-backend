using MarkBackend.Models;
using MarkBackend.Models.Pages;

namespace MarkBackend.Interfaces
{
    public interface IBrandRepository
    {
        Task<PagedList<Brand>> GetAllBrandsAsync(QueryOptions options);
        Task<IEnumerable<Brand>> GetAllBrandsListAsync();
        Task<Brand?> GetBrandByIdAsync(int brandId);
        Task AddBrandAsync(Brand brand);
        Task UpdateBrandAsync(Brand brand);
        Task DeleteBrandAsync(int brandId);
    }
}