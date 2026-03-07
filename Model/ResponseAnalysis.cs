// Model/ResponseAnalysis.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model
{
    public class ResponseAnalysis
    {
        [Key]
        public int Id { get; set; }

        // Links to existing tables
        [Required]
        public int ResponseId { get; set; }

        [Required]
        public int QuestionId { get; set; }

        // Question context (not PII)
        public string QuestionText { get; set; } = string.Empty;

        // Anonymized only — never store raw user text here
        public string AnonymizedText { get; set; } = string.Empty;

        // Sentiment (from Claude)
        public string SentimentLabel { get; set; } = "Neutral"; // Positive, Negative, Neutral, Mixed
        public double SentimentConfidence { get; set; }
        public double PositiveScore { get; set; }
        public double NegativeScore { get; set; }
        public double NeutralScore { get; set; }

        // Risk Assessment (from Claude)
        public string RiskLevel { get; set; } = "Low"; // Low, Moderate, High, Critical
        public double RiskScore { get; set; }
        public bool RequiresImmediateAttention { get; set; }
        public string RecommendedAction { get; set; } = string.Empty;

        // Complex data stored as JSON strings
        public string RiskIndicatorsJson { get; set; } = "[]";
        public string ProtectiveFactorsJson { get; set; } = "[]";
        public string KeyPhrasesJson { get; set; } = "[]";
        public string WorkplaceFactorsJson { get; set; } = "[]";
        public string EmotionalIndicatorsJson { get; set; } = "[]";
        public string InsightsJson { get; set; } = "[]"; // WorkplaceInsight list
        public string CategoriesJson { get; set; } = "[]";

        // Metadata
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("ResponseId")]
        public virtual Response? Response { get; set; }

        [ForeignKey("QuestionId")]
        public virtual Question? Question { get; set; }
    }
}