namespace FabricaHilos.Models.Ventas.Cotizacion
{
    // ══════════════════════════════════════════════════════════════════════════
    // Parámetros de entrada — corresponden 1:1 a PKG_COT.F_COTIZAR (Camino A).
    // Se reutilizan también para recalcular un ítem existente (Camino B), derivando
    // los valores desde COTIZACION_D/COTIZACION_P (ver CotizacionService).
    // ══════════════════════════════════════════════════════════════════════════
    public class CotizacionParametros
    {
        /// <summary>Código de título ERP ('014','020','030','040','151','152',...).</summary>
        public string? TituloCod { get; set; }
        public string? CodArtMp1 { get; set; }
        public string? CodArtMp2 { get; set; }
        public decimal PctMp1 { get; set; } = 100;
        /// <summary>'01'=cardado, '20'=peinado, '24'=peinado gaseado (PG).</summary>
        public string Proceso { get; set; } = "01";
        /// <summary>'0'=CRUDO,'5'=BLANCO,'1'=CLARO,'2'=MEDIO,'3'=OSCURO,'4'=INTENSO.</summary>
        public string IntensidadCod { get; set; } = "3";
        public decimal CantidadKg { get; set; } = 500;
        /// <summary>'MADEJA','CONO','RODETE'.</summary>
        public string Presentacion { get; set; } = "MADEJA";
        public int Nplies { get; set; } = 1;
        public decimal MargenPct { get; set; } = 30;
    }

    /// <summary>Fila de resultado para los buscadores (autocomplete) de título y materia prima.</summary>
    public class CotizacionLookupDto
    {
        public string Codigo { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string? Extra { get; set; }
    }

    /// <summary>
    /// Campos editables de un ítem real (COTIZACION_D) agrupados por sección, tal como se
    /// capturan/almacenan en el ERP (fibras en código corto de 1 letra, presentación en
    /// código corto V_PRESENTACION) — usado por el formulario de edición del Camino B.
    /// </summary>
    public class CotizacionItemEdicionDto
    {
        public string? Titulo { get; set; }
        public string Proceso { get; set; } = "01";
        public string IntensidadCod { get; set; } = "3";
        public string? Fibra1 { get; set; }
        public string? Fibra2 { get; set; }
        public string? Valpf { get; set; }
        /// <summary>Código corto tal como está en COTIZACION_D.PRESENTACION (empieza con 'C'=CONO, 'R'=RODETE, otro=MADEJA).</summary>
        public string Presentacion { get; set; } = "M";
        public decimal CantidadKg { get; set; } = 500;
        public decimal MargenPct { get; set; } = 30;
    }

    /// <summary>Fila cruda del cursor de PKG_COT.F_COTIZAR (columnas TIPO/DESCRIPCION/COSTO_USD_KG/NOTAS).</summary>
    public class CotizarPasoDto
    {
        public string Tipo { get; set; } = "";
        public string? Descripcion { get; set; }
        public decimal CostoUsdKg { get; set; }
        public string? Notas { get; set; }

        // ── Propiedades calculadas para la línea de tiempo (no vienen de BD) ──
        public int Orden { get; set; }
        public string Grupo { get; set; } = "componente"; // componente | resumen | precio
        public string EtiquetaCorta { get; set; } = "";
        public string Icono { get; set; } = "bi-circle";
        public string Color { get; set; } = "#6c757d";
    }

    /// <summary>Fila de detalle/trazabilidad de un componente de costo (PKG_COT.F_COTIZAR_DETALLE):
    /// de qué tabla/COT_KB sale, qué claves se buscaron y la fórmula aplicada. Usado en el
    /// panel "Ver detalle del cálculo" para que el usuario entienda de dónde sale cada número.</summary>
    public class CotizarDetalleDto
    {
        public string Componente { get; set; } = "";
        public string? Fuente { get; set; }
        public string? Detalle { get; set; }
        public decimal? ValorRef { get; set; }
    }

    /// <summary>Resultado completo de una simulación/recálculo: parámetros + línea de tiempo + resumen.</summary>
    public class CotizacionTimelineDto
    {
        public CotizacionParametros Parametros { get; set; } = new();
        public List<CotizarPasoDto> Pasos { get; set; } = [];
        public decimal CostoTotal { get; set; }
        public decimal Precio25 { get; set; }
        public decimal Precio30 { get; set; }
        public decimal Precio35 { get; set; }
        public decimal Precio40 { get; set; }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Comparativo por tonalidad — replica la hoja "Resumen" del Excel manual de
    // costeo: los mismos parámetros (título/MP/proceso/kg/margen) evaluados para
    // las 6 tonalidades (CRUDO/BLANCO/CLARO/MEDIO/OSCURO/INTENSO) lado a lado, en
    // el mismo orden de proceso que usa la línea de tiempo (_stepMeta).
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Encabezado de una columna del comparativo (una tonalidad).</summary>
    public class CotizacionComparativoColumnaDto
    {
        public string IntensidadCod { get; set; } = "";
        public string Etiqueta { get; set; } = "";
        /// <summary>True si es la tonalidad actualmente seleccionada en el formulario (se resalta en la vista).</summary>
        public bool EsActual { get; set; }
    }

