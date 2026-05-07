# Aquarius — Supervisores, Usuarios y Acceso a Empleados (06/05/2026)

## MODELO DE SEGURIDAD: CÓMO UN USUARIO VE SUS EMPLEADOS

No existe tabla directa supervisor→empleado. El acceso se define por **intersección de 4 filtros**:

```
PLA_PERSONAL visible para usuario :usr si cumple TODOS:
  1. cod_empresa  IN (MAE_USUARIO_EMP    WHERE cod_usuario=:usr AND cod_grupo_menu=:grp)
  2. cod_sucursal IN (MAE_SUCURSAL_USUARIO WHERE cod_usuario=:usr AND cod_empresa=p.cod_empresa AND cod_grupo_menu=:grp)
  3. cod_tipo_planilla IN (PLA_PERFIL_PLANILLA → PLA_PERFIL_ACCESO_PLANI → PLA_USUARIO_PLANILLA
                           WHERE cod_usuario=:usr AND cod_grupo_menu=:grp AND ind_asistencia='S')
  4. cod_c_costos IN (MAE_C_COSTOS_VERSION → MAE_C_COSTOS → MAE_C_COSTOS_USUARIO
                      WHERE cod_usuario=:usr AND cod_grupo_menu=:grp AND ind_vigente='S')
```
Esta lógica está en: `sp_SCA_Read_Per_AsiHor_ByCod`, `sp_SCA_Read_Per_AsiHor_ByNom`, `sp_SCA_Read_Per_AsiHor_Gen`, `sp_SCA_Read_Tareo_Masivo` y todos los SPs de lectura de asistencia.

---

## TABLAS DEL MODELO DE SEGURIDAD

### MAE_USUARIO — Maestro de usuarios
| Campo | Tipo | Descripción |
|---|---|---|
| COD_USUARIO | VARCHAR2(11) | PK, login del usuario |
| NOM_USUARIO | VARCHAR2(18) | nombre display |
| DES_PASSWORD | VARCHAR2(200) | contraseña (hash propietario .NET) |
| COD_EMPRESA | NVARCHAR2(8) | empresa del empleado vinculado |
| COD_PERSONAL | VARCHAR2(6) | empleado vinculado (su propio legajo) |
| IND_BAJA | VARCHAR2(1) | 'S'=dado de baja |
| IND_ADMIN | VARCHAR2(1) | 'S'=administrador global |
| IND_VER_TODOS | VARCHAR2(1) | ver todos (sin restricción CC) |

### MAE_USUARIO_EMP — Empresas/módulos habilitados por usuario
| Campo | Tipo | Descripción |
|---|---|---|
| COD_USUARIO | VARCHAR2(30) | FK MAE_USUARIO |
| COD_EMPRESA | NVARCHAR2(8) | empresa a la que tiene acceso |
| COD_GRUPO_MENU | VARCHAR2(4) | módulo: '1001'=Admin/Seg, '1002'=Control Asistencia |
| COD_PERFIL | VARCHAR2(4) | perfil (no usado activamente) |
| TIP_USUARIO | VARCHAR2(20) | **'Adm'=administrador (ve todo), 'Usu'=supervisor restringido** |
| INDCCO_USUEMP | NUMBER(1) | 1=acceso por centro de costos activo |

### MAE_SUCURSAL_USUARIO — Sucursales asignadas
- (COD_EMPRESA, COD_SUCURSAL, COD_USUARIO, COD_GRUPO_MENU)

### MAE_C_COSTOS_USUARIO — Centros de costos asignados
- (COD_EMPRESA, COD_USUARIO, COD_GRUPO_MENU, NUM_VER_C_COSTOS, COD_C_COSTOS)

### PLA_PERFIL_PLANILLA — Perfiles de planilla
- (COD_EMPRESA, COD_PERFIL_PLANI, DES_PERFIL_PLANI)

### PLA_PERFIL_ACCESO_PLANI — Tipos de planilla por perfil
- (COD_EMPRESA, COD_PERFIL_PLANI, COD_TIPO_PLANILLA)

### PLA_USUARIO_PLANILLA — Perfiles asignados a usuarios
- (COD_EMPRESA, COD_USUARIO, COD_GRUPO_MENU, COD_PERFIL_PLANI)

