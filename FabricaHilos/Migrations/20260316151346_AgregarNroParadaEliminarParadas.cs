using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FabricaHilos.Migrations
{
    /// <inheritdoc />
    public partial class AgregarNroParadaEliminarParadas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParadasProduccion");

            migrationBuilder.AlterColumn<string>(
                name: "Paso",
                table: "OrdenesProduccion",
                type: "TEXT",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<int>(
                name: "NroParada",
                table: "OrdenesProduccion",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NroParada",
                table: "OrdenesProduccion");

            migrationBuilder.AlterColumn<string>(
                name: "Paso",
                table: "OrdenesProduccion",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ParadasProduccion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrdenProduccionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Metraje = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NumeroParada = table.Column<int>(type: "INTEGER", nullable: false)
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
    }
}
