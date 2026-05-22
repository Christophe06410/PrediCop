using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrediCop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNewModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BloodType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    EmergencyContact1Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EmergencyContact1Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EmergencyContact1Relationship = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EmergencyContact2Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EmergencyContact2Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentProfiles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentProfiles_Users_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ElectronicTickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IssuedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssuedAtAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    PlateNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VehicleMake = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VehicleModel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VehicleColor = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InfractionType = table.Column<int>(type: "int", nullable: false),
                    ArticleCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FineAmount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PhotoUrls = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsSigned = table.Column<bool>(type: "bit", nullable: false),
                    SignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExportedToAntai = table.Column<bool>(type: "bit", nullable: false),
                    ExportedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectronicTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElectronicTickets_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ElectronicTickets_Users_IssuedById",
                        column: x => x.IssuedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentCatalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DefaultLifespanMonths = table.Column<int>(type: "int", nullable: true),
                    ReferenceCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentCatalog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentCatalog_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Leaves",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leaves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Leaves_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Leaves_Users_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShiftSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ShiftStart = table.Column<TimeOnly>(type: "time", nullable: false),
                    ShiftEnd = table.Column<TimeOnly>(type: "time", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftSchedules_PatrolVehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "PatrolVehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ShiftSchedules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShiftSchedules_Users_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UniformProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JacketSize = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PantSize = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShirtSize = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShoeSize = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HatSize = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UniformProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UniformProfiles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UniformProfiles_Users_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VehicleLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfficerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    KmStart = table.Column<int>(type: "int", nullable: false),
                    KmEnd = table.Column<int>(type: "int", nullable: false),
                    FuelAdded = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    Destination = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleLogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleLogEntries_PatrolVehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "PatrolVehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VehicleLogEntries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VehicleLogEntries_Users_OfficerId",
                        column: x => x.OfficerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VehicleMaintenances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    KmAtService = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Cost = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    ProviderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleMaintenances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleMaintenances_PatrolVehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "PatrolVehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VehicleMaintenances_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentIssuances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquipmentCatalogId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Size = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsReturned = table.Column<bool>(type: "bit", nullable: false),
                    ReturnedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentIssuances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentIssuances_EquipmentCatalog_EquipmentCatalogId",
                        column: x => x.EquipmentCatalogId,
                        principalTable: "EquipmentCatalog",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentIssuances_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentIssuances_Users_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentProfiles_AgentId",
                table: "AgentProfiles",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentProfiles_TenantId_AgentId",
                table: "AgentProfiles",
                columns: new[] { "TenantId", "AgentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicTickets_IssuedById",
                table: "ElectronicTickets",
                column: "IssuedById");

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicTickets_TenantId_IssuedAt",
                table: "ElectronicTickets",
                columns: new[] { "TenantId", "IssuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicTickets_TenantId_PlateNumber",
                table: "ElectronicTickets",
                columns: new[] { "TenantId", "PlateNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicTickets_TenantId_Status",
                table: "ElectronicTickets",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicTickets_TenantId_TicketNumber",
                table: "ElectronicTickets",
                columns: new[] { "TenantId", "TicketNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentCatalog_TenantId",
                table: "EquipmentCatalog",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentIssuances_AgentId",
                table: "EquipmentIssuances",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentIssuances_EquipmentCatalogId",
                table: "EquipmentIssuances",
                column: "EquipmentCatalogId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentIssuances_TenantId",
                table: "EquipmentIssuances",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Leaves_AgentId",
                table: "Leaves",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_Leaves_TenantId_AgentId",
                table: "Leaves",
                columns: new[] { "TenantId", "AgentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Leaves_TenantId_Status",
                table: "Leaves",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSchedules_AgentId",
                table: "ShiftSchedules",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSchedules_TenantId_AgentId_Date",
                table: "ShiftSchedules",
                columns: new[] { "TenantId", "AgentId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSchedules_TenantId_Date",
                table: "ShiftSchedules",
                columns: new[] { "TenantId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSchedules_VehicleId",
                table: "ShiftSchedules",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_UniformProfiles_AgentId",
                table: "UniformProfiles",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_UniformProfiles_TenantId",
                table: "UniformProfiles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleLogEntries_OfficerId",
                table: "VehicleLogEntries",
                column: "OfficerId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleLogEntries_TenantId_VehicleId_Date",
                table: "VehicleLogEntries",
                columns: new[] { "TenantId", "VehicleId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleLogEntries_VehicleId",
                table: "VehicleLogEntries",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleMaintenances_TenantId_VehicleId_ScheduledDate",
                table: "VehicleMaintenances",
                columns: new[] { "TenantId", "VehicleId", "ScheduledDate" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleMaintenances_VehicleId",
                table: "VehicleMaintenances",
                column: "VehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentProfiles");

            migrationBuilder.DropTable(
                name: "ElectronicTickets");

            migrationBuilder.DropTable(
                name: "EquipmentIssuances");

            migrationBuilder.DropTable(
                name: "Leaves");

            migrationBuilder.DropTable(
                name: "ShiftSchedules");

            migrationBuilder.DropTable(
                name: "UniformProfiles");

            migrationBuilder.DropTable(
                name: "VehicleLogEntries");

            migrationBuilder.DropTable(
                name: "VehicleMaintenances");

            migrationBuilder.DropTable(
                name: "EquipmentCatalog");
        }
    }
}
