using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FabricaHilos.Migrations
{
    /// <inheritdoc />
    public partial class AddNumeroPedidoUniqueIndexAndFhLcDocumento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FH_LC_DOCUMENTO",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NombreArchivo = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    TipoDocumento = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Serie = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Correlativo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    NumeroDocumento = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    FechaEmision = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HoraEmision = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    FechaVencimiento = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RucEmisor = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    RazonSocialEmisor = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    NombreComercialEmisor = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    DireccionEmisor = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    RucReceptor = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    RazonSocialReceptor = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    DireccionReceptor = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    Moneda = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    BaseImponible = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalIgv = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalExonerado = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalInafecto = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalGratuito = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalDescuento = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalCargo = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalAnticipos = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalPagar = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FormaPago = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    MontoNetoPendiente = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TieneDetraccion = table.Column<bool>(type: "INTEGER", nullable: false),
                    PctDetraccion = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    MontoDetraccion = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    NumeroPedido = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    NumeroGuia = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    NumeroDocRef = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ModalidadTraslado = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    MotivoTraslado = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ModoTransporte = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    PesoBruto = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    UnidadPeso = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    FechaInicioTraslado = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FechaFinTraslado = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RucTransportista = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    RazonSocTransportista = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    NombreConductor = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    LicenciaConductor = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    PlacaVehiculo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    MarcaVehiculo = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    NroDocConductor = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    UbigeoOrigen = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    DirOrigen = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    UbigeoDestino = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    DirDestino = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    Vendedor = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Estado = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    FechaProcesamiento = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Observaciones = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    FuenteExtraccion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Confianza = table.Column<double>(type: "REAL", nullable: false),
                    MensajeError = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FH_LC_DOCUMENTO", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_NumeroPedido",
                table: "Pedidos",
                column: "NumeroPedido",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FH_LC_DOCUMENTO");

            migrationBuilder.DropIndex(
                name: "IX_Pedidos_NumeroPedido",
                table: "Pedidos");
        }
    }
}
