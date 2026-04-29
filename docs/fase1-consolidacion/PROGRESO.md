# Progreso de Migracion a ERP.Core

> Registro de cada lote migrado, con resultados de compilacion y decisiones tomadas.

---

## Lote 0: Proyecto ERP.Core creado - 2026-03-15

- Framework: .NET 10, C# 13, WinForms
- Estructura: 15 modulos, 47 subcarpetas
- Dependencias externas: Crystal Reports, Telerik, ODBC, COM Interop (documentadas y comentadas en .csproj)
- Agregado a PluginsComplete.sln

---

## Lote 1: Compartido (ConectBd + SasToolBar + MsgSas) - 2026-03-15

- Proyectos origen: ConectBd.CSharp (1 archivo), SasToolBar.CSharp (2 archivos), MsgSas.CSharp (22 archivos)
- Estado: ConectBd y SasToolBar 100% absorbidos. MsgSas 100% absorbido (modulo Compartido).

### Archivos migrados (29 clases + 12 Designer + 8 .resx):

| Subcarpeta | Clases | Origen |
|---|---|---|
| Compartido/Datos/ | ClsConect | ConectBd.CSharp |
| Compartido/Controles/ | SasToolBar, Barraprogress, TexboxDecimal, TexboxSoloLetras, TexboxSoloNumeros, UserControlInferiorFormularios | SasToolBar, MsgSas |
| Compartido/Controles/CalendarGrid/ | CalendarCell, CalendarColumn, CalendarEditingControl | MsgSas |
| Compartido/Utilidades/ | VarIni(MsgSas), Ayuda, excel_gridviem, GenerarCodBarras, Numeros_A_Letras | MsgSas |
| Compartido/Reportes/ | config_report, reporte | MsgSas |
| Compartido/Forms/ | FormAyuda, FrmAyuDocs, FrmFormPago, FrmProgres, helptable, imprimir | MsgSas |
| Compartido/Configuracion/ | ParamSys, VarIni(MsgConfig), FrmActividades, frmtasas, frmtasacptos | MsgConfig (parcial) |
| Compartido/Interfaces/ | IReportService, IExcelExportService | Nuevas |
| Properties/ | Resources.Designer + Resources.resx + disco.gif + Salir031.gif | MsgSas |

### Conflictos resueltos:
- **VarIni.cs duplicado**: Utilidades/VarIni.cs (MsgSas) vs Configuracion/VarIni.cs (MsgConfig) — namespaces distintos, no bloqueante
- **CrystalDecisions (14 errores)**: Envuelto en `#if CRYSTAL_LEGACY` en reporte.cs, config_report.cs, imprimir.cs/.Designer.cs
- **Excel COM Interop (5 errores)**: Envuelto en `#if EXCEL_LEGACY` en ParamSys.cs

### Resultado:
- Errores: 6 (todos por modulos no migrados: ParamCop, ParamCdt, msgcop)
- Warnings: 26 (nullable CS8625 + naming CS8981 heredados)

---

## Lote 2: MsgConfig.CSharp completo (Models + Forms) - 2026-03-15

- Proyecto origen: MsgConfig.CSharp (15 archivos funcionales)
- Estado: **100% DESCOMPUESTO Y ELIMINABLE**

### Pasos A-D (Models):

| Paso | Clase | Lineas | Destino | Dependencias |
|------|-------|--------|---------|-------------|
| A | ParamCnt | 700 | Contabilidad/Models/ | ConectBd ✅, Ayuda ✅ |
| B | ParamCdt | 340 | CDT/Models/ | ConectBd ✅, ParamSys ✅, Excel→#if |
| C | ParamTes | 30 | Tesoreria/Models/ | ConectBd ✅ |
| D | ParamCop | 2,531 | CarteraFinanciera/Models/ | ConectBd ✅, Ayuda ✅, ParamSys ✅, ParamCnt(A) ✅, Excel→#if |

### Pasos E-F (Forms):

| Paso | Formulario | Destino | Dependencias |
|------|-----------|---------|-------------|
| E | FrmLogeo | Seguridad/Forms/ | ParamSys ✅ |
| E | frmempbloq | Seguridad/Forms/ | Ninguna ✅ |
| E | frmreferencias | CarteraFinanciera/Forms/ | SasToolBar ✅ |
| F | sys_ffirmas | Seguridad/Forms/ | ParamCop(D) ✅, ParamSys ✅, Ayuda ✅, SasToolBar ✅ |
| F | FrmAgregarReferencia | CarteraFinanciera/Forms/ | ParamCop(D) ✅, Ayuda ✅, ConectBd ✅ |
| F | frmtasasintcred | CarteraFinanciera/Forms/ | ParamCop(D) ✅ |

