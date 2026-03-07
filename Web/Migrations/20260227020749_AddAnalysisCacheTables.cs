using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisCacheTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuestionnaireAnalysisSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuestionnaireId = table.Column<int>(type: "int", nullable: false),
                    TotalResponses = table.Column<int>(type: "int", nullable: false),
                    AnalyzedResponses = table.Column<int>(type: "int", nullable: false),
                    OverallPositiveSentiment = table.Column<double>(type: "float", nullable: false),
                    OverallNegativeSentiment = table.Column<double>(type: "float", nullable: false),
                    OverallNeutralSentiment = table.Column<double>(type: "float", nullable: false),
                    LowRiskCount = table.Column<int>(type: "int", nullable: false),
                    ModerateRiskCount = table.Column<int>(type: "int", nullable: false),
                    HighRiskCount = table.Column<int>(type: "int", nullable: false),
                    CriticalRiskCount = table.Column<int>(type: "int", nullable: false),
                    ExecutiveSummary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TopWorkplaceIssuesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MostCommonKeyPhrasesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionnaireAnalysisSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionnaireAnalysisSnapshots_Questionnaires_QuestionnaireId",
                        column: x => x.QuestionnaireId,
                        principalTable: "Questionnaires",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ResponseAnalyses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResponseId = table.Column<int>(type: "int", nullable: false),
                    QuestionId = table.Column<int>(type: "int", nullable: false),
                    QuestionText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AnonymizedText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SentimentLabel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SentimentConfidence = table.Column<double>(type: "float", nullable: false),
                    PositiveScore = table.Column<double>(type: "float", nullable: false),
                    NegativeScore = table.Column<double>(type: "float", nullable: false),
                    NeutralScore = table.Column<double>(type: "float", nullable: false),
                    RiskLevel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RiskScore = table.Column<double>(type: "float", nullable: false),
                    RequiresImmediateAttention = table.Column<bool>(type: "bit", nullable: false),
                    RecommendedAction = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RiskIndicatorsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProtectiveFactorsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KeyPhrasesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WorkplaceFactorsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmotionalIndicatorsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InsightsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CategoriesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AnalyzedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResponseAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResponseAnalyses_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ResponseAnalyses_Responses_ResponseId",
                        column: x => x.ResponseId,
                        principalTable: "Responses",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionnaireAnalysisSnapshots_QuestionnaireId",
                table: "QuestionnaireAnalysisSnapshots",
                column: "QuestionnaireId");

            migrationBuilder.CreateIndex(
                name: "IX_ResponseAnalyses_QuestionId",
                table: "ResponseAnalyses",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_ResponseAnalyses_ResponseId",
                table: "ResponseAnalyses",
                column: "ResponseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestionnaireAnalysisSnapshots");

            migrationBuilder.DropTable(
                name: "ResponseAnalyses");
        }
    }
}
