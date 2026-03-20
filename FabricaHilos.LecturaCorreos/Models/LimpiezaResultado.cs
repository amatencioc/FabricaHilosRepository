namespace FabricaHilos.LecturaCorreos.Models;

/// <summary>
/// Resultado de la limpieza de tablas para pruebas.
/// </summary>
public record LimpiezaResultado(
    int FilasLineas,
    int FilasCuotas,
    int FilasFacturas,
    int FilasErrores,
    int FilasDocumentos)
{
    public int TotalFilas => FilasLineas + FilasCuotas + FilasFacturas + FilasErrores + FilasDocumentos;
}
