

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model
{
    /// <summary>
    /// Caches AI trajectory analysis results per user.
    /// Re-analyzed only when new responses are submitted.
    /// </summary>
    public class UserTrajectoryCache
    {
        public int Id { get; set; }

        /// <summary>
        /// The user's email — used as the lookup key
        /// </summary>
        [Required]
        [MaxLength(256)]
        public string UserEmail { get; set; } = string.Empty;

        /// <summary>
        /// How many responses were included in this analysis
        /// </summary>
        public int AnalyzedResponseCount { get; set; }

        /// <summary>
        /// Date of the most recent response that was analyzed
        /// Used to detect new responses
        /// </summary>
        public DateTime LastResponseDate { get; set; }

        /// <summary>
        /// The full trajectory analysis result stored as JSON
        /// </summary>
        [Required]
        public string TrajectoryJson { get; set; } = string.Empty;

        /// <summary>
        /// A shorter summary that Claude can use as context
        /// for incremental updates (when only new responses are sent)
        /// </summary>
        public string? PreviousSummary { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}


