/*******************************************************************************
    SCRIPTS DE PRUEBA - PAQUETE PKG_SCA_DEPURA_TAREO
    
    IMPORTANTE: ESQUEMA DE PRODUCCION -> SCA_ASISTENCIA_TAREO (AQUARIUS)
                Ejecutar con usuario AQUARIUS.
    
    Este script prueba DEPURA_TOTAL para UN empleado en UNA fecha.
    Solo procesa empleados con >= 1 marcacion.
    
    Secciones:
    1. Ver estado ANTES de depurar
    2. Ejecutar depuracion (DEPURA_TOTAL)
    3. Ver estado DESPUES de depurar
    
    Fecha: 13/04/2026
*******************************************************************************/


/*******************************************************************************
    CONFIGURAR PARAMETROS DE PRUEBA
    << CAMBIAR ESTOS VALORES SEGUN EL CASO A PROBAR >>
*******************************************************************************/
-- Para usar en todo el script:
DEFINE P_EMPRESA  = '0003'
DEFINE P_PERSONAL = '004266'
DEFINE P_FECHA    = '06/04/2026'


/*******************************************************************************
    1. VER ESTADO ANTES DE DEPURAR
*******************************************************************************/
PROMPT ============================================================
PROMPT ESTADO ACTUAL DEL EMPLEADO (ANTES DE DEPURAR)
PROMPT ============================================================

DECLARE
    cv_resultado SYS_REFCURSOR;
    -- Variables para el cursor
    v_empresa VARCHAR2(10);
    v_personal VARCHAR2(10);
    v_fotocheck VARCHAR2(20);
    v_nummarcas NUMBER;
    v_entrada VARCHAR2(20);
    v_inirefri VARCHAR2(20);
    v_finrefri VARCHAR2(20);
    v_salida VARCHAR2(20);
    v_hor_descripcion VARCHAR2(60);
    v_hor_clase VARCHAR2(20);
    v_hor_entrada VARCHAR2(20);
    v_hor_inirefri VARCHAR2(20);
    v_hor_finrefri VARCHAR2(20);
    v_hor_salida VARCHAR2(20);
    v_hor_total_hrs VARCHAR2(20);
    v_hor_descanso VARCHAR2(1);
    v_entrada_teo VARCHAR2(20);
    v_inirefri_teo VARCHAR2(20);
    v_finrefri_teo VARCHAR2(20);
    v_salida_teo VARCHAR2(20);
    v_hrs_brutas VARCHAR2(20);
    v_hrs_refri VARCHAR2(20);
    v_hrs_efectivas VARCHAR2(20);
    v_tardanza VARCHAR2(20);
    v_hrs_nocturnas VARCHAR2(20);
    v_hrs_nocturnas_of VARCHAR2(20);
    v_tothoras VARCHAR2(20);
    v_horadobles VARCHAR2(20);
    v_descanso VARCHAR2(1);
    v_cerrado VARCHAR2(1);
    v_obrero VARCHAR2(1);
    v_tipo_entrada VARCHAR2(10);
    v_tipo_salida VARCHAR2(10);
    -- Permisos/Ausencias
    v_desc_med VARCHAR2(1);
    v_subsidio VARCHAR2(1);
    v_perm_goce VARCHAR2(1);
    v_perm_sgoce VARCHAR2(1);
    v_vacaciones VARCHAR2(1);
    v_suspension VARCHAR2(1);
    v_lic_pat VARCHAR2(1);
    v_lic_fac VARCHAR2(1);
    -- Horas extras
    v_hora_extra VARCHAR2(20);
    v_hora_extra_antes VARCHAR2(20);
    v_total_horas_extras VARCHAR2(20);
    v_tiempo_anticipado VARCHAR2(20);
    v_he_antes_aut VARCHAR2(1);
    -- Horas extras - breakdown/rangos
    v_hora_desp_sal VARCHAR2(20);
    v_hora_extra_ofi VARCHAR2(20);
    v_total_extras_ofi VARCHAR2(20);
    v_hora_extra_ajus VARCHAR2(20);
    v_alerta06 VARCHAR2(2);
    v_he_25pct VARCHAR2(20);
    v_he_35pct VARCHAR2(20);
    v_he_50pct VARCHAR2(20);
    v_he_ofi_25pct VARCHAR2(20);
    v_he_ofi_35pct VARCHAR2(20);
    v_he_ofi_50pct VARCHAR2(20);
    v_cfg_h25f VARCHAR2(20);
    v_cfg_h35i VARCHAR2(20);
    v_cfg_h35f VARCHAR2(20);
    v_cfg_hni VARCHAR2(20);
    v_cfg_ajuste_he NUMBER;
    v_cfg_tippagohe VARCHAR2(1);
    -- Campos de auditoría de depuración
    v_cod_depuracion VARCHAR2(100);
    v_desc_depuracion VARCHAR2(500);
    -- Alerta y verificacion historial
    v_alerta01 VARCHAR2(2);
    v_marcas_historial NUMBER;
