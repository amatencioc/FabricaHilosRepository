using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace FabricaHilos.Controllers.RecursosHumanos.Aquarius
{
    [Authorize]
    [Route("RecursosHumanos/Aquarius/Diagnostico")]
    public class DiagnosticoController : OracleBaseController
    {
        private readonly string _baseConnectionString;
        private readonly ILogger<DiagnosticoController> _logger;

        public DiagnosticoController(IConfiguration configuration, ILogger<DiagnosticoController> logger)
        {
            _baseConnectionString = configuration.GetConnectionString("AquariusConnection")
                ?? throw new InvalidOperationException("Aquarius connection string not found.");
            _logger = logger;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public IActionResult Index()
        {
            return View("~/Views/RecursosHumanos/Aquarius/Diagnostico/Index.cshtml");
        }

        [HttpGet("TestConexion")]
        public async Task<IActionResult> TestConexion()
        {
            var csb = new OracleConnectionStringBuilder(_baseConnectionString);

            var resultado = new DiagnosticoResultado
            {
                DataSource = csb.DataSource,
                Usuario    = csb.UserID
            };

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await using var conn = new OracleConnection(_baseConnectionString);
                await conn.OpenAsync(cts.Token);

                // 1) Ping básico
                await using var cmdDual = conn.CreateCommand();
                cmdDual.CommandText = "SELECT 1 FROM DUAL";
                await cmdDual.ExecuteScalarAsync(cts.Token);
                resultado.PingOk = true;
                resultado.VersionBd = conn.ServerVersion;

                // 2) Verificar cuántos objetos del esquema AQUARIUS son visibles para este usuario
                //    (ALL_TABLES no requiere permisos directos sobre las tablas)
                await using var cmdAq = conn.CreateCommand();
                cmdAq.CommandText = @"
                    SELECT COUNT(*) FROM ALL_TABLES
                    WHERE OWNER = 'AQUARIUS'";
                try
                {
                    var totalTablas = Convert.ToInt32(await cmdAq.ExecuteScalarAsync(cts.Token));
                    resultado.AccesoAquariusOk = totalTablas > 0;
                    resultado.TablasVisibles    = totalTablas;
                    if (totalTablas == 0)
                        resultado.ErrorAquarius = "El usuario no tiene visibilidad sobre ninguna tabla del esquema AQUARIUS. Se requiere GRANT SELECT o un rol con acceso al esquema.";
                }
                catch (OracleException oexAq)
                {
                    resultado.AccesoAquariusOk = false;
                    resultado.ErrorAquarius = $"ORA-{oexAq.Number}: {oexAq.Message}";
                    _logger.LogWarning("Error al consultar ALL_TABLES para AQUARIUS: ORA-{Cod} {Msg}", oexAq.Number, oexAq.Message);
                }

                // 3) Verificar que el paquete PKG_SCA_DEPURA_TAREO existe en ALL_OBJECTS
                await using var cmdPkg = conn.CreateCommand();
                cmdPkg.CommandText = @"
                    SELECT STATUS FROM ALL_OBJECTS
                    WHERE OWNER = 'AQUARIUS'
                      AND OBJECT_NAME = 'PKG_SCA_DEPURA_TAREO'
                      AND OBJECT_TYPE = 'PACKAGE'";
                var statusPkg = await cmdPkg.ExecuteScalarAsync(cts.Token) as string;
                resultado.EstadoPaquete = string.IsNullOrEmpty(statusPkg) ? "NO ENCONTRADO" : statusPkg;

                // 4) Probar EXECUTE real sobre PKG_SCA_DEPURA_TAREO.BUSCAR_EMPLEADO
                //    Si el usuario no tiene GRANT EXECUTE el motor lanzará ORA-06550 / ORA-00904
                await using var cmdExec = conn.CreateCommand();
                cmdExec.CommandType = System.Data.CommandType.StoredProcedure;
                cmdExec.CommandText = "AQUARIUS.PKG_SCA_DEPURA_TAREO.BUSCAR_EMPLEADO";
                cmdExec.Parameters.Add(new OracleParameter("p_cod_empresa", OracleDbType.Varchar2) { Value = "0003" });
                cmdExec.Parameters.Add(new OracleParameter("p_nombre",      OracleDbType.Varchar2) { Value = "ZZZZTEST_NOEXISTE" });
                cmdExec.Parameters.Add(new OracleParameter("cv_resultado",  OracleDbType.RefCursor) { Direction = System.Data.ParameterDirection.Output });
                try
                {
                    await using var rdr = await cmdExec.ExecuteReaderAsync(cts.Token);
                    // Si llega aquí el EXECUTE funciona (aunque no devuelva filas)
                    resultado.EjecucionPaqueteOk    = true;
                    resultado.EjecucionPaqueteDetalle = "BUSCAR_EMPLEADO ejecutado correctamente";
                }
                catch (OracleException oexExec)
                {
                    resultado.EjecucionPaqueteOk    = false;
                    resultado.EjecucionPaqueteDetalle = $"ORA-{oexExec.Number}: {oexExec.Message}";
                    _logger.LogWarning("Sin EXECUTE sobre PKG_SCA_DEPURA_TAREO: ORA-{Cod} {Msg}", oexExec.Number, oexExec.Message);
                }
            }
            catch (OperationCanceledException)
            {
                resultado.PingOk = false;
                resultado.Error  = "Timeout (10 s): no se pudo conectar al servidor Oracle.";
                _logger.LogError("Timeout al conectar a Oracle para diagnóstico Aquarius.");
            }
            catch (OracleException oex)
            {
                resultado.PingOk = false;
                resultado.Error  = $"ORA-{oex.Number}: {oex.Message}";
                _logger.LogError("Error Oracle en diagnóstico Aquarius: ORA-{Cod} {Msg}", oex.Number, oex.Message);
            }
            catch (Exception ex)
            {
                resultado.PingOk = false;
                resultado.Error  = ex.Message;
                _logger.LogError(ex, "Error inesperado en diagnóstico Aquarius.");
            }

            return Json(resultado);
        }

        private sealed class DiagnosticoResultado
        {
            public string? DataSource              { get; set; }
            public string? Usuario                 { get; set; }
            public bool    PingOk                  { get; set; }
            public string? VersionBd               { get; set; }
            public bool    AccesoAquariusOk        { get; set; }
            public int     TablasVisibles          { get; set; }
            public string? EstadoPaquete           { get; set; }
            public bool    EjecucionPaqueteOk      { get; set; }
            public string? EjecucionPaqueteDetalle { get; set; }
            public string? Error                   { get; set; }
            public string? ErrorAquarius           { get; set; }
        }
    }
}