    /// <summary>Fila del comparativo (un componente de costo o de resumen/precio), con un valor por columna/tonalidad.</summary>
    public class CotizacionComparativoFilaDto
    {
        public string Tipo { get; set; } = "";
        public string Etiqueta { get; set; } = "";
        public string Grupo { get; set; } = "componente"; // componente | resumen | precio
        public string Icono { get; set; } = "bi-circle";
        public string Color { get; set; } = "#6c757d";
        /// <summary>Un valor por columna, en el mismo orden que Columnas.</summary>
        public List<decimal> Valores { get; set; } = [];
    }

    /// <summary>Resultado completo del comparativo por tonalidad.</summary>
    public class CotizacionComparativoDto
    {
        public List<CotizacionComparativoColumnaDto> Columnas { get; set; } = [];
        public List<CotizacionComparativoFilaDto> Filas { get; set; } = [];
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Auxiliares y Servicios (COT_KB + PARAMCOS) — catálogo de referencia que
    // alimenta las fórmulas de F_COTIZAR (equivalente a las hojas "Auxiliares" y
    // "Gas Natural" del Excel manual). Es dato GLOBAL, no cambia por cotización;
    // se muestra aparte para que el usuario pueda revisar/auditar los montos base.
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Una fila de COT_KB (base de conocimiento de PKG_COT).</summary>
    public class CotizacionAuxiliarDto
    {
        public string Categoria { get; set; } = "";
        public string? Clave1 { get; set; }
        public string? Clave2 { get; set; }
        public string? Clave3 { get; set; }
        public string? Clave4 { get; set; }
        public decimal? ValorNum { get; set; }
        public string? ValorText { get; set; }
        public string? Unidad { get; set; }
        public string? Fuente { get; set; }
        public string? Confianza { get; set; }
        public string? Observacion { get; set; }
        public DateTime? FchActualiz { get; set; }
    }

    /// <summary>Un parámetro general vigente (PARAMCOS), ya con la corrección de las columnas
    /// desfasadas aplicada, para que se muestre con su significado real.</summary>
    public class CotizacionParametroGlobalDto
    {
        public string Nombre { get; set; } = "";
        public decimal? Valor { get; set; }
        public string? Unidad { get; set; }
        public string? Nota { get; set; }
    }

