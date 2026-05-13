using Microsoft.AspNetCore.SignalR;
using System.Text.RegularExpressions;

namespace Promethaion.API.Hubs
{
    /// <summary>
    /// Broadcasts real-time training progress to connected clients.
    /// Clients connect to /hubs/training and listen for these events:
    ///   - TrainingStarted(gameName)
    ///   - PipelineProgress(pipelineName, percent, message)
    ///   - TrainingCompleted(metricsJson)
    ///   - TrainingFailed(errorMessage)
    ///   - PredictionGenerated(predictionDto)
    /// </summary>
    public class LearningHub : Hub
    {
        public async Task JoinTrainingRoom(string gameName)
        => await Groups.AddToGroupAsync(Context.ConnectionId, gameName);

        public async Task LeaveTrainingRoom(string gameName)
            => await Groups.RemoveFromGroupAsync(Context.ConnectionId, gameName);
    }
}
