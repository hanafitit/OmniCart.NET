using Microsoft.EntityFrameworkCore;
using OmniCart.Domain.Entities;

namespace OmniCart.Infrastructure
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<UserAddress> UserAddresses { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<CartItem> CartItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Конфигурация User
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.HasIndex(e => e.TelegramUserId)
                    .IsUnique();

                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.CreatedAt);

                entity.Property(e => e.Username)
                    .IsRequired()
                    .HasMaxLength(255);

                entity.Property(e => e.FirstName)
                    .IsRequired()
                    .HasMaxLength(255);

                entity.Property(e => e.LastName)
                    .HasMaxLength(255);

                entity.Property(e => e.DeliveryAddress)
                    .HasMaxLength(500);

                entity.Property(e => e.PhoneNumber)
                    .HasMaxLength(20);
            });

            modelBuilder.Entity<UserAddress>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.AddressLine).IsRequired().HasMaxLength(500);
                entity.HasOne(e => e.User)
                    .WithMany(u => u.Addresses)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Description).HasMaxLength(2000);
                entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Stock).HasDefaultValue(0);
                entity.HasIndex(e => e.CreatedAt);

                entity.HasData(
                    new Product { Id = 1, Name = "iPhone 15 Pro", Description = "Флагманский смартфон от Apple", Price = 120000m, Stock = 10, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new Product { Id = 2, Name = "MacBook Air M3", Description = "Тонкий и легкий ноутбук с чипом M3", Price = 150000m, Stock = 5, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new Product { Id = 3, Name = "AirPods Pro 2", Description = "Наушники с активным шумоподавлением", Price = 25000m, Stock = 20, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new Product { Id = 4, Name = "Sony WH-1000XM5", Description = "Лучшие полноразмерные наушники с шумоподавлением", Price = 35000m, Stock = 15, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new Product { Id = 5, Name = "Samsung Galaxy S24 Ultra", Description = "Ультимативный флагман на Android", Price = 110000m, Stock = 8, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new Product { Id = 6, Name = "iPad Pro M4", Description = "Самый мощный планшет от Apple", Price = 130000m, Stock = 7, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new Product { Id = 7, Name = "Apple Watch Series 9", Description = "Умные часы для здоровья и спорта", Price = 45000m, Stock = 15, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new Product { Id = 8, Name = "Logitech MX Master 3S", Description = "Лучшая мышь для продуктивности", Price = 12000m, Stock = 25, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new Product { Id = 9, Name = "PlayStation 5 Slim", Description = "Игровая консоль нового поколения", Price = 55000m, Stock = 10, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new Product { Id = 10, Name = "Nintendo Switch OLED", Description = "Портативная игровая приставка", Price = 35000m, Stock = 12, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new Product { Id = 11, Name = "Steam Deck OLED", Description = "Мощная портативная игровая консоль с OLED-дисплеем", Price = 65000m, Stock = 5, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new Product { Id = 12, Name = "DJI Mini 4 Pro", Description = "Компактный дрон с профессиональной камерой", Price = 95000m, Stock = 3, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new Product { Id = 13, Name = "Keychron K2 V2", Description = "Механическая клавиатура с Bluetooth", Price = 9000m, Stock = 20, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new Product { Id = 14, Name = "Marshall Emberton II", Description = "Портативная колонка с легендарным звуком", Price = 18000m, Stock = 10, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new Product { Id = 15, Name = "Kindle Paperwhite 5", Description = "Электронная книга с 6.8-дюймовым экраном", Price = 16000m, Stock = 15, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new Product { Id = 16, Name = "Dyson V15 Detect", Description = "Мощный беспроводной пылесос с лазерной подсветкой", Price = 75000m, Stock = 8, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new Product { Id = 17, Name = "Xbox Series X", Description = "Самая мощная консоль Xbox", Price = 50000m, Stock = 6, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
                );
            });

            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TotalPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.DeliveryAddress).HasMaxLength(500);
            });

            modelBuilder.Entity<OrderItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<CartItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Quantity).HasDefaultValue(1);
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Product)
                    .WithMany()
                    .HasForeignKey(e => e.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => new { e.UserId, e.ProductId }).IsUnique();
            });
        }
    }
}
