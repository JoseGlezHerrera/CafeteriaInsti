using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CafeIES.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAlergenos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "FranjasHorarias",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "FranjasHorarias",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "FranjasHorarias",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.AddColumn<bool>(
                name: "EsBloqueada",
                table: "FranjasHorarias",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Alergenos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Emoji = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alergenos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductoAlergeno",
                columns: table => new
                {
                    AlergenosId = table.Column<int>(type: "int", nullable: false),
                    ProductosId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductoAlergeno", x => new { x.AlergenosId, x.ProductosId });
                    table.ForeignKey(
                        name: "FK_ProductoAlergeno_Alergenos_AlergenosId",
                        column: x => x.AlergenosId,
                        principalTable: "Alergenos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductoAlergeno_Productos_ProductosId",
                        column: x => x.ProductosId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Alergenos",
                columns: new[] { "Id", "Emoji", "Nombre" },
                values: new object[,]
                {
                    { 1, "🌾", "Gluten" },
                    { 2, "🦐", "Crustáceos" },
                    { 3, "🥚", "Huevo" },
                    { 4, "🐟", "Pescado" },
                    { 5, "🥜", "Cacahuetes" },
                    { 6, "🫘", "Soja" },
                    { 7, "🥛", "Lácteos" },
                    { 8, "🌰", "Frutos secos" },
                    { 9, "🌿", "Apio" },
                    { 10, "🌻", "Mostaza" },
                    { 11, "🌱", "Sésamo" },
                    { 12, "🍷", "Sulfitos" },
                    { 13, "🌼", "Altramuces" },
                    { 14, "🦑", "Moluscos" }
                });

            migrationBuilder.UpdateData(
                table: "FranjasHorarias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Descripcion", "EsBloqueada", "HoraFin", "HoraInicio" },
                values: new object[] { "Horario de clase", true, "14:00", "08:00" });

            migrationBuilder.UpdateData(
                table: "FranjasHorarias",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Descripcion", "EsBloqueada", "HoraFin", "HoraInicio", "Turno" },
                values: new object[] { "Horario de clase", true, "20:30", "14:30", 1 });

            migrationBuilder.UpdateData(
                table: "FranjasHorarias",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Descripcion", "EsBloqueada", "HoraFin", "HoraInicio", "Turno" },
                values: new object[] { "Horario de clase", true, "03:00", "21:00", 2 });

            migrationBuilder.CreateIndex(
                name: "IX_ProductoAlergeno_ProductosId",
                table: "ProductoAlergeno",
                column: "ProductosId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductoAlergeno");

            migrationBuilder.DropTable(
                name: "Alergenos");

            migrationBuilder.DropColumn(
                name: "EsBloqueada",
                table: "FranjasHorarias");

            migrationBuilder.UpdateData(
                table: "FranjasHorarias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Descripcion", "HoraFin", "HoraInicio" },
                values: new object[] { "Antes de entrar", "08:00", "07:30" });

            migrationBuilder.UpdateData(
                table: "FranjasHorarias",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Descripcion", "HoraFin", "HoraInicio", "Turno" },
                values: new object[] { "Recreo", "11:30", "11:00", 0 });

            migrationBuilder.UpdateData(
                table: "FranjasHorarias",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Descripcion", "HoraFin", "HoraInicio", "Turno" },
                values: new object[] { "Antes de entrar", "14:00", "13:45", 1 });

            migrationBuilder.InsertData(
                table: "FranjasHorarias",
                columns: new[] { "Id", "Activa", "Descripcion", "HoraFin", "HoraInicio", "Turno" },
                values: new object[,]
                {
                    { 4, true, "Recreo", "17:30", "17:00", 1 },
                    { 5, true, "Antes de entrar", "21:00", "20:45", 2 },
                    { 6, true, "Recreo", "23:20", "23:00", 2 }
                });
        }
    }
}
