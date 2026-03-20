using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FabricaHilos.Migrations
{
    /// <inheritdoc />
    public partial class EliminarDatosPrueba : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Eliminar registros de prueba de OrdenesProduccion
            migrationBuilder.Sql("DELETE FROM OrdenesProduccion WHERE NumeroOrden LIKE 'RC-2024-%'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No se restauran los datos de prueba
        }
    }
}
