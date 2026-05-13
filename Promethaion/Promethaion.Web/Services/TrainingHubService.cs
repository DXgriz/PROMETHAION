using Microsoft.AspNetCore.SignalR.Client;


namespace Promethaion.Web.Services;

public class TrainingHubService : IAsyncDisposable
{
    private HubConnection? _hub;
    private readonly string _baseUrl;

    public event Action<string>? OnTrainingStarted;
    public event Action<string, int, string>? OnPipelineProgress;
    public event Action<List<PipelineResult>>? OnTrainingCompleted;
    public event Action<string>? OnTrainingFailed;

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public TrainingHubService(IConfiguration config)
    {
        _baseUrl = config["ApiBaseUrl"] ?? "https://localhost:7001";
    }

    public async Task ConnectAsync(string gameName = "SA Lotto")
    {
        _hub = new HubConnectionBuilder()
            .WithUrl($"{_baseUrl}/hubs/training")
            .WithAutomaticReconnect()
            .Build();

        _hub.On<string>("TrainingStarted", name => OnTrainingStarted?.Invoke(name));
        _hub.On<string, int, string>("PipelineProgress", (name, pct, msg) =>
            OnPipelineProgress?.Invoke(name, pct, msg));
        _hub.On<List<PipelineResult>>("TrainingCompleted", results =>
            OnTrainingCompleted?.Invoke(results));
        _hub.On<string>("TrainingFailed", err => OnTrainingFailed?.Invoke(err));

        await _hub.StartAsync();
        await _hub.InvokeAsync("JoinTrainingRoom", gameName);
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

    public record PipelineResult(
        string PipelineName,
        string ModelVersion,
        double RSquared,
        double MeanAbsoluteError,
        bool IsBestVersion);
}