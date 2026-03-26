using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeIES.API.Migrations
{
    /// <inheritdoc />
    public partial class AddIndicesEstadoYDispositivoToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_Estado",
                table: "Pedidos",
                column: "Estado");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Pedidos_Estado",
                table: "Pedidos");
        }
    }
}
