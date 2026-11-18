using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GAS.Admin.Data.Migrations
{
    /// <inheritdoc />
    public partial class Proyectos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Trabajos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Folio = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Prioridad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ClienteId = table.Column<int>(type: "int", nullable: true),
                    ProductoId = table.Column<int>(type: "int", nullable: true),
                    CotizacionId = table.Column<int>(type: "int", nullable: true),
                    DepartamentoId = table.Column<int>(type: "int", nullable: true),
                    ResponsableId = table.Column<int>(type: "int", nullable: true),
                    FechaInicio = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaEntrega = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaCierre = table.Column<DateOnly>(type: "date", nullable: true),
                    Presupuesto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Moneda = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CostoEstimado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    GastosDirectos = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModificadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModificadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trabajos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Trabajos_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trabajos_Cotizaciones_CotizacionId",
                        column: x => x.CotizacionId,
                        principalTable: "Cotizaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trabajos_Departamentos_DepartamentoId",
                        column: x => x.DepartamentoId,
                        principalTable: "Departamentos",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Trabajos_Personas_ResponsableId",
                        column: x => x.ResponsableId,
                        principalTable: "Personas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Trabajos_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CiclosTrabajo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TrabajoId = table.Column<int>(type: "int", nullable: false),
                    Periodo = table.Column<DateOnly>(type: "date", nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModificadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModificadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CiclosTrabajo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CiclosTrabajo_Trabajos_TrabajoId",
                        column: x => x.TrabajoId,
                        principalTable: "Trabajos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Entregables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TrabajoId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FechaCompromiso = table.Column<DateOnly>(type: "date", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FechaEntrega = table.Column<DateOnly>(type: "date", nullable: true),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModificadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModificadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entregables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Entregables_Trabajos_TrabajoId",
                        column: x => x.TrabajoId,
                        principalTable: "Trabajos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tareas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TrabajoId = table.Column<int>(type: "int", nullable: true),
                    EntregableId = table.Column<int>(type: "int", nullable: true),
                    DepartamentoId = table.Column<int>(type: "int", nullable: true),
                    ResponsableId = table.Column<int>(type: "int", nullable: true),
                    Prioridad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FechaInicio = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaVencimiento = table.Column<DateOnly>(type: "date", nullable: true),
                    HorasEstimadas = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    FechaTerminadaUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModificadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModificadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tareas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tareas_Departamentos_DepartamentoId",
                        column: x => x.DepartamentoId,
                        principalTable: "Departamentos",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Tareas_Entregables_EntregableId",
                        column: x => x.EntregableId,
                        principalTable: "Entregables",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Tareas_Personas_ResponsableId",
                        column: x => x.ResponsableId,
                        principalTable: "Personas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Tareas_Trabajos_TrabajoId",
                        column: x => x.TrabajoId,
                        principalTable: "Trabajos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Adjuntos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TrabajoId = table.Column<int>(type: "int", nullable: true),
                    EntregableId = table.Column<int>(type: "int", nullable: true),
                    TareaId = table.Column<int>(type: "int", nullable: true),
                    NombreOriginal = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Clave = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TipoContenido = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Tamano = table.Column<long>(type: "bigint", nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubidoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Adjuntos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Adjuntos_Entregables_EntregableId",
                        column: x => x.EntregableId,
                        principalTable: "Entregables",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Adjuntos_Tareas_TareaId",
                        column: x => x.TareaId,
                        principalTable: "Tareas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Adjuntos_Trabajos_TrabajoId",
                        column: x => x.TrabajoId,
                        principalTable: "Trabajos",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RegistrosHoras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PersonaId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Horas = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    TrabajoId = table.Column<int>(type: "int", nullable: true),
                    TareaId = table.Column<int>(type: "int", nullable: true),
                    Nota = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CostoHora = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MonedaCosto = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModificadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModificadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosHoras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrosHoras_Personas_PersonaId",
                        column: x => x.PersonaId,
                        principalTable: "Personas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegistrosHoras_Tareas_TareaId",
                        column: x => x.TareaId,
                        principalTable: "Tareas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RegistrosHoras_Trabajos_TrabajoId",
                        column: x => x.TrabajoId,
                        principalTable: "Trabajos",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TareaComentarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TareaId = table.Column<int>(type: "int", nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Autor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TareaComentarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TareaComentarios_Tareas_TareaId",
                        column: x => x.TareaId,
                        principalTable: "Tareas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Adjuntos_Clave",
                table: "Adjuntos",
                column: "Clave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Adjuntos_EntregableId",
                table: "Adjuntos",
                column: "EntregableId");

            migrationBuilder.CreateIndex(
                name: "IX_Adjuntos_TareaId",
                table: "Adjuntos",
                column: "TareaId");

            migrationBuilder.CreateIndex(
                name: "IX_Adjuntos_TrabajoId",
                table: "Adjuntos",
                column: "TrabajoId");

            migrationBuilder.CreateIndex(
                name: "IX_CiclosTrabajo_TrabajoId_Periodo",
                table: "CiclosTrabajo",
                columns: new[] { "TrabajoId", "Periodo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Entregables_TrabajoId",
                table: "Entregables",
                column: "TrabajoId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosHoras_PersonaId_Fecha",
                table: "RegistrosHoras",
                columns: new[] { "PersonaId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosHoras_TareaId",
                table: "RegistrosHoras",
                column: "TareaId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosHoras_TrabajoId",
                table: "RegistrosHoras",
                column: "TrabajoId");

            migrationBuilder.CreateIndex(
                name: "IX_TareaComentarios_TareaId",
                table: "TareaComentarios",
                column: "TareaId");

            migrationBuilder.CreateIndex(
                name: "IX_Tareas_DepartamentoId",
                table: "Tareas",
                column: "DepartamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Tareas_EntregableId",
                table: "Tareas",
                column: "EntregableId");

            migrationBuilder.CreateIndex(
                name: "IX_Tareas_Estado_Orden",
                table: "Tareas",
                columns: new[] { "Estado", "Orden" });

            migrationBuilder.CreateIndex(
                name: "IX_Tareas_ResponsableId",
                table: "Tareas",
                column: "ResponsableId");

            migrationBuilder.CreateIndex(
                name: "IX_Tareas_TrabajoId",
                table: "Tareas",
                column: "TrabajoId");

            migrationBuilder.CreateIndex(
                name: "IX_Trabajos_ClienteId",
                table: "Trabajos",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Trabajos_CotizacionId",
                table: "Trabajos",
                column: "CotizacionId",
                unique: true,
                filter: "[CotizacionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Trabajos_DepartamentoId",
                table: "Trabajos",
                column: "DepartamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Trabajos_Folio",
                table: "Trabajos",
                column: "Folio",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trabajos_ProductoId",
                table: "Trabajos",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_Trabajos_ResponsableId",
                table: "Trabajos",
                column: "ResponsableId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Adjuntos");

            migrationBuilder.DropTable(
                name: "CiclosTrabajo");

            migrationBuilder.DropTable(
                name: "RegistrosHoras");

            migrationBuilder.DropTable(
                name: "TareaComentarios");

            migrationBuilder.DropTable(
                name: "Tareas");

            migrationBuilder.DropTable(
                name: "Entregables");

            migrationBuilder.DropTable(
                name: "Trabajos");
        }
    }
}
