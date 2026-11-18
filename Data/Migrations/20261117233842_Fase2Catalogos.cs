using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GAS.Admin.Data.Migrations
{
    /// <inheritdoc />
    public partial class Fase2Catalogos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NombreComercial = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Rfc = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Ciudad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SitioWeb = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ContactoNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ContactoEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ContactoTelefono = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ResponsableId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModificadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModificadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clientes_AspNetUsers_ResponsableId",
                        column: x => x.ResponsableId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Productos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Modalidad = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TieneWeb = table.Column<bool>(type: "bit", nullable: false),
                    TieneApp = table.Column<bool>(type: "bit", nullable: false),
                    UsaStripe = table.Column<bool>(type: "bit", nullable: false),
                    SitioWeb = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SuperAdmin = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SuperAdminUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ResponsableId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModificadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModificadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Productos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Productos_AspNetUsers_ResponsableId",
                        column: x => x.ResponsableId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ServiciosExternos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UrlPanel = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CuentaAsociada = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResponsableId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Plan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Costo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Moneda = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Periodicidad = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ProximaRenovacion = table.Column<DateOnly>(type: "date", nullable: true),
                    ReferenciaCredenciales = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DocumentacionUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModificadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModificadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiciosExternos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiciosExternos_AspNetUsers_ResponsableId",
                        column: x => x.ResponsableId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ClientesProductos",
                columns: table => new
                {
                    ClienteId = table.Column<int>(type: "int", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Desde = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientesProductos", x => new { x.ClienteId, x.ProductoId });
                    table.ForeignKey(
                        name: "FK_ClientesProductos_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientesProductos_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiciosProductos",
                columns: table => new
                {
                    ServicioExternoId = table.Column<int>(type: "int", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiciosProductos", x => new { x.ServicioExternoId, x.ProductoId });
                    table.ForeignKey(
                        name: "FK_ServiciosProductos_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiciosProductos_ServiciosExternos_ServicioExternoId",
                        column: x => x.ServicioExternoId,
                        principalTable: "ServiciosExternos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_Nombre",
                table: "Clientes",
                column: "Nombre");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_ResponsableId",
                table: "Clientes",
                column: "ResponsableId");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_Rfc",
                table: "Clientes",
                column: "Rfc");

            migrationBuilder.CreateIndex(
                name: "IX_ClientesProductos_ProductoId",
                table: "ClientesProductos",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_Nombre",
                table: "Productos",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Productos_ResponsableId",
                table: "Productos",
                column: "ResponsableId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiciosExternos_Nombre",
                table: "ServiciosExternos",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiciosExternos_ResponsableId",
                table: "ServiciosExternos",
                column: "ResponsableId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiciosProductos_ProductoId",
                table: "ServiciosProductos",
                column: "ProductoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientesProductos");

            migrationBuilder.DropTable(
                name: "ServiciosProductos");

            migrationBuilder.DropTable(
                name: "Clientes");

            migrationBuilder.DropTable(
                name: "Productos");

            migrationBuilder.DropTable(
                name: "ServiciosExternos");
        }
    }
}
