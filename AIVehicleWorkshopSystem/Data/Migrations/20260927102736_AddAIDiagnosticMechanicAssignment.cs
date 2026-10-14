using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIVehicleWorkshopSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAIDiagnosticMechanicAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedMechanicId",
                table: "AIDiagnostics",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedMechanicId",
                table: "AIDiagnostics");
        }
    }
}
