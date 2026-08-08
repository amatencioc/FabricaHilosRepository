using System.Data;
using Oracle.ManagedDataAccess.Client;
using FabricaHilos.Models.RecursosHumanos;

namespace FabricaHilos.Services.RecursosHumanos;

public interface IPlanillaIngDsctoAportesService
{
    Task<List<PlanillaIngDsctoAportesDto>> ObtenerAsync(int anio, int semana);

    /// <summary>Invoca PKG_RPT_PLANILLA.P_RESUMEN_PAGO_BANCO (pestaña "Resumen").</summary>
    Task<List<ResumenPagoBancoDto>> ObtenerResumenBancoAsync(int anio, int semana);

    /// <summary>Arma el reporte pivote de resumen por banco (agrupado por banco, columnas por mes/planilla).</summary>
    Task<ResumenPagoBancoReporteDto> ObtenerResumenBancoReporteAsync(int anio, int semana);

    /// <summary>Invoca PKG_RPT_PLANILLA.P_RESUMEN_PAGO_CCOSTO (pestaña "Detalle").</summary>
    Task<List<ResumenPagoCcostoDto>> ObtenerResumenCcostoAsync(int anio, int semana);

    /// <summary>Obtiene liquidaciones filtradas por fecha (PKG_RPT_PLANILLA.P_LIQUIDACIONES_BANCO).</summary>
    Task<LiquidacionesReporteDto> ObtenerLiquidacionesBancoAsync(DateTime fechaLiquidacion);
}

/// <summary>
/// Invoca SIG.PKG_RPT_PLANILLA.P_INGR_DESC_APORT (reporte "Planilla de ingreso y
/// descuento de aportes"). Parametro de entrada expuesto al usuario: Anio + Semana.
/// </summary>
public class PlanillaIngDsctoAportesService : OracleServiceBase, IPlanillaIngDsctoAportesService
{
    private readonly ILogger<PlanillaIngDsctoAportesService> _logger;

    public PlanillaIngDsctoAportesService(
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PlanillaIngDsctoAportesService> logger)
        : base(configuration, httpContextAccessor)
    {
        _logger = logger;
    }

    private static decimal Dec(OracleDataReader r, string col)
    {
        try
        {
            var idx = r.GetOrdinal(col);
            return r.IsDBNull(idx) ? 0m : Convert.ToDecimal(r.GetValue(idx));
        }
        catch { return 0m; }
    }

    private static string? Str(OracleDataReader r, string col)
    {
        try
        {
            var idx = r.GetOrdinal(col);
            return r.IsDBNull(idx) ? null : r.GetValue(idx)?.ToString()?.Trim();
        }
        catch { return null; }
    }

    private static DateTime? Fecha(OracleDataReader r, string col)
    {
        try
        {
            var idx = r.GetOrdinal(col);
            return r.IsDBNull(idx) ? null : Convert.ToDateTime(r.GetValue(idx));
        }
        catch { return null; }
    }

    private static long Long(OracleDataReader r, string col)
    {
        try
        {
            var idx = r.GetOrdinal(col);
            return r.IsDBNull(idx) ? 0L : Convert.ToInt64(r.GetValue(idx));
        }
        catch { return 0L; }
    }

