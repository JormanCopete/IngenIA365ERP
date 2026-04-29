# Mapa de Migracion: Proyectos Originales → ERP.Core

> Actualizado: 2026-03-15
> Referencia rapida: donde fue a parar cada archivo de cada proyecto original.

---

## Leyenda de estados

- ✅ Migrado y compilando
- ⚠️ Migrado con directiva condicional (#if)
- ❌ Pendiente de migracion

---

## 1. ConectBd.CSharp → ✅ 100% ABSORBIDO

| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| ClsConect.cs | Compartido/Datos/ClsConect.cs | ERP.Core.Compartido.Datos |

---

## 2. SasToolBar.CSharp → ✅ 100% ABSORBIDO

| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| SasToolBar.cs | Compartido/Controles/SasToolBar.cs | ERP.Core.Compartido.Controles |
| SasToolBar.Designer.cs | Compartido/Controles/SasToolBar.Designer.cs | ERP.Core.Compartido.Controles |

---

## 3. MsgSas.CSharp → ✅ 100% ABSORBIDO

| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| VarIni.cs | Compartido/Utilidades/VarIni.cs | ERP.Core.Compartido.Utilidades |
| Ayuda.cs | Compartido/Utilidades/Ayuda.cs | ERP.Core.Compartido.Utilidades |
| excel_gridviem.cs | Compartido/Utilidades/excel_gridviem.cs | ERP.Core.Compartido.Utilidades |
| GenerarCodBarras.cs | Compartido/Utilidades/GenerarCodBarras.cs | ERP.Core.Compartido.Utilidades |
| Numeros_A_Letras.cs | Compartido/Utilidades/Numeros_A_Letras.cs | ERP.Core.Compartido.Utilidades |
| Barraprogress.cs | Compartido/Controles/Barraprogress.cs | ERP.Core.Compartido.Controles |
| controlesModificados/TexboxDecimal.cs | Compartido/Controles/TexboxDecimal.cs | ERP.Core.Compartido.Controles |
| controlesModificados/TexboxSoloLetras.cs | Compartido/Controles/TexboxSoloLetras.cs | ERP.Core.Compartido.Controles |
| controlesModificados/TexboxSoloNumeros.cs | Compartido/Controles/TexboxSoloNumeros.cs | ERP.Core.Compartido.Controles |
| userControl/UserControlInferiorFormularios.cs | Compartido/Controles/UserControlInferiorFormularios.cs | ERP.Core.Compartido.Controles |
| calendargrid/CalendarCell.cs | Compartido/Controles/CalendarGrid/CalendarCell.cs | ERP.Core.Compartido.Controles |
| calendargrid/CalendarColumn.cs | Compartido/Controles/CalendarGrid/CalendarColumn.cs | ERP.Core.Compartido.Controles |
| calendargrid/CalendarEditingControl.cs | Compartido/Controles/CalendarGrid/CalendarEditingControl.cs | ERP.Core.Compartido.Controles |
| config_report.cs | Compartido/Reportes/config_report.cs ⚠️ | ERP.Core.Compartido.Reportes |
| reporte.cs | Compartido/Reportes/reporte.cs ⚠️ | ERP.Core.Compartido.Reportes |
| Forms/FormAyuda.cs | Compartido/Forms/FormAyuda.cs | ERP.Core.Compartido.Forms |
| Forms/FrmAyuDocs.cs | Compartido/Forms/FrmAyuDocs.cs | ERP.Core.Compartido.Forms |
| Forms/FrmFormPago.cs | Compartido/Forms/FrmFormPago.cs | ERP.Core.Compartido.Forms |
| Forms/FrmProgres.cs | Compartido/Forms/FrmProgres.cs | ERP.Core.Compartido.Forms |
| Forms/helptable.cs | Compartido/Forms/helptable.cs | ERP.Core.Compartido.Forms |
| Forms/imprimir.cs | Compartido/Forms/imprimir.cs ⚠️ | ERP.Core.Compartido.Forms |
| Properties/Resources.Designer.cs | Properties/Resources.Designer.cs | ERP.Core.Properties |

> ⚠️ config_report.cs, reporte.cs, imprimir.cs: codigo Crystal Reports envuelto en `#if CRYSTAL_LEGACY`

---

## 4. MsgConfig.CSharp → ✅ 100% ABSORBIDO (distribuido en 6 modulos)

| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| ParamSys.cs | Compartido/Configuracion/ParamSys.cs ⚠️ | ERP.Core.Compartido.Configuracion |
| VarIni.cs | Compartido/Configuracion/VarIni.cs | ERP.Core.Compartido.Configuracion |
| forma/FrmActividades.cs | Compartido/Configuracion/FrmActividades.cs | ERP.Core.Compartido.Configuracion |
| forma/frmtasas.cs | Compartido/Configuracion/frmtasas.cs | ERP.Core.Compartido.Configuracion |
| forma/frmtasacptos.cs | Compartido/Configuracion/frmtasacptos.cs | ERP.Core.Compartido.Configuracion |
| ParamCnt.cs | Contabilidad/Models/ParamCnt.cs | ERP.Core.Contabilidad.Models |
| ParamCop.cs | CarteraFinanciera/Models/ParamCop.cs ⚠️ | ERP.Core.CarteraFinanciera.Models |
| ParamCdt.cs | CDT/Models/ParamCdt.cs ⚠️ | ERP.Core.CDT.Models |
| ParamTes.cs | Tesoreria/Models/ParamTes.cs | ERP.Core.Tesoreria.Models |
| forma/FrmLogeo.cs | Seguridad/Forms/FrmLogeo.cs | ERP.Core.Seguridad.Forms |
| forma/frmempbloq.cs | Seguridad/Forms/frmempbloq.cs | ERP.Core.Seguridad.Forms |
| forma/sys_ffirmas.cs | Seguridad/Forms/sys_ffirmas.cs | ERP.Core.Seguridad.Forms |
| forma/frmreferencias.cs | CarteraFinanciera/Forms/frmreferencias.cs | ERP.Core.CarteraFinanciera.Forms |
| forma/FrmAgregarReferencia.cs | CarteraFinanciera/Forms/FrmAgregarReferencia.cs | ERP.Core.CarteraFinanciera.Forms |
| forma/frmtasasintcred.cs | CarteraFinanciera/Forms/frmtasasintcred.cs | ERP.Core.CarteraFinanciera.Forms |

> ⚠️ ParamSys.cs, ParamCop.cs, ParamCdt.cs: campo `Excel.Application` envuelto en `#if EXCEL_LEGACY`

---

## 5. Nitgen.CSharp → ✅ 100% ABSORBIDO

| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| ClsNitgen.cs | Seguridad/Biometria/ClsNitgen.cs ⚠️ | ERP.Core.Seguridad.Biometria |
| NetBioApi.cs | Seguridad/Biometria/NetBioApi.cs ⚠️ | ERP.Core.Seguridad.Biometria |

> ⚠️ Ambos archivos envueltos en `#if NITGEN_SDK` (DLL biometrica no disponible en .NET 10)

---

## 6. msgnomconfig.CSharp → ✅ 100% ABSORBIDO

| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| msgnomconfig.cs | Nomina/Services/msgnomconfig.cs | ERP.Core.Nomina.Services |
| forms/frmfiltros.cs | Nomina/Forms/frmfiltros.cs | ERP.Core.Nomina.Forms |
| forms/frmfiltros.Designer.cs | Nomina/Forms/frmfiltros.Designer.cs | ERP.Core.Nomina.Forms |

---

## 7. msccircular.CSharp → ✅ 100% ABSORBIDO

| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| clscircular.cs | CarteraFinanciera/Services/clscircular.cs ⚠️ | ERP.Core.CarteraFinanciera.Services |

> ⚠️ Metodo ImprimirCirculares envuelto en `#if CRYSTAL_LEGACY`

---

## 8. MsgRecaudos.CSharp → ✅ 100% ABSORBIDO

| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| msgrecaudos.cs | Recaudos/Services/msgrecaudos.cs | ERP.Core.Recaudos.Services |

---

## 9. mscext01.CSharp → ✅ 100% ABSORBIDO

| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| ClsMscext01.cs | Reportes/Services/ClsMscext01.cs | ERP.Core.Reportes.Services |

---

## 10. mscext02.CSharp → ✅ 100% ABSORBIDO

| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| clsmscext02.cs | Reportes/Services/clsmscext02.cs ⚠️ | ERP.Core.Reportes.Services |

> ⚠️ Clase completa envuelta en `#if CRYSTAL_LEGACY`

---

## 11. mscProcesoProd.CSharp → ✅ 100% ABSORBIDO

| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| ProcesoProd.cs | Produccion/Services/ProcesoProd.cs | ERP.Core.Produccion.Services |

> Fix: `ref string param = ""` → separado en 2 overloads (C# 13 no permite default en ref)

---

## Archivos nuevos (no provienen de proyectos originales)

| Archivo | Ubicacion | Proposito |
|---|---|---|
| IReportService.cs | Compartido/Interfaces/ | Interfaz para reemplazar Crystal Reports |
| IExcelExportService.cs | Compartido/Interfaces/ | Interfaz para reemplazar Excel COM Interop |

---

## 12. msgcnt.CSharp → ✅ 100% ABSORBIDO

| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| ClsContabilidad.cs | Contabilidad/Services/ClsContabilidad.cs | ERP.Core.Contabilidad.Services |
| ClsContabilidad.Promedios.cs | Contabilidad/Services/ClsContabilidad.Promedios.cs | ERP.Core.Contabilidad.Services |
| Forms/FrmCntProgres.cs + Designer + resx | Contabilidad/Forms/ | ERP.Core.Contabilidad.Forms |
| Forms/cnt_cuadredoc.cs + Designer + resx | Contabilidad/Forms/ | ERP.Core.Contabilidad.Forms |
| Forms/frmMovCiclo.cs + Designer + resx | Contabilidad/Forms/ | ERP.Core.Contabilidad.Forms |
| Forms/frmTipodoc.cs + Designer + resx | Contabilidad/Forms/ | ERP.Core.Contabilidad.Forms |
| Forms/frmdocaux.cs + Designer + resx | Contabilidad/Forms/ | ERP.Core.Contabilidad.Forms |

> Fix: 3,020 lineas duplicadas eliminadas de ClsContabilidad.cs (metodos repetidos en bloque, artefacto de traduccion VB→C# por chunks). De 6,990 → 3,972 lineas.

---

## 13. MsgImpCnt.CSharp → ✅ 100% ABSORBIDO

| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| ImpreDoc.cs | Contabilidad/Reportes/ImpreDoc.cs | ERP.Core.Contabilidad.Reportes |
| VarIni.cs | Contabilidad/Helpers/VarIni.cs | ERP.Core.Contabilidad.Helpers |

---

## 14. msgcop.CSharp → ✅ 100% ABSORBIDO

| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| Clscartera.cs (13 partials) | CarteraFinanciera/Services/Cartera/ | ERP.Core.CarteraFinanciera.Services.Cartera |
| 20 Forms + Designer + resx | CarteraFinanciera/Forms/ | ERP.Core.CarteraFinanciera.Forms |

> Fixes: constructor duplicado, ActuaEstaAntici duplicado, NivelIngreso duplicado eliminados. 4 ref param defaults corregidos. Crystal #if en Part6b, Part7b, Part8b, Part9b.

## 15. msgliqcre.CSharp → ✅ 100% ABSORBIDO

| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| ClsLiqcreditos.cs (4 partials) | CarteraFinanciera/Services/Creditos/ | ERP.Core.CarteraFinanciera.Services.Creditos |
| SolicitudCredito + 8 Forms | CarteraFinanciera/Forms/ | ERP.Core.CarteraFinanciera.Forms |

## 16. msgdep.CSharp → ✅ 100% ABSORBIDO

| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| ClsDepositos.cs, Var.cs | CarteraFinanciera/Services/Depositos/ | ERP.Core.CarteraFinanciera.Services.Depositos |
| 6 Forms + Designer | CarteraFinanciera/Forms/ | ERP.Core.CarteraFinanciera.Forms |

## 17. msgtes.CSharp → ✅ 100% ABSORBIDO

| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| clstesoreria.cs, VarIni.cs | Tesoreria/Services/ | ERP.Core.Tesoreria.Services |
| FrmPagoFact + Designer | Tesoreria/Forms/ | ERP.Core.Tesoreria.Forms |

## 18. MsgDeb.CSharp → ✅ 100% ABSORBIDO

| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| ClsMsgDeb.cs | CarteraFinanciera/Services/Debitos/ | ERP.Core.CarteraFinanciera.Services.Debitos |
| FrmCupoTarj + Designer | CarteraFinanciera/Forms/ | ERP.Core.CarteraFinanciera.Forms |

> Fix: 38 ref param defaults eliminados (C# 13 incompatible con ref param = default)

## 19. msgcre.CSharp → ✅ 100% ABSORBIDO

| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| Clstarjcredito.cs | CarteraFinanciera/Services/TarjetaCred/ | ERP.Core.CarteraFinanciera.Services.TarjetaCred |

## 20. MsgImpCop.CSharp → ✅ 100% ABSORBIDO

| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| ImpreDoc.cs, VarIni.cs | CarteraFinanciera/Reportes/ | ERP.Core.CarteraFinanciera.Reportes |
| Ordencomercio + Designer | CarteraFinanciera/Forms/ | ERP.Core.CarteraFinanciera.Forms |

---

## 21. MscImpCert.CSharp → ✅ 100% ABSORBIDO

| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| ClsImpCert.cs | CarteraFinanciera/Reportes/ClsImpCert.cs | ERP.Core.CarteraFinanciera.Reportes |

## 22. mscimpsol.CSharp → ✅ 100% ABSORBIDO

| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| clsimpsol.cs | Creditos/Reportes/clsimpsol.cs | ERP.Core.Creditos.Reportes |

## 23. MsgCdats.CSharp → ✅ 100% ABSORBIDO

| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| ClsMsgCdats.cs | CDT/Services/ClsMsgCdats.cs | ERP.Core.CDT.Services |

---

## 24. mscimpcdat.CSharp → ✅ 100% ABSORBIDO
| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| clsimpcdats.cs | CDT/Reportes/clsimpcdats.cs | ERP.Core.CDT.Reportes |

## 25. MscAplNom.CSharp → ✅ 100% ABSORBIDO
| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| AplicaDstos.cs | CarteraFinanciera/Services/AplicaDstos.cs | ERP.Core.CarteraFinanciera.Services |

## 26. msginvconfig.CSharp → ✅ 100% ABSORBIDO
| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| ClsInvConfig.cs | Inventario/Services/ClsInvConfig.cs | ERP.Core.Inventario.Services |

## 27. msginv.CSharp → ✅ 100% ABSORBIDO
| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| msginv.cs + Part2-5 | Inventario/Services/ | ERP.Core.Inventario.Services |
| 6 Forms + Designer + resx | Inventario/Forms/ | ERP.Core.Inventario.Forms |

## 28. msgtiket.CSharp → ✅ 100% ABSORBIDO
| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| clsmsgtiket.cs | Inventario/Services/clsmsgtiket.cs | ERP.Core.Inventario.Services |

## 29. msgcli.CSharp → ✅ 100% ABSORBIDO
| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| Clsmsgcli.cs | Inventario/Services/Clsmsgcli.cs | ERP.Core.Inventario.Services |

## 30. msgnom.CSharp → ✅ 100% ABSORBIDO
| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| msgnom.cs + Part2-8 | Nomina/Services/ | ERP.Core.Nomina.Services |
| 2 Forms + Designer + resx | Nomina/Forms/ | ERP.Core.Nomina.Forms |

## 31. msgimp.CSharp → ✅ 100% ABSORBIDO
| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| clsmsgimp.cs | Nomina/Reportes/clsmsgimp.cs | ERP.Core.Nomina.Reportes |

## 32. mscpagonomi.CSharp → ✅ 100% ABSORBIDO
| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| Clspagonomi.cs | Nomina/Services/Clspagonomi.cs | ERP.Core.Nomina.Services |

## 33. arnomgen.CSharp → ✅ 100% ABSORBIDO
| Archivo origen | Destino ERP.Core | Namespace nuevo |
|---|---|---|
| inicio.cs | Nomina/Services/inicio.cs | ERP.Core.Nomina.Services |
| Form1.cs + Designer | Nomina/Forms/ | ERP.Core.Nomina.Forms |
| num_solicitud.cs + Designer | Nomina/Forms/ | ERP.Core.Nomina.Forms |

---

## Directivas condicionales de compilacion

| Directiva | Proposito | Archivos afectados |
|---|---|---|
| `CRYSTAL_LEGACY` | Codigo que depende de CrystalDecisions (incompatible .NET 10) | reporte.cs, config_report.cs, imprimir.cs, imprimir.Designer.cs, clscircular.cs, clsmscext02.cs, Clscartera.Part6b.cs, Clscartera.Part7b.cs, Clscartera.Part8b.cs, Clscartera.Part9b.cs, MsgImpCop/ImpreDoc.cs |
| `EXCEL_LEGACY` | Codigo que depende de Excel COM Interop | ParamSys.cs, ParamCop.cs, ParamCdt.cs |
| `NITGEN_SDK` | Codigo que depende de NITGEN.SDK.NBioBSP | ClsNitgen.cs, NetBioApi.cs |
| `OFFICE_INTEROP` | Codigo que depende de Microsoft.Office.Interop | inicio.cs (arnomgen) |

---

## Resumen visual de progreso

```
Proyectos totales:  33
Absorbidos:         33  █████████████████████████████████  100% ✅
Pendientes:          0

Archivos en ERP.Core: 231 .cs
Errores namespace:    0 ✅
Errores VB compat:    12 (10 CS0111 duplicados + 2 CS1737 param order)
Warnings:             170
```
