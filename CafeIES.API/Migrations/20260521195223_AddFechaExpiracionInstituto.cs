using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeIES.API.Migrations
{
    /// <inheritdoc />
    public partial class AddFechaExpiracionInstituto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaExpiracion",
                table: "Institutos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Institutos",
                keyColumn: "Id",
                keyValue: 1,
                column: "FechaExpiracion",
                value: null);

            migrationBuilder.UpdateData(
                table: "Institutos",
                keyColumn: "Id",
                keyValue: 2,
                column: "FechaExpiracion",
                value: null);

            migrationBuilder.UpdateData(
                table: "Institutos",
                keyColumn: "Id",
                keyValue: 3,
                column: "FechaExpiracion",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaExpiracion",
                table: "Institutos");
        }
    }
}
