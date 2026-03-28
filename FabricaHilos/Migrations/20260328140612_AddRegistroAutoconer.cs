using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FabricaHilos.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistroAutoconer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegistrosAutoconer",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NumeroAutoconer = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CodigoOperador = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Turno = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    CodigoReceta = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Lote = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DescripcionMaterial = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    VelocidadMMin = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    HusosInactivos = table.Column<int>(type: "INTEGER", nullable: true),
                    HoraInicio = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HoraFinal = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Bloque = table.Column<int>(type: "INTEGER", nullable: true),
                    Titulo = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PesoBruto = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Cantidad = table.Column<int>(type: "INTEGER", nullable: true),
                    Puntaje = table.Column<int>(type: "INTEGER", nullable: true),
                    Tramo1 = table.Column<bool>(type: "INTEGER", nullable: false),
                    Tramo2 = table.Column<bool>(type: "INTEGER", nullable: false),
                    Tramo3 = table.Column<bool>(type: "INTEGER", nullable: false),
                    Tramo4 = table.Column<bool>(type: "INTEGER", nullable: false),
                    Tramo5 = table.Column<bool>(type: "INTEGER", nullable: false),
                    Tramo6 = table.Column<bool>(type: "INTEGER", nullable: false),
                    Destino = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Cliente = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Reproceso = table.Column<bool>(type: "INTEGER", nullable: false),
                    MotivoParalizacion = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Estado = table.Column<int>(type: "INTEGER", nullable: false),
                    Cerrado = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosAutoconer", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegistrosAutoconer");
        }
    }
}
