using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Product.Application;
namespace Product.Api.Controllers;

[ApiController]
[Route("api/resilience")]
public sealed class ResilienceController(IResilienceLab lab, IHostEnvironment environment) : ControllerBase
{
    [HttpGet("{policy}")]
    public async Task<ActionResult<LabResult>> Run(string policy, [Range(0, 100)] int failures = 0,
        [Range(0, 10000)] int delayMs = 0, CancellationToken ct = default)
    {
        if (!environment.IsDevelopment()) return NotFound();
        return Ok(await lab.RunAsync(policy, failures, delayMs, ct));
    }
}
