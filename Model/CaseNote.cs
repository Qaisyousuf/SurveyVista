using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model
{
    public class CaseNote
    {
        public int Id { get; set; }

        [Required]
        public int ResponseId { get; set; }

        [ForeignKey("ResponseId")]
        public Response? Response { get; set; }

        [Required]
        public string AuthorName { get; set; } = string.Empty;

        public string? AuthorEmail { get; set; }

        [Required]
        public string NoteText { get; set; } = string.Empty;

        /// <summary>
        /// Category: General, Risk, Intervention, FollowUp, Resolution
        /// </summary>
        public string Category { get; set; } = "General";

        public bool IsConfidential { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}