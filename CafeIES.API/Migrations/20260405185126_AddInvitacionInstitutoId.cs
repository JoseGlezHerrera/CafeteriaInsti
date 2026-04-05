using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeIES.API.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitacionInstitutoId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InstitutoId",
                table: "Invitaciones",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InstitutoId",
                table: "Invitaciones");
        }
    }
}
