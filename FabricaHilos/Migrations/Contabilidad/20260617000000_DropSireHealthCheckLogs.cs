using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FabricaHilos.Migrations.Contabilidad
{
    /// <inheritdoc />
    public partial class DropSireHealthCheckLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SireHealthCheckLogs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No se restaura: SIRE_HEALTH fue eliminado intencionalmente.
        }
    }
}
