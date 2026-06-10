using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FabricaHilos.Migrations.Contabilidad
{
    /// <inheritdoc />
    public partial class AddSireHealthCheckLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SireHealthCheckLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FechaUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AuthOk = table.Column<bool>(type: "INTEGER", nullable: false),
                    TokenMinutosRestantes = table.Column<double>(type: "REAL", nullable: true),
                    RvieOk = table.Column<bool>(type: "INTEGER", nullable: false),
                    RviePeriodos = table.Column<int>(type: "INTEGER", nullable: true),
                    RvieError = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    RceOk = table.Column<bool>(type: "INTEGER", nullable: false),
                    RcePeriodos = table.Column<int>(type: "INTEGER", nullable: true),
                    RceError = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    AlertaEnviada = table.Column<bool>(type: "INTEGER", nullable: false),
                    UltimaAlertaUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SireHealthCheckLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SireHealthCheckLogs_AlertaEnviada",
                table: "SireHealthCheckLogs",
                column: "AlertaEnviada");

            migrationBuilder.CreateIndex(
                name: "IX_SireHealthCheckLogs_FechaUtc",
                table: "SireHealthCheckLogs",
                column: "FechaUtc",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_SireHealthCheckLogs_Status_FechaUtc",
                table: "SireHealthCheckLogs",
                columns: new[] { "Status", "FechaUtc" },
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SireHealthCheckLogs");
        }
    }
}
