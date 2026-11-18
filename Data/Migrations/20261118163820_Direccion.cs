using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GAS.Admin.Data.Migrations
{
    /// <inheritdoc />
    public partial class Direccion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CierresMensuales",
                columns: table => new
                {
                    Mes = table.Column<DateOnly>(type: "date", nullable: false),
                    CapturadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Parcial = table.Column<bool>(type: "bit", nullable: false),
                    IndicadoresJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CierresMensuales", x => x.Mes);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CierresMensuales");
        }
    }
}
