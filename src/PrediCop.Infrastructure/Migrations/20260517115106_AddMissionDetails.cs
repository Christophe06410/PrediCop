using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrediCop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMissionDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaAttachments_Missions_MissionId",
                table: "MediaAttachments");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArrivedAt",
                table: "Missions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DispatchedAt",
                table: "Missions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationDetail",
                table: "Missions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NarrativeReport",
                table: "Missions",
                type: "nvarchar(max)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MissionIntervenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    IsInjured = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionIntervenants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissionIntervenants_Missions_MissionId",
                        column: x => x.MissionId,
                        principalTable: "Missions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MissionIntervenants_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MissionIntervenants_MissionId_Order",
                table: "MissionIntervenants",
                columns: new[] { "MissionId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_MissionIntervenants_TenantId",
                table: "MissionIntervenants",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaAttachments_Missions_MissionId",
                table: "MediaAttachments",
                column: "MissionId",
                principalTable: "Missions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaAttachments_Missions_MissionId",
                table: "MediaAttachments");

            migrationBuilder.DropTable(
                name: "MissionIntervenants");

            migrationBuilder.DropColumn(
                name: "ArrivedAt",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "DispatchedAt",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "LocationDetail",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "NarrativeReport",
                table: "Missions");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaAttachments_Missions_MissionId",
                table: "MediaAttachments",
                column: "MissionId",
                principalTable: "Missions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
