using MarkBackend.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MarkBackend.Data
{
    public class ApplicationContext : IdentityDbContext<User>
    {
        public ApplicationContext(DbContextOptions<ApplicationContext> options)
            : base(options) {
            // Database.EnsureCreated() moved to Program.cs to avoid conflicts with test InMemory provider
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Brand> Brands { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartEntry> CartEntries { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<ProductRating> ProductRatings { get; set; }
        public DbSet<CartOrderStatus> CartOrderStatuses { get; set; }

        // TODO: Favourites
        // public DbSet<Favourite> Favourites { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // CartEntry — composite PK
            modelBuilder.Entity<CartEntry>()
                .HasKey(ce => new { ce.CartId, ce.ProductId });

            // Product -> Category
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany()
                .HasForeignKey(p => p.CategoryId);

            // Product -> Brand
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Brand)
                .WithMany()
                .HasForeignKey(p => p.BrandId);

            modelBuilder.Entity<Product>()
                .Property(p => p.Price)
                .HasPrecision(18, 2);

            // Cart -> User (many-to-one: user has many carts over time — active + orders)
            modelBuilder.Entity<Cart>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(c => c.OwnerId);

            // Cart -> CartOrderStatus
            modelBuilder.Entity<Cart>()
                .HasOne(c => c.Status)
                .WithMany()
                .HasForeignKey(c => c.StatusId);

            // Seed the lookup rows. To rename a status: change Name only, never change Id.
            modelBuilder.Entity<CartOrderStatus>().HasData(
                new CartOrderStatus { Id = (int)OrderStatus.Processing, Name = "В обработке" },
                new CartOrderStatus { Id = (int)OrderStatus.InTransit, Name = "В пути" },
                new CartOrderStatus { Id = (int)OrderStatus.Delivered, Name = "Отправлен" }
            );

            modelBuilder.Entity<ProductImage>()
                .HasOne(i => i.Product)
                .WithMany()
                .HasForeignKey(i => i.ProductId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ProductImage>()
                .Property(i => i.Data)
                .HasColumnType("varbinary(max)");

            // DB-level backstop: reject any blob exceeding 8 MB even if the controller is bypassed.
            // 8 388 608 = 8 * 1024 * 1024
            modelBuilder.Entity<ProductImage>()
                .ToTable(t => t.HasCheckConstraint(
                    "CK_ProductImages_DataSize",
                    "DATALENGTH([Data]) <= 8388608"));

            // ProductRating PK — Per-star counters
            modelBuilder.Entity<ProductRating>()
                .HasKey(r => new { r.UserId, r.ProductId });

            modelBuilder.Entity<ProductRating>()
                .HasOne(r => r.Product)
                .WithMany()
                .HasForeignKey(r => r.ProductId);

            // TODO: Favourites
            // modelBuilder.Entity<Favourite>().HasKey(f => new { f.UserId, f.ProductId });
        }
    }
}