---

## GRUPOS DE MÓDULOS (MAE_FUNCION_GRUPO)
| COD_GRUPO_MENU | DES_GRUPO_MENU | TIP_GRUPO |
|---|---|---|
| 1001 | ADMINISTRACION Y SEGURIDAD | SI |
| 1002 | CONTROL DE ASISTENCIA | SI |

---

## USUARIOS ACTIVOS DEL SISTEMA (datos reales BD)

### Tipo 'Usu' (supervisores operativos — acceso restringido a CCs específicos)
| Usuario | Nombre | Empresa | Empleado | Centros de Costo |
|---|---|---|---|---|
| CCAL1 | CONTROL CALIDAD | 0003 | VILLACORTA | P710, P711 |
| JTEJARB1 | JEFE TELAR ARB | 0001 | MODESTO | 310000-342000, 351000, 370000-371000 |
| PREP1 | PREPARATORIA | 0003 | SANDOVAL | P110-P170, P190, P210 |
| TINT1 | TINTCOLONIAL | 0003 | CARBAJAL | P510, P520, P540, P560, P820 |
| TINT2 | SUPERV. TINTO | 0003 | TREBEJO | P510-P530, P540, P560 |
| calidcol1 | JEFE CALIDAD | 0003 | SANCHEZ | P710, P711 |
| calidcol2 | JEFE CALIDAD TINTO | 0003 | JARA | P780 |
| coneracol1 | SUPERV. CONERA | 0003 | QUICAÑA | P310, P360, P390, P410 |
| conticol1 | SUPERV. CONTINUAS | 0003 | RIVAS | P210, P310 |
| dsolorz | DANIEL SOLORZANO | 0003 | SOLORZANO | P390, P410 |
| flocacol | ENCARGADO FLOCA | 0003 | AQUINO | P790 |
| hilar1 | JEFATURA PLANTA | 0003 | BUSTAMANTE | P110-P170, P190-P320 (planta completa) |
| jalmint | JEFE INTERMEDIOS | 0003 | PORTOCARRERO | P210, P310, etc. |
| jalmqui | JEFE ALM QUIMICOS | 0003 | RIVERA | químicos |
| labocol1 | JEFE LABORATORIO | 0003 | MARCACUZCO | lab |
| mant1 | JEF MANTENIMIENTO | 0003 | PAJARES | mant. |
| retorcol1 | ENCARG. RETORC | 0003 | MUÑOZ | retor. |
| segucol1 | JEFE SEGURIDAD | 0003 | TOSCANO | seguridad |
| segucol2 | SUPERV SEGURIDAD | 0003 | GUILLEN | seguridad |
| suptej | Sup. Tej ARB | 0001 | CRUZ | 001, 002 (suc.) |
| vigicol1 | VIGILANCIA | 0003 | SULCA | vigilancia |

### Tipo 'Adm' (administradores — ven todos los CCs de la empresa)
| Usuario | Nombre | Empresas |
|---|---|---|
| rrhh1 | JEFE RRHH ARBONA | 0002, 0003 |
| rrhh2 | JEFE RRHH COLONIAL | 0001, 0003 |
| rrhh3 | PLANILLA ARBONA | 0001, 0002, 0003 |
| rrhh4 | SELECCION COLONIAL | 0001, 0003 |
| rrhh5 | ASISTENTE COLONIAL | 0003 |
| rrhh6 | ASISTENTE ARBONA | 0001, 0002 |
| rrhh7 | ASIST SELEC | 0001, 0002, 0003 |
| financol1 | JEFE ADMINISTRAC | 0003 |
| sistemas1 | JEFE DE SISTEMAS | 0001, 0002, 0003 |
| sistemas2 | COORD. SISTEMAS | 0001, 0002, 0003 |
| a | Administrador | 0001, 0002, 0003 |

---

## HORARIOS Y SU RELACIÓN CON SUPERVISORES

### SCA_HORARIO_CAB — 88+ horarios definidos
Clasificaciones HORCLA:
- `'ES'` = estándar (diurno normal)
- `'AM'` = amanecida (turno nocturno que cruza medianoche)
- NULL = sin clasificar (algunos especiales)

