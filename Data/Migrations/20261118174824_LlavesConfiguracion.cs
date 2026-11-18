using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GAS.Admin.Data.Migrations
{
    /// <inheritdoc />
    public partial class LlavesConfiguracion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LlavesConfiguracion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Clave = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ValorCifrado = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EsSecreto = table.Column<bool>(type: "bit", nullable: false),
                    Pista = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ModificadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlavesConfiguracion", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LlavesConfiguracion_Clave",
                table: "LlavesConfiguracion",
                column: "Clave",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LlavesConfiguracion");
        }
    }
}
