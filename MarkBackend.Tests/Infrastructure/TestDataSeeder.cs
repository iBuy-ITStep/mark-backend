using Microsoft.AspNetCore.Identity;
using MarkBackend.Data;
using MarkBackend.Models;

namespace MarkBackend.Tests.Infrastructure;

public static class TestDataSeeder
{
    // Test user credentials
    public const string SuperAdminEmail = "test.superadmin@test.com";
    public const string AdminEmail = "test.admin@test.com";
    public const string SellerEmail = "test.seller@test.com";
    public const string UserEmail = "test.user@test.com";
    public const string TestPassword = "Test123!";

    public static async Task SeedAsync(
        ApplicationContext db,
        UserManager<User> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        // Seed roles
        string[] roles = ["SuperAdmin", "Admin", "Seller", "User"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(role));
                if (!result.Succeeded)
                {
                    throw new Exception($"Failed to create role {role}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }
        }

        // Seed test users
        await CreateTestUserAsync(userManager, SuperAdminEmail, "SuperAdmin");
        await CreateTestUserAsync(userManager, AdminEmail, "Admin");
        await CreateTestUserAsync(userManager, SellerEmail, "Seller");
        await CreateTestUserAsync(userManager, UserEmail, "User");

        // Seed cart order statuses
        if (!db.CartOrderStatuses.Any())
        {
            db.CartOrderStatuses.AddRange(
                new CartOrderStatus { Id = (int)OrderStatus.Processing, Name = "В обработке" },
                new CartOrderStatus { Id = (int)OrderStatus.InTransit, Name = "В пути" },
                new CartOrderStatus { Id = (int)OrderStatus.Delivered, Name = "Отправлен" }
            );
            await db.SaveChangesAsync();
        }

        // Seed categories - Use explicit IDs for InMemory database
        if (!db.Categories.Any())
        {
            var categories = new[]
            {
                new Category { Id = 1, Name = "Electronics", ParentId = 0 },
                new Category { Id = 2, Name = "Clothing", ParentId = 0 },
                new Category { Id = 3, Name = "Books", ParentId = 0 },
                new Category { Id = 4, Name = "Smartphones", ParentId = 1 },
                new Category { Id = 5, Name = "Laptops", ParentId = 1 }
            };
            db.Categories.AddRange(categories);
            await db.SaveChangesAsync();
        }

        // Seed brands
        if (!db.Brands.Any())
        {
            var brands = new[]
            {
                new Brand { Id = 1, Name = "Apple" },
                new Brand { Id = 2, Name = "Samsung" },
                new Brand { Id = 3, Name = "Nike" },
                new Brand { Id = 4, Name = "Adidas" }
            };
            db.Brands.AddRange(brands);
            await db.SaveChangesAsync();
        }

        // Seed products - Use explicit IDs
        if (!db.Products.Any())
        {
            var products = new[]
            {
                new Product
                {
                    Id = 1,
                    Name = "iPhone 15 Pro",
                    Description = "<p>Latest iPhone model with advanced features</p>",
                    Price = 999.99m,
                    CategoryId = 4,
                    BrandId = 1,
                    StockQuantity = 50,
                    DateOfCreation = DateTime.UtcNow
                },
                new Product
                {
                    Id = 2,
                    Name = "Galaxy S24",
                    Description = "<p>Samsung flagship smartphone</p>",
                    Price = 899.99m,
                    CategoryId = 4,
                    BrandId = 2,
                    StockQuantity = 30,
                    DateOfCreation = DateTime.UtcNow
                },
                new Product
                {
                    Id = 3,
                    Name = "MacBook Pro",
                    Description = "<p>Powerful laptop for professionals</p>",
                    Price = 1999.99m,
                    CategoryId = 5,
                    BrandId = 1,
                    StockQuantity = 20,
                    DateOfCreation = DateTime.UtcNow
                },
                new Product
                {
                    Id = 4,
                    Name = "Out of Stock Product",
                    Description = "<p>This product is out of stock</p>",
                    Price = 49.99m,
                    CategoryId = 2,
                    BrandId = 3,
                    StockQuantity = 0,
                    DateOfCreation = DateTime.UtcNow
                }
            };
            db.Products.AddRange(products);
            await db.SaveChangesAsync();
        }
    }

    private static async Task CreateTestUserAsync(UserManager<User> userManager, string email, string role)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing == null)
        {
            var user = new User
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(user, TestPassword);
            if (!result.Succeeded)
            {
                throw new Exception($"Failed to create user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            var roleResult = await userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
            {
                throw new Exception($"Failed to add user {email} to role {role}: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
            }
        }
    }
}
