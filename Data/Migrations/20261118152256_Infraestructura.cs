using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GAS.Admin.Data.Migrations
{
    /// <inheritdoc />
    public partial class Infraestructura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DominioId",
                table: "Movimientos",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Dominios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(253)", maxLength: 253, nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: true),
                    RegistradorId = table.Column<int>(type: "int", nullable: true),
                    Dns = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    FechaVencimiento = table.Column<DateOnly>(type: "date", nullable: true),
                    AniosRenovacion = table.Column<int>(type: "int", nullable: false),
                    RenovacionAutomatica = table.Column<bool>(type: "bit", nullable: false),
                    Costo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Moneda = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    SslVence = table.Column<DateOnly>(type: "date", nullable: true),
                    SslEmisor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModificadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModificadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dominios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dominios_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Dominios_ServiciosExternos_RegistradorId",
                        column: x => x.RegistradorId,
                        principalTable: "ServiciosExternos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Repositorios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProductoId = table.Column<int>(type: "int", nullable: true),
                    ProveedorId = table.Column<int>(type: "int", nullable: true),
                    RamaPrincipal = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Visibilidad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Tecnologia = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResponsableId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Archivado = table.Column<bool>(type: "bit", nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModificadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModificadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Repositorios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Repositorios_AspNetUsers_ResponsableId",
                        column: x => x.ResponsableId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Repositorios_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Repositorios_ServiciosExternos_ProveedorId",
                        column: x => x.ProveedorId,
                        principalTable: "ServiciosExternos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Ambientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Plataforma = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HostingId = table.Column<int>(type: "int", nullable: true),
                    RepositorioId = table.Column<int>(type: "int", nullable: true),
                    Rama = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UltimoDespliegueUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModificadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModificadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ambientes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ambientes_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ambientes_Repositorios_RepositorioId",
                        column: x => x.RepositorioId,
                        principalTable: "Repositorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ambientes_ServiciosExternos_HostingId",
                        column: x => x.HostingId,
                        principalTable: "ServiciosExternos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Credenciales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: true),
                    AmbienteId = table.Column<int>(type: "int", nullable: true),
                    ServicioExternoId = table.Column<int>(type: "int", nullable: true),
                    Referencia = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ResponsableId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UltimaRotacion = table.Column<DateOnly>(type: "date", nullable: true),
                    RotacionDias = table.Column<int>(type: "int", nullable: true),
                    Vence = table.Column<DateOnly>(type: "date", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModificadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModificadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Credenciales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Credenciales_Ambientes_AmbienteId",
                        column: x => x.AmbienteId,
                        principalTable: "Ambientes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Credenciales_AspNetUsers_ResponsableId",
                        column: x => x.ResponsableId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Credenciales_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Credenciales_ServiciosExternos_ServicioExternoId",
                        column: x => x.ServicioExternoId,
                        principalTable: "ServiciosExternos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Despliegues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AmbienteId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Resultado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Referencia = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Realizo = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModificadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModificadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Despliegues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Despliegues_Ambientes_AmbienteId",
                        column: x => x.AmbienteId,
                        principalTable: "Ambientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Incidentes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Folio = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: true),
                    AmbienteId = table.Column<int>(type: "int", nullable: true),
                    Severidad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InicioUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Impacto = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Causa = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Acciones = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ResponsableId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModificadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModificadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incidentes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Incidentes_Ambientes_AmbienteId",
                        column: x => x.AmbienteId,
                        principalTable: "Ambientes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Incidentes_AspNetUsers_ResponsableId",
                        column: x => x.ResponsableId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Incidentes_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Movimientos_DominioId",
                table: "Movimientos",
                column: "DominioId");

            migrationBuilder.CreateIndex(
                name: "IX_Ambientes_HostingId",
                table: "Ambientes",
                column: "HostingId");

            migrationBuilder.CreateIndex(
                name: "IX_Ambientes_ProductoId",
                table: "Ambientes",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_Ambientes_RepositorioId",
                table: "Ambientes",
                column: "RepositorioId");

            migrationBuilder.CreateIndex(
                name: "IX_Credenciales_AmbienteId",
                table: "Credenciales",
                column: "AmbienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Credenciales_ProductoId",
                table: "Credenciales",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_Credenciales_ResponsableId",
                table: "Credenciales",
                column: "ResponsableId");

            migrationBuilder.CreateIndex(
                name: "IX_Credenciales_ServicioExternoId",
                table: "Credenciales",
                column: "ServicioExternoId");

            migrationBuilder.CreateIndex(
                name: "IX_Despliegues_AmbienteId_FechaUtc",
                table: "Despliegues",
                columns: new[] { "AmbienteId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Dominios_Nombre",
                table: "Dominios",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dominios_ProductoId",
                table: "Dominios",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_Dominios_RegistradorId",
                table: "Dominios",
                column: "RegistradorId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidentes_AmbienteId",
                table: "Incidentes",
                column: "AmbienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidentes_Estado_InicioUtc",
                table: "Incidentes",
                columns: new[] { "Estado", "InicioUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Incidentes_Folio",
                table: "Incidentes",
                column: "Folio",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Incidentes_ProductoId",
                table: "Incidentes",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidentes_ResponsableId",
                table: "Incidentes",
                column: "ResponsableId");

            migrationBuilder.CreateIndex(
                name: "IX_Repositorios_Nombre",
                table: "Repositorios",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Repositorios_ProductoId",
                table: "Repositorios",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_Repositorios_ProveedorId",
                table: "Repositorios",
                column: "ProveedorId");

            migrationBuilder.CreateIndex(
                name: "IX_Repositorios_ResponsableId",
                table: "Repositorios",
                column: "ResponsableId");

            migrationBuilder.AddForeignKey(
                name: "FK_Movimientos_Dominios_DominioId",
                table: "Movimientos",
                column: "DominioId",
                principalTable: "Dominios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Movimientos_Dominios_DominioId",
                table: "Movimientos");

            migrationBuilder.DropTable(
                name: "Credenciales");

            migrationBuilder.DropTable(
                name: "Despliegues");

            migrationBuilder.DropTable(
                name: "Dominios");

            migrationBuilder.DropTable(
                name: "Incidentes");

            migrationBuilder.DropTable(
                name: "Ambientes");

            migrationBuilder.DropTable(
                name: "Repositorios");

            migrationBuilder.DropIndex(
                name: "IX_Movimientos_DominioId",
                table: "Movimientos");

            migrationBuilder.DropColumn(
                name: "DominioId",
                table: "Movimientos");
        }
    }
}
