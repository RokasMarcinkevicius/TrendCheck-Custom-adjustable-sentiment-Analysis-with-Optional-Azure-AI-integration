using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class NewsController(INewsAggregator aggregator, IArticleStore store) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? q, [FromQuery] string[]? tickers, [FromQuery] int limit = 100, CancellationToken ct = default)
    {
        var res = await store.GetAsync(new NewsQuery(q, tickers, Math.Clamp(limit, 1, 500)), ct);
        return Ok(res);
    }

    [HttpGet("sources")]
    public async Task<IActionResult> Sources(CancellationToken ct = default)
    {
        var res = await store.GetSourcesAsync(ct);
        return Ok(res);
    }

    public sealed record PollRequest(string? q, string[]? tickers, int limit = 100);

    [HttpPost("poll")]
    public async Task<IActionResult> Poll([FromBody] PollRequest req, CancellationToken ct = default)
    {
        var count = await aggregator.PollAllAsync(new NewsQuery(req.q, req.tickers, Math.Clamp(req.limit, 1, 500)), ct);
        return Ok(new { fetched = count });
    }
}