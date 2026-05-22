using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrediCop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleBeaconUuid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BeaconUuid",
                table: "PatrolVehicles",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BeaconUuid",
                table: "PatrolVehicles");
        }
    }
}
