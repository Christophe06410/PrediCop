using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrediCop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStreetPredictiveRisk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BuildingDensityFetchedAt",
                table: "Streets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BuildingDensityScore",
                table: "Streets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ComputedBaseRiskScore",
                table: "Streets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsRiskLocked",
                table: "Streets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RiskAdjustment",
                table: "Streets",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuildingDensityFetchedAt",
                table: "Streets");

            migrationBuilder.DropColumn(
                name: "BuildingDensityScore",
                table: "Streets");

            migrationBuilder.DropColumn(
                name: "ComputedBaseRiskScore",
                table: "Streets");

            migrationBuilder.DropColumn(
                name: "IsRiskLocked",
                table: "Streets");

            migrationBuilder.DropColumn(
                name: "RiskAdjustment",
                table: "Streets");
        }
    }
}
