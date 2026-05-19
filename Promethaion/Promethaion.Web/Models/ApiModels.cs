namespace Promethaion.Web.Models
{
    public class ApiModels
    {
        public class PatternEventDto
        {
            public int Id { get; set; }
            public int DrawNumber { get; set; }
            public DateTime DrawDate { get; set; }

            public int Ball1 { get; set; }
            public int Ball2 { get; set; }
            public int Ball3 { get; set; }
            public int Ball4 { get; set; }
            public int Ball5 { get; set; }
            public int Ball6 { get; set; }

            public int? BonusBall { get; set; }

            public int Sum { get; set; }
            public int Range { get; set; }
            public int OddCount { get; set; }
            public int EvenCount { get; set; }

            public string GameName { get; set; } = "SA Lotto";
        }

        public class ForecastDto
        {
            public int Id { get; set; }
            public DateTime GeneratedAt { get; set; }
            public int TargetDrawNumber { get; set; }

            public int Ball1 { get; set; }
            public int Ball2 { get; set; }
            public int Ball3 { get; set; }
            public int Ball4 { get; set; }
            public int Ball5 { get; set; }
            public int Ball6 { get; set; }

            public int? BonusBall { get; set; }

            public double ConfidenceScore { get; set; }

            public Dictionary<string, double> PerBallConfidence { get; set; } = new();

            public string ModelVersion { get; set; } = "";

            public int? MatchCount { get; set; }

            public int? ActualDrawNumber { get; set; }
        }

        public class TrainingMetricsDto
        {
            public int Id { get; set; }

            public string PipelineName { get; set; } = "";

            public string ModelVersion { get; set; } = "";

            public DateTime TrainedAt { get; set; }

            public int TrainingSetSize { get; set; }

            public int TestSetSize { get; set; }

            public double MeanAbsoluteError { get; set; }

            public double RootMeanSquaredError { get; set; }

            public double RSquared { get; set; }

            public double AverageMatchCount { get; set; }

            public bool IsBestVersion { get; set; }
        }

        public class SystemStatusDto
        {
            public int TotalDraws { get; set; }

            public int TotalPredictions { get; set; }

            public int EvaluatedPredictions { get; set; }

            public double RollingAccuracy { get; set; }

            public bool ShouldRetrain { get; set; }

            public bool ModelsLoaded { get; set; }

            public DateTime? LastTrainedAt { get; set; }

            public string LatestModelVersion { get; set; } = "";
        }

        public class BallScoreDto
        {
            public int Ball { get; set; }

            public double Score { get; set; }

            public int Rank { get; set; }
        }

        public class AddPatternEventDto
        {
            public int DrawNumber { get; set; }

            public DateTime DrawDate { get; set; }

            public int Ball1 { get; set; }

            public int Ball2 { get; set; }

            public int Ball3 { get; set; }

            public int Ball4 { get; set; }

            public int Ball5 { get; set; }

            public int Ball6 { get; set; }

            public int? BonusBall { get; set; }

            public string GameName { get; set; } = "SA Lotto";
        }

        public class AccuracyDto
        {
            public double RollingAccuracy { get; set; }

            public bool ShouldRetrain { get; set; }

            public int Window { get; set; }
        }

        public class HarvestResultDto
        {
            public int NewRecords { get; set; }

            public int SkippedDuplicates { get; set; }

            public int FailedRows { get; set; }

            public string? Message { get; set; }
        }
    }
}