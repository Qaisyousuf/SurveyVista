using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model
{
    /// <summary>
    /// Concrete action plans for contacting/intervening with the respondent.
    /// </summary>
    public class ActionPlan
    {
        public int Id { get; set; }

        [Required]
        public int ResponseId { get; set; }

        [ForeignKey("ResponseId")]
        public Response? Response { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        /// <summary>
        /// Type: ImmediateContact, CounselingReferral, WorkplaceAccommodation, FollowUpAssessment, ManagementAlert
        /// </summary>
        [Required]
        public string ActionType { get; set; } = "ImmediateContact";

        /// <summary>
        /// Priority: Urgent, High, Normal, Low
        /// </summary>
        [Required]
        public string Priority { get; set; } = "Normal";

        /// <summary>
        /// Status: Pending, InProgress, Completed, Cancelled
        /// </summary>
        public string Status { get; set; } = "Pending";

        public string? AssignedTo { get; set; }

        public string? AssignedToEmail { get; set; }

        public DateTime? ScheduledDate { get; set; }

        public DateTime? CompletedDate { get; set; }

        public string? CompletionNotes { get; set; }

        public string CreatedByName { get; set; } = string.Empty;

        public string? CreatedByEmail { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}