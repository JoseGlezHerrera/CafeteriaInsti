using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeIES.API.Migrations
{
    /// <inheritdoc />
    public partial class FixCategoriaCafeEmoji : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Corrige la categoría "Café" cuyo nombre y emoji quedaron corruptos
            // en el seed inicial (é → e, ☕ → ?) por problemas de encoding.
            migrationBuilder.Sql("UPDATE [Categorias] SET [Nombre] = N'Caf\u00e9', [Emoji] = N'\u2615' WHERE [Id] = 5");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [Categorias] SET [Nombre] = N'Cafe', [Emoji] = N'?' WHERE [Id] = 5");
        }
    }
}
