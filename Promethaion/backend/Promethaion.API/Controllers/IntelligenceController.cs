using Microsoft.AspNetCore.Mvc;
using Promethaion.API.Services;
using Promethaion.ML.Pipelines;

namespace Promethaion.API.Controllers
{
    [ApiController]
    [Route("api/intelligence")]
    public class IntelligenceController(PredictionService predictionService) : ControllerBase
    {
        [HttpGet("status")]
        public IActionResult Status(string gameName)
        {
            return Ok(new { gameName, status = "Idle" });
        }

        [HttpGet("metrics")]
        public IActionResult Metrics()
        {
            return Ok(new { accuracy = 0.0 });
        }

        [HttpPost("load")]
        public IActionResult Load()
        {
            return Ok(new { message = "Model loaded ✅" });
        }

        [HttpPost("train")]
        public IActionResult Train(string gameName)
        {
            return Ok(new { message = $"Training {gameName} ✅" });
        }

        [HttpGet("predict")]
        public async Task<IActionResult> Predict(string gameName = "SA Lotto")
        {
            try
            {
                var result = await predictionService.PredictAsync(gameName);

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}