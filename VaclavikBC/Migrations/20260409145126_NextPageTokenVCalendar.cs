using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VaclavikBC.Migrations
{
    /// <inheritdoc />
    public partial class NextPageTokenVCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendarConnection",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AccessToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RefreshToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpirationTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarConnection", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Calendar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IDProvider = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TimeZone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BackgroundColor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ForegroundColor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Selected = table.Column<bool>(type: "bit", nullable: false),
                    NextSyncToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CalendarConnectionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Calendar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Calendar_CalendarConnection_CalendarConnectionId",
                        column: x => x.CalendarConnectionId,
                        principalTable: "CalendarConnection",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CalendarEvent",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProviderId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartInfo_DateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StartInfo_Date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartInfo_TimeZone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EndInfo_DateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndInfo_Date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndInfo_TimeZone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecurrenceRules = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CalendarId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarEvent_Calendar_CalendarId",
                        column: x => x.CalendarId,
                        principalTable: "Calendar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Calendar_CalendarConnectionId",
                table: "Calendar",
                column: "CalendarConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvent_CalendarId",
                table: "CalendarEvent",
                column: "CalendarId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarEvent");

            migrationBuilder.DropTable(
                name: "Calendar");

            migrationBuilder.DropTable(
                name: "CalendarConnection");
        }
    }
}
