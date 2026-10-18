using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CallCalendar.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLunchBreak : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "LunchBreakEnd",
                table: "CalendarSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "LunchBreakStart",
                table: "CalendarSettings",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LunchBreakEnd",
                table: "CalendarSettings");

            migrationBuilder.DropColumn(
                name: "LunchBreakStart",
                table: "CalendarSettings");
        }
    }
}
