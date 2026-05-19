using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;

namespace Promethaion.Web.Services;

/// <summary>
/// Blazor-side SignalR client that connects to LearningHub on the API
/// and exposes C# events the Intelligence.razor page subscribes to.
/// </summary>
public class TrainingHubService : IAsyncDisposable
{
    private HubConnection? _hub;
    private readonly string _baseUrl;

    // ── Events the Intelligence page subscribes to ────────────────────────────
    public event Action<string>? OnTrainingStarted;
    public event Action<string, int, string>? OnPipelineProgress;
    public event Action<List<PipelineResult>>? OnTrainingCompleted;
    public event Action<string>? OnTrainingFailed;
    public event Action<string>? OnHarvestStarted;
    public event Action<int, int, int>? OnHarvestCompleted;

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public TrainingHubService(IConfiguration config)
    {
        _baseUrl = config["ApiBaseUrl"] ?? "https://localhost:7161";
    }

    // ── Connection ────────────────────────────────────────────────────────────

    public async Task ConnectAsync(string gameName = "SA Lotto")
    {
        _hub = new HubConnectionBuilder()
            .WithUrl($"{_baseUrl}/hubs/training")
            .WithAutomaticReconnect()
            .Build();

        // Must match exactly the event names SendAsync uses in LearningBackgroundService
        _hub.On<string>("TrainingStarted",
            name => OnTrainingStarted?.Invoke(name));

        _hub.On<string, int, string>("PipelineProgress",
            (name, pct, msg) => OnPipelineProgress?.Invoke(name, pct, msg));

        _hub.On<List<PipelineResult>>("TrainingCompleted",
            results => OnTrainingCompleted?.Invoke(results));

        _hub.On<string>("TrainingFailed",
            err => OnTrainingFailed?.Invoke(err));

        _hub.On<string>("HarvestStarted",
            name => OnHarvestStarted?.Invoke(name));

        _hub.On<int, int, int>("HarvestCompleted",
            (n, s, f) => OnHarvestCompleted?.Invoke(n, s, f));

        await _hub.StartAsync();

        // Join game-specific group so we only receive relevant updates
        await _hub.InvokeAsync("JoinLearningRoom", gameName);
    }

    public async Task DisconnectAsync()
    {
        if (_hub is not null)
            await _hub.StopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
            await _hub.DisposeAsync();
    }

    // ── Result type (mirrors the anonymous object from LearningBackgroundService) ──

    public record PipelineResult(
        string PipelineName,
        string ModelVersion,
        double RSquared,
        double MeanAbsoluteError,
        double RootMeanSquaredError,
        bool IsBestVersion);
}
