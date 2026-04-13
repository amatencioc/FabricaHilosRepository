namespace FabricaHilos.Filters
{
    /// <summary>
    /// Marca un controlador/acción como accesible desde internet (fuera de la red interna).
    /// El control real lo aplica NetworkAccessMiddleware vía rutas configuradas en appsettings.json.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AccesoExternoPermitidoAttribute : Attribute { }
}
