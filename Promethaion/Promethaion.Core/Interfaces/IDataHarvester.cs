using System;
using System.Collections.Generic;
using System.Text;
using Promethaion.Core.Entities;

/// <summary>
/// Contract for any service that fetches and normalises
/// external draw result data into PatternEvent records.
/// </summary>
public interface IDataHarvester
{
    string SourceName { get; }

    /// <summary>
    /// Fetches all available draw results from the source,
    /// skipping any draw numbers already in the database.
    /// Returns the number of new records saved.
    /// </summary>
    Task<HarvestResult> SyncAsync(string gameName = "SA Lotto", CancellationToken ct = default);

    /// <summary>
    /// Fetches only the most recent month — used for weekly auto-sync.
    /// </summary>
    Task<HarvestResult> SyncLatestAsync(string gameName = "SA Lotto", CancellationToken ct = default);
}

public record HarvestResult(
    int NewRecords,
    int SkippedDuplicates,
    int FailedRows,
    string SourceName,
    DateTime HarvestedAt,
    List<string> Errors);