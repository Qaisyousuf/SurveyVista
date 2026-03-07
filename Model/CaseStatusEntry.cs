using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model
{
    /// <summary>
    /// Tracks the status workflow: New → UnderReview → InterventionScheduled → Resolved
    /// Each entry is a log of a status change (full audit trail)
    /// </summary>
    public class CaseStatusEntry
    {
        public int Id { get; set; }

        [Required]
        public int ResponseId { get; set; }

        [ForeignKey("ResponseId")]
        public Response? Response { get; set; }

        [Required]
        public CaseStatusType Status { get; set; } = CaseStatusType.New;

        public string? ChangedByName { get; set; }

        public string? ChangedByEmail { get; set; }

        /// <summary>
        /// Optional reason/note for the status change
        /// </summary>
        public string? Reason { get; set; }

        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    }

    public enum CaseStatusType
    {
        New = 0,
        UnderReview = 1,
        InterventionScheduled = 2,
        InProgress = 3,
        FollowUp = 4,
        Resolved = 5,
        Closed = 6
    }
}
