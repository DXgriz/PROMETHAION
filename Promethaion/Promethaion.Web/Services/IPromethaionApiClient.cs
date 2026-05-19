using Promethaion.Web.Models;
using static Promethaion.Web.Models.ApiModels;

namespace Promethaion.Web.Services
{
        public interface IPromethaionApiClient
        {
            // Events
            Task<List<PatternEventDto>> GetAllEventsAsync(string gameName = "SA Lotto");
            Task<List<PatternEventDto>> GetRecentEventsAsync(int count, string gameName = "SA Lotto");
            Task<int> GetEventCountAsync(string gameName = "SA Lotto");
            Task<PatternEventDto?> AddEventAsync(AddPatternEventDto dto);
            Task<(int Imported, int Skipped)> ImportCsvAsync(Stream fileStream, string fileName, string gameName = "SA Lotto");
            Task<int> SeedSampleDataAsync(int drawCount = 200, string gameName = "SA Lotto");

        Task<HarvestResultDto?> HarvestLatestAsync(string gameName);

        // Forecasts
        Task<List<ForecastDto>> GetAllForecastsAsync();
            Task<ForecastDto?> GetLatestForecastAsync();
            Task<ForecastDto?> GenerateForecastAsync(string gameName = "SA Lotto");
            Task<List<BallScoreDto>> GetBallScoresAsync(string gameName = "SA Lotto");
            Task<object?> EvaluateForecastAsync(int forecastId, int actualDrawNumber, string gameName = "SA Lotto");
            Task<AccuracyDto?> GetAccuracyAsync(int window = 20);

            // Intelligence / Models
            Task<SystemStatusDto?> GetSystemStatusAsync(string gameName = "SA Lotto");
            Task<List<TrainingMetricsDto>> GetAllMetricsAsync();
            Task<List<TrainingMetricsDto>> GetMetricsByPipelineAsync(string pipelineName);
            Task TriggerTrainingAsync(string gameName = "SA Lotto");
            Task LoadModelsAsync();
        }
    
}


