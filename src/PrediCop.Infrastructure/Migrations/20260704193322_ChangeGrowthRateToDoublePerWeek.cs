using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrediCop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeGrowthRateToDoublePerWeek : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NightRiskGrowthRatePerHour",
                table: "Streets");

            migrationBuilder.DropColumn(
                name: "RiskGrowthRatePerHour",
                table: "Streets");

            migrationBuilder.AddColumn<double>(
                name: "NightRiskGrowthRatePerWeek",
                table: "Streets",
                type: "float",
                nullable: false,
                defaultValue: 2.0);

            migrationBuilder.AddColumn<double>(
                name: "RiskGrowthRatePerWeek",
                table: "Streets",
                type: "float",
                nullable: false,
                defaultValue: 1.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NightRiskGrowthRatePerWeek",
                table: "Streets");

            migrationBuilder.DropColumn(
                name: "RiskGrowthRatePerWeek",
                table: "Streets");

            migrationBuilder.AddColumn<int>(
                name: "NightRiskGrowthRatePerHour",
                table: "Streets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RiskGrowthRatePerHour",
                table: "Streets",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
