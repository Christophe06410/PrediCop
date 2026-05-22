using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrediCop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantFeatureFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AgentBloodTypeEnabled",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AgentEmergencyContactEnabled",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AuditLogRetentionDays",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GpsDataRetentionDays",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "GpsTrackingEnabled",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ModuleFleetEnabled",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ModuleFourriereEnabled",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ModuleLogisticsEnabled",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ModuleRhEnabled",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ModuleVerbalisationEnabled",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PhotoAttachmentsEnabled",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgentBloodTypeEnabled",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "AgentEmergencyContactEnabled",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "AuditLogRetentionDays",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "GpsDataRetentionDays",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "GpsTrackingEnabled",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ModuleFleetEnabled",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ModuleFourriereEnabled",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ModuleLogisticsEnabled",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ModuleRhEnabled",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ModuleVerbalisationEnabled",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PhotoAttachmentsEnabled",
                table: "Tenants");
        }
    }
}
