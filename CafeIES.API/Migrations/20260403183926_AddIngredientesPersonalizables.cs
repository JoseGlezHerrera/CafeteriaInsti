using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeIES.API.Migrations
{
    /// <inheritdoc />
    public partial class AddIngredientesPersonalizables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ingredientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Emoji = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PrecioExtra = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    Stock = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ingredientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LineaPedidoIngredientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LineaPedidoId = table.Column<int>(type: "int", nullable: false),
                    IngredienteId = table.Column<int>(type: "int", nullable: true),
                    Accion = table.Column<int>(type: "int", nullable: false),
                    PrecioAplicado = table.Column<decimal>(type: "decimal(6,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineaPedidoIngredientes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LineaPedidoIngredientes_Ingredientes_IngredienteId",
                        column: x => x.IngredienteId,
                        principalTable: "Ingredientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LineaPedidoIngredientes_LineasPedido_LineaPedidoId",
                        column: x => x.LineaPedidoId,
                        principalTable: "LineasPedido",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductoIngredientes",
                columns: table => new
                {
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    IngredienteId = table.Column<int>(type: "int", nullable: false),
                    EsBase = table.Column<bool>(type: "bit", nullable: false),
                    EsQuitable = table.Column<bool>(type: "bit", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductoIngredientes", x => new { x.ProductoId, x.IngredienteId });
                    table.ForeignKey(
                        name: "FK_ProductoIngredientes_Ingredientes_IngredienteId",
                        column: x => x.IngredienteId,
                        principalTable: "Ingredientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductoIngredientes_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ingredientes_Nombre",
                table: "Ingredientes",
                column: "Nombre");

            migrationBuilder.CreateIndex(
                name: "IX_LineaPedidoIngredientes_IngredienteId",
                table: "LineaPedidoIngredientes",
                column: "IngredienteId");

            migrationBuilder.CreateIndex(
                name: "IX_LineaPedidoIngredientes_LineaPedidoId",
                table: "LineaPedidoIngredientes",
                column: "LineaPedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductoIngredientes_IngredienteId",
                table: "ProductoIngredientes",
                column: "IngredienteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LineaPedidoIngredientes");

            migrationBuilder.DropTable(
                name: "ProductoIngredientes");

            migrationBuilder.DropTable(
                name: "Ingredientes");
        }
    }
}
