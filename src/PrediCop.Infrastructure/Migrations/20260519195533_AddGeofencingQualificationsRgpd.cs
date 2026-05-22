using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrediCop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGeofencingQualificationsRgpd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DpoEmail",
                table: "Tenants",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "GeofencingEnabled",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedGeoZoneId",
                table: "PatrolVehicles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AgentQualifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IssuingAuthority = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentQualifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentQualifications_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentQualifications_Users_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RgpdRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestType = table.Column<int>(type: "int", nullable: false),
                    RequesterName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequesterEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsProcessed = table.Column<bool>(type: "bit", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AdminNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RgpdRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RgpdRequests_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatrolVehicles_AssignedGeoZoneId",
                table: "PatrolVehicles",
                column: "AssignedGeoZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentQualifications_AgentId",
                table: "AgentQualifications",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentQualifications_TenantId",
                table: "AgentQualifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RgpdRequests_TenantId",
                table: "RgpdRequests",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_PatrolVehicles_GeoZones_AssignedGeoZoneId",
                table: "PatrolVehicles",
                column: "AssignedGeoZoneId",
                principalTable: "GeoZones",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PatrolVehicles_GeoZones_AssignedGeoZoneId",
                table: "PatrolVehicles");

            migrationBuilder.DropTable(
                name: "AgentQualifications");

            migrationBuilder.DropTable(
                name: "RgpdRequests");

            migrationBuilder.DropIndex(
                name: "IX_PatrolVehicles_AssignedGeoZoneId",
                table: "PatrolVehicles");

            migrationBuilder.DropColumn(
                name: "DpoEmail",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "GeofencingEnabled",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "AssignedGeoZoneId",
                table: "PatrolVehicles");
        }
    }
}
