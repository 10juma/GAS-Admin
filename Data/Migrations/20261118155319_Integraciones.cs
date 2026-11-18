using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GAS.Admin.Data.Migrations
{
    /// <inheritdoc />
    public partial class Integraciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternoId",
                table: "Despliegues",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EstadosIntegracion",
                columns: table => new
                {
                    Nombre = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UltimaSincronizacionUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimoResultado = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UltimoError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstadosIntegracion", x => x.Nombre);
                });

            migrationBuilder.CreateTable(
                name: "MonitoresUptime",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExternoId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Uptime30d = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: true),
                    UltimoCambioUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AmbienteId = table.Column<int>(type: "int", nullable: true),
                    IncidenteId = table.Column<int>(type: "int", nullable: true),
                    Existe = table.Column<bool>(type: "bit", nullable: false),
                    ActualizadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoresUptime", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonitoresUptime_Ambientes_AmbienteId",
                        column: x => x.AmbienteId,
                        principalTable: "Ambientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MonitoresUptime_Incidentes_IncidenteId",
                        column: x => x.IncidenteId,
                        principalTable: "Incidentes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProyectosSentry",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExternoId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Plataforma = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProductoId = table.Column<int>(type: "int", nullable: true),
                    SinResolver = table.Column<int>(type: "int", nullable: false),
                    Nuevos24h = table.Column<int>(type: "int", nullable: false),
                    Existe = table.Column<bool>(type: "bit", nullable: false),
                    ActualizadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProyectosSentry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProyectosSentry_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ReposGitHub",
                columns: table => new
                {
                    RepositorioId = table.Column<int>(type: "int", nullable: false),
                    NombreCompleto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RamaPrincipal = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Privado = table.Column<bool>(type: "bit", nullable: false),
                    UltimoCommitSha = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UltimoCommitMensaje = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    UltimoCommitAutor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    UltimoCommitUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PrsAbiertos = table.Column<int>(type: "int", nullable: false),
                    IssuesAbiertos = table.Column<int>(type: "int", nullable: false),
                    UltimoRelease = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UltimoReleaseUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ActualizadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReposGitHub", x => x.RepositorioId);
                    table.ForeignKey(
                        name: "FK_ReposGitHub_Repositorios_RepositorioId",
                        column: x => x.RepositorioId,
                        principalTable: "Repositorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ErroresSentry",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProyectoSentryId = table.Column<int>(type: "int", nullable: false),
                    ExternoId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Culpable = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Nivel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Eventos = table.Column<int>(type: "int", nullable: false),
                    Usuarios = table.Column<int>(type: "int", nullable: false),
                    PrimeraVezUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimaVezUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ActualizadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErroresSentry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ErroresSentry_ProyectosSentry_ProyectoSentryId",
                        column: x => x.ProyectoSentryId,
                        principalTable: "ProyectosSentry",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Despliegues_AmbienteId_ExternoId",
                table: "Despliegues",
                columns: new[] { "AmbienteId", "ExternoId" },
                unique: true,
                filter: "[ExternoId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ErroresSentry_ProyectoSentryId_ExternoId",
                table: "ErroresSentry",
                columns: new[] { "ProyectoSentryId", "ExternoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonitoresUptime_AmbienteId",
                table: "MonitoresUptime",
                column: "AmbienteId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoresUptime_ExternoId",
                table: "MonitoresUptime",
                column: "ExternoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonitoresUptime_IncidenteId",
                table: "MonitoresUptime",
                column: "IncidenteId");

            migrationBuilder.CreateIndex(
                name: "IX_ProyectosSentry_ExternoId",
                table: "ProyectosSentry",
                column: "ExternoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProyectosSentry_ProductoId",
                table: "ProyectosSentry",
                column: "ProductoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ErroresSentry");

            migrationBuilder.DropTable(
                name: "EstadosIntegracion");

            migrationBuilder.DropTable(
                name: "MonitoresUptime");

            migrationBuilder.DropTable(
                name: "ReposGitHub");

            migrationBuilder.DropTable(
                name: "ProyectosSentry");

            migrationBuilder.DropIndex(
                name: "IX_Despliegues_AmbienteId_ExternoId",
                table: "Despliegues");

            migrationBuilder.DropColumn(
                name: "ExternoId",
                table: "Despliegues");
        }
    }
}
