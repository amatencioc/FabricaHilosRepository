-- ============================================================================
-- PKG_ORGATEX — Carga incremental de recetas de tintura ORGATEX -> CARGA_ORGATEX
-- ============================================================================
-- Versión: 1.0 — Fecha: 2026-07-22
--
-- Propósito:
--   Recibe fila por fila los datos leídos de ORGATEX (SQL Server, tablas
--   Dyelots + Dyelot_Recipe, ver credencialesOrgaText.txt en el workspace
--   ORGATEX) y los MERGEa en SIG.CARGA_ORGATEX. ORGATEX es de solo lectura:
--   ningún objeto se crea/modifica ahí; toda la lógica de idempotencia vive
--   en este paquete, del lado Oracle.
--
-- Clave natural de MERGE (CARGA_ORGATEX no tiene PK propia):
--   PARTIDA + LLAMADA + CONTADOR + COD_PRODUCTO + FECHA
--   (evita duplicados si el job se re-ejecuta para el mismo día).
--
-- Columnas NO tocadas por este paquete (pobladas por otro proceso, LOGIX):
--   RECETA_LOGIX, CANT_LOGIX, CANT_REAL_LOGIX
--
-- Requisitos previos de despliegue:
--   El usuario de conexión (ConnectionStrings:LaColonialConnection en
--   appsettings.json, hoy SIG) debe tener privilegios EXECUTE sobre este
--   paquete e INSERT/UPDATE sobre CARGA_ORGATEX. Si CARGA_ORGATEX pertenece
--   al esquema SIG y se conecta como SIG, no se necesitan grants extra.
--
-- Consumido por:
--   FabricaHilos.OrgatexSync (Worker Service .NET) — Data/OrgatexRepository.cs,
--   método MergeCargaOrgatexAsync → llama PKG_ORGATEX.SP_MERGE_FILA una vez
--   por cada fila leída de ORGATEX (ventana: día anterior completo, diario).
-- ============================================================================

CREATE OR REPLACE PACKAGE PKG_ORGATEX AS

  PROCEDURE SP_MERGE_FILA(
    P_RECETA_ORGATEX    IN  CARGA_ORGATEX.RECETA_ORGATEX%TYPE,
    P_PARTIDA           IN  CARGA_ORGATEX.PARTIDA%TYPE,
    P_COD_COLOR         IN  CARGA_ORGATEX.COD_COLOR%TYPE,
    P_DESC_COLOR        IN  CARGA_ORGATEX.DESC_COLOR%TYPE,
    P_MAQUINA           IN  CARGA_ORGATEX.MAQUINA%TYPE,
    P_PESO              IN  CARGA_ORGATEX.PESO%TYPE,
    P_LLAMADA           IN  CARGA_ORGATEX.LLAMADA%TYPE,
    P_CONTADOR          IN  CARGA_ORGATEX.CONTADOR%TYPE,
    P_COD_PRODUCTO      IN  CARGA_ORGATEX.COD_PRODUCTO%TYPE,
    P_DESCRIPCION       IN  CARGA_ORGATEX.DESCRIPCION%TYPE,
    P_CANT_ORGATEX      IN  CARGA_ORGATEX.CANT_ORGATEX%TYPE,
    P_CANT_REAL_ORGATEX IN  CARGA_ORGATEX.CANT_REAL_ORGATEX%TYPE,
    P_UNIDAD            IN  CARGA_ORGATEX.UNIDAD%TYPE,
    P_FECHA             IN  CARGA_ORGATEX.FECHA%TYPE,
    P_CODIGO_RESULTADO  OUT NUMBER,
    P_MENSAJE_RESULTADO OUT VARCHAR2
  );

END PKG_ORGATEX;
/