    public async Task<List<PlanillaIngDsctoAportesDto>> ObtenerAsync(int anio, int semana)
    {
        var lista = new List<PlanillaIngDsctoAportesDto>();

        await using var conn = await AbrirConexionAsync();
        await using var cmd  = new OracleCommand($"{S}PKG_RPT_PLANILLA.P_INGR_DESC_APORT", conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName  = true;

        cmd.Parameters.Add(new OracleParameter("P_ANIO",   OracleDbType.Decimal)   { Value = anio });
        cmd.Parameters.Add(new OracleParameter("P_SEMANA", OracleDbType.Decimal)   { Value = semana });
        cmd.Parameters.Add(new OracleParameter("P_FECINI", OracleDbType.Varchar2) { Value = DBNull.Value });
        cmd.Parameters.Add(new OracleParameter("P_FECFIN", OracleDbType.Varchar2) { Value = DBNull.Value });
        cmd.Parameters.Add(new OracleParameter("P_NROPLA", OracleDbType.Varchar2) { Value = DBNull.Value });
        cmd.Parameters.Add(new OracleParameter("P_CURSOR", OracleDbType.RefCursor) { Direction = ParameterDirection.Output });

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lista.Add(new PlanillaIngDsctoAportesDto
            {
                CCodigo       = Str(reader, "C_CODIGO"),
                CCodper       = Str(reader, "C_CODPER"),
                Nombre        = Str(reader, "NOMBRE") ?? "",
                Horas         = Dec(reader, "HORAS"),
                Basico        = Dec(reader, "BASICO"),
                BasicoTarifa  = Dec(reader, "BASICO_TARIFA"),
                Dominical     = Dec(reader, "DOMINICAL"),
                Turno2        = Dec(reader, "TURNO_2"),
                Turno3        = Dec(reader, "TURNO_3"),
                He25          = Dec(reader, "HE_25"),
                He100         = Dec(reader, "HE_100"),
                PrimaTextil   = Dec(reader, "PRIMA_TEXTIL"),
                Dl25981       = Dec(reader, "DL_25981"),
                AsigFam       = Dec(reader, "ASIG_FAM"),
                AsigFamLey    = Dec(reader, "ASIG_FAM_LEY"),
                Movilidad     = Dec(reader, "MOVILIDAD"),
                Colacion      = Dec(reader, "COLACION"),
                He35          = Dec(reader, "HE_35"),
                DmEnfermedad  = Dec(reader, "DM_ENFERMEDAD"),
                BonVac        = Dec(reader, "BON_VAC"),
                DmAccidente   = Dec(reader, "DM_ACCIDENTE"),
                LicCh         = Dec(reader, "LIC_CH"),
                TotIngreso    = Dec(reader, "TOT_INGRESO"),
                DsctoJudicial = Dec(reader, "DSCTO_JUDICIAL"),
                DsctoSindical = Dec(reader, "DSCTO_SINDICAL"),
                Tardanza      = Dec(reader, "TARDANZA"),
                DsctoMedico   = Dec(reader, "DSCTO_MEDICO"),
                CuotPrestamo  = Dec(reader, "CUOT_PRESTAMO"),
                DsctoComedor  = Dec(reader, "DSCTO_COMEDOR"),
                Snp           = Dec(reader, "SNP"),
                QuintaCat     = Dec(reader, "QUINTA_CAT"),
                Afp10         = Dec(reader, "AFP_10"),
                AfpCom        = Dec(reader, "AFP_COM"),
                AfpSeg        = Dec(reader, "AFP_SEG"),
                TotDscto      = Dec(reader, "TOT_DSCTO"),
                Neto          = Dec(reader, "NETO")
            });
        }

        return lista;
    }

    public async Task<List<ResumenPagoBancoDto>> ObtenerResumenBancoAsync(int anio, int semana)
    {
        var lista = new List<ResumenPagoBancoDto>();

        await using var conn = await AbrirConexionAsync();
        await using var cmd  = new OracleCommand($"{S}PKG_RPT_PLANILLA.P_RESUMEN_PAGO_BANCO", conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName  = true;

        cmd.Parameters.Add(new OracleParameter("P_ANIO",   OracleDbType.Decimal)   { Value = anio });
        cmd.Parameters.Add(new OracleParameter("P_SEMANA", OracleDbType.Decimal)   { Value = semana });
        cmd.Parameters.Add(new OracleParameter("P_FECINI", OracleDbType.Varchar2) { Value = DBNull.Value });
        cmd.Parameters.Add(new OracleParameter("P_FECFIN", OracleDbType.Varchar2) { Value = DBNull.Value });
        cmd.Parameters.Add(new OracleParameter("P_NROPLA", OracleDbType.Varchar2) { Value = DBNull.Value });
        cmd.Parameters.Add(new OracleParameter("P_CURSOR", OracleDbType.RefCursor) { Direction = ParameterDirection.Output });

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lista.Add(new ResumenPagoBancoDto
            {
                CBanco       = Str(reader, "C_BANCO"),
                DescBanco    = Str(reader, "DESC_BANCO"),
                NumPla       = Long(reader, "NUM_PLA"),
                DescPlanilla = Str(reader, "DESC_PLANILLA"),
                FInicio      = Fecha(reader, "F_INICIO"),
                FFinal       = Fecha(reader, "F_FINAL"),
                CCodigo      = Str(reader, "C_CODIGO"),
                CCodper      = Str(reader, "C_CODPER"),
                Nombre       = Str(reader, "NOMBRE") ?? "",
                DescCargo    = Str(reader, "DESC_CARGO"),
                FIngreso     = Fecha(reader, "F_INGRESO"),
                FVencto      = Fecha(reader, "F_VENCTO"),
                CEstado      = Str(reader, "C_ESTADO"),
                Situacion    = Str(reader, "SITUACION"),
                FCese        = Fecha(reader, "F_CESE"),
                Subtotal     = Dec(reader, "SUBTOTAL"),
                Extra        = Dec(reader, "EXTRA"),
                ImpVacac     = Dec(reader, "IMP_VACAC"),
                Importe      = Dec(reader, "IMPORTE")
            });
        }

        return lista;
    }

