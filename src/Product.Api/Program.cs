using Microsoft.EntityFrameworkCore;
using Product.Application;
using Product.Infrastructure;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<Product.Api.ApiExceptionHandler>();
builder.Services.AddDbContextFactory<ProductsDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("Products") ?? throw new InvalidOperationException("ConnectionStrings:Products is required")));
builder.Services.AddSingleton<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IResilienceLab, ResilienceLab>();
var app = builder.Build();
app.UseExceptionHandler();
app.MapControllers();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "swagger";
        options.SwaggerEndpoint("../openapi/v1.json", "Product Resilience API v1");
        options.EnableTryItOutByDefault();
    });
}
if (args.Contains("--initialize-db"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ProductsDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
    return;
}
app.Run();
public partial class Program;
