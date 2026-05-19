using Promethaion.Web.Models;
using System.Net.Http.Json;
using static Promethaion.Web.Models.ApiModels;


namespace Promethaion.Web.Services;

public class PromethaionApiClient : IPromethaionApiClient
{
    private readonly HttpClient _http;

    public PromethaionApiClient(HttpClient http) => _http = http;

    // ── Events ───────────────────────────────────────────────────────────────

    public async Task<List<PatternEventDto>> GetAllEventsAsync(string gameName = "SA Lotto") =>
        await _http.GetFromJsonAsync<List<PatternEventDto>>($"api/events?gameName={Uri.EscapeDataString(gameName)}") ?? [];

    public async Task<List<PatternEventDto>> GetRecentEventsAsync(int count, string gameName = "SA Lotto") =>
        await _http.GetFromJsonAsync<List<PatternEventDto>>($"api/events/recent/{count}?gameName={Uri.EscapeDataString(gameName)}") ?? [];

    public async Task<int> GetEventCountAsync(string gameName = "SA Lotto")
    {
        var result = await _http.GetFromJsonAsync<CountResult>($"api/events/count?gameName={Uri.EscapeDataString(gameName)}");
        return result?.Count ?? 0;
    }

    public async Task<PatternEventDto?> AddEventAsync(AddPatternEventDto dto)
    {
        var response = await _http.PostAsJsonAsync("api/events", dto);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PatternEventDto>()
            : null;
    }

    public async Task<(int Imported, int Skipped)> ImportCsvAsync(Stream fileStream, string fileName, string gameName = "SA Lotto")
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "file", fileName);
        var response = await _http.PostAsync($"api/events/import/csv?gameName={Uri.EscapeDataString(gameName)}", content);
        if (!response.IsSuccessStatusCode) return (0, 0);
        var result = await response.Content.ReadFromJsonAsync<ImportResult>();
        return (result?.Imported ?? 0, result?.Skipped ?? 0);
    }

    public async Task<int> SeedSampleDataAsync(int drawCount = 200, string gameName = "SA Lotto")
    {
        var response = await _http.PostAsync($"api/events/seed/sample?drawCount={drawCount}&gameName={Uri.EscapeDataString(gameName)}", null);
        if (!response.IsSuccessStatusCode) return 0;
        var result = await response.Content.ReadFromJsonAsync<SeedResult>();
        return result?.Seeded ?? 0;
    }

    public async Task<HarvestResultDto?> HarvestLatestAsync(string gameName)
    {
        return await _http.GetFromJsonAsync<HarvestResultDto>(
            $"api/events/harvest/latest?gameName={Uri.EscapeDataString(gameName)}");
    }
    // ── Forecasts ─────────────────────────────────────────────────────────────

    public async Task<List<ForecastDto>> GetAllForecastsAsync() =>
        await _http.GetFromJsonAsync<List<ForecastDto>>("api/forecasts") ?? [];

    public async Task<ForecastDto?> GetLatestForecastAsync() =>
        await _http.GetFromJsonAsync<ForecastDto>("api/forecasts/latest");

    public async Task<ForecastDto?> GenerateForecastAsync(string gameName = "SA Lotto")
    {
        var response = await _http.PostAsync($"api/forecasts/generate?gameName={Uri.EscapeDataString(gameName)}", null);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ForecastDto>()
            : null;
    }

    public async Task<List<BallScoreDto>> GetBallScoresAsync(string gameName = "SA Lotto") =>
        await _http.GetFromJsonAsync<List<BallScoreDto>>($"api/forecasts/scores?gameName={Uri.EscapeDataString(gameName)}") ?? [];

    public async Task<object?> EvaluateForecastAsync(int forecastId, int actualDrawNumber, string gameName = "SA Lotto")
    {
        var response = await _http.PostAsync(
            $"api/forecasts/{forecastId}/evaluate/{actualDrawNumber}?gameName={Uri.EscapeDataString(gameName)}", null);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<object>()
            : null;
    }

    public async Task<AccuracyDto?> GetAccuracyAsync(int window = 20) =>
        await _http.GetFromJsonAsync<AccuracyDto>($"api/forecasts/accuracy?window={window}");

    // ── Intelligence ──────────────────────────────────────────────────────────

    public async Task<SystemStatusDto?> GetSystemStatusAsync(string gameName = "SA Lotto") =>
        await _http.GetFromJsonAsync<SystemStatusDto>($"api/intelligence/status?gameName={Uri.EscapeDataString(gameName)}");

    public async Task<List<TrainingMetricsDto>> GetAllMetricsAsync() =>
        await _http.GetFromJsonAsync<List<TrainingMetricsDto>>("api/intelligence/metrics") ?? [];

    public async Task<List<TrainingMetricsDto>> GetMetricsByPipelineAsync(string pipelineName) =>
        await _http.GetFromJsonAsync<List<TrainingMetricsDto>>($"api/intelligence/metrics/{pipelineName}") ?? [];

    public async Task TriggerTrainingAsync(string gameName = "SA Lotto") =>
        await _http.PostAsync($"api/intelligence/train?gameName={Uri.EscapeDataString(gameName)}", null);

    public async Task LoadModelsAsync() =>
        await _http.PostAsync("api/intelligence/load", null);

    // ── Private helpers ───────────────────────────────────────────────────────
    private record CountResult(int Count);
    private record ImportResult(int Imported, int Skipped);
    private record SeedResult(int Seeded);
}