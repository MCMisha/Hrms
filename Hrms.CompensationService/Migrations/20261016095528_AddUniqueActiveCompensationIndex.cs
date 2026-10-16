using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.CompensationService.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueActiveCompensationIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Compensations_EmployeeId",
                table: "Compensations",
                column: "EmployeeId",
                unique: true,
                filter: "\"EffectiveTo\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Compensations_EmployeeId",
                table: "Compensations");
        }
    }
}