BEGIN
    PKG_SCA_DEPURA_TAREO.VER_ESTADO(
        p_cod_empresa  => '&P_EMPRESA',
        p_cod_personal => '&P_PERSONAL',
        p_fecha        => '&P_FECHA',
        cv_resultado   => cv_resultado
    );
    
    FETCH cv_resultado INTO 
        v_empresa, v_personal, v_fotocheck, v_nummarcas,
        v_entrada, v_inirefri, v_finrefri, v_salida,
        v_hor_descripcion, v_hor_clase, v_hor_entrada, v_hor_inirefri,
        v_hor_finrefri, v_hor_salida, v_hor_total_hrs, v_hor_descanso,
        v_entrada_teo, v_inirefri_teo, v_finrefri_teo, v_salida_teo,
        v_hrs_brutas, v_hrs_refri, v_hrs_efectivas, v_tardanza,
        v_hrs_nocturnas, v_hrs_nocturnas_of, v_tothoras,
        v_horadobles,
        v_descanso, v_cerrado, v_obrero, v_tipo_entrada, v_tipo_salida,
        v_desc_med, v_subsidio, v_perm_goce, v_perm_sgoce,
        v_vacaciones, v_suspension, v_lic_pat, v_lic_fac,
        v_hora_extra, v_hora_extra_antes, v_total_horas_extras,
        v_tiempo_anticipado, v_he_antes_aut,
        v_hora_desp_sal, v_hora_extra_ofi, v_total_extras_ofi,
        v_hora_extra_ajus, v_alerta06,
        v_he_25pct, v_he_35pct, v_he_50pct,
        v_he_ofi_25pct, v_he_ofi_35pct, v_he_ofi_50pct,
        v_cfg_h25f, v_cfg_h35i, v_cfg_h35f, v_cfg_hni,
        v_cfg_ajuste_he, v_cfg_tippagohe,
        v_cod_depuracion, v_desc_depuracion,
        v_alerta01, v_marcas_historial;
    
    DBMS_OUTPUT.PUT_LINE('Fotocheck: ' || v_fotocheck);
    DBMS_OUTPUT.PUT_LINE('Empresa/Personal: ' || v_empresa || '/' || v_personal);
    DBMS_OUTPUT.PUT_LINE('Num Marcaciones: ' || v_nummarcas);
    DBMS_OUTPUT.PUT_LINE('----------------------------------------');
    DBMS_OUTPUT.PUT_LINE('MARCACIONES ACTUALES:');
    DBMS_OUTPUT.PUT_LINE('  Entrada:  ' || NVL(v_entrada, '(NULL)') || ' [' || v_tipo_entrada || ']');
    DBMS_OUTPUT.PUT_LINE('  IniRefri: ' || NVL(v_inirefri, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  FinRefri: ' || NVL(v_finrefri, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  Salida:   ' || NVL(v_salida, '(NULL)') || ' [' || v_tipo_salida || ']');
    DBMS_OUTPUT.PUT_LINE('----------------------------------------');
    DBMS_OUTPUT.PUT_LINE('HORARIO ORIGINAL (SCA_HORARIO_DET):');
    DBMS_OUTPUT.PUT_LINE('  Descripcion: ' || NVL(v_hor_descripcion, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  Clase:       ' || NVL(v_hor_clase, '(NULL)') || '  Descanso: ' || v_hor_descanso);
    DBMS_OUTPUT.PUT_LINE('  Entrada:  ' || NVL(v_hor_entrada, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  IniRefri: ' || NVL(v_hor_inirefri, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  FinRefri: ' || NVL(v_hor_finrefri, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  Salida:   ' || NVL(v_hor_salida, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  Total Hrs: ' || NVL(v_hor_total_hrs, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('----------------------------------------');
    DBMS_OUTPUT.PUT_LINE('HORARIO FIJADO EN TAREO:');
    DBMS_OUTPUT.PUT_LINE('  Entrada:  ' || NVL(v_entrada_teo, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  IniRefri: ' || NVL(v_inirefri_teo, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  FinRefri: ' || NVL(v_finrefri_teo, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  Salida:   ' || NVL(v_salida_teo, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('----------------------------------------');
    DBMS_OUTPUT.PUT_LINE('HORAS CALCULADAS:');
    DBMS_OUTPUT.PUT_LINE('  TOTHORAS (teor):' || NVL(v_tothoras, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  Hrs Brutas:    ' || NVL(v_hrs_brutas, '00:00'));
    DBMS_OUTPUT.PUT_LINE('  Hrs Refrigerio:' || NVL(v_hrs_refri, '00:00'));
    DBMS_OUTPUT.PUT_LINE('  Hrs Efectivas: ' || NVL(v_hrs_efectivas, '00:00'));
    DBMS_OUTPUT.PUT_LINE('  Hrs Nocturnas: ' || NVL(v_hrs_nocturnas, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  Hrs Noct Ofic: ' || NVL(v_hrs_nocturnas_of, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  Tardanza:      ' || NVL(v_tardanza, '00:00'));
    DBMS_OUTPUT.PUT_LINE('  Hrs Dobles:    ' || NVL(v_horadobles, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('----------------------------------------');
    DBMS_OUTPUT.PUT_LINE('Descanso: ' || v_descanso || ' | Cerrado: ' || v_cerrado || ' | Obrero: ' || v_obrero);
    DBMS_OUTPUT.PUT_LINE('----------------------------------------');
    DBMS_OUTPUT.PUT_LINE('PERMISOS/AUSENCIAS:');
    IF v_desc_med = 'S' OR v_subsidio = 'S' OR v_perm_goce = 'S' OR v_perm_sgoce = 'S' 
       OR v_vacaciones = 'S' OR v_suspension = 'S' OR v_lic_pat = 'S' OR v_lic_fac = 'S' THEN
        IF v_desc_med = 'S' THEN DBMS_OUTPUT.PUT_LINE('  >> DESCANSO MEDICO'); END IF;
        IF v_subsidio = 'S' THEN DBMS_OUTPUT.PUT_LINE('  >> SUBSIDIO'); END IF;
        IF v_perm_goce = 'S' THEN DBMS_OUTPUT.PUT_LINE('  >> PERMISO CON GOCE'); END IF;
        IF v_perm_sgoce = 'S' THEN DBMS_OUTPUT.PUT_LINE('  >> PERMISO SIN GOCE'); END IF;
        IF v_vacaciones = 'S' THEN DBMS_OUTPUT.PUT_LINE('  >> VACACIONES'); END IF;
        IF v_suspension = 'S' THEN DBMS_OUTPUT.PUT_LINE('  >> SUSPENSION'); END IF;
        IF v_lic_pat = 'S' THEN DBMS_OUTPUT.PUT_LINE('  >> LICENCIA PATERNIDAD'); END IF;
        IF v_lic_fac = 'S' THEN DBMS_OUTPUT.PUT_LINE('  >> LICENCIA FALLECIMIENTO'); END IF;
        DBMS_OUTPUT.PUT_LINE('  (!) NO SE COMPLETARAN MARCACIONES');
    ELSE
        DBMS_OUTPUT.PUT_LINE('  (Ninguno)');
    END IF;
    DBMS_OUTPUT.PUT_LINE('----------------------------------------');
    DBMS_OUTPUT.PUT_LINE('HORAS EXTRAS:');
    DBMS_OUTPUT.PUT_LINE('  H.Extra:           ' || NVL(v_hora_extra, '00:00'));
    DBMS_OUTPUT.PUT_LINE('  H.Extra Antes:     ' || NVL(v_hora_extra_antes, '00:00'));
    DBMS_OUTPUT.PUT_LINE('  Total H.Extras:    ' || NVL(v_total_horas_extras, '00:00'));
    DBMS_OUTPUT.PUT_LINE('  Tiempo Anticipado: ' || NVL(v_tiempo_anticipado, '00:00'));
    DBMS_OUTPUT.PUT_LINE('  HE Antes Autoriz:  ' || v_he_antes_aut);
    DBMS_OUTPUT.PUT_LINE('----------------------------------------');
    DBMS_OUTPUT.PUT_LINE('HORAS EXTRAS - BREAKDOWN (REPORTE):');
    DBMS_OUTPUT.PUT_LINE('  H.Desp.Salida:     ' || NVL(v_hora_desp_sal, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  H.Extra Oficial:   ' || NVL(v_hora_extra_ofi, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  Total Extras Ofi:  ' || NVL(v_total_extras_ofi, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  H.Extra Ajustado:  ' || NVL(v_hora_extra_ajus, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  Alerta06:          ' || NVL(v_alerta06, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  H.Extra 25%:       ' || NVL(v_he_25pct, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  H.Extra 35%:       ' || NVL(v_he_35pct, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  H.Extra 50%:       ' || NVL(v_he_50pct, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  H.Ofi 25%:         ' || NVL(v_he_ofi_25pct, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  H.Ofi 35%:         ' || NVL(v_he_ofi_35pct, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  H.Ofi 50%:         ' || NVL(v_he_ofi_50pct, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  [Config] H25F=' || NVL(v_cfg_h25f,'?') || ' H35I=' || NVL(v_cfg_h35i,'?') || ' H35F=' || NVL(v_cfg_h35f,'?') || ' HNI=' || NVL(v_cfg_hni,'?'));
    DBMS_OUTPUT.PUT_LINE('  [Config] Ajuste=' || NVL(TO_CHAR(v_cfg_ajuste_he),'?') || ' TipPagoHE=' || NVL(v_cfg_tippagohe,'?'));
    DBMS_OUTPUT.PUT_LINE('----------------------------------------');
    DBMS_OUTPUT.PUT_LINE('DEPURACION APLICADA:');
    IF v_cod_depuracion IS NOT NULL THEN
        DBMS_OUTPUT.PUT_LINE('  Codigo: ' || v_cod_depuracion);
        DBMS_OUTPUT.PUT_LINE('  Descrip: ' || v_desc_depuracion);
    ELSE
        DBMS_OUTPUT.PUT_LINE('  (Ninguna)');
    END IF;
    DBMS_OUTPUT.PUT_LINE('----------------------------------------');
    DBMS_OUTPUT.PUT_LINE('VERIFICACION HISTORIAL:');
    DBMS_OUTPUT.PUT_LINE('  Alerta01:       ' || NVL(v_alerta01, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  Marcas SCA_HIS: ' || NVL(TO_CHAR(v_marcas_historial), '0'));
    IF v_alerta01 = 'MI' THEN
        DBMS_OUTPUT.PUT_LINE('  (!) MARCACION IMPAR - Historial tiene numero impar de marcas');
    END IF;
    
    CLOSE cv_resultado;
END;
/


/*******************************************************************************
    2. EJECUTAR DEPURACION PARA ESTE EMPLEADO
*******************************************************************************/
PROMPT 
PROMPT ============================================================
PROMPT EJECUTANDO DEPURACION...
PROMPT ============================================================

DECLARE
    cv_resultado SYS_REFCURSOR;
    v_resultado VARCHAR2(600);
    v_fecha VARCHAR2(20);
    v_nocturno NUMBER;
    v_entrada NUMBER;
    v_anticipada NUMBER;
    v_salida NUMBER;
    v_inirefri NUMBER;
    v_finrefri NUMBER;
    v_anomala NUMBER;
    v_noct_sin_refri NUMBER;  -- RN: Nocturnos sin refrigerio limpiados
    v_recalculo NUMBER;
    v_total NUMBER;
    v_historial NUMBER;
BEGIN
    PKG_SCA_DEPURA_TAREO.DEPURA_TOTAL(
        p_cod_empresa    => '&P_EMPRESA',
        p_cod_personal   => '&P_PERSONAL',
        p_fecha          => '&P_FECHA',
        p_solo_obreros   => 'N',    -- 'N' para incluir aunque no sea obrero
        cv_resultado     => cv_resultado
    );
    
    FETCH cv_resultado INTO v_resultado, v_fecha, v_nocturno, v_entrada, v_anticipada,
        v_salida, v_inirefri, v_finrefri, v_anomala, v_noct_sin_refri, v_recalculo, v_total, v_historial;
    
    DBMS_OUTPUT.PUT_LINE('Resultado: ' || v_resultado);
    DBMS_OUTPUT.PUT_LINE('Fecha: ' || v_fecha);
    DBMS_OUTPUT.PUT_LINE('----------------------------------------');
    DBMS_OUTPUT.PUT_LINE('Turnos nocturnos corregidos:' || v_nocturno);
    DBMS_OUTPUT.PUT_LINE('Entradas completadas:       ' || v_entrada);
    DBMS_OUTPUT.PUT_LINE('Entradas anticipadas adj:   ' || v_anticipada);
    DBMS_OUTPUT.PUT_LINE('Salidas completadas:        ' || v_salida);
    DBMS_OUTPUT.PUT_LINE('IniRefri completados:       ' || v_inirefri);
    DBMS_OUTPUT.PUT_LINE('FinRefri completados:       ' || v_finrefri);
    DBMS_OUTPUT.PUT_LINE('Marcaciones anomalas:       ' || v_anomala);
    DBMS_OUTPUT.PUT_LINE('Nocturnos sin refri (RN):   ' || v_noct_sin_refri);
    DBMS_OUTPUT.PUT_LINE('Horas recalculadas:         ' || v_recalculo);
    DBMS_OUTPUT.PUT_LINE('Marcas historial insertadas:' || v_historial);
    DBMS_OUTPUT.PUT_LINE('----------------------------------------');
    DBMS_OUTPUT.PUT_LINE('TOTAL MARCAS GENERADAS:     ' || v_total);
    
    CLOSE cv_resultado;
END;
/


/*******************************************************************************
    3. VER ESTADO DESPUES DE DEPURAR
*******************************************************************************/
PROMPT 
PROMPT ============================================================
PROMPT ESTADO DEL EMPLEADO (DESPUES DE DEPURAR)
PROMPT ============================================================

DECLARE
    cv_resultado SYS_REFCURSOR;
    v_empresa VARCHAR2(10);
    v_personal VARCHAR2(10);
    v_fotocheck VARCHAR2(20);
    v_nummarcas NUMBER;
    v_entrada VARCHAR2(20);
    v_inirefri VARCHAR2(20);
    v_finrefri VARCHAR2(20);
    v_salida VARCHAR2(20);
    v_hor_descripcion VARCHAR2(60);
    v_hor_clase VARCHAR2(20);
    v_hor_entrada VARCHAR2(20);
    v_hor_inirefri VARCHAR2(20);
    v_hor_finrefri VARCHAR2(20);
    v_hor_salida VARCHAR2(20);
    v_hor_total_hrs VARCHAR2(20);
    v_hor_descanso VARCHAR2(1);
    v_entrada_teo VARCHAR2(20);
    v_inirefri_teo VARCHAR2(20);
    v_finrefri_teo VARCHAR2(20);
    v_salida_teo VARCHAR2(20);
    v_hrs_brutas VARCHAR2(20);
    v_hrs_refri VARCHAR2(20);
    v_hrs_efectivas VARCHAR2(20);
    v_tardanza VARCHAR2(20);
    v_hrs_nocturnas VARCHAR2(20);
    v_hrs_nocturnas_of VARCHAR2(20);
    v_tothoras VARCHAR2(20);
    v_horadobles VARCHAR2(20);
    v_descanso VARCHAR2(1);
    v_cerrado VARCHAR2(1);
    v_obrero VARCHAR2(1);
    v_tipo_entrada VARCHAR2(10);
    v_tipo_salida VARCHAR2(10);
    -- Permisos/Ausencias (se ignoran en el despues)
    v_desc_med VARCHAR2(1);
    v_subsidio VARCHAR2(1);
    v_perm_goce VARCHAR2(1);
    v_perm_sgoce VARCHAR2(1);
    v_vacaciones VARCHAR2(1);
    v_suspension VARCHAR2(1);
    v_lic_pat VARCHAR2(1);
    v_lic_fac VARCHAR2(1);
    -- Horas extras
    v_hora_extra VARCHAR2(20);
    v_hora_extra_antes VARCHAR2(20);
    v_total_horas_extras VARCHAR2(20);
    v_tiempo_anticipado VARCHAR2(20);
    v_he_antes_aut VARCHAR2(1);
    -- Horas extras - breakdown/rangos
    v_hora_desp_sal VARCHAR2(20);
    v_hora_extra_ofi VARCHAR2(20);
    v_total_extras_ofi VARCHAR2(20);
    v_hora_extra_ajus VARCHAR2(20);
    v_alerta06 VARCHAR2(2);
    v_he_25pct VARCHAR2(20);
    v_he_35pct VARCHAR2(20);
    v_he_50pct VARCHAR2(20);
    v_he_ofi_25pct VARCHAR2(20);
    v_he_ofi_35pct VARCHAR2(20);
    v_he_ofi_50pct VARCHAR2(20);
    v_cfg_h25f VARCHAR2(20);
    v_cfg_h35i VARCHAR2(20);
    v_cfg_h35f VARCHAR2(20);
    v_cfg_hni VARCHAR2(20);
    v_cfg_ajuste_he NUMBER;
    v_cfg_tippagohe VARCHAR2(1);
    -- Campos de auditoría de depuración
    v_cod_depuracion VARCHAR2(100);
    v_desc_depuracion VARCHAR2(500);
    -- Alerta y verificacion historial
    v_alerta01 VARCHAR2(2);
    v_marcas_historial NUMBER;
BEGIN
    PKG_SCA_DEPURA_TAREO.VER_ESTADO(
        p_cod_empresa  => '&P_EMPRESA',
        p_cod_personal => '&P_PERSONAL',
        p_fecha        => '&P_FECHA',
        cv_resultado   => cv_resultado
    );
    
    FETCH cv_resultado INTO 
        v_empresa, v_personal, v_fotocheck, v_nummarcas,
        v_entrada, v_inirefri, v_finrefri, v_salida,
        v_hor_descripcion, v_hor_clase, v_hor_entrada, v_hor_inirefri,
        v_hor_finrefri, v_hor_salida, v_hor_total_hrs, v_hor_descanso,
        v_entrada_teo, v_inirefri_teo, v_finrefri_teo, v_salida_teo,
        v_hrs_brutas, v_hrs_refri, v_hrs_efectivas, v_tardanza,
        v_hrs_nocturnas, v_hrs_nocturnas_of, v_tothoras,
        v_horadobles,
        v_descanso, v_cerrado, v_obrero, v_tipo_entrada, v_tipo_salida,
        v_desc_med, v_subsidio, v_perm_goce, v_perm_sgoce,
        v_vacaciones, v_suspension, v_lic_pat, v_lic_fac,
        v_hora_extra, v_hora_extra_antes, v_total_horas_extras,
        v_tiempo_anticipado, v_he_antes_aut,
        v_hora_desp_sal, v_hora_extra_ofi, v_total_extras_ofi,
        v_hora_extra_ajus, v_alerta06,
        v_he_25pct, v_he_35pct, v_he_50pct,
        v_he_ofi_25pct, v_he_ofi_35pct, v_he_ofi_50pct,
        v_cfg_h25f, v_cfg_h35i, v_cfg_h35f, v_cfg_hni,
        v_cfg_ajuste_he, v_cfg_tippagohe,
        v_cod_depuracion, v_desc_depuracion,
        v_alerta01, v_marcas_historial;
    
    DBMS_OUTPUT.PUT_LINE('Fotocheck: ' || v_fotocheck);
    DBMS_OUTPUT.PUT_LINE('Num Marcaciones: ' || v_nummarcas);
    DBMS_OUTPUT.PUT_LINE('----------------------------------------');
    DBMS_OUTPUT.PUT_LINE('MARCACIONES ACTUALIZADAS:');
    DBMS_OUTPUT.PUT_LINE('  Entrada:  ' || NVL(v_entrada, '(NULL)') || ' [' || v_tipo_entrada || ']');
    DBMS_OUTPUT.PUT_LINE('  IniRefri: ' || NVL(v_inirefri, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  FinRefri: ' || NVL(v_finrefri, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  Salida:   ' || NVL(v_salida, '(NULL)') || ' [' || v_tipo_salida || ']');
    DBMS_OUTPUT.PUT_LINE('----------------------------------------');
    DBMS_OUTPUT.PUT_LINE('HORARIO ORIGINAL (SCA_HORARIO_DET):');
    DBMS_OUTPUT.PUT_LINE('  Descripcion: ' || NVL(v_hor_descripcion, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  Clase:       ' || NVL(v_hor_clase, '(NULL)') || '  Descanso: ' || v_hor_descanso);
    DBMS_OUTPUT.PUT_LINE('  Entrada:  ' || NVL(v_hor_entrada, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  IniRefri: ' || NVL(v_hor_inirefri, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  FinRefri: ' || NVL(v_hor_finrefri, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  Salida:   ' || NVL(v_hor_salida, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  Total Hrs: ' || NVL(v_hor_total_hrs, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('----------------------------------------');
    DBMS_OUTPUT.PUT_LINE('HORARIO FIJADO EN TAREO:');
    DBMS_OUTPUT.PUT_LINE('  Entrada:  ' || NVL(v_entrada_teo, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  IniRefri: ' || NVL(v_inirefri_teo, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  FinRefri: ' || NVL(v_finrefri_teo, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  Salida:   ' || NVL(v_salida_teo, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('----------------------------------------');
    DBMS_OUTPUT.PUT_LINE('HORAS RECALCULADAS:');
    DBMS_OUTPUT.PUT_LINE('  TOTHORAS (teor):' || NVL(v_tothoras, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  Hrs Brutas:    ' || NVL(v_hrs_brutas, '00:00'));
    DBMS_OUTPUT.PUT_LINE('  Hrs Refrigerio:' || NVL(v_hrs_refri, '00:00'));
    DBMS_OUTPUT.PUT_LINE('  Hrs Efectivas: ' || NVL(v_hrs_efectivas, '00:00'));
    DBMS_OUTPUT.PUT_LINE('  Hrs Nocturnas: ' || NVL(v_hrs_nocturnas, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  Hrs Noct Ofic: ' || NVL(v_hrs_nocturnas_of, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  Tardanza:      ' || NVL(v_tardanza, '00:00'));
    DBMS_OUTPUT.PUT_LINE('  Hrs Dobles:    ' || NVL(v_horadobles, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('----------------------------------------');
    DBMS_OUTPUT.PUT_LINE('HORAS EXTRAS (DESPUES):');
    DBMS_OUTPUT.PUT_LINE('  H.Extra:           ' || NVL(v_hora_extra, '00:00'));
    DBMS_OUTPUT.PUT_LINE('  H.Extra Antes:     ' || NVL(v_hora_extra_antes, '00:00'));
    DBMS_OUTPUT.PUT_LINE('  Total H.Extras:    ' || NVL(v_total_horas_extras, '00:00'));
    DBMS_OUTPUT.PUT_LINE('  Tiempo Anticipado: ' || NVL(v_tiempo_anticipado, '00:00'));
    DBMS_OUTPUT.PUT_LINE('----------------------------------------');
    DBMS_OUTPUT.PUT_LINE('HORAS EXTRAS - BREAKDOWN (REPORTE):');
    DBMS_OUTPUT.PUT_LINE('  H.Desp.Salida:     ' || NVL(v_hora_desp_sal, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  H.Extra Oficial:   ' || NVL(v_hora_extra_ofi, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  Total Extras Ofi:  ' || NVL(v_total_extras_ofi, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  H.Extra Ajustado:  ' || NVL(v_hora_extra_ajus, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  Alerta06:          ' || NVL(v_alerta06, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  H.Extra 25%:       ' || NVL(v_he_25pct, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  H.Extra 35%:       ' || NVL(v_he_35pct, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  H.Extra 50%:       ' || NVL(v_he_50pct, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  H.Ofi 25%:         ' || NVL(v_he_ofi_25pct, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  H.Ofi 35%:         ' || NVL(v_he_ofi_35pct, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  H.Ofi 50%:         ' || NVL(v_he_ofi_50pct, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  [Config] H25F=' || NVL(v_cfg_h25f,'?') || ' H35I=' || NVL(v_cfg_h35i,'?') || ' H35F=' || NVL(v_cfg_h35f,'?') || ' HNI=' || NVL(v_cfg_hni,'?'));
    DBMS_OUTPUT.PUT_LINE('  [Config] Ajuste=' || NVL(TO_CHAR(v_cfg_ajuste_he),'?') || ' TipPagoHE=' || NVL(v_cfg_tippagohe,'?'));
    DBMS_OUTPUT.PUT_LINE('----------------------------------------');
    DBMS_OUTPUT.PUT_LINE('DEPURACION APLICADA:');
    IF v_cod_depuracion IS NOT NULL THEN
        DBMS_OUTPUT.PUT_LINE('  Codigo: ' || v_cod_depuracion);
        DBMS_OUTPUT.PUT_LINE('  Descrip: ' || v_desc_depuracion);
    ELSE
        DBMS_OUTPUT.PUT_LINE('  (Ninguna)');
    END IF;
    DBMS_OUTPUT.PUT_LINE('----------------------------------------');
    DBMS_OUTPUT.PUT_LINE('VERIFICACION HISTORIAL:');
    DBMS_OUTPUT.PUT_LINE('  Alerta01:       ' || NVL(v_alerta01, '(NULL)'));
    DBMS_OUTPUT.PUT_LINE('  Marcas SCA_HIS: ' || NVL(TO_CHAR(v_marcas_historial), '0'));
    IF v_alerta01 = 'MI' THEN
        DBMS_OUTPUT.PUT_LINE('  (!) MARCACION IMPAR - No se logro resolver');
    ELSE
        DBMS_OUTPUT.PUT_LINE('  (OK) Alerta impar resuelta');
    END IF;
    
    CLOSE cv_resultado;
END;
/

PROMPT 
PROMPT ============================================================
PROMPT PRUEBA COMPLETADA
PROMPT ============================================================
