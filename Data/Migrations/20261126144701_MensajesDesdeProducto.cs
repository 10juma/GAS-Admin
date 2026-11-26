using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GAS.Admin.Data.Migrations
{
    /// <inheritdoc />
    public partial class MensajesDesdeProducto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferenciaExterna",
                table: "TicketMensajes",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            // Tickets ya creados desde un producto: su mensaje inicial (el primero del cliente) lo tiene el
            // producto, así que lleva la referencia del ticket para no importarlo de nuevo al sincronizar.
            migrationBuilder.Sql(@"
UPDATE m SET m.ReferenciaExterna = LEFT(t.ReferenciaExterna, 150)
FROM TicketMensajes m
JOIN Tickets t ON t.Id = m.TicketId
WHERE t.Canal = 'Producto' AND t.ReferenciaExterna IS NOT NULL AND m.ReferenciaExterna IS NULL
  AND m.Id = (SELECT MIN(m2.Id) FROM TicketMensajes m2 WHERE m2.TicketId = t.Id AND m2.Tipo = 'Cliente');");

            migrationBuilder.CreateIndex(
                name: "IX_TicketMensajes_ReferenciaExterna",
                table: "TicketMensajes",
                column: "ReferenciaExterna",
                unique: true,
                filter: "[ReferenciaExterna] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TicketMensajes_ReferenciaExterna",
                table: "TicketMensajes");

            migrationBuilder.DropColumn(
                name: "ReferenciaExterna",
                table: "TicketMensajes");
        }
    }
}
