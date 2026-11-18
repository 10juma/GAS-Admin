using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GAS.Admin.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConexionesProductos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConexionesProducto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    Clave = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UrlApi = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UrlSuperAdmin = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    UltimaSincronizacionUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimoError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EstadoOk = table.Column<bool>(type: "bit", nullable: true),
                    Version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Ambiente = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LatenciaMs = table.Column<int>(type: "int", nullable: true),
                    DependenciasJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CuentasSincronizadasUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModificadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModificadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConexionesProducto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConexionesProducto_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CuentasProducto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConexionProductoId = table.Column<int>(type: "int", nullable: false),
                    ExternoId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Plan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreadaUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimoUsoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PruebaHastaUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Usuarios = table.Column<int>(type: "int", nullable: false),
                    StripeCustomerId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClienteId = table.Column<int>(type: "int", nullable: true),
                    ActualizadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CuentasProducto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CuentasProducto_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CuentasProducto_ConexionesProducto_ConexionProductoId",
                        column: x => x.ConexionProductoId,
                        principalTable: "ConexionesProducto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MetricasProducto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConexionProductoId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    CuentasTotales = table.Column<int>(type: "int", nullable: false),
                    CuentasActivas = table.Column<int>(type: "int", nullable: false),
                    CuentasEnPrueba = table.Column<int>(type: "int", nullable: false),
                    CuentasSuspendidas = table.Column<int>(type: "int", nullable: false),
                    UsuariosTotales = table.Column<int>(type: "int", nullable: false),
                    UsuariosActivos24h = table.Column<int>(type: "int", nullable: false),
                    UsuariosActivos30d = table.Column<int>(type: "int", nullable: false),
                    Altas30d = table.Column<int>(type: "int", nullable: false),
                    Bajas30d = table.Column<int>(type: "int", nullable: false),
                    AdicionalesJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ActualizadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricasProducto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetricasProducto_ConexionesProducto_ConexionProductoId",
                        column: x => x.ConexionProductoId,
                        principalTable: "ConexionesProducto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConexionesProducto_Clave",
                table: "ConexionesProducto",
                column: "Clave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConexionesProducto_ProductoId",
                table: "ConexionesProducto",
                column: "ProductoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CuentasProducto_ClienteId",
                table: "CuentasProducto",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_CuentasProducto_ConexionProductoId_ExternoId",
                table: "CuentasProducto",
                columns: new[] { "ConexionProductoId", "ExternoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CuentasProducto_Estado",
                table: "CuentasProducto",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_MetricasProducto_ConexionProductoId_Fecha",
                table: "MetricasProducto",
                columns: new[] { "ConexionProductoId", "Fecha" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CuentasProducto");

            migrationBuilder.DropTable(
                name: "MetricasProducto");

            migrationBuilder.DropTable(
                name: "ConexionesProducto");
        }
    }
}
