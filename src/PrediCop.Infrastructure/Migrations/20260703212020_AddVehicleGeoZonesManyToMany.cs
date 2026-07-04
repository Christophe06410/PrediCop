using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrediCop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleGeoZonesManyToMany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PatrolVehicles_GeoZones_AssignedGeoZoneId",
                table: "PatrolVehicles");

            migrationBuilder.DropIndex(
                name: "IX_PatrolVehicles_AssignedGeoZoneId",
                table: "PatrolVehicles");

            migrationBuilder.DropColumn(
                name: "AssignedGeoZoneId",
                table: "PatrolVehicles");

            migrationBuilder.CreateTable(
                name: "VehicleGeoZones",
                columns: table => new
                {
                    AssignedGeoZonesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedVehiclesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleGeoZones", x => new { x.AssignedGeoZonesId, x.AssignedVehiclesId });
                    table.ForeignKey(
                        name: "FK_VehicleGeoZones_GeoZones_AssignedGeoZonesId",
                        column: x => x.AssignedGeoZonesId,
                        principalTable: "GeoZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VehicleGeoZones_PatrolVehicles_AssignedVehiclesId",
                        column: x => x.AssignedVehiclesId,
                        principalTable: "PatrolVehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleGeoZones_AssignedVehiclesId",
                table: "VehicleGeoZones",
                column: "AssignedVehiclesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VehicleGeoZones");

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedGeoZoneId",
                table: "PatrolVehicles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatrolVehicles_AssignedGeoZoneId",
                table: "PatrolVehicles",
                column: "AssignedGeoZoneId");

            migrationBuilder.AddForeignKey(
                name: "FK_PatrolVehicles_GeoZones_AssignedGeoZoneId",
                table: "PatrolVehicles",
                column: "AssignedGeoZoneId",
                principalTable: "GeoZones",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
