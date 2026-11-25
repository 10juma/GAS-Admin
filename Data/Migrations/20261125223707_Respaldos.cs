using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GAS.Admin.Data.Migrations
{
    /// <inheritdoc />
    public partial class Respaldos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EjecucionesRespaldo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ClaveProgramada = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    IniciadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    InicioUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Resumen = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Bitacora = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EjecucionesRespaldo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArchivosRespaldo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EjecucionId = table.Column<int>(type: "int", nullable: false),
                    Producto = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    BaseDatos = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IdArchivoB2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BytesOriginal = table.Column<long>(type: "bigint", nullable: false),
                    Bytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RetenerHastaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BorradoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivosRespaldo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchivosRespaldo_EjecucionesRespaldo_EjecucionId",
                        column: x => x.EjecucionId,
                        principalTable: "EjecucionesRespaldo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosRespaldo_BorradoUtc_RetenerHastaUtc",
                table: "ArchivosRespaldo",
                columns: new[] { "BorradoUtc", "RetenerHastaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosRespaldo_EjecucionId",
                table: "ArchivosRespaldo",
                column: "EjecucionId");

            migrationBuilder.CreateIndex(
                name: "IX_EjecucionesRespaldo_ClaveProgramada",
                table: "EjecucionesRespaldo",
                column: "ClaveProgramada",
                unique: true,
                filter: "[ClaveProgramada] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EjecucionesRespaldo_InicioUtc",
                table: "EjecucionesRespaldo",
                column: "InicioUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchivosRespaldo");

            migrationBuilder.DropTable(
                name: "EjecucionesRespaldo");
        }
    }
}
