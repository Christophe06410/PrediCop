using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PrediCop.Infrastructure.Data;

#nullable disable

namespace PrediCop.Infrastructure.Migrations
{
    /// <summary>
    /// Journal technique des flux (serveur + mobile) — table FlowLogs.
    /// Migration écrite à la main (attribut [Migration] porté directement par la classe)
    /// pour être appliquée par db.Database.MigrateAsync() au démarrage de l'API.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260701210000_AddFlowLog")]
    public partial class AddFlowLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FlowLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Level = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Context = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FlowLogs_Timestamp",
                table: "FlowLogs",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlowLogs");
        }
    }
}
