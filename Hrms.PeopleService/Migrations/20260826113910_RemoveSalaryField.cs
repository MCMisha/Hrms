using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.PeopleService.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSalaryField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Salary",
                table: "Employees");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Salary",
                table: "Employees",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
