using Microsoft.EntityFrameworkCore;
using Product.Application;
namespace Product.Infrastructure;

public sealed class ProductsDbContext(DbContextOptions<ProductsDbContext> options) : DbContext(options)
{
    public DbSet<Domain.Product> Products => Set<Domain.Product>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Domain.Product>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.Property(p => p.Price).HasPrecision(18, 2);
            e.ToTable("Products", t => { t.HasCheckConstraint("CK_Price", "\"Price\" >= 0"); t.HasCheckConstraint("CK_Stock", "\"Stock\" >= 0"); });
        });
    }
}
public sealed class ProductRepository(IDbContextFactory<ProductsDbContext> factory) : IProductRepository
{
    private static ProductDto Map(Domain.Product p) => new(p.Id, p.Name, p.Price, p.Stock);
    public async Task<IReadOnlyList<ProductDto>> ListAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Products.AsNoTracking().OrderBy(p => p.Name).ThenBy(p => p.Id).Select(p => new ProductDto(p.Id, p.Name, p.Price, p.Stock)).ToListAsync(ct);
    }
    public async Task<ProductDto?> GetAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var p = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct); return p is null ? null : Map(p);
    }
    public async Task<ProductDto> CreateAsync(ProductInput input, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var p = new Domain.Product { Name = input.Name, Price = input.Price, Stock = input.Stock };
        db.Add(p); await db.SaveChangesAsync(ct); return Map(p);
    }
    public async Task<bool> UpdateAsync(Guid id, ProductInput input, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Products.Where(p => p.Id == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.Name, input.Name).SetProperty(p => p.Price, input.Price).SetProperty(p => p.Stock, input.Stock), ct) > 0;
    }
    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Products.Where(p => p.Id == id).ExecuteDeleteAsync(ct) > 0;
    }
}
