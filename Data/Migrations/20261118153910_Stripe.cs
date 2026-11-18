using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GAS.Admin.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stripe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CuentasStripe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    Clave = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PreciosIncluyenIva = table.Column<bool>(type: "bit", nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    CuentaId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NombreStripe = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ModoPrueba = table.Column<bool>(type: "bit", nullable: false),
                    UltimaSincronizacionUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimoResultado = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UltimoError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModificadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModificadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CuentasStripe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CuentasStripe_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MetricasStripe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CuentaStripeId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Moneda = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Mrr = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Activas = table.Column<int>(type: "int", nullable: false),
                    EnPrueba = table.Column<int>(type: "int", nullable: false),
                    Atrasadas = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricasStripe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetricasStripe_CuentasStripe_CuentaStripeId",
                        column: x => x.CuentaStripeId,
                        principalTable: "CuentasStripe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SuscriptoresStripe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CuentaStripeId = table.Column<int>(type: "int", nullable: false),
                    StripeId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ClienteId = table.Column<int>(type: "int", nullable: true),
                    CreadoStripeUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualizadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuscriptoresStripe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SuscriptoresStripe_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SuscriptoresStripe_CuentasStripe_CuentaStripeId",
                        column: x => x.CuentaStripeId,
                        principalTable: "CuentasStripe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SuscripcionesStripe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CuentaStripeId = table.Column<int>(type: "int", nullable: false),
                    StripeId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SuscriptorId = table.Column<int>(type: "int", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Plan = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Moneda = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    MontoMensual = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Intervalo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    IntervaloCantidad = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    InicioUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PeriodoFinUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PruebaHastaUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelarAlFinal = table.Column<bool>(type: "bit", nullable: false),
                    CanceladaUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualizadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuscripcionesStripe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SuscripcionesStripe_CuentasStripe_CuentaStripeId",
                        column: x => x.CuentaStripeId,
                        principalTable: "CuentasStripe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SuscripcionesStripe_SuscriptoresStripe_SuscriptorId",
                        column: x => x.SuscriptorId,
                        principalTable: "SuscriptoresStripe",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FacturasStripe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CuentaStripeId = table.Column<int>(type: "int", nullable: false),
                    StripeId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SuscriptorId = table.Column<int>(type: "int", nullable: true),
                    SuscripcionId = table.Column<int>(type: "int", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Moneda = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Impuestos = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Pagado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Comision = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MonedaComision = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PagadaUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Intentos = table.Column<int>(type: "int", nullable: false),
                    ProximoIntentoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MovimientoIngresoId = table.Column<int>(type: "int", nullable: true),
                    MovimientoComisionId = table.Column<int>(type: "int", nullable: true),
                    ActualizadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacturasStripe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FacturasStripe_CuentasStripe_CuentaStripeId",
                        column: x => x.CuentaStripeId,
                        principalTable: "CuentasStripe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FacturasStripe_SuscripcionesStripe_SuscripcionId",
                        column: x => x.SuscripcionId,
                        principalTable: "SuscripcionesStripe",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FacturasStripe_SuscriptoresStripe_SuscriptorId",
                        column: x => x.SuscriptorId,
                        principalTable: "SuscriptoresStripe",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CuentasStripe_Clave",
                table: "CuentasStripe",
                column: "Clave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CuentasStripe_ProductoId",
                table: "CuentasStripe",
                column: "ProductoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FacturasStripe_CuentaStripeId_StripeId",
                table: "FacturasStripe",
                columns: new[] { "CuentaStripeId", "StripeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FacturasStripe_Estado_FechaUtc",
                table: "FacturasStripe",
                columns: new[] { "Estado", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FacturasStripe_SuscripcionId",
                table: "FacturasStripe",
                column: "SuscripcionId");

            migrationBuilder.CreateIndex(
                name: "IX_FacturasStripe_SuscriptorId",
                table: "FacturasStripe",
                column: "SuscriptorId");

            migrationBuilder.CreateIndex(
                name: "IX_MetricasStripe_CuentaStripeId_Fecha",
                table: "MetricasStripe",
                columns: new[] { "CuentaStripeId", "Fecha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SuscripcionesStripe_CuentaStripeId_StripeId",
                table: "SuscripcionesStripe",
                columns: new[] { "CuentaStripeId", "StripeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SuscripcionesStripe_Estado",
                table: "SuscripcionesStripe",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_SuscripcionesStripe_SuscriptorId",
                table: "SuscripcionesStripe",
                column: "SuscriptorId");

            migrationBuilder.CreateIndex(
                name: "IX_SuscriptoresStripe_ClienteId",
                table: "SuscriptoresStripe",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_SuscriptoresStripe_CuentaStripeId_StripeId",
                table: "SuscriptoresStripe",
                columns: new[] { "CuentaStripeId", "StripeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FacturasStripe");

            migrationBuilder.DropTable(
                name: "MetricasStripe");

            migrationBuilder.DropTable(
                name: "SuscripcionesStripe");

            migrationBuilder.DropTable(
                name: "SuscriptoresStripe");

            migrationBuilder.DropTable(
                name: "CuentasStripe");
        }
    }
}
