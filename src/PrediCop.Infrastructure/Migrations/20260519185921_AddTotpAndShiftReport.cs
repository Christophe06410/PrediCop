using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrediCop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTotpAndShiftReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TotpEnabled",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TotpRecoveryCodes",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TotpSecretKey",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ShiftReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShiftStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ShiftEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OfficerNames = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MissionCount = table.Column<int>(type: "int", nullable: false),
                    CompletedMissionCount = table.Column<int>(type: "int", nullable: false),
                    RefusedMissionCount = table.Column<int>(type: "int", nullable: false),
                    PatrolRecordCount = table.Column<int>(type: "int", nullable: false),
                    EstimatedKm = table.Column<double>(type: "float", nullable: false),
                    DocumentCount = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsSigned = table.Column<bool>(type: "bit", nullable: false),
                    SignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftReports_PatrolVehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "PatrolVehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShiftReports_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftReports_TenantId",
                table: "ShiftReports",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftReports_VehicleId",
                table: "ShiftReports",
                column: "VehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShiftReports");

            migrationBuilder.DropColumn(
                name: "TotpEnabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TotpRecoveryCodes",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TotpSecretKey",
                table: "Users");
        }
    }
}
