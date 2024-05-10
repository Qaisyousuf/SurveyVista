using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Migrations
{
    /// <inheritdoc />
    public partial class SubscriptionRelationWithMailJetEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SubscriptionId",
                table: "SentNewsletterEamils",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_SentNewsletterEamils_SubscriptionId",
                table: "SentNewsletterEamils",
                column: "SubscriptionId");

            migrationBuilder.AddForeignKey(
                name: "FK_SentNewsletterEamils_Subscriptions_SubscriptionId",
                table: "SentNewsletterEamils",
                column: "SubscriptionId",
                principalTable: "Subscriptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SentNewsletterEamils_Subscriptions_SubscriptionId",
                table: "SentNewsletterEamils");

            migrationBuilder.DropIndex(
                name: "IX_SentNewsletterEamils_SubscriptionId",
                table: "SentNewsletterEamils");

            migrationBuilder.DropColumn(
                name: "SubscriptionId",
                table: "SentNewsletterEamils");
        }
    }
}
