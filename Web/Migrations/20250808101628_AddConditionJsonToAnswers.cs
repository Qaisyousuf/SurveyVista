using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Migrations
{
    /// <inheritdoc />
    public partial class AddConditionJsonToAnswers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NextQuestionId",
                table: "ResponseAnswers");

            migrationBuilder.DropColumn(
                name: "SkipToEnd",
                table: "ResponseAnswers");

            migrationBuilder.DropColumn(
                name: "NextQuestionId",
                table: "Answers");

            migrationBuilder.DropColumn(
                name: "SkipToEnd",
                table: "Answers");

            migrationBuilder.AddColumn<string>(
                name: "ConditionJson",
                table: "Answers",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConditionJson",
                table: "Answers");

            migrationBuilder.AddColumn<int>(
                name: "NextQuestionId",
                table: "ResponseAnswers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SkipToEnd",
                table: "ResponseAnswers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "NextQuestionId",
                table: "Answers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SkipToEnd",
                table: "Answers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