    /// <summary>
    /// Arma el reporte pivote de "Resumen de pago por banco": agrupado por banco, con una
    /// columna (Planilla semanal / Importe horas extras) por cada NUM_PLA/mes de la semana
    /// consultada (una semana puede tener más de un NUM_PLA cuando cruza fin de mes) y una
    /// fila por empleado con el total semana.
    /// </summary>
    public async Task<ResumenPagoBancoReporteDto> ObtenerResumenBancoReporteAsync(int anio, int semana)
    {
        var datos = await ObtenerResumenBancoAsync(anio, semana);
        var reporte = new ResumenPagoBancoReporteDto();

        if (datos.Count == 0)
        {
            reporte.Titulo = $"PLLA DE OBREROS SEMANA {semana:00}/{anio} ";
            return reporte;
        }

        var fInicio = datos.Where(d => d.FInicio.HasValue).Select(d => d.FInicio!.Value).DefaultIfEmpty().Min();
        var fFinal  = datos.Where(d => d.FFinal.HasValue).Select(d => d.FFinal!.Value).DefaultIfEmpty().Max();
        reporte.Titulo = fInicio != default && fFinal != default
            ? $"PLLA DE OBREROS SEMANA {semana:00}/{anio} (DEL {fInicio:dd/MM/yyyy} AL {fFinal:dd/MM/yyyy})"
            : $"PLLA DE OBREROS SEMANA {semana:00}/{anio}";

        var cultura = new System.Globalization.CultureInfo("es-ES");

        foreach (var bancoGrp in datos.GroupBy(d => d.CBanco).OrderBy(g => g.First().DescBanco))
        {
            var descBanco = bancoGrp
                .Select(d => d.DescBanco)
                .FirstOrDefault(d => !string.IsNullOrWhiteSpace(d))
                ?? $"BANCO {bancoGrp.Key}";

            var grupo = new ResumenPagoBancoGrupoDto
            {
                CBanco    = bancoGrp.Key,
                DescBanco = descBanco
            };

            var columnas = bancoGrp
                .GroupBy(d => d.NumPla)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    NumPla = g.Key,
                    Mes    = (g.First().FInicio ?? g.First().FFinal)?.ToString("MMMM", cultura).ToUpper(cultura) ?? g.First().DescPlanilla ?? ""
                })
                .ToList();

            grupo.Meses = columnas.Select(c => c.Mes).ToList();

            var empleados = bancoGrp
                .GroupBy(d => new { d.CCodper, d.Nombre })
                .OrderBy(g => g.Key.Nombre);

            int item = 1;
            foreach (var empGrp in empleados)
            {
                var fila = new ResumenPagoBancoFilaDto
                {
                    Item    = item++,
                    CCodper = empGrp.Key.CCodper,
                    Nombre  = empGrp.Key.Nombre
                };

                foreach (var col in columnas)
                {
                    var registro = empGrp.FirstOrDefault(d => d.NumPla == col.NumPla);
                    fila.Montos.Add(new ResumenPagoBancoMesDto
                    {
                        PlanillaSemanal = registro?.Subtotal ?? 0m,
                        ImporteExtra    = registro?.Extra    ?? 0m
                    });
                }

                fila.TotalSemana = fila.Montos.Sum(m => m.PlanillaSemanal + m.ImporteExtra);
                grupo.Filas.Add(fila);
            }

            grupo.TotalesPorMes = columnas.Select(col => new ResumenPagoBancoMesDto
            {
                PlanillaSemanal = grupo.Filas.Sum(f => f.Montos[columnas.IndexOf(col)].PlanillaSemanal),
                ImporteExtra    = grupo.Filas.Sum(f => f.Montos[columnas.IndexOf(col)].ImporteExtra)
            }).ToList();

            grupo.TotalGeneral = grupo.Filas.Sum(f => f.TotalSemana);
            grupo.TotalImpVacac = bancoGrp.Sum(d => d.ImpVacac);

