using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace Promethaion.ML.Models;

/// <summary>
/// Feature row fed into all ML.NET pipelines.
/// Each row represents a single (draw × ballNumber) observation.
/// </summary>
public class FeatureVector
{
    [ColumnName("Label")]
    public float Label { get; set; }            // 1 = drawn, 0 = not drawn

    [ColumnName(nameof(BallNumber))]
    public float BallNumber { get; set; }

    [ColumnName(nameof(DrawIndex))]
    public float DrawIndex { get; set; }

    [ColumnName(nameof(DayOfWeek))]
    public float DayOfWeek { get; set; }        // 0=Sun … 6=Sat

    [ColumnName(nameof(WeekOfYear))]
    public float WeekOfYear { get; set; }       // 1–53

    [ColumnName(nameof(MonthOfYear))]
    public float MonthOfYear { get; set; }

    [ColumnName(nameof(GlobalFrequency))]
    public float GlobalFrequency { get; set; }  // fraction of all draws

    [ColumnName(nameof(Freq10))]
    public float Freq10 { get; set; }           // last 10 draws

    [ColumnName(nameof(Freq20))]
    public float Freq20 { get; set; }

    [ColumnName(nameof(Freq52))]
    public float Freq52 { get; set; }

    [ColumnName(nameof(GapSinceSeen))]
    public float GapSinceSeen { get; set; }     // draws since last appearance

    [ColumnName(nameof(IsOverdue))]
    public float IsOverdue { get; set; }        // 1 = gap > 10

    [ColumnName(nameof(AvgPositionWhenDrawn))]
    public float AvgPositionWhenDrawn { get; set; } // 1–6

    [ColumnName(nameof(PairFreqWithPrev))]
    public float PairFreqWithPrev { get; set; } // co-occurrence with previous draw

    [ColumnName(nameof(DistFromLastMedian))]
    public float DistFromLastMedian { get; set; }
}

/// <summary>
/// Output from regression / binary classification models.
/// Score = predicted probability that this ball will be drawn next.
/// </summary>
public class ScoredOutcome
{
    [ColumnName("Score")]
    public float Score { get; set; }

    [ColumnName("Probability")]
    public float Probability { get; set; }
}