    /// <summary>Envoltorio para la pestaña "Auxiliares y Servicios" de Simular.cshtml.</summary>
    public class CotizacionAuxiliaresDto
    {
        public List<CotizacionParametroGlobalDto> Parametros { get; set; } = [];
        public List<CotizacionAuxiliarDto> Auxiliares { get; set; } = [];
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Ficha técnica de ruta (COT_RUTA_TECNICA_CAB/DET) — reemplaza el Excel manual
    // que Preparatoria llenaba a mano ("1_DATOS_BASE_...xlsx") para que la contadora
    // arme la cotización. Se muestra como dato informativo en Simular/Detalle y se
    // congela (JSON) en COT_HISTORIAL.RUTA_TECNICA_JSON al guardar.
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Cabecera de la ficha técnica de una ruta (título+tonalidad) — mantenida por Preparatoria.</summary>
    public class RutaTecnicaCabDto
    {
        public long IdCab { get; set; }
        /// <summary>Código real de H_TITULOS.TITULO (Ej. '040' = "20/2") — es la CLAVE de búsqueda/match
        /// contra el título que el usuario selecciona en Simular.cshtml (CotizacionParametros.TituloCod).
        /// Obligatorio: sin esto la ficha nunca aparece como "vigente" para ninguna cotización.</summary>
        public string TituloCod { get; set; } = "";
        /// <summary>Descripción de H_TITULOS para el código anterior (Ej. "20/2") — solo lectura,
        /// resuelta vía JOIN para mostrar en listados; no se persiste en COT_RUTA_TECNICA_CAB.</summary>
        public string? TituloDesc { get; set; }
        /// <summary>Nombre/etiqueta descriptiva libre que usa Preparatoria para identificar la ficha
        /// (Ej. "20/2 ATP FLAME" = título + fibra + acabado) — solo informativo, NO se usa para el
        /// match con la cotización (eso lo hace TituloCod).</summary>
        public string TituloRoute { get; set; } = "";
        /// <summary>'CRUDO'|'BLANCO'|'CLARO'|'MEDIO'|'OSCURO_INTENSO'|'TODOS'.</summary>
        public string Tonalidad { get; set; } = "TODOS";
        public string? ClienteRef { get; set; }
        public string? ProductoDesc { get; set; }
        public DateTime? FchActualizado { get; set; }
        public decimal? PedidoMinKg { get; set; }
        public decimal? PedidoMaxKg { get; set; }
        public string? LineaAlimPct { get; set; }
        public string? LineaAlimDesc { get; set; }
        public string? NotaPedidoMin { get; set; }
        public string? NotaPartida { get; set; }
        /// <summary>Código fibra 1 (mismo dominio VARCHAR2(1) que ITEMPED/COTIZACION_D.FIBRA1, resuelto contra CV_FIBRA).</summary>
        public string? Fibra1 { get; set; }
        /// <summary>Código fibra 2 (mismo dominio que ITEMPED/COTIZACION_D.FIBRA2).</summary>
        public string? Fibra2 { get; set; }
        /// <summary>Código fibra 3 (mismo dominio que ITEMPED/COTIZACION_D.FIBRA3).</summary>
        public string? Fibra3 { get; set; }
        /// <summary>"Fr/%" — fracción o porcentaje único (Ej. '05%', '10/77/13'), mismo código que
        /// ITEMPED/COTIZACION_D.VALPF, resuelto contra V_VALPF. NO son 3 valores independientes por fibra.</summary>
        public string? Valpf { get; set; }
        /// <summary>Código de proceso, mismo dominio que ITEMPED/COTIZACION_D.PROCESO ('01'=Cardado, '20'=Peinado, '24'=Peinado Gaseado).</summary>
        public string? Proceso { get; set; }
        /// <summary>'CRUDO'="C: Crudo" (sin TT) | 'CRUDO_HEATHER'="C: Heather" (blend pre-teñido en cinta/floca, sin TT posterior) | 'TINTORERIA'="Ct" (con Tintorería posterior al hilado).</summary>
        public string ClaseColor { get; set; } = "CRUDO";
        public string Estado { get; set; } = "A";
        public List<RutaTecnicaDetDto> Detalle { get; set; } = [];
    }

    /// <summary>Fila de detalle por sección de la ficha técnica (1:1 con las columnas del Excel origen).</summary>
    public class RutaTecnicaDetDto
    {
        public long IdDet { get; set; }
        public int Orden { get; set; }
        public string Seccion { get; set; } = "";
        public decimal? PctMerma { get; set; }
        public int? NroH { get; set; }
        /// <summary>Texto literal: numérico (Ej. '47.62') o anotación de ciclo batch (Ej. '1Hrs','Ciclo de Secado').</summary>
        public string? KgHMaq { get; set; }
        public string? KgHMaqTeorico { get; set; }
        public string? Ne { get; set; }
        public string? PctEfic { get; set; }
        public string? Oper { get; set; }
        public string? MMin { get; set; }
        public string? Obs { get; set; }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Camino B — datos reales leídos de COTIZACION_G / COTIZACION_D / COTIZACION_P
    // ══════════════════════════════════════════════════════════════════════════

    public class CotizacionHeaderDto
    {
        public string TipoDoc { get; set; } = "CT";
        public int Serie { get; set; }
        public long Numero { get; set; }
        public DateTime? Fecha { get; set; }
        public string? CodCliente { get; set; }
        public string? NombreCliente { get; set; }
        public string? CodVende { get; set; }
        public string? Estado { get; set; }
        public string? Moneda { get; set; }
        public string? Observaciones { get; set; }
    }

    public class CotizacionItemDto
    {
        public string TipoDoc { get; set; } = "CT";
        public int Serie { get; set; }
        public long Numero { get; set; }
        public int Item { get; set; }
        public string? Titulo { get; set; }
        public string? Proceso { get; set; }
        public string? Intensidad { get; set; }
        public string? Fibra1 { get; set; }
        public string? Fibra2 { get; set; }
        public string? Valpf { get; set; }
        /// <summary>Código de 1 letra real (V_PRESENTACION): A,B,C,D,E,F,G,M,T.</summary>
        public string? Presentacion { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? PrecioSugerido { get; set; }
        public decimal? PrecioMax { get; set; }
        public string? ColorDet { get; set; }
        public string? Estado { get; set; }

        public List<CotizacionPrecioDto> Precios { get; set; } = [];
    }

    public class CotizacionPrecioDto
    {
        public string TipoDoc { get; set; } = "CT";
        public int Serie { get; set; }
        public long Numero { get; set; }
        public int? Item { get; set; }
        public decimal Rango { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrecioMax { get; set; }
        public decimal? Costo { get; set; }
        public decimal? PorcRent { get; set; }
        public decimal? PorcRentMax { get; set; }
        public int? PrecioElegido { get; set; }
    }

