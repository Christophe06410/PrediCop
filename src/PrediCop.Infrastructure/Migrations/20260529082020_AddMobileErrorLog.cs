using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrediCop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMobileErrorLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MissionId1",
                table: "TrackingDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MobileErrorLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppVersion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeviceModel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StackTrace = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Endpoint = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HttpStatusCode = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MobileErrorLogs", x => x.Id);
                });

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TrackingDocuments_Missions_MissionId1",
                table: "TrackingDocuments");

            migrationBuilder.DropTable(
                name: "MobileErrorLogs");

            migrationBuilder.DropIndex(
                name: "IX_TrackingDocuments_MissionId1",
                table: "TrackingDocuments");

            migrationBuilder.DropColumn(
                name: "MissionId1",
                table: "TrackingDocuments");
        }
    }
}
