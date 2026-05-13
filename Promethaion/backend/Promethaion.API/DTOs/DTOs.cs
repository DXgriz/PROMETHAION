namespace Promethaion.API.DTOs;

public class DTOs
{
    public record PatternEventDto(
    int Id,
    int DrawNumber,
    DateTime DrawDate,
    int Ball1, int Ball2, int Ball3, int Ball4, int Ball5, int Ball6,
    int? BonusBall,
    int Sum, int Range, int OddCount, int EvenCount,
    string GameName);

    public record PatternForecastDto(
        int Id,
        DateTime GeneratedAt,
        int TargetDrawNumber,
        int Ball1, int Ball2, int Ball3, int Ball4, int Ball5, int Ball6,
        int? BonusBall,
        double ConfidenceScore,
        Dictionary<string, double> PerBallConfidence,
        string ModelVersion,
        int? MatchCount,
        int? ActualDrawNumber);

    public record TrainingMetricsDto(
        int Id,
        string PipelineName,
        string ModelVersion,
        DateTime TrainedAt,
        int TrainingSetSize,
        int TestSetSize,
        double MeanAbsoluteError,
        double RootMeanSquaredError,
        double RSquared,
        double AverageMatchCount,
        bool IsBestVersion);

    public record TrainRequestDto(string GameName = "SA Lotto");

    public record AddEventDto(
        int DrawNumber,
        DateTime DrawDate,
        int Ball1, int Ball2, int Ball3, int Ball4, int Ball5, int Ball6,
        int? BonusBall,
        string GameName = "SA Lotto");

    public record SystemStatusDto(
        int TotalDraws,
        int TotalPredictions,
        int EvaluatedPredictions,
        double RollingAccuracy,
        bool ShouldRetrain,
        bool ModelsLoaded,
        DateTime? LastTrainedAt,
        string LatestModelVersion);


}
