using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FabricaHilos.Migrations
{
    /// <inheritdoc />
    public partial class AddLogRegistrosOc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LogRegistrosOc",
                columns: table => new
                {
                    Id         = table.Column<long>(type: "INTEGER", nullable: false)
                                      .Annotation("Sqlite:Autoincrement", true),
                    TipoDocto  = table.Column<string>(type: "TEXT", nullable: false),
                    Serie      = table.Column<int>(type: "INTEGER", nullable: false),
                    NumPed     = table.Column<long>(type: "INTEGER", nullable: false),
                    CodProveed = table.Column<string>(type: "TEXT", nullable: false),
                    Moneda     = table.Column<string>(type: "TEXT", nullable: false),
                    CantItems  = table.Column<int>(type: "INTEGER", nullable: false),
                    Fecha      = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FEntrega   = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Detalle    = table.Column<string>(type: "TEXT", nullable: true),
                    Impsto     = table.Column<decimal>(type: "TEXT", nullable: false),
                    Usuario    = table.Column<string>(type: "TEXT", nullable: false),
                    Notificado = table.Column<bool>(type: "INTEGER", nullable: false),
                    FechaLog   = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogRegistrosOc", x => x.Id);
                });

            migrationBuilder.DropTable(name: "Asistencias");
            migrationBuilder.DropTable(name: "MateriasPrimas");
            migrationBuilder.DropTable(name: "ProductosTerminados");
            migrationBuilder.DropTable(name: "Empleados");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "LogRegistrosOc");

            migrationBuilder.CreateTable(
                name: "Empleados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false),
                    Area = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Cargo = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Correo = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Dni = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FechaIngreso = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NombreCompleto = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Salario = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Telefono = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Empleados", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MateriasPrimas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CantidadDisponible = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FechaUltimoIngreso = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Observaciones = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Proveedor = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    StockMinimo = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    UnidadMedida = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MateriasPrimas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductosTerminados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Calibre = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    Cantidad = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Unidad = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductosTerminados", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Asistencias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EmpleadoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Estado = table.Column<int>(type: "INTEGER", nullable: false),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    HoraEntrada = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    HoraSalida = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    Observaciones = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Asistencias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Asistencias_Empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "Empleados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Asistencias_EmpleadoId",
                table: "Asistencias",
                column: "EmpleadoId");
        }
    }
}
