using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FabricaHilos.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCamposPabileras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ContadorFinal",
                table: "OrdenesProduccion",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ContadorInicial",
                table: "OrdenesProduccion",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HorasInactivas",
                table: "OrdenesProduccion",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ParadasProduccion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrdenProduccionId = table.Column<int>(type: "INTEGER", nullable: false),
                    NumeroParada = table.Column<int>(type: "INTEGER", nullable: false),
                    Metraje = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParadasProduccion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParadasProduccion_OrdenesProduccion_OrdenProduccionId",
                        column: x => x.OrdenProduccionId,
                        principalTable: "OrdenesProduccion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParadasProduccion_OrdenProduccionId",
                table: "ParadasProduccion",
                column: "OrdenProduccionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParadasProduccion");

            migrationBuilder.DropColumn(
                name: "ContadorFinal",
                table: "OrdenesProduccion");

            migrationBuilder.DropColumn(
                name: "ContadorInicial",
                table: "OrdenesProduccion");

            migrationBuilder.DropColumn(
                name: "HorasInactivas",
                table: "OrdenesProduccion");
        }
    }
}