Tipos de turno más comunes:
| HORID | Descripción | HORCLA | IND_ROTATIVO |
|---|---|---|---|
| 0023 | PRIMER TURNO | ES | S |
| 0024 | SEGUNDO TURNO | ES | S |
| 0025 | TERCER TURNO | AM | S |
| 0032 | SIN HORARIO | ES | N |
| 0034 | HORARIO VIGILANCIA | ES | S |
| 0035 | LACTANCIA II | ES | S |

Los supervisores ven el horario vigente de cada empleado a su cargo via `sp_SCA_Read_Per_AsiHor_Gen`.

### SCA_HORARIO_PERSONAL — Asignación horario→empleado
- PK: (COD_EMPRESA, COD_PERSONAL, FEC_VIGENCIA)
- Para obtener vigente: `MAX(fec_vigencia) WHERE fec_vigencia <= :fecha_proceso`
- Un supervisor puede listar empleados **sin horario** (`sp_SCA_Read_Per_AsiHor_Sin`) y **con horario** (`sp_SCA_Read_Per_AsiHor_Gen`)
- Campo `CODAUX1`, `CODAUX2` se copian al tareo (útiles para agrupamiento)

---

## SCA_PERMISO_PORAUTORIZAR — Permisos pendientes de aprobación

Cuando un supervisor/empleado solicita un permiso pero RRHH aún no aprueba:
| Campo clave | Descripción |
|---|---|
| PERID_PORAUT | PK (char 6) |
| COD_USUARIO | quien solicita |
| PERPEND | 'S' = pendiente de aprobación RRHH |
| COD_USUARIO_AUT_RRHH | usuario RRHH que aprueba |
| FEC_AUT_RRHH | fecha de aprobación |
| AUX1 | campo auxiliar (observaciones extra) |
| PERDIAG/PERCITT/PERLUGATE | para permisos médicos: diagnóstico CIE, CIT, lugar atención |

Flujo: solicitud → SCA_PERMISO_PORAUTORIZAR (PERPEND='S') → RRHH aprueba → pasa a SCA_PERMISO_CAB+DET

---

## CONSULTA: EMPLEADOS A CARGO DE UN SUPERVISOR

```sql
-- Ver empleados visibles para usuario :usr en módulo Control de Asistencia
SELECT p.cod_empresa, p.cod_personal, 
       p.ape_paterno||' '||p.ape_materno||' '||p.nom_trabajador nombre,
       p.cod_sucursal, p.cod_c_costos, p.cod_tipo_planilla
FROM PLA_PERSONAL p
JOIN PLA_TIPO_PLANILLA tp ON tp.cod_empresa=p.cod_empresa AND tp.cod_tipo_planilla=p.cod_tipo_planilla
WHERE p.tip_estado <> '3'   -- no cesados
AND p.cod_empresa IN (
    SELECT cod_empresa FROM MAE_USUARIO_EMP 
    WHERE cod_usuario=:usr AND cod_grupo_menu='1002')
AND p.cod_sucursal IN (
    SELECT cod_sucursal FROM MAE_SUCURSAL_USUARIO 
    WHERE cod_usuario=:usr AND cod_empresa=p.cod_empresa AND cod_grupo_menu='1002')
AND p.cod_tipo_planilla IN (
    SELECT ppa.cod_tipo_planilla FROM PLA_PERFIL_PLANILLA pp
    JOIN PLA_PERFIL_ACCESO_PLANI ppa ON pp.cod_empresa=ppa.cod_empresa AND pp.cod_perfil_plani=ppa.cod_perfil_plani
    JOIN PLA_USUARIO_PLANILLA pup ON pp.cod_empresa=pup.cod_empresa AND pp.cod_perfil_plani=pup.cod_perfil_plani
    WHERE pup.cod_usuario=:usr AND pup.cod_grupo_menu='1002' 
      AND pp.cod_empresa=p.cod_empresa AND tp.ind_asistencia='S')
AND p.cod_c_costos IN (
    SELECT cc.cod_c_costos FROM MAE_C_COSTOS_VERSION ccv
    JOIN MAE_C_COSTOS cc ON ccv.cod_empresa=cc.cod_empresa AND ccv.num_ver_c_costos=cc.num_ver_c_costos
    JOIN MAE_C_COSTOS_USUARIO ccu ON cc.cod_empresa=ccu.cod_empresa AND cc.num_ver_c_costos=ccu.num_ver_c_costos AND cc.cod_c_costos=ccu.cod_c_costos
    WHERE ccu.cod_usuario=:usr AND cc.cod_empresa=p.cod_empresa 
      AND ccu.cod_grupo_menu='1002' AND ccv.ind_vigente='S')
ORDER BY p.ape_paterno;
```

