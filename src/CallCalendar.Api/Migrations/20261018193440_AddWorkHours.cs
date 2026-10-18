using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CallCalendar.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "WorkEnd",
                table: "CalendarSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: new TimeOnly(18, 0, 0));

            migrationBuilder.AddColumn<TimeOnly>(
                name: "WorkStart",
                table: "CalendarSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: new TimeOnly(9, 0, 0));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WorkEnd",
                table: "CalendarSettings");

            migrationBuilder.DropColumn(
                name: "WorkStart",
                table: "CalendarSettings");
        }
    }
}
