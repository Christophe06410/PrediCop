using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrediCop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNightRiskAndRecurringEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NightEndHour",
                table: "Streets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NightRiskGrowthRatePerHour",
                table: "Streets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NightStartHour",
                table: "Streets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecurrenceEndDate",
                table: "StreetRiskEvents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceType",
                table: "StreetRiskEvents",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NightEndHour",
                table: "Streets");

            migrationBuilder.DropColumn(
                name: "NightRiskGrowthRatePerHour",
                table: "Streets");

            migrationBuilder.DropColumn(
                name: "NightStartHour",
                table: "Streets");

            migrationBuilder.DropColumn(
                name: "RecurrenceEndDate",
                table: "StreetRiskEvents");

            migrationBuilder.DropColumn(
                name: "RecurrenceType",
                table: "StreetRiskEvents");
        }
    }
}
