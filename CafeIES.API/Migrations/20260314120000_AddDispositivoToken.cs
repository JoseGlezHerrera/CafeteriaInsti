using System;
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
            // Idempotente: crea la tabla solo si no existe.
            // La migración fue creada manualmente sin Designer.cs, por lo que en algunos
            // entornos puede que la tabla ya exista (creada manualmente) pero esta migración
            // no esté en __EFMigrationsHistory. IF NOT EXISTS evita el error en ese caso.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DispositivoTokens')
BEGIN
    CREATE TABLE [DispositivoTokens] (
        [Id]                  INT            NOT NULL IDENTITY(1,1),
        [UsuarioId]           INT            NOT NULL,
        [Token]               NVARCHAR(512)  NOT NULL,
        [Plataforma]          NVARCHAR(10)   NOT NULL,
        [FechaActualizacion]  DATETIME2      NOT NULL,
        CONSTRAINT [PK_DispositivoTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DispositivoTokens_Usuarios_UsuarioId]
            FOREIGN KEY ([UsuarioId]) REFERENCES [Usuarios]([Id]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX [IX_DispositivoTokens_Token] ON [DispositivoTokens]([Token]);
    CREATE INDEX [IX_DispositivoTokens_UsuarioId] ON [DispositivoTokens]([UsuarioId]);
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DispositivoTokens");
        }
    }
}
