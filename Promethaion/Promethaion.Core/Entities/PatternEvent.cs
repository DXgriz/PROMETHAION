using System;
using System.Collections.Generic;
using System.Text;

namespace Promethaion.Core.Entities
{
    /// <summary>
    /// Represents a single historical result.
    /// </summary>
    public class PatternEvent
    {
        public int Id { get; set; }

        /// <summary>Draw date (used as a feature: day-of-week, week-of-year, etc.).</summary>
        public DateTime DrawDate { get; set; }

        /// <summary>Draw number in the sequence (monotonically increasing).</summary>
        public int DrawNumber { get; set; }

        // The 6 main numbers drawn (sorted ascending).
        public int Ball1 { get; set; }
        public int Ball2 { get; set; }
        public int Ball3 { get; set; }
        public int Ball4 { get; set; }
        public int Ball5 { get; set; }
        public int Ball6 { get; set; }

        /// <summary>Optional bonus/power ball.</summary>
        public int? BonusBall { get; set; }

        /// <summary>Free-text source identifier (e.g. "SA Lotto", "Powerball").</summary>
        public string GameName { get; set; } = "SA Lotto";

        // Computed helpers — not persisted.
        public int[] Balls => [Ball1, Ball2, Ball3, Ball4, Ball5, Ball6];
        public int Sum => Balls.Sum();
        public int Range => Ball6 - Ball1;
        public bool HasConsecutive => Balls.Zip(Balls.Skip(1)).Any(p => p.Second - p.First == 1);
        public int OddCount => Balls.Count(b => b % 2 != 0);
        public int EvenCount => 6 - OddCount;
    }
}
