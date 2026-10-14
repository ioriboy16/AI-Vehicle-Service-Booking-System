using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIVehicleWorkshopSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAIDiagnostic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatMessageReads_MessageId",
                table: "ChatMessageReads");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "ChatMessageReads",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "AIDiagnostics",
                columns: table => new
                {
                    DiagnosticId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    Symptoms = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    PossibleIssue = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RecommendedInspection = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MaintenanceSuggestion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReviewStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReviewedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MechanicComments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIDiagnostics", x => x.DiagnosticId);
                    table.ForeignKey(
                        name: "FK_AIDiagnostics_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessageReads_MessageId_UserId",
                table: "ChatMessageReads",
                columns: new[] { "MessageId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AIDiagnostics_VehicleId",
                table: "AIDiagnostics",
                column: "VehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AIDiagnostics");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessageReads_MessageId_UserId",
                table: "ChatMessageReads");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "ChatMessageReads",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessageReads_MessageId",
                table: "ChatMessageReads",
                column: "MessageId");
        }
    }
}
