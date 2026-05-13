using Promethaion.Core.Entities;
using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace Promethaion.Data.Seeders;

/// <summary>
/// Imports history from a CSV file.
///
/// Expected CSV columns (case-insensitive):
///   DrawNumber, DrawDate, Ball1, Ball2, Ball3, Ball4, Ball5, Ball6, BonusBall (optional)
///
/// Example row:
///   1,2000-01-05,3,11,18,24,32,49,7
/// </summary>
internal class EventDataImporter
{
    public static IReadOnlyList<PatternEvent> Import(string filePath, string gameName = "SA Lotto")
    {
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null
        });

        var records = new List<PatternEvent>();
        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
            var balls = new[]
            {
                csv.GetField<int>("Ball1"),
                csv.GetField<int>("Ball2"),
                csv.GetField<int>("Ball3"),
                csv.GetField<int>("Ball4"),
                csv.GetField<int>("Ball5"),
                csv.GetField<int>("Ball6")
            };
            Array.Sort(balls);

            int? bonus = null;
            if (csv.TryGetField<int>("BonusBall", out var b)) bonus = b;

            records.Add(new PatternEvent
            {
                DrawNumber = csv.GetField<int>("DrawNumber"),
                DrawDate = csv.GetField<DateTime>("DrawDate"),
                Ball1 = balls[0],
                Ball2 = balls[1],
                Ball3 = balls[2],
                Ball4 = balls[3],
                Ball5 = balls[4],
                Ball6 = balls[5],
                BonusBall = bonus,
                GameName = gameName
            });
        }

        return records.OrderBy(r => r.DrawNumber).ToList();
    }

    /// <summary>Generates a sample CSV with synthetic data so you can test immediately.</summary>
    public static void GenerateSampleCsv(string outputPath, int drawCount = 200)
    {
        var rng = new Random(42);
        using var writer = new StreamWriter(outputPath);
        writer.WriteLine("DrawNumber,DrawDate,Ball1,Ball2,Ball3,Ball4,Ball5,Ball6,BonusBall");

        var date = new DateTime(2020, 1, 4); // first Saturday of 2020
        for (int i = 1; i <= drawCount; i++)
        {
            // Pick 6 unique numbers from 1–52
            var pool = Enumerable.Range(1, 52).OrderBy(_ => rng.Next()).Take(7).OrderBy(x => x).ToArray();
            var bonus = pool[6];
            var main = pool[..6];

            writer.WriteLine($"{i},{date:yyyy-MM-dd},{main[0]},{main[1]},{main[2]},{main[3]},{main[4]},{main[5]},{bonus}");
            date = date.AddDays(7); // weekly draw
        }
    }
}
