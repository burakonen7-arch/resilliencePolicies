using Microsoft.AspNetCore.Mvc;
using Product.Application;
namespace Product.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(IProductService service) : ControllerBase
{
    [HttpGet] public async Task<ActionResult<IReadOnlyList<ProductDto>>> List(CancellationToken ct) => Ok(await service.ListAsync(ct));
    [HttpGet("{id:guid}")] public async Task<ActionResult<ProductDto>> Get(Guid id, CancellationToken ct) => await service.GetAsync(id, ct) is { } p ? Ok(p) : NotFound();
    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(ProductInput input, CancellationToken ct)
    {
        var p = await service.CreateAsync(input, ct); return CreatedAtAction(nameof(Get), new { id = p.Id }, p);
    }
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, ProductInput input, CancellationToken ct) => await service.UpdateAsync(id, input, ct) ? NoContent() : NotFound();
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) => await service.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
