using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrediCop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketMissionLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MissionId",
                table: "ElectronicTickets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicTickets_MissionId",
                table: "ElectronicTickets",
                column: "MissionId");

            migrationBuilder.AddForeignKey(
                name: "FK_ElectronicTickets_Missions_MissionId",
                table: "ElectronicTickets",
                column: "MissionId",
                principalTable: "Missions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ElectronicTickets_Missions_MissionId",
                table: "ElectronicTickets");

            migrationBuilder.DropIndex(
                name: "IX_ElectronicTickets_MissionId",
                table: "ElectronicTickets");

            migrationBuilder.DropColumn(
                name: "MissionId",
                table: "ElectronicTickets");
        }
    }
}
