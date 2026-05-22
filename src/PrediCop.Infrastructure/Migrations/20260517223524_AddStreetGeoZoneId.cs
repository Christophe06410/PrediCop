using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrediCop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStreetGeoZoneId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GeoZoneId",
                table: "Streets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Streets_GeoZoneId",
                table: "Streets",
                column: "GeoZoneId");

            migrationBuilder.AddForeignKey(
                name: "FK_Streets_GeoZones_GeoZoneId",
                table: "Streets",
                column: "GeoZoneId",
                principalTable: "GeoZones",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Streets_GeoZones_GeoZoneId",
                table: "Streets");

            migrationBuilder.DropIndex(
                name: "IX_Streets_GeoZoneId",
                table: "Streets");

            migrationBuilder.DropColumn(
                name: "GeoZoneId",
                table: "Streets");
        }
    }
}
