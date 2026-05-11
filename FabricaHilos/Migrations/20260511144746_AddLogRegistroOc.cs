using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FabricaHilos.Migrations
{
    /// <inheritdoc />
    public partial class AddLogRegistroOc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LogRegistrosOc",
                columns: table => new
                {
                    Id         = table.Column<long>(type: "bigint", nullable: false)
                                      .Annotation("SqlServer:Identity", "1, 1"),
                    Usuario    = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TipoDocto  = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NumPed     = table.Column<long>(type: "bigint", nullable: false),
                    Serie      = table.Column<int>(type: "int", nullable: false),
                    CodProveed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Moneda     = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Impsto     = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Fecha      = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FEntrega   = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CantItems  = table.Column<int>(type: "int", nullable: false),
                    Detalle    = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaLog   = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notificado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogRegistrosOc", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogRegistrosOc_Usuario_Notificado",
                table: "LogRegistrosOc",
                columns: new[] { "Usuario", "Notificado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "LogRegistrosOc");
        }
    }
}