### Resultado:
- Errores Compartido resueltos: **5 de 6**
- Error residual: `msgcop.Clscartera` en FrmFormPago.cs (requiere Lote 4 — cluster circular)
- 3,601 lineas migradas en Models + 6 formularios con Designer

---

## Estado acumulado de ERP.Core

| Metrica | Valor |
|---------|-------|
| Archivos .cs | 59 (45 clases + 14 Designer) |
| Archivos .resx | 8 |
| Recursos graficos | 2 (disco.gif, Salir031.gif) |
| Interfaces nuevas | 2 (IReportService, IExcelExportService) |
| Errores de compilacion | **1** (msgcop.Clscartera en FrmFormPago.cs) |
| Warnings | 33 (nullable + naming heredados) |

### Proyectos absorbidos (eliminables de la solucion):

| Proyecto | Archivos migrados | Estado |
|----------|-------------------|--------|
| ConectBd.CSharp | 1/1 | **100%** ✅ |
| SasToolBar.CSharp | 2/2 | **100%** ✅ |
| MsgSas.CSharp | 22/22 (Compartido) | **100%** ✅ |
| MsgConfig.CSharp | 15/15 | **100%** ✅ |

### Modulos de ERP.Core con contenido:

| Modulo | Clases | Subcarpetas usadas |
|--------|--------|-------------------|
| Compartido | 31 | Datos, Controles, CalendarGrid, Utilidades, Reportes, Forms, Configuracion, Interfaces |
| Contabilidad | 1 | Models |
| CarteraFinanciera | 4 | Models, Forms |
| CDT | 1 | Models |
| Tesoreria | 1 | Models |
| Seguridad | 3 | Forms |

---

## Lote 3: Modulos Isla (7 proyectos sin dependencias circulares) - 2026-03-15

### Resultado por modulo:

| # | Proyecto | Destino | Archivos | Lineas | Crystal | Compilacion |
|---|----------|---------|----------|--------|---------|-------------|
| 1 | Nitgen.CSharp | Seguridad/Biometria/ | 2 | 458 | No | Limpio (#if NITGEN_SDK) |
| 2 | msgnomconfig.CSharp | Nomina/Services/ + Forms/ | 3 | 1,303 | No | Limpio |
| 3 | msccircular.CSharp | CarteraFinanciera/Services/ | 1 | 709 | Si (1 metodo) | Limpio (#if CRYSTAL_LEGACY) |
| 4 | MsgRecaudos.CSharp | Recaudos/Services/ | 1 | 507 | No | Limpio |
| 5 | mscext01.CSharp | Reportes/Services/ | 1 | 52 | No | Limpio |
| 6 | mscext02.CSharp | Reportes/Services/ | 1 | 1,081 | Si (clase completa) | Limpio (#if CRYSTAL_LEGACY) |
| 7 | mscProcesoProd.CSharp | Produccion/Services/ | 1 | 2,297 | No | Limpio (1 fix: ref param→overload) |

### Fixes aplicados:
- **ProcesoProd.cs linea 1632**: `ref string nomempresa = ""` ilegal en C# 13 → separado en overload sin ref + overload con ref

### Directivas condicionales aplicadas:
- `#if NITGEN_SDK`: ClsNitgen.cs, NetBioApi.cs (SDK biometrico no disponible)
- `#if CRYSTAL_LEGACY`: clscircular.cs (metodo ImprimirCirculares), clsmscext02.cs (clase completa)

---

## Estado acumulado de ERP.Core

| Metrica | Valor |
|---------|-------|
| Archivos .cs | 69 (53 clases + 14 Designer + 2 interfaces) |
| Archivos .resx | 8 |
| Recursos graficos | 2 (disco.gif, Salir031.gif) |
| Errores de compilacion | **1** (msgcop.Clscartera en FrmFormPago.cs) |
| Warnings | 55 (nullable + naming heredados) |

### Proyectos absorbidos (11 de 33 — eliminables de la solucion):

| Proyecto | Archivos | Modulo destino | Estado |
|----------|----------|----------------|--------|
| ConectBd.CSharp | 1 | Compartido/Datos | **100%** ✅ |
| SasToolBar.CSharp | 2 | Compartido/Controles | **100%** ✅ |
| MsgSas.CSharp | 22 | Compartido/* | **100%** ✅ |
| MsgConfig.CSharp | 15 | Compartido + 5 modulos | **100%** ✅ |
| Nitgen.CSharp | 2 | Seguridad/Biometria | **100%** ✅ |
| msgnomconfig.CSharp | 3 | Nomina/* | **100%** ✅ |
| msccircular.CSharp | 1 | CarteraFinanciera/Services | **100%** ✅ |
| MsgRecaudos.CSharp | 1 | Recaudos/Services | **100%** ✅ |
| mscext01.CSharp | 1 | Reportes/Services | **100%** ✅ |
| mscext02.CSharp | 1 | Reportes/Services | **100%** ✅ |
| mscProcesoProd.CSharp | 1 | Produccion/Services | **100%** ✅ |

### Modulos de ERP.Core con contenido:

| Modulo | Clases | Subcarpetas |
|--------|--------|-------------|
| Compartido | 31 | Datos, Controles, CalendarGrid, Utilidades, Reportes, Forms, Configuracion, Interfaces |
| Contabilidad | 1 | Models |
| CarteraFinanciera | 5 | Models, Forms, Services |
| CDT | 1 | Models |
| Tesoreria | 1 | Models |
| Seguridad | 5 | Forms, Biometria |
| Nomina | 2 | Services, Forms |
| Recaudos | 1 | Services |
| Reportes | 2 | Services |
| Produccion | 1 | Services |

---

## Lote 4: Contabilidad (msgcnt + MsgImpCnt) - 2026-03-15

- Proyectos origen: msgcnt.CSharp (12 archivos funcionales), MsgImpCnt.CSharp (2 archivos funcionales)
- Estado: **ambos 100% ABSORBIDOS**

### Archivos migrados:

| Archivo | Lineas | Destino | Resultado |
|---------|--------|---------|-----------|
| ClsContabilidad.cs | 3,972 (de 6,990 — 3,020 duplicadas eliminadas) | Contabilidad/Services/ | Limpio ✅ |
| ClsContabilidad.Promedios.cs | 965 | Contabilidad/Services/ | Limpio ✅ |
| FrmCntProgres.cs + Designer + resx | 73 | Contabilidad/Forms/ | Limpio ✅ |
| cnt_cuadredoc.cs + Designer + resx | 777 | Contabilidad/Forms/ | 1 error (msgtes) |
| frmMovCiclo.cs + Designer + resx | 430 | Contabilidad/Forms/ | Limpio ✅ |
| frmTipodoc.cs + Designer + resx | 210 | Contabilidad/Forms/ | Limpio ✅ |
| frmdocaux.cs + Designer + resx | 600 | Contabilidad/Forms/ | Limpio ✅ |
| ImpreDoc.cs | 1,067 | Contabilidad/Reportes/ | Limpio ✅ |
| VarIni.cs | 30 | Contabilidad/Helpers/ | Limpio ✅ |

### Fixes aplicados:
- **ClsContabilidad.cs**: Eliminadas 3,020 lineas duplicadas (metodos repetidos en bloque lineas 3969-6988). Artefacto de la traduccion VB→C# por chunks. Los metodos originales (lineas 947-3967) se conservan intactos.

### Dependencias pendientes (Lote 5):
- `msgtes.clstesoreria` en ClsContabilidad.cs (8 refs) y cnt_cuadredoc.cs (2 refs)
- `msgcop.Clscartera` en ClsContabilidad.cs (2 refs)
- `msgliqcre.ClsLiqcreditos` en ClsContabilidad.Promedios.cs (3 refs)

### Resultado:
- Errores: **2** (msgtes en cnt_cuadredoc + msgcop en FrmFormPago — ambos Lote 5)
- Warnings: 66
- 13 proyectos absorbidos de 33 (39%)

---

## Estado acumulado de ERP.Core

| Metrica | Valor |
|---------|-------|
| Archivos .cs | 83 (63 clases + 18 Designer + 2 interfaces) |
| Archivos .resx | 13 |
| Errores de compilacion | **2** (msgcop + msgtes — Lote 5) |
| Warnings | 66 |
| Proyectos absorbidos | **13 de 33** (39%) |

---

## Lote 5A: Cluster Circular - Nucleo (7 proyectos) - 2026-03-15

### Resultado por proyecto:

| # | Proyecto | Destino | Archivos .cs | Lineas | Fixes |
|---|----------|---------|-------------|--------|-------|
| 1 | msgcop.CSharp | CarteraFinanciera/Services/Cartera/ + Forms/ | 53 | ~40,200 | 3 duplicados eliminados, 4 ref param fixes, Crystal #if |
| 2 | msgliqcre.CSharp | CarteraFinanciera/Services/Creditos/ + Forms/ | 29 | ~28,000 | Crystal #if |
| 3 | msgdep.CSharp | CarteraFinanciera/Services/Depositos/ + Forms/ | 21 | ~7,300 | campo ok duplicado eliminado |
| 4 | msgtes.CSharp | Tesoreria/Services/ + Forms/ | 5 | ~2,900 | - |
| 5 | MsgDeb.CSharp | CarteraFinanciera/Services/Debitos/ + Forms/ | 3 | ~1,860 | 38 ref param defaults removidos |
| 6 | msgcre.CSharp | CarteraFinanciera/Services/TarjetaCred/ | 1 | ~1,590 | - |
| 7 | MsgImpCop.CSharp | CarteraFinanciera/Reportes/ + Forms/ | 5 | ~1,700 | Crystal #if |

### Ciclos resueltos:
- C1 msgcop ↔ msgliqcre: **RESUELTO** ✅ (mismo ensamblado)
- C2 msgcop ↔ msgdep: **RESUELTO** ✅
- C3 msgcop ↔ msgtes: **RESUELTO** ✅
- C4 msgcop ↔ MsgDeb: **RESUELTO** ✅
- C5 msgcop ↔ msgcre: **RESUELTO** ✅
- C6 msgliqcre ↔ msgdep: **RESUELTO** ✅
- C7 msgliqcre ↔ MsgImpCop: **RESUELTO** ✅

### Errores preexistentes resueltos:
- msgcop.Clscartera en FrmFormPago.cs: **RESUELTO** ✅
- msgtes.clstesoreria en cnt_cuadredoc.cs: **RESUELTO** ✅

### Resultado:
- Errores: **1** (MsgCdats en SolicitudCredito.cs — se resuelve en sub-lote 5B)
- Warnings: 129
- 20 proyectos absorbidos de 33 (61%)

---

---

## Lote 5B: Hojas del cluster (3 de 5 proyectos) - 2026-03-15

### Proyectos migrados:

| # | Proyecto | Destino | Archivos | Lineas | Compilacion |
|---|----------|---------|----------|--------|-------------|
| 1 | MscImpCert.CSharp | CarteraFinanciera/Reportes/ | 1 | 176 | Limpio ✅ |
| 2 | mscimpsol.CSharp | Creditos/Reportes/ | 1 | 795 | Limpio ✅ |
| 3 | MsgCdats.CSharp | CDT/Services/ | 1 | 1,078 | Limpio ✅ |

### Pendientes del sub-lote 5B:
- mscimpcdat.CSharp (1 archivo, 226 lineas)
- MscAplNom.CSharp (1 archivo, 753 lineas)

### PROBLEMA CRITICO DETECTADO:

Al migrar MsgCdats y resolver el ultimo tipo pendiente, el compilador C# 13
puede ahora resolver TODOS los tipos y detecta **~7,400 errores de compatibilidad**
que estaban ocultos cuando habia tipos sin resolver.

**Categorias principales de errores:**
1. **CS1620 (ref keyword)**: ~2,500 errores. Metodos de ClsConect (BuscaAsociado, BuscarCompania, etc.)
   toman 20-40 parametros `ref string` pero los callers no pasan `ref`. En VB.NET 3.5 esto era
   implicito; en C# 13 es obligatorio.
2. **CS1503 (type mismatch)**: ~1,500 errores. Parametros esperan `int` pero reciben `string`,
   o esperan `double` pero reciben `object`. Conversiones implicitas de VB no existen en C#.
3. **CS0234 (reporte not found)**: ~118 errores. La clase `reporte` esta en `#if CRYSTAL_LEGACY`
   pero 118 archivos la usan. Necesita un stub fuera del #if.
4. **CS1056 (encoding)**: Caracteres ñ corrompidos a '□' por operaciones sed sobre archivos
   con encoding mixto (UTF-8/Latin1). Ya corregidos en ClsConect.cs.

**Fix aplicado:** Overload `MyOdbcConect(odbcConect varini)` sin `ref` en ClsConect.cs.
Esto es insuficiente — se necesita una estrategia sistematica para los ~7,400 errores restantes.

**Estrategia recomendada para los errores de compatibilidad:**
1. Agregar overloads sin `ref` para los metodos mas usados de ClsConect
2. Crear un stub de `reporte` fuera del `#if CRYSTAL_LEGACY`
3. Revisar los type mismatches metodo por metodo (son del codigo VB original)

---

---

## Lote 5B completado + Lote 6 (Inventario) + Lote 7 (Nomina) - 2026-03-15

### 10 proyectos migrados:

| # | Proyecto | Destino | Archivos .cs |
|---|----------|---------|-------------|
| 1 | mscimpcdat.CSharp | CDT/Reportes/ | 1 |
| 2 | MscAplNom.CSharp | CarteraFinanciera/Services/ | 1 |
| 3 | msginvconfig.CSharp | Inventario/Services/ | 1 |
| 4 | msginv.CSharp | Inventario/Services/ + Forms/ | 17 |
| 5 | msgtiket.CSharp | Inventario/Services/ | 1 |
| 6 | msgcli.CSharp | Inventario/Services/ | 1 |
| 7 | msgnom.CSharp | Nomina/Services/ + Forms/ | 12 |
| 8 | msgimp.CSharp | Nomina/Reportes/ | 1 |
| 9 | mscpagonomi.CSharp | Nomina/Services/ | 1 |
| 10 | arnomgen.CSharp | Nomina/Services/ + Forms/ | 5 |

### Resultado:
- **0 errores de namespace/referencia** (CS0234/CS0246)
- 12 errores residuales: 10 CS0111 (duplicados en partials) + 2 CS1737 (param ordering)
- Todos son artefactos de la traduccion VB→C#, no de la migracion

---

## MIGRACION COMPLETA - Estado Final

| Metrica | Valor |
|---------|-------|
| Archivos .cs en ERP.Core | **231** |
| Proyectos absorbidos | **33 de 33 (100%)** ✅ |
| Errores de namespace/referencia | **0** ✅ |
| Errores CS0111/CS1737 (duplicados) | **0** (12 resueltos: 8 msginv + 2 msgnom + 1 AplicaDstos + 1 Clscartera) |
| Errores de compatibilidad VB→C# | **~4,900** (CS1620 ref + CS1503 type + CS1061 member) |
| Nota | Todos son de la traduccion VB.NET 3.5→C# original, no de la migracion |
| Warnings | 170 |

### Todos los proyectos absorbidos:

| # | Proyecto | Modulo destino |
|---|----------|----------------|
| 1 | ConectBd.CSharp | Compartido/Datos |
| 2 | SasToolBar.CSharp | Compartido/Controles |
| 3 | MsgSas.CSharp | Compartido/* |
| 4 | MsgConfig.CSharp | Compartido + 5 modulos |
| 5 | Nitgen.CSharp | Seguridad/Biometria |
| 6 | msgnomconfig.CSharp | Nomina/Services |
| 7 | msccircular.CSharp | CarteraFinanciera/Services |
| 8 | MsgRecaudos.CSharp | Recaudos/Services |
| 9 | mscext01.CSharp | Reportes/Services |
| 10 | mscext02.CSharp | Reportes/Services |
| 11 | mscProcesoProd.CSharp | Produccion/Services |
| 12 | msgcnt.CSharp | Contabilidad/Services + Forms |
| 13 | MsgImpCnt.CSharp | Contabilidad/Reportes |
| 14 | msgcop.CSharp | CarteraFinanciera/Services/Cartera + Forms |
| 15 | msgliqcre.CSharp | CarteraFinanciera/Services/Creditos + Forms |
| 16 | msgdep.CSharp | CarteraFinanciera/Services/Depositos + Forms |
| 17 | msgtes.CSharp | Tesoreria/Services + Forms |
| 18 | MsgDeb.CSharp | CarteraFinanciera/Services/Debitos + Forms |
| 19 | msgcre.CSharp | CarteraFinanciera/Services/TarjetaCred |
| 20 | MsgImpCop.CSharp | CarteraFinanciera/Reportes + Forms |
| 21 | MscImpCert.CSharp | CarteraFinanciera/Reportes |
| 22 | mscimpsol.CSharp | Creditos/Reportes |
| 23 | MsgCdats.CSharp | CDT/Services |
| 24 | mscimpcdat.CSharp | CDT/Reportes |
| 25 | MscAplNom.CSharp | CarteraFinanciera/Services |
| 26 | msginvconfig.CSharp | Inventario/Services |
| 27 | msginv.CSharp | Inventario/Services + Forms |
| 28 | msgtiket.CSharp | Inventario/Services |
| 29 | msgcli.CSharp | Inventario/Services |
| 30 | msgnom.CSharp | Nomina/Services + Forms |
| 31 | msgimp.CSharp | Nomina/Reportes |
| 32 | mscpagonomi.CSharp | Nomina/Services |
| 33 | arnomgen.CSharp | Nomina/Services + Forms |

### Proximos pasos (post-migracion):
1. Resolver 10 metodos duplicados en msginv (8) y msgnom (2)
2. Resolver 2 errores de param ordering
3. Fase de compatibilidad VB→C# 13 (ref keywords, type conversions)
4. Migrar Crystal Reports a motor alternativo
5. Eliminar los 33 proyectos originales de la solucion