---

## CONSULTA: HORARIO VIGENTE DE EMPLEADOS A CARGO DE UN SUPERVISOR

```sql
SELECT hp.cod_empresa, hp.cod_personal,
       p.ape_paterno||' '||p.nom_trabajador nombre,
       p.cod_c_costos, hp.horid, hc.hordes, hp.fec_vigencia
FROM SCA_HORARIO_PERSONAL hp
JOIN PLA_PERSONAL p ON p.cod_empresa=hp.cod_empresa AND p.cod_personal=hp.cod_personal
JOIN SCA_HORARIO_CAB hc ON hc.horid=hp.horid
WHERE hp.fec_vigencia = (
    SELECT MAX(hp2.fec_vigencia) FROM SCA_HORARIO_PERSONAL hp2
    WHERE hp2.cod_empresa=hp.cod_empresa AND hp2.cod_personal=hp.cod_personal
    AND hp2.fec_vigencia <= SYSDATE)
AND p.tip_estado <> '3'
-- más los mismos filtros de empresa/sucursal/CC/tipo planilla del usuario
ORDER BY p.cod_c_costos, hc.hordes, p.ape_paterno;
```

---

## SISTEMA DE AUTENTICACIÓN (MAE_USUARIO)

### Campos de login
| Campo | Tipo | Descripción |
|---|---|---|
| COD_USUARIO | VARCHAR2(11) | nombre de login (ej: "rrhh3", "hilar1", "a") |
| DES_PASSWORD | VARCHAR2(200) | contraseña codificada por el cliente .NET |
| IND_BAJA | VARCHAR2(1) | 'S'=cuenta desactivada |
| CHGPSS_USUA | DECIMAL | 1=debe cambiar clave al próximo login, 0=no |
| IND_ADMIN | VARCHAR2(1) | 'S'=administrador global (solo usuario "a") |

### Cómo se almacena la contraseña
**NO es texto plano ni hash estándar** (MD5/SHA). Es una codificación numérica propietaria que hace el cliente .NET ANTES de enviarla a BD. Cada carácter de la clave ocupa 3 dígitos decimales (código ASCII numérico). Longitudes observadas en BD: 30 chars ≈ contraseña de ~10 chars, 54 ≈ ~18 chars, etc.

### Flujo de login
```
1. Usuario escribe: COD_USUARIO + clave en pantalla .NET
2. .NET codifica la clave con su algoritmo numérico propio
3. Llama sp_SEG_Read_Usuario_ByCod(v_cod_usuario)
4. BD devuelve: des_password, ind_baja, chgpss_usua, datos del empleado vinculado
5. .NET compara localmente: clave_codificada == des_password de BD
6. Si ind_baja='S' → rechaza aunque clave sea correcta
7. Si chgpss_usua=1 → obliga cambio de clave
```
> La validación ocurre en el cliente .NET, NO en Oracle. La BD solo devuelve el hash.

### Creación/modificación de usuarios
- `sp_SEG_Insert_Usuario` — recibe `des_password` ya codificada desde .NET
- `sp_SEG_Update_Usuario` — actualiza clave + chgpss_usua
- Solo el usuario `"a"` (IND_ADMIN='S') puede gestionar usuarios (módulo 1001)

### Estado actual de cuentas (datos reales BD)
- **32 usuarios**, todos con `IND_BAJA='N'` (ninguno dado de baja)
- **Todos con `CHGPSS_USUA=0`** (nadie tiene pendiente cambio de clave)
- Solo usuario `"a"` tiene `IND_ADMIN='S'`

---

## USUARIOS TIPO 'Usu' — DETALLE COMPLETO (21 supervisores)

