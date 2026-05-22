using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrediCop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CurrentPeriodEnd",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeCheckoutSessionId",
                table: "Tenants",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeCustomerId",
                table: "Tenants",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeSubscriptionId",
                table: "Tenants",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionPeriod",
                table: "Tenants",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionPlan",
                table: "Tenants",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionStatus",
                table: "Tenants",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "TrialEndsAt",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserLimit",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VehicleLimit",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_StripeCustomerId",
                table: "Tenants",
                column: "StripeCustomerId");

            migrationBuilder.Sql(
                "UPDATE Tenants SET SubscriptionStatus = 'Active', SubscriptionPlan = 'Premium', " +
                "SubscriptionPeriod = 'Monthly', VehicleLimit = 9999, UserLimit = 9999 " +
                "WHERE SubscriptionStatus = '' OR SubscriptionStatus IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_StripeCustomerId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CurrentPeriodEnd",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "StripeCheckoutSessionId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "StripeCustomerId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "StripeSubscriptionId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SubscriptionPeriod",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SubscriptionPlan",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SubscriptionStatus",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TrialEndsAt",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "UserLimit",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "VehicleLimit",
                table: "Tenants");
        }
    }
}
