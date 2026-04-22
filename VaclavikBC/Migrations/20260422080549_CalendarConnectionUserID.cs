using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VaclavikBC.Migrations
{
    /// <inheritdoc />
    public partial class CalendarConnectionUserID : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "CalendarConnection",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserId",
                table: "CalendarConnection");
        }
    }
}
