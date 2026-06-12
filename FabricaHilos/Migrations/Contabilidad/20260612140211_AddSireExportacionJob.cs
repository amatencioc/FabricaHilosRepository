using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FabricaHilos.Migrations.Contabilidad
{
    /// <inheritdoc />
    public partial class AddSireExportacionJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SireExportacionJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    TipoRegistro = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Periodo = table.Column<string>(type: "TEXT", maxLength: 6, nullable: false),
                    UsuarioId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Estado = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    NumTicket = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    NombreArchivo = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    RutaArchivo = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CodTipoArchivo = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    CodProceso = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    RegistrosInsertados = table.Column<int>(type: "INTEGER", nullable: true),
                    RegistrosDuplicados = table.Column<int>(type: "INTEGER", nullable: true),
                    MensajeError = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaFinalizacion = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SireExportacionJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SireExportacionJobs_Estado_FechaCreacion",
                table: "SireExportacionJobs",
                columns: new[] { "Estado", "FechaCreacion" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_SireExportacionJobs_JobId",
                table: "SireExportacionJobs",
                column: "JobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SireExportacionJobs_TipoRegistro_Periodo_UsuarioId",
                table: "SireExportacionJobs",
                columns: new[] { "TipoRegistro", "Periodo", "UsuarioId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SireExportacionJobs");
        }
    }
}
