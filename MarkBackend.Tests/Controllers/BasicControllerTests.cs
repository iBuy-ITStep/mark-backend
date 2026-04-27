using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MarkBackend.Tests.Helpers;
using MarkBackend.Tests.Infrastructure;

namespace MarkBackend.Tests.Controllers;

public class ImagesControllerTests : IntegrationTestBase
{
    public ImagesControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetByProduct_ReturnsImageMetadata()
    {
        // Act - No auth required (public endpoint)
        var response = await Client.GetAsync("/api/images/product/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMyImages_AsSeller_ReturnsUserImages()
    {
        // Arrange
        await Client.AuthenticateAsAsync(TestDataSeeder.SellerEmail, TestDataSeeder.TestPassword);

        // Act
        var response = await Client.GetAsync("/api/images/my-images");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMyImages_AsUser_ReturnsForbidden()
    {
        // Arrange
        await Client.AuthenticateAsAsync(TestDataSeeder.UserEmail, TestDataSeeder.TestPassword);

        // Act
        var response = await Client.GetAsync("/api/images/my-images");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

public class CategoriesControllerTests : IntegrationTestBase
{
    public CategoriesControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetAll_ReturnsCategories()
    {
        // Act
        var response = await Client.GetAsync("/api/categories");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResponse<CategoryDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Create_AsAdmin_ReturnsCreated()
    {
        // Arrange
        await Client.AuthenticateAsAsync(TestDataSeeder.AdminEmail, TestDataSeeder.TestPassword);
        var newCategory = new { name = "Test Category" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/categories", newCategory);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_AsUser_ReturnsForbidden()
    {
        // Arrange
        await Client.AuthenticateAsAsync(TestDataSeeder.UserEmail, TestDataSeeder.TestPassword);
        var newCategory = new { name = "Test Category" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/categories", newCategory);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private class CategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
    }

    private class PagedResponse<T>
    {
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int PageSize { get; set; }
        public bool HasPreviousPage { get; set; }
        public bool HasNextPage { get; set; }
        public List<T> Items { get; set; } = new();
    }
}

public class BrandsControllerTests : IntegrationTestBase
{
    public BrandsControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetAll_ReturnsBrands()
    {
        // Act
        var response = await Client.GetAsync("/api/brands");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResponse<BrandDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Create_AsSeller_ReturnsCreated()
    {
        // Arrange
        await Client.AuthenticateAsAsync(TestDataSeeder.SellerEmail, TestDataSeeder.TestPassword);
        var newBrand = new { name = "Test Brand" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/brands", newBrand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private class BrandDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
    }

    private class PagedResponse<T>
    {
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int PageSize { get; set; }
        public bool HasPreviousPage { get; set; }
        public bool HasNextPage { get; set; }
        public List<T> Items { get; set; } = new();
    }
}
