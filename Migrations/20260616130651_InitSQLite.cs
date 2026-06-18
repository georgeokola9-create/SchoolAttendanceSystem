using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SchoolBiometricSystem.Migrations
{
    /// <inheritdoc />
    public partial class InitSQLite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Holidays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holidays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchoolSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SchoolName = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedCheckInTime = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    LateArrivalTime = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    AllowEarlyCheckIn = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllowLateCheckIn = table.Column<bool>(type: "INTEGER", nullable: false),
                    RetentionDays = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Teachers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    NationalId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DepartmentId = table.Column<int>(type: "INTEGER", nullable: false),
                    FingerprintTemplate = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    DateRegistered = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: true),
                    CredentialsCreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HasChangedPassword = table.Column<bool>(type: "INTEGER", nullable: false),
                    RegisteredDeviceId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DeviceRegisteredAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeviceRegisteredIp = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teachers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Teachers_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeacherId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CheckInTime = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviceReregistrationRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeacherId = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestedIp = table.Column<string>(type: "TEXT", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReviewNote = table.Column<string>(type: "TEXT", nullable: true),
                    PendingDeviceId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceReregistrationRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceReregistrationRequests_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Departments",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Mathematics" },
                    { 2, "Humanities" },
                    { 3, "Languages" },
                    { 4, "Sciences" }
                });

            migrationBuilder.InsertData(
                table: "Holidays",
                columns: new[] { "Id", "CreatedDate", "Description", "EndDate", "Name", "StartDate" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Christmas break", new DateTime(2026, 12, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), "Christmas Holiday", new DateTime(2026, 12, 15, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Building maintenance", new DateTime(2026, 3, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), "School Closure - Maintenance", new DateTime(2026, 3, 20, 0, 0, 0, 0, DateTimeKind.Unspecified) }
                });

            migrationBuilder.InsertData(
                table: "SchoolSettings",
                columns: new[] { "Id", "AllowEarlyCheckIn", "AllowLateCheckIn", "ExpectedCheckInTime", "LastUpdated", "LateArrivalTime", "RetentionDays", "SchoolName" },
                values: new object[] { 1, true, true, new TimeSpan(0, 9, 0, 0, 0), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 10, 0, 0, 0), 365, "K.P. Senior Secondary School" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_TeacherId_Date",
                table: "AttendanceRecords",
                columns: new[] { "TeacherId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceReregistrationRequests_TeacherId",
                table: "DeviceReregistrationRequests",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_DepartmentId",
                table: "Teachers",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_NationalId",
                table: "Teachers",
                column: "NationalId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceRecords");

            migrationBuilder.DropTable(
                name: "DeviceReregistrationRequests");

            migrationBuilder.DropTable(
                name: "Holidays");

            migrationBuilder.DropTable(
                name: "SchoolSettings");

            migrationBuilder.DropTable(
                name: "Teachers");

            migrationBuilder.DropTable(
                name: "Departments");
        }
    }
}
