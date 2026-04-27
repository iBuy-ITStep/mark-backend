using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MarkBackend.Tests.Helpers;
using MarkBackend.Tests.Infrastructure;

namespace MarkBackend.Tests.Controllers;

public class ProductsControllerTests : IntegrationTestBase
{
    public ProductsControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetAll_ReturnsProducts()
    {
        // Act
        var response = await Client.GetAsync("/api/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResponse>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_WithCategoryFilter_ReturnsFilteredProducts()
    {
        // Act - Filter by Electronics category (ID 1)
        var response = await Client.GetAsync("/api/products?categoryId=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResponse>();
        result!.Items.Should().NotBeEmpty();
        result.Items.Should().AllSatisfy(p => p.CategoryId.Should().BeOneOf(1, 4, 5)); // Parent or children
    }

    [Fact(Skip = "Test isolation issue - Passes individually but fails in suite due to shared database state.")]
    public async Task GetById_WithValidId_ReturnsProduct()
    {
        // Act
        var response = await Client.GetAsync("/api/products/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var product = await response.Content.ReadFromJsonAsync<ProductDto>();
        product.Should().NotBeNull();
        product!.Id.Should().Be(1);
        product.Name.Should().Be("iPhone 15 Pro");
    }

    [Fact]
    public async Task GetById_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var response = await Client.GetAsync("/api/products/9999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTooltip_WithValidId_ReturnsLightweightData()
    {
        // Act
        var response = await Client.GetAsync("/api/products/1/tooltip");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tooltip = await response.Content.ReadFromJsonAsync<ProductTooltipDto>();
        tooltip.Should().NotBeNull();
        tooltip!.Id.Should().Be(1);
        tooltip.Name.Should().NotBeNullOrEmpty();
        tooltip.Price.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Create_AsAdmin_ReturnsCreated()
    {
        // Arrange
        await Client.AuthenticateAsAsync(TestDataSeeder.AdminEmail, TestDataSeeder.TestPassword);
        var newProduct = new
        {
            name = "Test Product",
            description = "<p>Test description</p>",
            price = 99.99,
            categoryId = 1,
            brandId = 1
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/products", newProduct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<ProductDto>();
        created.Should().NotBeNull();
        created!.Name.Should().Be("Test Product");
    }

    [Fact]
    public async Task Create_AsUser_ReturnsForbidden()
    {
        // Arrange
        await Client.AuthenticateAsAsync(TestDataSeeder.UserEmail, TestDataSeeder.TestPassword);
        var newProduct = new
        {
            name = "Test Product",
            description = "<p>Test description</p>",
            price = 99.99,
            categoryId = 1,
            brandId = 1
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/products", newProduct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var newProduct = new
        {
            name = "Test Product",
            description = "<p>Test description</p>",
            price = 99.99,
            categoryId = 1,
            brandId = 1
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/products", newProduct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_AsSeller_ReturnsNoContent()
    {
        // Arrange
        await Client.AuthenticateAsAsync(TestDataSeeder.SellerEmail, TestDataSeeder.TestPassword);
        var updateProduct = new
        {
            name = "Updated Product",
            description = "<p>Updated description</p>",
            price = 199.99,
            categoryId = 1,
            brandId = 1
        };

        // Act
        var response = await Client.PutAsJsonAsync("/api/products/1", updateProduct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_AsAdmin_ReturnsNoContent()
    {
        // Arrange
        await Client.AuthenticateAsAsync(TestDataSeeder.AdminEmail, TestDataSeeder.TestPassword);

        // Act
        var response = await Client.DeleteAsync("/api/products/4");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_AsSeller_ReturnsForbidden()
    {
        // Arrange
        await Client.AuthenticateAsAsync(TestDataSeeder.SellerEmail, TestDataSeeder.TestPassword);

        // Act
        var response = await Client.DeleteAsync("/api/products/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SetStock_AsSeller_ReturnsNoContent()
    {
        // Arrange
        await Client.AuthenticateAsAsync(TestDataSeeder.SellerEmail, TestDataSeeder.TestPassword);
        var stockUpdate = new { quantity = 100 };

        // Act
        var response = await Client.PutAsJsonAsync("/api/products/1/stock", stockUpdate);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private class PagedResponse
    {
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int PageSize { get; set; }
        public List<ProductDto> Items { get; set; } = new();
    }

    private class ProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Description { get; set; } = null!;
        public decimal Price { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = null!;
        public int BrandId { get; set; }
        public string BrandName { get; set; } = null!;
        public bool InStock { get; set; }
        public int StockQuantity { get; set; }
    }

    private class ProductTooltipDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public decimal Price { get; set; }
        public Guid? PreviewImageId { get; set; }
        public string CategoryName { get; set; } = null!;
        public string BrandName { get; set; } = null!;
        public bool InStock { get; set; }
    }
}
