using Microsoft.AspNetCore.SignalR;

namespace Promethaion.API.Hubs;

/// <summary>
/// SignalR hub that broadcasts real-time training and harvest
/// progress to connected Blazor clients.
///
/// Clients connect to /hubs/training and listen for these events:
///
///   TrainingStarted(string gameName)
///   PipelineProgress(string pipelineName, int percent, string message)
///   TrainingCompleted(object[] metricsArray)
///   TrainingFailed(string errorMessage)
///   HarvestStarted(string gameName)
///   HarvestCompleted(int newRecords, int skipped, int failed)
/// </summary>
public class LearningHub : Hub
{
    /// <summary>
    /// Called by the Blazor client to subscribe to updates
    /// for a specific game (e.g. "SA Lotto").
    /// </summary>
    public async Task JoinLearningRoom(string gameName)
        => await Groups.AddToGroupAsync(Context.ConnectionId, gameName);

    /// <summary>
    /// Called by the Blazor client to unsubscribe.
    /// </summary>
    public async Task LeaveLearningRoom(string gameName)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, gameName);
}