    /// <summary>Fila de listado (Index) — mezcla cotizaciones reales (CT) y simulaciones (SM).</summary>
    public class CotizacionResumenDto
    {
        public string TipoDoc { get; set; } = "CT";
        public int Serie { get; set; }
        public long Numero { get; set; }
        public DateTime? Fecha { get; set; }
        public string? CodCliente { get; set; }
        public string? NombreCliente { get; set; }
        public int TotalItems { get; set; }
        public bool EsSimulacion => TipoDoc == "SM";
        public bool Eliminada { get; set; }
        public DateTime? UltimaModificacion { get; set; }
        public string? Titulo { get; set; }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Historial / versionado (COT_HISTORIAL)
    // ══════════════════════════════════════════════════════════════════════════

    public class CotizacionHistorialDto
    {
        public long IdHist { get; set; }
        public string TipoDoc { get; set; } = "";
        public int Serie { get; set; }
        public long Numero { get; set; }
        public int Item { get; set; }
        public int NroVersion { get; set; }
        public string Accion { get; set; } = "";
        public DateTime FchHist { get; set; }
        public string? Usuario { get; set; }
        public string? Titulo { get; set; }
        public string? Proceso { get; set; }
        public string? Intensidad { get; set; }
        public string? Fibra1 { get; set; }
        public string? Fibra2 { get; set; }
        public string? Valpf { get; set; }
        public string? Presentacion { get; set; }
        public decimal? CantidadKg { get; set; }
        public int? Nplies { get; set; }
        public decimal? MargenPct { get; set; }
        public decimal? CostoTotal { get; set; }
        public decimal? Precio25 { get; set; }
        public decimal? Precio30 { get; set; }
        public decimal? Precio35 { get; set; }
        public decimal? Precio40 { get; set; }
        public string? DetalleJson { get; set; }
        /// <summary>Snapshot congelado (JSON de RutaTecnicaCabDto) de la ficha técnica vigente al momento
        /// de guardar esta versión — no cambia aunque Preparatoria edite la ficha después.</summary>
        public string? RutaTecnicaJson { get; set; }
        public string? Observacion { get; set; }
        public long? NumeroOrigen { get; set; }

        public string AccionLabel => Accion switch
        {
            "CREACION" => "Creación",
            "EDICION" => "Edición",
            "RECALCULO" => "Recálculo",
            "DUPLICADO_ORIGEN" => "Duplicada hacia otra cotización",
            "DUPLICADO_DESTINO" => "Duplicada desde otra cotización",
            "ELIMINACION" => "Eliminada",
            "RESTAURACION" => "Restaurada",
            _ => Accion
        };

        public string AccionColor => Accion switch
        {
            "CREACION" => "success",
            "EDICION" => "primary",
            "RECALCULO" => "info",
            "DUPLICADO_ORIGEN" or "DUPLICADO_DESTINO" => "secondary",
            "ELIMINACION" => "danger",
            "RESTAURACION" => "warning",
            _ => "secondary"
        };
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ViewModels compuestos para las vistas
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Un ítem de la cotización + su línea de tiempo calculada + su historial de versiones.</summary>
    public class CotizacionItemDetalleViewModel
    {
        public CotizacionItemDto Item { get; set; } = new();
        public CotizacionTimelineDto Timeline { get; set; } = new();
        public List<CotizacionHistorialDto> Historial { get; set; } = [];

        /// <summary>Ficha técnica de ruta CONGELADA (snapshot tomado al guardar la última versión de este
        /// ítem) — no cambia aunque Preparatoria edite la ficha después. Null si aún no se guardó ninguna
        /// versión con ruta técnica disponible.</summary>
        public RutaTecnicaCabDto? RutaTecnica
        {
            get
            {
                var json = Historial.OrderByDescending(h => h.NroVersion).FirstOrDefault(h => h.RutaTecnicaJson != null)?.RutaTecnicaJson;
                return string.IsNullOrEmpty(json) ? null : System.Text.Json.JsonSerializer.Deserialize<RutaTecnicaCabDto>(json);
            }
        }
    }

    /// <summary>ViewModel de la pantalla Detalle (cotización real o simulación).</summary>
    public class CotizacionDetalleViewModel
    {
        public CotizacionHeaderDto Header { get; set; } = new();
        public List<CotizacionItemDetalleViewModel> Items { get; set; } = [];
        public bool EsSimulacion => Header.TipoDoc == "SM";
        public bool Eliminada { get; set; }
    }

    /// <summary>ViewModel de la pantalla Index (listado).</summary>
    public class CotizacionIndexViewModel
    {
        public List<CotizacionResumenDto> Cotizaciones { get; set; } = [];
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 15;
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public string? Buscar { get; set; }
        public bool IncluirEliminadas { get; set; }
    }
}
