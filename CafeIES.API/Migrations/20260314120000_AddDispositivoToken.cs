using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeIES.API.Migrations
{
    /// <inheritdoc />
    public partial class AddDispositivoToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: la tabla DispositivoTokens la crea EnsureDispositivoTokens (20260325)
            // con IF NOT EXISTS para que sea idempotente en cualquier entorno.
            // Esta migración existe solo para cerrar el hueco en el historial de migraciones.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op
        }
    }
}
