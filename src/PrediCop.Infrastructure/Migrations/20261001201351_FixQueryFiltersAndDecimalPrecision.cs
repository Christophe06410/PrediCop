using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrediCop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixQueryFiltersAndDecimalPrecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TrackingDocuments_Missions_MissionId1",
                table: "TrackingDocuments");

            migrationBuilder.DropIndex(
                name: "IX_TrackingDocuments_MissionId1",
                table: "TrackingDocuments");

            migrationBuilder.DropColumn(
                name: "MissionId1",
                table: "TrackingDocuments");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalDays",
                table: "LeaveEntitlements",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MissionId1",
                table: "TrackingDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalDays",
                table: "LeaveEntitlements",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2);

            migrationBuilder.CreateIndex(
                name: "IX_TrackingDocuments_MissionId1",
                table: "TrackingDocuments",
                column: "MissionId1");

            migrationBuilder.AddForeignKey(
                name: "FK_TrackingDocuments_Missions_MissionId1",
                table: "TrackingDocuments",
                column: "MissionId1",
                principalTable: "Missions",
                principalColumn: "Id");
        }
    }
}
