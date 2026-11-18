using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GAS.Admin.Data.Migrations
{
    /// <inheritdoc />
    public partial class Comercial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Oportunidades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClienteId = table.Column<int>(type: "int", nullable: false),
                    Etapa = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ValorEstimado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Moneda = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Probabilidad = table.Column<int>(type: "int", nullable: false),
                    FechaCierreEstimada = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaCierreUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Origen = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: true),
                    ResponsableId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    MotivoPerdida = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModificadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModificadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Oportunidades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Oportunidades_AspNetUsers_ResponsableId",
                        column: x => x.ResponsableId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Oportunidades_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Oportunidades_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ServiciosComerciales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Modalidad = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PrecioBase = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Moneda = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Unidad = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CostoEstimado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Entregables = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TiempoEstimado = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProductoId = table.Column<int>(type: "int", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModificadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModificadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiciosComerciales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiciosComerciales_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Cotizaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Folio = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClienteId = table.Column<int>(type: "int", nullable: false),
                    OportunidadId = table.Column<int>(type: "int", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Moneda = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    FechaEmision = table.Column<DateOnly>(type: "date", nullable: false),
                    VigenciaDias = table.Column<int>(type: "int", nullable: false),
                    TasaIva = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Descuento = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Iva = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CostoEstimado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CondicionPago = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AnticipoPorcentaje = table.Column<int>(type: "int", nullable: true),
                    Condiciones = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    NotasInternas = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResponsableId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FechaEnvioUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaAprobacionUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TipoTrabajo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModificadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModificadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cotizaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cotizaciones_AspNetUsers_ResponsableId",
                        column: x => x.ResponsableId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Cotizaciones_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cotizaciones_Oportunidades_OportunidadId",
                        column: x => x.OportunidadId,
                        principalTable: "Oportunidades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CotizacionEnvios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CotizacionId = table.Column<int>(type: "int", nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Para = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Copia = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Asunto = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    EnviadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Exito = table.Column<bool>(type: "bit", nullable: false),
                    IdProveedor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Error = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CotizacionEnvios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CotizacionEnvios_Cotizaciones_CotizacionId",
                        column: x => x.CotizacionId,
                        principalTable: "Cotizaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CotizacionLineas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CotizacionId = table.Column<int>(type: "int", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    ServicioComercialId = table.Column<int>(type: "int", nullable: true),
                    Concepto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Cantidad = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Unidad = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DescuentoPorcentaje = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    CostoUnitario = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Importe = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CotizacionLineas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CotizacionLineas_Cotizaciones_CotizacionId",
                        column: x => x.CotizacionId,
                        principalTable: "Cotizaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CotizacionEnvios_CotizacionId",
                table: "CotizacionEnvios",
                column: "CotizacionId");

            migrationBuilder.CreateIndex(
                name: "IX_Cotizaciones_ClienteId",
                table: "Cotizaciones",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Cotizaciones_Folio_Version",
                table: "Cotizaciones",
                columns: new[] { "Folio", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cotizaciones_OportunidadId",
                table: "Cotizaciones",
                column: "OportunidadId");

            migrationBuilder.CreateIndex(
                name: "IX_Cotizaciones_ResponsableId",
                table: "Cotizaciones",
                column: "ResponsableId");

            migrationBuilder.CreateIndex(
                name: "IX_CotizacionLineas_CotizacionId",
                table: "CotizacionLineas",
                column: "CotizacionId");

            migrationBuilder.CreateIndex(
                name: "IX_Oportunidades_ClienteId",
                table: "Oportunidades",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Oportunidades_Etapa_Orden",
                table: "Oportunidades",
                columns: new[] { "Etapa", "Orden" });

            migrationBuilder.CreateIndex(
                name: "IX_Oportunidades_ProductoId",
                table: "Oportunidades",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_Oportunidades_ResponsableId",
                table: "Oportunidades",
                column: "ResponsableId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiciosComerciales_Nombre",
                table: "ServiciosComerciales",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiciosComerciales_ProductoId",
                table: "ServiciosComerciales",
                column: "ProductoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CotizacionEnvios");

            migrationBuilder.DropTable(
                name: "CotizacionLineas");

            migrationBuilder.DropTable(
                name: "ServiciosComerciales");

            migrationBuilder.DropTable(
                name: "Cotizaciones");

            migrationBuilder.DropTable(
                name: "Oportunidades");
        }
    }
}
