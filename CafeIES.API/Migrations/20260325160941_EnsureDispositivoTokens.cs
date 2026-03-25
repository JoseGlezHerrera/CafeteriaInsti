using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeIES.API.Migrations
{
    /// <inheritdoc />
    public partial class EnsureDispositivoTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Crea la tabla DispositivoTokens solo si no existe.
            // La migración manual 20260314120000_AddDispositivoToken no tenía Designer.cs
            // (sin atributo [Migration]) por lo que EF Core nunca la reconoció ni aplicó.
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
            // Limpiar duplicados en ReferenciasPago antes de crear el índice único.
            // Los pedidos duplicados del bug de double-submit comparten el mismo PaymentIntentId.
            // Conservamos el pedido más antiguo (MIN Id) y ponemos NULL en los demás.
            migrationBuilder.Sql(@"
UPDATE [Pedidos] SET [ReferenciasPago] = NULL
WHERE [Id] NOT IN (
    SELECT MIN([Id]) FROM [Pedidos]
    WHERE [ReferenciasPago] IS NOT NULL
    GROUP BY [ReferenciasPago]
) AND [ReferenciasPago] IS NOT NULL;
");

            // Crea el índice único de ReferenciasPago solo si no existe.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Pedidos_ReferenciasPago' AND object_id = OBJECT_ID('Pedidos'))
BEGIN
    CREATE UNIQUE INDEX [IX_Pedidos_ReferenciasPago]
        ON [Pedidos]([ReferenciasPago])
        WHERE [ReferenciasPago] IS NOT NULL;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No revertimos: eliminar tokens o índices en rollback sería destructivo
        }
    }
}
