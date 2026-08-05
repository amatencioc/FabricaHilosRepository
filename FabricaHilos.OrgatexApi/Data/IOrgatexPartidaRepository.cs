namespace FabricaHilos.OrgatexApi.Data;

using FabricaHilos.OrgatexApi.Models;

public interface IOrgatexPartidaRepository
{
    /// <summary>
    /// Ejecuta los queries SQL directos de ORGATEX (@BatchRefNo) en ORGATEX (SQL Server) y devuelve
    /// la cabecera (o null si no existe el batch) y el detalle de líneas de producto/dosis.
    /// </summary>
    Task<(PartidaCabecera? Cabecera, IReadOnlyList<PartidaDetalle> Detalle)> ObtenerDatosPartidaAsync(
        string batchRefNo, CancellationToken ct);

    /// <summary>
    /// Registra en Oracle SIG.ING_RECETAS_G/D (vía PKG_ORGATEX.SP_MERGE_ING_RECETA) una línea
    /// de detalle de la partida. Idempotente (MERGE); se puede reintentar sin duplicar.
    /// </summary>
    Task<(int Codigo, string? Mensaje)> MergeIngRecetaAsync(
        string numero, PartidaCabecera cabecera, PartidaDetalle detalle, int proceso, int item, CancellationToken ct);

    /// <summary>
    /// Vincula la receta (NUMERO=numero) con la PARTIDA real del ERP, a partir del campo
    /// "Partida" de OrgaTex, vía PKG_ORGATEX.SP_MERGE_PARTIDA_MAS.
    /// </summary>
    Task<(int Codigo, string? Mensaje)> MergePartidaMasAsync(
        string numero, string partidaOrgatex, CancellationToken ct);
}
