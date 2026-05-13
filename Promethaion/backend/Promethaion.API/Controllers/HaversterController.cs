using Microsoft.AspNetCore.Mvc;
using Promethaion.Core.Interfaces;

namespace Promethaion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HarvesterController : ControllerBase
{
    private readonly IDataHarvester _harvester;

    public HarvesterController(IDataHarvester harvester) =>
        _harvester = harvester;

    /// <summary>
    /// Manually trigger a full historical sync.
    /// Fetches all months from lotteryresults.co.za and saves any missing draws.
    /// This can take 30–60 seconds for the first run (25+ years of data).
    /// </summary>
    [HttpPost("sync/full")]
    public async Task<IActionResult> FullSync(
        [FromQuery] string gameName = "SA Lotto",
        CancellationToken ct = default)
    {
        var result = await _harvester.SyncAsync(gameName, ct);
        return Ok(result);
    }

    /// <summary>
    /// Sync only the current and previous month.
    /// Use this to pick up the latest draw results quickly.
    /// </summary>
    [HttpPost("sync/latest")]
    public async Task<IActionResult> LatestSync(
        [FromQuery] string gameName = "SA Lotto",
        CancellationToken ct = default)
    {
        var result = await _harvester.SyncLatestAsync(gameName, ct);
        return Ok(result);
    }
}