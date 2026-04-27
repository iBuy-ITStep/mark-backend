using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MarkBackend.Tests.Helpers;
using MarkBackend.Tests.Infrastructure;

namespace MarkBackend.Tests.Integration;

/// <summary>
/// End-to-end tests simulating complete user journeys through the API
/// </summary>
public class E2EUserJourneyTests : IntegrationTestBase
{
    public E2EUserJourneyTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CompleteUserJourney_BrowseAddToCartCheckout()
    {
        // Step 1: Browse products (public)
        var productsResponse = await Client.GetAsync("/api/products");
        productsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 2: View product details (public)
        var productResponse = await Client.GetAsync("/api/products/1");
        productResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 3: Login as user
        await Client.AuthenticateAsAsync(TestDataSeeder.UserEmail, TestDataSeeder.TestPassword);

        // Step 4: Add products to cart
        var addItem1 = await Client.PostAsJsonAsync("/api/cart/items", new { productId = 1, quantity = 2 });
        addItem1.StatusCode.Should().Be(HttpStatusCode.OK);

        var addItem2 = await Client.PostAsJsonAsync("/api/cart/items", new { productId = 2, quantity = 1 });
        addItem2.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 5: View cart
        var cartResponse = await Client.GetAsync("/api/cart");
        cartResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var cart = await cartResponse.Content.ReadFromJsonAsync<CartDto>();
        cart!.Items.Should().HaveCount(2);
        cart.DistinctProductCount.Should().Be(2);

        // Step 6: Update quantity
        var updateResponse = await Client.PutAsJsonAsync("/api/cart/items/1", new { quantity = 3 });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 7: Checkout
        var checkoutResponse = await Client.PostAsync("/api/cart/checkout", null);
        checkoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 8: View order history
        var ordersResponse = await Client.GetAsync("/api/cart/orders");
        ordersResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var orders = await ordersResponse.Content.ReadFromJsonAsync<PagedResponse>();
        orders!.Items.Should().NotBeEmpty();
        orders.Items.Should().Contain(o => o.IsOrder == true);
    }

    [Fact]
    public async Task SellerJourney_CreateAndManageProduct()
    {
        // Step 1: Login as seller
        await Client.AuthenticateAsAsync(TestDataSeeder.SellerEmail, TestDataSeeder.TestPassword);

        // Step 2: Create a new product
        var newProduct = new
        {
            name = "Seller's New Product",
            description = "<p>Created by seller in E2E test</p>",
            price = 299.99,
            categoryId = 1,
            brandId = 1
        };

        var createResponse = await Client.PostAsJsonAsync("/api/products", newProduct);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<ProductDto>();
        var productId = created!.Id;

        // Step 3: Update the product
        var updateProduct = new
        {
            name = "Updated Product Name",
            description = "<p>Updated description</p>",
            price = 349.99,
            categoryId = 1,
            brandId = 1
        };

        var updateResponse = await Client.PutAsJsonAsync($"/api/products/{productId}", updateProduct);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Step 4: Set stock quantity
        var stockResponse = await Client.PutAsJsonAsync($"/api/products/{productId}/stock", new { quantity = 100 });
        stockResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Step 5: Verify product is visible publicly
        var publicResponse = await Client.GetAsync($"/api/products/{productId}");
        publicResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var publicProduct = await publicResponse.Content.ReadFromJsonAsync<ProductDto>();
        publicProduct!.Name.Should().Be("Updated Product Name");
        publicProduct.StockQuantity.Should().Be(100);
    }

    [Fact(Skip = "Test isolation issue - Passes individually but fails in suite due to shared database state.")]
    public async Task AdminJourney_ManageUsersAndProducts()
    {
        // Step 1: Login as admin
        await Client.AuthenticateAsAsync(TestDataSeeder.AdminEmail, TestDataSeeder.TestPassword);

        // Step 2: View all users
        var usersResponse = await Client.GetAsync("/api/admin/users");
        usersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 3: Create a new category
        var newCategory = new { name = "Admin Test Category" };
        var categoryResponse = await Client.PostAsJsonAsync("/api/categories", newCategory);
        categoryResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Step 4: Create a new brand
        var newBrand = new { name = "Admin Test Brand" };
        var brandResponse = await Client.PostAsJsonAsync("/api/brands", newBrand);
        brandResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Step 5: View all orders
        var ordersResponse = await Client.GetAsync("/api/cart/orders/all");
        ordersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 6: Delete a product (only admin can delete)
        var deleteResponse = await Client.DeleteAsync("/api/products/4");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(Skip = "Test isolation issue - Passes individually but fails in suite due to shared database state.")]
    public async Task HierarchicalCategories_FilterProductsByParentCategory()
    {
        // Step 1: Get products from parent category (Electronics, ID=1)
        // Should include products from child categories (Smartphones=4, Laptops=5)
        var response = await Client.GetAsync("/api/products?categoryId=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ProductPagedResponse>();
        result!.Items.Should().NotBeEmpty();

        // Should contain products from Electronics and its children
        result.Items.Should().Contain(p => p.Name == "iPhone 15 Pro"); // Smartphones category
        result.Items.Should().Contain(p => p.Name == "Galaxy S24"); // Smartphones category
        result.Items.Should().Contain(p => p.Name == "MacBook Pro"); // Laptops category
    }

    private class CartDto
    {
        public string CartId { get; set; } = null!;
        public int DistinctProductCount { get; set; }
        public bool IsOrder { get; set; }
        public List<CartEntryDto> Items { get; set; } = new();
    }

    private class CartEntryDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    private class PagedResponse
    {
        public List<CartDto> Items { get; set; } = new();
    }

    private class ProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public int StockQuantity { get; set; }
    }

    private class ProductPagedResponse
    {
        public List<ProductDto> Items { get; set; } = new();
    }
}
