using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FabricaHilos.Migrations
{
    /// <inheritdoc />
    public partial class DropColorBloqueHusosFromAutoconer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bloque",
                table: "RegistrosAutoconer");

            migrationBuilder.DropColumn(
                name: "Color",
                table: "RegistrosAutoconer");

            migrationBuilder.DropColumn(
                name: "HusosInactivos",
                table: "RegistrosAutoconer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Bloque",
                table: "RegistrosAutoconer",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "RegistrosAutoconer",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HusosInactivos",
                table: "RegistrosAutoconer",
                type: "INTEGER",
                nullable: true);
        }
    }
}
