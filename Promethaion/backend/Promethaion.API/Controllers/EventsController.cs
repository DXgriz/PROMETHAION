using Microsoft.AspNetCore.Mvc;


namespace Promethaion.API.Controllers
{
    [ApiController]
    [Route("api/events")]
    public class EventsController : ControllerBase
    {
        // GET: /api/events
        [HttpGet]
        public IActionResult GetAll()
        {
            return Ok(new { message = "Events endpoint working ✅" });
        }

        // GET: /api/events/recent/8
        [HttpGet("recent/{count}")]
        public IActionResult GetRecent(int count)
        {
            return Ok(new
            {
                message = $"Returning {count} recent events ✅"
            });
        }

        // GET: /api/events/count
        [HttpGet("count")]
        public IActionResult Count()
        {
            return Ok(0);
        }

        [HttpGet("harvest/latest")]
        public async Task<IActionResult> HarvestLatest(
    [FromServices] IDataHarvester harvester,
    string gameName = "SA Lotto")
        {
            var result = await harvester.SyncLatestAsync(gameName);
            return Ok(result);
        }
    }
}
