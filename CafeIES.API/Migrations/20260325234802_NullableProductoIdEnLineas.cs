using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeIES.API.Migrations
{
    /// <inheritdoc />
    public partial class NullableProductoIdEnLineas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LineasPedido_Productos_ProductoId",
                table: "LineasPedido");

            migrationBuilder.AlterColumn<int>(
                name: "ProductoId",
                table: "LineasPedido",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_LineasPedido_Productos_ProductoId",
                table: "LineasPedido",
                column: "ProductoId",
                principalTable: "Productos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LineasPedido_Productos_ProductoId",
                table: "LineasPedido");

            migrationBuilder.AlterColumn<int>(
                name: "ProductoId",
                table: "LineasPedido",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LineasPedido_Productos_ProductoId",
                table: "LineasPedido",
                column: "ProductoId",
                principalTable: "Productos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
