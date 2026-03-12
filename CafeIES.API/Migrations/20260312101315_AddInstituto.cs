using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CafeIES.API.Migrations
{
    /// <inheritdoc />
    public partial class AddInstituto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InstitutoId",
                table: "Usuarios",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Institutos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Direccion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CodigoCorto = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Institutos", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Institutos",
                columns: new[] { "Id", "Activo", "CodigoCorto", "Direccion", "Nombre" },
                values: new object[,]
                {
                    { 1, true, "IES-1", "", "IES Instituto 1" },
                    { 2, true, "IES-2", "", "IES Instituto 2" },
                    { 3, true, "IES-3", "", "IES Instituto 3" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_InstitutoId",
                table: "Usuarios",
                column: "InstitutoId");

            migrationBuilder.CreateIndex(
                name: "IX_Institutos_CodigoCorto",
                table: "Institutos",
                column: "CodigoCorto",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Usuarios_Institutos_InstitutoId",
                table: "Usuarios",
                column: "InstitutoId",
                principalTable: "Institutos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Usuarios_Institutos_InstitutoId",
                table: "Usuarios");

            migrationBuilder.DropTable(
                name: "Institutos");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_InstitutoId",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "InstitutoId",
                table: "Usuarios");
        }
    }
}
