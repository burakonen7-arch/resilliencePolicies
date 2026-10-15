using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Polly;
using Npgsql;
using Polly.CircuitBreaker;
using Polly.Fallback;
using Polly.Hedging;
using Polly.Retry;
using Polly.Timeout;
using Product.Application;
namespace Product.Infrastructure;

public sealed class SimulatedFailureException : Exception;
public sealed class ResilienceLab(IProductRepository repository, IMemoryCache cache) : IResilienceLab, IDisposable
{
    private readonly ConcurrentDictionary<string, ResiliencePipeline<IReadOnlyList<ProductDto>>> pipelines = new();
    private readonly List<IDisposable> resources = [];
    private static PredicateBuilder<IReadOnlyList<ProductDto>> Transient() => new PredicateBuilder<IReadOnlyList<ProductDto>>().Handle<SimulatedFailureException>().Handle<TimeoutRejectedException>().Handle<NpgsqlException>(e => e.IsTransient);
    private ResiliencePipeline<IReadOnlyList<ProductDto>> Build(string policy)
    {
        var b = new ResiliencePipelineBuilder<IReadOnlyList<ProductDto>>();
        if (policy is "fallback" or "combined") b.AddFallback(new FallbackStrategyOptions<IReadOnlyList<ProductDto>>
        {
            ShouldHandle = Transient().Handle<BrokenCircuitException>(),
            FallbackAction = _ => Outcome.FromResultAsValueTask<IReadOnlyList<ProductDto>>(Array.Empty<ProductDto>())
        });
        if (policy is "retry" or "combined") b.AddRetry(new RetryStrategyOptions<IReadOnlyList<ProductDto>>
        {
            ShouldHandle = Transient(),
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(100),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true
        });
        if (policy is "circuit-breaker" or "combined") b.AddCircuitBreaker(new CircuitBreakerStrategyOptions<IReadOnlyList<ProductDto>>
        {
            ShouldHandle = Transient(),
            FailureRatio = 0.5,
            MinimumThroughput = 4,
            SamplingDuration = TimeSpan.FromSeconds(10),
            BreakDuration = TimeSpan.FromSeconds(5)
        });
        if (policy is "rate-limit" or "bulkhead")
        {
            RateLimiter limiter = policy == "bulkhead"
                ? new ConcurrencyLimiter(new ConcurrencyLimiterOptions { PermitLimit = 2, QueueLimit = 0 })
                : new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions { PermitLimit = 3, Window = TimeSpan.FromSeconds(5), QueueLimit = 0 });
            lock (resources) resources.Add(limiter);
            b.AddRateLimiter(limiter);
        }
        if (policy == "hedging") b.AddHedging(new HedgingStrategyOptions<IReadOnlyList<ProductDto>>
        {
            ShouldHandle = Transient(),
            MaxHedgedAttempts = 2,
            Delay = TimeSpan.FromMilliseconds(100)
        });
        if (policy is "timeout" or "combined" or "hedging") b.AddTimeout(TimeSpan.FromSeconds(1));
        return b.Build();
    }
    public async Task<LabResult> RunAsync(string policy, int failures, int delayMs, CancellationToken ct)
    {
        string[] supported = ["retry", "circuit-breaker", "timeout", "fallback", "hedging", "rate-limit", "bulkhead", "cache", "combined"];
        if (!supported.Contains(policy)) throw new ArgumentException("Unknown policy: " + policy);
        if (policy == "cache" && cache.TryGetValue<IReadOnlyList<ProductDto>>("products", out var hit)) return new(policy, 0, false, hit!);
        int attempts = 0;
        var succeeded = 0;
        async ValueTask<IReadOnlyList<ProductDto>> Read(CancellationToken token)
        {
            var attempt = Interlocked.Increment(ref attempts);
            if (delayMs > 0) await Task.Delay(delayMs, token);
            if (attempt <= failures) throw new SimulatedFailureException();
            var result = await repository.ListAsync(token);
            Interlocked.Exchange(ref succeeded, 1);
            return result;
        }
        var pipeline = pipelines.GetOrAdd(policy, Build);
        var products = await pipeline.ExecuteAsync(Read, ct);
        if (policy == "cache") cache.Set("products", products, TimeSpan.FromSeconds(10));
        return new(policy, attempts, succeeded == 0, products);
    }
    public void Dispose() { foreach (var resource in resources) resource.Dispose(); }
}