CREATE OR REPLACE PACKAGE BODY PKG_ORGATEX AS

  PROCEDURE SP_MERGE_FILA(
    P_RECETA_ORGATEX    IN  CARGA_ORGATEX.RECETA_ORGATEX%TYPE,
    P_PARTIDA           IN  CARGA_ORGATEX.PARTIDA%TYPE,
    P_COD_COLOR         IN  CARGA_ORGATEX.COD_COLOR%TYPE,
    P_DESC_COLOR        IN  CARGA_ORGATEX.DESC_COLOR%TYPE,
    P_MAQUINA           IN  CARGA_ORGATEX.MAQUINA%TYPE,
    P_PESO              IN  CARGA_ORGATEX.PESO%TYPE,
    P_LLAMADA           IN  CARGA_ORGATEX.LLAMADA%TYPE,
    P_CONTADOR          IN  CARGA_ORGATEX.CONTADOR%TYPE,
    P_COD_PRODUCTO      IN  CARGA_ORGATEX.COD_PRODUCTO%TYPE,
    P_DESCRIPCION       IN  CARGA_ORGATEX.DESCRIPCION%TYPE,
    P_CANT_ORGATEX      IN  CARGA_ORGATEX.CANT_ORGATEX%TYPE,
    P_CANT_REAL_ORGATEX IN  CARGA_ORGATEX.CANT_REAL_ORGATEX%TYPE,
    P_UNIDAD            IN  CARGA_ORGATEX.UNIDAD%TYPE,
    P_FECHA             IN  CARGA_ORGATEX.FECHA%TYPE,
    P_CODIGO_RESULTADO  OUT NUMBER,
    P_MENSAJE_RESULTADO OUT VARCHAR2
  ) IS
  BEGIN
    MERGE INTO CARGA_ORGATEX dst
    USING (
      SELECT
        P_PARTIDA      AS PARTIDA,
        P_LLAMADA      AS LLAMADA,
        P_CONTADOR     AS CONTADOR,
        P_COD_PRODUCTO AS COD_PRODUCTO,
        P_FECHA        AS FECHA
      FROM DUAL
    ) src
    ON (    dst.PARTIDA      = src.PARTIDA
        AND dst.LLAMADA      = src.LLAMADA
        AND dst.CONTADOR     = src.CONTADOR
        AND dst.COD_PRODUCTO = src.COD_PRODUCTO
        AND dst.FECHA        = src.FECHA)
    WHEN MATCHED THEN UPDATE SET
      dst.RECETA_ORGATEX    = P_RECETA_ORGATEX,
      dst.COD_COLOR         = P_COD_COLOR,
      dst.DESC_COLOR        = P_DESC_COLOR,
      dst.MAQUINA           = P_MAQUINA,
      dst.PESO              = P_PESO,
      dst.DESCRIPCION       = P_DESCRIPCION,
      dst.CANT_ORGATEX      = P_CANT_ORGATEX,
      dst.CANT_REAL_ORGATEX = P_CANT_REAL_ORGATEX,
      dst.UNIDAD            = P_UNIDAD
      -- No se tocan RECETA_LOGIX / CANT_LOGIX / CANT_REAL_LOGIX (proceso LOGIX aparte).
    WHEN NOT MATCHED THEN INSERT (
      RECETA_ORGATEX, PARTIDA, COD_COLOR, DESC_COLOR, MAQUINA, PESO,
      LLAMADA, CONTADOR, COD_PRODUCTO, DESCRIPCION,
      CANT_ORGATEX, CANT_REAL_ORGATEX, UNIDAD, FECHA
    ) VALUES (
      P_RECETA_ORGATEX, P_PARTIDA, P_COD_COLOR, P_DESC_COLOR, P_MAQUINA, P_PESO,
      P_LLAMADA, P_CONTADOR, P_COD_PRODUCTO, P_DESCRIPCION,
      P_CANT_ORGATEX, P_CANT_REAL_ORGATEX, P_UNIDAD, P_FECHA
    );

    P_CODIGO_RESULTADO  := 0;
    P_MENSAJE_RESULTADO := 'OK';
  EXCEPTION
    WHEN OTHERS THEN
      P_CODIGO_RESULTADO  := SQLCODE;
      P_MENSAJE_RESULTADO := SUBSTR(SQLERRM, 1, 500);
  END SP_MERGE_FILA;

END PKG_ORGATEX;
/
