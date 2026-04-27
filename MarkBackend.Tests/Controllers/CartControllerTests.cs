using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MarkBackend.Tests.Helpers;
using MarkBackend.Tests.Infrastructure;

namespace MarkBackend.Tests.Controllers;

public class CartControllerTests : IntegrationTestBase
{
    public CartControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetCart_WhenAuthenticated_ReturnsCart()
    {
        // Arrange
        await Client.AuthenticateAsAsync(TestDataSeeder.UserEmail, TestDataSeeder.TestPassword);

        // Act
        var response = await Client.GetAsync("/api/cart");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cart = await response.Content.ReadFromJsonAsync<CartDto>();
        cart.Should().NotBeNull();
        cart!.Items.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCart_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.GetAsync("/api/cart");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AddItem_WithValidProduct_ReturnsSuccess()
    {
        // Arrange
        await Client.AuthenticateAsAsync(TestDataSeeder.UserEmail, TestDataSeeder.TestPassword);
        var addItemRequest = new
        {
            productId = 1,
            quantity = 2
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/cart/items", addItemRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SetItemQuantity_UpdatesQuantity()
    {
        // Arrange
        await Client.AuthenticateAsAsync(TestDataSeeder.UserEmail, TestDataSeeder.TestPassword);

        // Add item first
        await Client.PostAsJsonAsync("/api/cart/items", new { productId = 1, quantity = 1 });

        // Act - Update quantity
        var updateRequest = new { quantity = 5 };
        var response = await Client.PutAsJsonAsync("/api/cart/items/1", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DecrementItem_ReducesQuantityByOne()
    {
        // Arrange
        await Client.AuthenticateAsAsync(TestDataSeeder.UserEmail, TestDataSeeder.TestPassword);

        // Add item with quantity 3
        await Client.PostAsJsonAsync("/api/cart/items", new { productId = 1, quantity = 3 });

        // Act - Decrement
        var response = await Client.DeleteAsync("/api/cart/items/1/one");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RemoveItem_RemovesProductFromCart()
    {
        // Arrange
        await Client.AuthenticateAsAsync(TestDataSeeder.UserEmail, TestDataSeeder.TestPassword);

        // Add item
        await Client.PostAsJsonAsync("/api/cart/items", new { productId = 1, quantity = 2 });

        // Act - Remove
        var response = await Client.DeleteAsync("/api/cart/items/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Checkout_WithItemsInCart_CreatesOrder()
    {
        // Arrange - Use Seller account to avoid conflicts with other User tests
        await Client.AuthenticateAsAsync(TestDataSeeder.SellerEmail, TestDataSeeder.TestPassword);

        // Add items to cart
        var addResponse1 = await Client.PostAsJsonAsync("/api/cart/items", new { productId = 1, quantity = 1 });
        Console.WriteLine($"Add item 1 status: {addResponse1.StatusCode}");

        var addResponse2 = await Client.PostAsJsonAsync("/api/cart/items", new { productId = 2, quantity = 2 });
        Console.WriteLine($"Add item 2 status: {addResponse2.StatusCode}");

        // Check cart before checkout
        var cartResponse = await Client.GetAsync("/api/cart");
        var cartContent = await cartResponse.Content.ReadAsStringAsync();
        Console.WriteLine($"Cart before checkout: {cartContent}");

        // Act - Checkout
        var response = await Client.PostAsync("/api/cart/checkout", null);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Checkout failed: {errorContent}");
        }

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Checkout_WithEmptyCart_ReturnsBadRequest()
    {
        // Arrange - Use Admin account to avoid conflicts with other User tests
        await Client.AuthenticateAsAsync(TestDataSeeder.AdminEmail, TestDataSeeder.TestPassword);

        // Act - Checkout without adding items
        var response = await Client.PostAsync("/api/cart/checkout", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetOrders_ReturnsUserOrders()
    {
        // Arrange - Use SuperAdmin account to avoid conflicts
        await Client.AuthenticateAsAsync(TestDataSeeder.SuperAdminEmail, TestDataSeeder.TestPassword);

        // Create an order first
        await Client.PostAsJsonAsync("/api/cart/items", new { productId = 1, quantity = 1 });
        await Client.PostAsync("/api/cart/checkout", null);

        // Act
        var response = await Client.GetAsync("/api/cart/orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResponse>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAllOrders_AsAdmin_ReturnsAllOrders()
    {
        // Arrange
        await Client.AuthenticateAsAsync(TestDataSeeder.AdminEmail, TestDataSeeder.TestPassword);

        // Act
        var response = await Client.GetAsync("/api/cart/orders/all");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAllOrders_AsUser_ReturnsForbidden()
    {
        // Arrange
        await Client.AuthenticateAsAsync(TestDataSeeder.UserEmail, TestDataSeeder.TestPassword);

        // Act
        var response = await Client.GetAsync("/api/cart/orders/all");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private class CartDto
    {
        public string CartId { get; set; } = null!;
        public DateTime TimestampLastUpdate { get; set; }
        public int DistinctProductCount { get; set; }
        public bool IsOrder { get; set; }
        public string Status { get; set; } = null!;
        public List<CartEntryDto> Items { get; set; } = new();
        public decimal TotalPrice { get; set; }
    }

    private class CartEntryDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = null!;
        public decimal ProductPrice { get; set; }
        public int Quantity { get; set; }
    }

    private class PagedResponse
    {
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public List<CartDto> Items { get; set; } = new();
    }
}
