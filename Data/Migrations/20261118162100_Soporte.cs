using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GAS.Admin.Data.Migrations
{
    /// <inheritdoc />
    public partial class Soporte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArticulosConocimiento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Contenido = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: true),
                    Categoria = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Etiquetas = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Publicable = table.Column<bool>(type: "bit", nullable: false),
                    Usos = table.Column<int>(type: "int", nullable: false),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModificadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModificadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticulosConocimiento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticulosConocimiento_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SlaPrioridades",
                columns: table => new
                {
                    Prioridad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PrimeraRespuestaHoras = table.Column<int>(type: "int", nullable: false),
                    ResolucionHoras = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaPrioridades", x => x.Prioridad);
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Folio = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Asunto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Prioridad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Canal = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: true),
                    ClienteId = table.Column<int>(type: "int", nullable: true),
                    CuentaProductoId = table.Column<int>(type: "int", nullable: true),
                    ContactoNombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ContactoEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AsignadoId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    VencePrimeraRespuestaUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VenceResolucionUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PrimeraRespuestaUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResueltoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimaActividadUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TareaId = table.Column<int>(type: "int", nullable: true),
                    ReferenciaExterna = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModificadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModificadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tickets_AspNetUsers_AsignadoId",
                        column: x => x.AsignadoId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Tickets_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tickets_CuentasProducto_CuentaProductoId",
                        column: x => x.CuentaProductoId,
                        principalTable: "CuentasProducto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Tickets_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tickets_Tareas_TareaId",
                        column: x => x.TareaId,
                        principalTable: "Tareas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TicketMensajes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Autor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AutorEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Texto = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CorreoEnviado = table.Column<bool>(type: "bit", nullable: true),
                    CorreoId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CorreoError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketMensajes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketMensajes_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArticulosConocimiento_ProductoId",
                table: "ArticulosConocimiento",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketMensajes_TicketId_FechaUtc",
                table: "TicketMensajes",
                columns: new[] { "TicketId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_AsignadoId",
                table: "Tickets",
                column: "AsignadoId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ClienteId",
                table: "Tickets",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_CuentaProductoId",
                table: "Tickets",
                column: "CuentaProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Estado_UltimaActividadUtc",
                table: "Tickets",
                columns: new[] { "Estado", "UltimaActividadUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Folio",
                table: "Tickets",
                column: "Folio",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ProductoId",
                table: "Tickets",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_TareaId",
                table: "Tickets",
                column: "TareaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArticulosConocimiento");

            migrationBuilder.DropTable(
                name: "SlaPrioridades");

            migrationBuilder.DropTable(
                name: "TicketMensajes");

            migrationBuilder.DropTable(
                name: "Tickets");
        }
    }
}
