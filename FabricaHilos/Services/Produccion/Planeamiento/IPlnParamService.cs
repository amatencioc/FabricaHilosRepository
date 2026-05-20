using FabricaHilos.Models.Produccion.Planeamiento;

namespace FabricaHilos.Services.Produccion.Planeamiento;

public interface IPlnParamService
{
    /// <summary>Lee las 9 filas de PLN_PARAM (todos los parámetros configurables).</summary>
    Task<IEnumerable<PlnParam>> GetAllAsync();

    /// <summary>Lee un parámetro por clave primaria (ej. 'HRS_HILANDERIA').</summary>
    Task<PlnParam?> GetAsync(string codParam);

    /// <summary>
    /// Actualiza VALOR_NUM de un parámetro y registra A_MDUSER / A_MDFECHA.
    /// Solo modifica VALOR_NUM — para cambiar VALOR_TEXT o VALOR_DATE usar sobrecarga con más parámetros.
    /// </summary>
    Task UpdateAsync(string codParam, decimal valorNum, string usuario);

    /// <summary>
    /// Actualiza cualquiera de los tres valores (NUM / TEXT / DATE) del parámetro.
    /// Los argumentos nulos conservan el valor actual en BD.
    /// </summary>
    Task UpdateAsync(string codParam, decimal? valorNum, string? valorText, DateTime? valorDate, string usuario);
}
