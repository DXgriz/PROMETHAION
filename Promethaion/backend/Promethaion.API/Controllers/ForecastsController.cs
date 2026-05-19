using Microsoft.AspNetCore.Mvc;

namespace Promethaion.API.Controllers
{
    [ApiController]
    [Route("api/forecasts")]
    public class ForecastsController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetAll()
        {
            return Ok(new { message = "Forecasts working ✅" });
        }

        [HttpGet("latest")]
        public IActionResult Latest()
        {
            return Ok(new { message = "Latest forecast ✅" });
        }

        [HttpGet("accuracy")]
        public IActionResult Accuracy(int window)
        {
            return Ok(new { window, accuracy = 0.0 });
        }

        [HttpGet("scores")]
        public IActionResult Scores(string gameName)
        {
            return Ok(new { gameName, scores = new int[] { } });
        }
    }
}
