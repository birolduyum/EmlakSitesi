using Emlak.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Emlak.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<Advisor> Advisors => Set<Advisor>();
    public DbSet<Owner> Owners => Set<Owner>();
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<FeatureTag> FeatureTags => Set<FeatureTag>();
    public DbSet<Showing> Showings => Set<Showing>();
    public DbSet<CurrencyRate> CurrencyRates => Set<CurrencyRate>();
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Listing üzerinde bilerek global sorgu filtresi yok: silinenler yönetici filtresi için görünür kalır.
        b.Entity<Listing>(e =>
        {
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Currency).HasConversion<string>().HasMaxLength(3);
            e.Property(x => x.Heating).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Furnishing).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Usage).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Deed).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Kitchen).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.Property(x => x.Deposit).HasPrecision(18, 2);
            e.Property(x => x.Aidat).HasPrecision(18, 2);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Badge).HasMaxLength(50);
            e.Property(x => x.City).HasMaxLength(100);
            e.Property(x => x.District).HasMaxLength(100);
            e.Property(x => x.Neighborhood).HasMaxLength(100);
            e.Property(x => x.Rooms).HasMaxLength(20);
            e.Property(x => x.CreatedBy).HasMaxLength(100);
            e.Property(x => x.UpdatedBy).HasMaxLength(100);
            e.Property(x => x.DeletedBy).HasMaxLength(100);
            e.Property(x => x.RestoredBy).HasMaxLength(100);

            e.HasOne(x => x.Owner).WithOne().HasForeignKey<Owner>(o => o.ListingId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Photos).WithOne().HasForeignKey(p => p.ListingId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Tags).WithMany();
            e.HasOne(x => x.Advisor).WithMany().HasForeignKey(x => x.AdvisorId).OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => new { x.Type, x.IsDeleted });
            e.HasIndex(x => new { x.City, x.District });
            e.HasIndex(x => x.AdvisorId);
        });

        b.Entity<Advisor>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.WhatsApp).HasMaxLength(200);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedBy).HasMaxLength(100);
            e.Property(x => x.UpdatedBy).HasMaxLength(100);
        });

        b.Entity<Owner>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Phone).HasMaxLength(50);
        });

        b.Entity<Photo>(e => e.Property(x => x.FilePath).HasMaxLength(500));

        b.Entity<FeatureTag>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Group).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<Showing>(e =>
        {
            e.Property(x => x.Username).HasMaxLength(100);
            e.HasOne<Listing>().WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.ListingId, x.Username });
        });

        b.Entity<CurrencyRate>(e =>
        {
            e.HasKey(x => x.Currency);
            e.Property(x => x.Currency).HasConversion<string>().HasMaxLength(3).ValueGeneratedNever();
            e.Property(x => x.TryPerUnit).HasPrecision(18, 6);
            e.Property(x => x.UpdatedBy).HasMaxLength(100);
            // Yer tutucu kurlar; yönetici ayar ekranından değiştirir.
            var seedTime = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);
            e.HasData(
                new CurrencyRate { Currency = Currency.USD, TryPerUnit = 35m, UpdatedAt = seedTime, UpdatedBy = "seed" },
                new CurrencyRate { Currency = Currency.EUR, TryPerUnit = 38m, UpdatedAt = seedTime, UpdatedBy = "seed" });
        });

        b.Entity<AppUser>(e =>
        {
            e.Property(x => x.Username).HasMaxLength(100);
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Username).IsUnique();
        });
    }
}