            reporte.Grupos.Add(grupo);
        }

        return reporte;
    }

    public async Task<LiquidacionesReporteDto> ObtenerLiquidacionesBancoAsync(DateTime fechaLiquidacion)
    {
        var lista = new List<LiquidacionesDto>();
        var reporte = new LiquidacionesReporteDto
        {
            FechaInicio = fechaLiquidacion,
            FechaFin = fechaLiquidacion,
            Titulo = $"Liquidaciones por Banco - {fechaLiquidacion:dd/MM/yyyy}"
        };

        await using var conn = await AbrirConexionAsync();
        await using var cmd = new OracleCommand($"{S}PKG_RPT_PLANILLA.P_LIQUIDACIONES_BANCO", conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        var fechaStr = fechaLiquidacion.ToString("dd/MM/yyyy");
        cmd.Parameters.Add(new OracleParameter("P_FECHAI", OracleDbType.Varchar2) { Value = fechaStr });
        cmd.Parameters.Add(new OracleParameter("P_FECHAF", OracleDbType.Varchar2) { Value = fechaStr });
        cmd.Parameters.Add(new OracleParameter("P_CURSOR", OracleDbType.RefCursor) { Direction = ParameterDirection.Output });

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lista.Add(new LiquidacionesDto
            {
                ItemSeq    = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("ITEM_SEQ")) ?? 0),
                CBanco     = Str(reader, "C_BANCO"),
                DescBanco  = Str(reader, "DESC_BANCO"),
                CCodigo    = Str(reader, "C_CODIGO"),
                CCodper    = Str(reader, "C_CODPER"),
                Nombre     = Str(reader, "NOMBRE") ?? "",
                PagoVacac  = Dec(reader, "PAGO_VACAC"),
                PagoCts    = Dec(reader, "PAGO_CTS"),
                TotalLiqui = Dec(reader, "TOTAL_LIQUI")
            });
        }

        // Agrupar por banco (solo por c\u00f3digo, ya que desc_banco puede venir vac\u00edo en algunas filas)
        var gruposPorBanco = lista
            .GroupBy(x => x.CBanco)
            .ToList();

        foreach (var bancoPair in gruposPorBanco)
        {
            var descBanco = bancoPair
                .Select(x => x.DescBanco)
                .FirstOrDefault(d => !string.IsNullOrWhiteSpace(d))
                ?? $"BANCO {bancoPair.Key}";

            var grupo = new LiquidacionesGrupoDto
            {
                CBanco = bancoPair.Key,
                DescBanco = descBanco
            };

            var item = 1;
            foreach (var row in bancoPair)
            {
                grupo.Filas.Add(new LiquidacionesFilaDto
                {
                    Item = item++,
                    CCodper = row.CCodper,
                    Nombre = row.Nombre,
                    PagoVacac = row.PagoVacac,
                    PagoCts = row.PagoCts,
                    Total = row.TotalLiqui
                });
            }

            grupo.TotalPagoVacac = grupo.Filas.Sum(f => f.PagoVacac);
            grupo.TotalPagoCts = grupo.Filas.Sum(f => f.PagoCts);
            grupo.TotalGeneral = grupo.Filas.Sum(f => f.Total);

            reporte.Grupos.Add(grupo);
        }

        return reporte;
    }

    public async Task<List<ResumenPagoCcostoDto>> ObtenerResumenCcostoAsync(int anio, int semana)
    {
        var lista = new List<ResumenPagoCcostoDto>();

        await using var conn = await AbrirConexionAsync();
        await using var cmd  = new OracleCommand($"{S}PKG_RPT_PLANILLA.P_RESUMEN_PAGO_CCOSTO", conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName  = true;

        cmd.Parameters.Add(new OracleParameter("P_ANIO",   OracleDbType.Decimal)   { Value = anio });
        cmd.Parameters.Add(new OracleParameter("P_SEMANA", OracleDbType.Decimal)   { Value = semana });
        cmd.Parameters.Add(new OracleParameter("P_FECINI", OracleDbType.Varchar2) { Value = DBNull.Value });
        cmd.Parameters.Add(new OracleParameter("P_FECFIN", OracleDbType.Varchar2) { Value = DBNull.Value });
        cmd.Parameters.Add(new OracleParameter("P_NROPLA", OracleDbType.Varchar2) { Value = DBNull.Value });
        cmd.Parameters.Add(new OracleParameter("P_CURSOR", OracleDbType.RefCursor) { Direction = ParameterDirection.Output });

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lista.Add(new ResumenPagoCcostoDto
            {
                GranCcosto     = Str(reader, "GRAN_CCOSTO"),
                DescGranCcosto = Str(reader, "G_CCOSTO_2"),
                Cant           = Dec(reader, "CANT_2"),
                ImpDiaLab      = Dec(reader, "IMP_DIA_LAB_2"),
                HorExtra       = Dec(reader, "HOR_EXTRA_2"),
                ImpExtra       = Dec(reader, "IMP_EXTRA_2"),
                ImpVacac       = Dec(reader, "IMP_VACAC_2"),
                ImpAsig        = Dec(reader, "IMP_ASIG_2"),
                ImpTot         = Dec(reader, "IMP_TOT_2"),
                Subtotal       = Dec(reader, "SUBTOTAL_2")
            });
        }

        return lista;
    }
}