| Usuario | Nombre display | Empresa | Empleado | CCs asignados |
|---|---|---|---|---|
| CCAL1 | CONTROL CALIDAD | 0003 | VILLACORTA AMANDA | P710, P711 |
| JTEJARB1 | JEFE TELAR ARB | 0001 | MODESTO ANTHONY | 310000–319000, 340000–342000, 351000, 370000–371000 |
| PREP1 | PREPARATORIA | 0003 | SANDOVAL ALBERTO | P110–P170, P190, P210 |
| TINT1 | TINTCOLONIAL | 0003 | CARBAJAL FREDDY | P510, P520, P540, P560, P820 |
| TINT2 | SUPERV. TINTO | 0003 | TREBEJO ALONSO | P510, P520, P530, P540, P560 |
| calidcol1 | JEFE CALIDAD | 0003 | SANCHEZ URSULA | P710, P711 |
| calidcol2 | JEFE CALIDAD TINTO | 0003 | JARA IVON | P780 |
| coneracol1 | SUPERV. CONERA | 0003 | QUICAÑA ALBERTO | P310, P360, P390, P410 |
| conticol1 | SUPERV. CONTINUAS | 0003 | RIVAS ELEUTERIO | P210, P310 |
| dsolorz | DANIEL SOLORZANO | 0003 | SOLORZANO DANIEL | P390, P410 |
| flocacol | ENCARGADO FLOCA | 0003 | AQUINO RAQUEL | P790 |
| hilar1 | JEFATURA PLANTA | 0003 | BUSTAMANTE ELMER | P110–P320 (planta completa Colonial) |
| jalmint | JEFE INTERMEDIOS | 0003 | PORTOCARRERO FRANCISCO | intermedios |
| jalmqui | JEFE ALM QUIMICOS | 0003 | RIVERA RIVELINO | químicos |
| labocol1 | JEFE LABORATORIO | 0003 | MARCACUZCO MARIA | laboratorio |
| mant1 | JEF MANTENIMIENTO | 0003 | PAJARES RAMON | mantenimiento |
| retorcol1 | ENCARG. RETORC | 0003 | MUÑOZ WALTER | retorcido |
| segucol1 | JEFE SEGURIDAD | 0003 | TOSCANO ARMANDO | seguridad |
| segucol2 | SUPERV SEGURIDAD | 0003 | GUILLEN EDGAR | seguridad (+ entrada en módulo 1001) |
| suptej | Sup. Tej ARB | 0001 | CRUZ CARLOS | sucursales 001 y 002 |
| vigicol1 | VIGILANCIA | 0003 | SULCA SANDRO | vigilancia |

**Notas sobre solapamientos**:
- `TINT1` y `TINT2` comparten P510, P520, P540, P560 — ambos ven mismos empleados tintorería
- `coneracol1` y `dsolorz` comparten P390, P410
- `CCAL1` y `calidcol1` comparten P710, P711
- `hilar1` es el de mayor alcance: cubre toda la planta Colonial

---

## REGLAS Y NOTAS IMPORTANTES
1. **Supervisor 'Usu'** solo ve empleados de sus CCs asignados. RRHH 'Adm' ve toda la empresa.
2. **`INDCCO_USUEMP=1`** activa filtro por centro de costos incluso para tipo 'Adm' (ej: financol1, rrhh5, segucol2).
3. **El supervisor puede registrar autorizaciones de HE** (sp_SCA_Insert_Autorizacion) para empleados de sus CC.
4. **No hay tabla física jefe→empleado**: se deduce del conjunto CCs del usuario vs CCs del empleado.
5. **PLA_CARGOS.FLG_JEFE** existe pero está vacío en la BD actual — no se usa.
6. **MAE_USUARIO.COD_PERSONAL** vincula un login a un legajo de PLA_PERSONAL (el propio empleado del supervisor).
7. **Permisos de asistencia** solo los puede ver/aprobar quien tiene acceso al CC del empleado + grupo_menu 1002.
8. **sp_SCA_Read_Tareo_Masivo** usa los mismos 4 filtros para el cronograma masivo de supervisores.
9. **Solo módulo 1002** para todos los 'Usu' — ninguno tiene acceso a módulo 1001 (Admin/Seguridad).
10. **Cada usuario es también un empleado** con su propio tareo en SCA_ASISTENCIA_TAREO.
