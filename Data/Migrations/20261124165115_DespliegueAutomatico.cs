using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GAS.Admin.Data.Migrations
{
    /// <inheritdoc />
    public partial class DespliegueAutomatico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Servidores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Puerto = table.Column<int>(type: "int", nullable: false),
                    Usuario = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    HuellaHost = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PreparadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimaConexionUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Sistema = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    UltimoError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModificadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModificadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Servidores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracionesDespliegue",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AmbienteId = table.Column<int>(type: "int", nullable: false),
                    ServidorId = table.Column<int>(type: "int", nullable: false),
                    Dockerfile = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Contenedor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Imagen = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Puertos = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Red = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Volumenes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RutaSalud = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IncluirSecretoGas = table.Column<bool>(type: "bit", nullable: false),
                    Migraciones = table.Column<bool>(type: "bit", nullable: false),
                    DockerfileMigraciones = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RespaldarBd = table.Column<bool>(type: "bit", nullable: false),
                    ContenedorSql = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BaseDatos = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModificadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModificadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionesDespliegue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConfiguracionesDespliegue_Ambientes_AmbienteId",
                        column: x => x.AmbienteId,
                        principalTable: "Ambientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConfiguracionesDespliegue_Servidores_ServidorId",
                        column: x => x.ServidorId,
                        principalTable: "Servidores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EjecucionesDespliegue",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    ServidorId = table.Column<int>(type: "int", nullable: false),
                    Ambientes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Referencia = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Commit = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    MensajeCommit = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Resumen = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IniciadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InicioUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Bitacora = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EjecucionesDespliegue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EjecucionesDespliegue_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EjecucionesDespliegue_Servidores_ServidorId",
                        column: x => x.ServidorId,
                        principalTable: "Servidores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionesDespliegue_AmbienteId",
                table: "ConfiguracionesDespliegue",
                column: "AmbienteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionesDespliegue_ServidorId",
                table: "ConfiguracionesDespliegue",
                column: "ServidorId");

            migrationBuilder.CreateIndex(
                name: "IX_EjecucionesDespliegue_CreadoUtc",
                table: "EjecucionesDespliegue",
                column: "CreadoUtc");

            migrationBuilder.CreateIndex(
                name: "IX_EjecucionesDespliegue_ProductoId",
                table: "EjecucionesDespliegue",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_EjecucionesDespliegue_ServidorId",
                table: "EjecucionesDespliegue",
                column: "ServidorId");

            migrationBuilder.CreateIndex(
                name: "IX_Servidores_Nombre",
                table: "Servidores",
                column: "Nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracionesDespliegue");

            migrationBuilder.DropTable(
                name: "EjecucionesDespliegue");

            migrationBuilder.DropTable(
                name: "Servidores");
        }
    }
}
