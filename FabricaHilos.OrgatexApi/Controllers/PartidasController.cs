namespace FabricaHilos.OrgatexApi.Controllers;

using FabricaHilos.OrgatexApi.Data;
using FabricaHilos.OrgatexApi.Models;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/partidas")]
public sealed class PartidasController : ControllerBase
{
    private readonly IOrgatexPartidaRepository _repo;
    private readonly ILogger<PartidasController> _logger;

    public PartidasController(IOrgatexPartidaRepository repo, ILogger<PartidasController> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    /// <summary>
    /// Orquesta la sincronización completa de una partida de OrgaTex hacia Oracle:
    /// 1) Lee cabecera + detalle desde ORGATEX (queries SQL directos, sin procedimiento almacenado).
    /// 2) Registra cada línea de detalle en ING_RECETAS_G/D (SP_MERGE_ING_RECETA).
    /// 3) Vincula la receta con la PARTIDA real del ERP (SP_MERGE_PARTIDA_MAS).
    /// Pensado para ser llamado por Oracle Forms una vez por número de referencia (BatchRefNo).
    /// </summary>
    [HttpPost("{batchRefNo}/sincronizar")]
    [ProducesResponseType(typeof(ResultadoSincronizacion), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResultadoSincronizacion), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ResultadoSincronizacion>> Sincronizar(string batchRefNo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(batchRefNo))
        {
            return BadRequest("Debe indicar el número de referencia de partida (BatchRefNo).");
        }

        (PartidaCabecera? cabecera, IReadOnlyList<PartidaDetalle> detalle) datosPartida;
        try
        {
            datosPartida = await _repo.ObtenerDatosPartidaAsync(batchRefNo, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error consultando ORGATEX para BatchRefNo={BatchRefNo}.", batchRefNo);
            return StatusCode(StatusCodes.Status500InternalServerError,
                $"Error consultando ORGATEX: {ex.Message}");
        }

        var (cabecera, detalle) = datosPartida;
        if (cabecera is null)
        {
            _logger.LogWarning("No se encontró la partida BatchRefNo={BatchRefNo} en ORGATEX.", batchRefNo);
            return NotFound($"No se encontró la partida con referencia '{batchRefNo}' en ORGATEX.");
        }

        var resultado = new ResultadoSincronizacion
        {
            BatchRefNo    = batchRefNo,
            Partida       = cabecera.Partida,
            FuenteDetalle = cabecera.FuenteDetalle,
        };

        var contadorItem = 0;
        foreach (var linea in detalle)
        {
            ct.ThrowIfCancellationRequested();

            // Cuando FuenteDetalle=PARCIAL_SOLO_COLOR, Llamada/Pos vienen NULL (ver
            // pkg_orgatex.sql): PROCESO e ITEM son NOT NULL (forman la PK de
            // ING_RECETAS_D), así que se resuelven aquí: PROCESO=0, ITEM=correlativo.
            var proceso = linea.Llamada ?? 0;
            var item    = linea.Pos ?? ++contadorItem;

            try
            {
                var (codigo, mensaje) = await _repo.MergeIngRecetaAsync(
                    batchRefNo, cabecera, linea, proceso, item, ct);

                var ok = codigo == 0;
                if (ok) resultado.LineasOk++; else resultado.LineasError++;

                resultado.Lineas.Add(new ResultadoLinea
                {
                    Llamada          = proceso,
                    Item             = item,
                    ProductCode      = linea.ProductCode,
                    Ok               = ok,
                    CodigoResultado  = codigo,
                    MensajeResultado = mensaje,
                });

                if (!ok)
                {
                    _logger.LogWarning(
                        "SP_MERGE_ING_RECETA falló para BatchRefNo={BatchRefNo} ProductCode={ProductCode}: {Cod} - {Msg}",
                        batchRefNo, linea.ProductCode, codigo, mensaje);
                }
            }
            catch (Exception ex)
            {
                resultado.LineasError++;
                resultado.Lineas.Add(new ResultadoLinea
                {
                    Llamada          = proceso,
                    Item             = item,
                    ProductCode      = linea.ProductCode,
                    Ok               = false,
                    CodigoResultado  = -1,
                    MensajeResultado = ex.Message,
                });
                _logger.LogError(ex,
                    "Excepción en SP_MERGE_ING_RECETA para BatchRefNo={BatchRefNo} ProductCode={ProductCode}.",
                    batchRefNo, linea.ProductCode);
            }
        }

        if (!string.IsNullOrWhiteSpace(cabecera.Partida))
        {
            try
            {
                var (codigo, mensaje) = await _repo.MergePartidaMasAsync(batchRefNo, cabecera.Partida, ct);
                resultado.PartidaVinculada = codigo == 0;
                resultado.MensajePartida   = mensaje;

                if (codigo != 0)
                {
                    _logger.LogWarning(
                        "SP_MERGE_PARTIDA_MAS falló para BatchRefNo={BatchRefNo} Partida={Partida}: {Cod} - {Msg}",
                        batchRefNo, cabecera.Partida, codigo, mensaje);
                }
            }
            catch (Exception ex)
            {
                resultado.PartidaVinculada = false;
                resultado.MensajePartida   = ex.Message;
                _logger.LogError(ex,
                    "Excepción en SP_MERGE_PARTIDA_MAS para BatchRefNo={BatchRefNo} Partida={Partida}.",
                    batchRefNo, cabecera.Partida);
            }
        }
        else
        {
            resultado.PartidaVinculada = false;
            resultado.MensajePartida   = "La cabecera de OrgaTex no trae campo Partida; no se pudo vincular.";
        }

        // Éxito total: todas las líneas OK y la partida quedó vinculada -> 200.
        // Éxito parcial (alguna línea o la vinculación de partida falló) -> 422, para que
        // Oracle Forms pueda distinguir y decidir si reintenta o alerta al usuario.
        var exitoTotal = resultado.LineasError == 0 && resultado.PartidaVinculada;
        return exitoTotal
            ? Ok(resultado)
            : StatusCode(StatusCodes.Status422UnprocessableEntity, resultado);
    }
}
