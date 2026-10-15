using System.ComponentModel.DataAnnotations;
namespace Product.Application;

public sealed record ProductInput([Required, StringLength(200, MinimumLength = 1)] string Name,
    [Range(typeof(decimal), "0", "999999999")] decimal Price,
    [Range(0, int.MaxValue)] int Stock);
public sealed record ProductDto(Guid Id, string Name, decimal Price, int Stock);
public interface IProductRepository
{
    Task<IReadOnlyList<ProductDto>> ListAsync(CancellationToken ct);
    Task<ProductDto?> GetAsync(Guid id, CancellationToken ct);
    Task<ProductDto> CreateAsync(ProductInput input, CancellationToken ct);
    Task<bool> UpdateAsync(Guid id, ProductInput input, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct);
}
public interface IProductService : IProductRepository;
public sealed class ProductService(IProductRepository repository) : IProductService
{
    public Task<IReadOnlyList<ProductDto>> ListAsync(CancellationToken ct) => repository.ListAsync(ct);
    public Task<ProductDto?> GetAsync(Guid id, CancellationToken ct) => repository.GetAsync(id, ct);
    public Task<ProductDto> CreateAsync(ProductInput input, CancellationToken ct) => repository.CreateAsync(input, ct);
    public Task<bool> UpdateAsync(Guid id, ProductInput input, CancellationToken ct) => repository.UpdateAsync(id, input, ct);
    public Task<bool> DeleteAsync(Guid id, CancellationToken ct) => repository.DeleteAsync(id, ct);
}
public sealed record LabResult(string Policy, int Attempts, bool Fallback, IReadOnlyList<ProductDto> Products);
public interface IResilienceLab
{
    Task<LabResult> RunAsync(string policy, int failures, int delayMs, CancellationToken ct);
}
