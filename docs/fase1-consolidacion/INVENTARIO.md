# Inventario de Proyectos C# - PluginsComplete.sln

> Generado: 2026-03-15
> Total proyectos .CSharp: 32 + 1 adicional (Nitgen.CSharp)

---

## 1. ConectBd.CSharp

| Archivo | Descripcion |
|---------|-------------|
| ClsConect.cs | Clase de conexion ODBC con struct odbcConect y metodos de ejecucion de queries |

**ProjectReference:** Ninguna
**PackageReference:** Ninguno

---

## 2. MscAplNom.CSharp

| Archivo | Descripcion |
|---------|-------------|
| AplicaDstos.cs | Procesa y aplica descuentos a deducciones de nomina con logica por tipo de descuento |

**ProjectReference:** ConectBd.CSharp, MsgConfig.CSharp, msgcop.CSharp, MsgSas.CSharp
**PackageReference:** Ninguno

---

## 3. MscImpCert.CSharp

| Archivo | Descripcion |
|---------|-------------|
| ClsImpCert.cs | Impresion/generacion de certificados por linea de credito y numeracion de documentos |

**ProjectReference:** ConectBd.CSharp, MsgConfig.CSharp, msgcop.CSharp, MsgSas.CSharp
**PackageReference:** CrystalDecisions.CrystalReports.Engine (10.5.3700.0), CrystalDecisions.Windows.Forms (10.5.3700.0)

---

## 4. MsgCdats.CSharp

| Archivo | Descripcion |
|---------|-------------|
| ClsMsgCdats.cs | Gestion de CDTs (Certificados de Deposito a Termino) con enums de pago, interes y liquidacion |

**ProjectReference:** ConectBd.CSharp
**PackageReference:** CrystalDecisions.CrystalReports.Engine (10.5.3700.0), CrystalDecisions.Windows.Forms (10.5.3700.0)

---

## 5. MsgConfig.CSharp

| Archivo | Descripcion |
|---------|-------------|
| ParamCdt.cs | Configuracion de parametros CDT con enum Navega para navegacion de registros |
| ParamCnt.cs | Configuracion de parametros de contabilidad con enums de acciones y navegacion |
| ParamCop.cs | Configuracion de parametros de cooperativa con enums de acciones y navegacion |
| ParamSys.cs | Configuracion de parametros del sistema con enums, envio de email, I/O de archivos |
| ParamTes.cs | Configuracion minima de parametros de tesoreria |
| VarIni.cs | Clase estatica con variables globales de inicializacion del sistema y conexion BD |
| FrmLogeo.cs | Formulario de login/autenticacion con tipos de validacion para sobregiros, transacciones, CDTs |
| FrmActividades.cs | Formulario para gestion de actividades culturales, deportivas, cursos y recreativas |
| frmtasas.cs | Formulario para gestion de tasas de cambio/interes |
| frmempbloq.cs | Formulario para gestion de empresas bloqueadas |
| FrmAgregarReferencia.cs | Formulario para agregar referencias financieras |
| frmreferencias.cs | Formulario para gestion de referencias |
| frmtasacptos.cs | Formulario para gestion de tasas por conceptos |
| frmtasasintcred.cs | Formulario para gestion de tasas de interes de creditos |
| sys_ffirmas.cs | Formulario para gestion de firmas/aprobaciones del sistema |

**ProjectReference:** ConectBd.CSharp, SasToolBar.CSharp
**PackageReference:** Interop.Excel, Interop.Outlook

---

## 6. MsgDeb.CSharp

| Archivo | Descripcion |
|---------|-------------|
| ClsMsgDeb.cs | Gestion de tarjetas debito con enums de tipos de movimiento y navegacion |
| FrmCupoTarj.cs | Formulario para gestion de cupos/limites de tarjeta debito |

**ProjectReference:** ConectBd.CSharp, MsgConfig.CSharp, msgdep.CSharp
**PackageReference:** Ninguno

---

## 7. MsgImpCnt.CSharp

| Archivo | Descripcion |
|---------|-------------|
| ImpreDoc.cs | Impresion de documentos contables: recibos, comprobantes y reportes de cheques |
| VarIni.cs | Variables globales de inicializacion para impresion de documentos contables |

**ProjectReference:** ConectBd.CSharp, msgcnt.CSharp, MsgConfig.CSharp, MsgSas.CSharp
**PackageReference:** CrystalDecisions.CrystalReports.Engine (10.5.3700.0), CrystalDecisions.ReportSource (10.5.3700.0), CrystalDecisions.Shared (10.5.3700.0), CrystalDecisions.Windows.Forms (10.5.3700.0)

---

## 8. MsgImpCop.CSharp

| Archivo | Descripcion |
|---------|-------------|
| ImpreDoc.cs | Impresion de documentos de cooperativa con helpers de consulta de empresa |
| VarIni.cs | Variables globales y metodo GrabaFormapago para persistencia de formas de pago |
| Ordencomercio.cs | Formulario de ordenes de compra con busqueda de proveedor (NIT) |

**ProjectReference:** ConectBd.CSharp, MsgConfig.CSharp, msgliqcre.CSharp, MsgSas.CSharp
**PackageReference:** CrystalDecisions.CrystalReports.Engine (10.5.3700.0), CrystalDecisions.Shared (10.5.3700.0), CrystalDecisions.Windows.Forms (10.5.3700.0)

---

## 9. MsgRecaudos.CSharp

| Archivo | Descripcion |
|---------|-------------|
| msgrecaudos.cs | CRUD y navegacion de conceptos de tipo recaudo/facturacion via ODBC |
| Properties/AssemblyInfo.cs | Metadata del ensamblado |

**ProjectReference:** ConectBd.CSharp, MsgConfig.CSharp, MsgSas.CSharp
**PackageReference:** CrystalDecisions.CrystalReports.Engine (10.5.3700.0)

---

## 10. MsgSas.CSharp

| Archivo | Descripcion |
|---------|-------------|
| VarIni.cs | Utilidades estaticas: GrabaFormapago, buscaFormapago, wrappers de queries ODBC |
| Ayuda.cs | Dialogo de ayuda/busqueda con CargaAyuda (4 overloads), UnloadHelp, ConfiguraForma |
| Barraprogress.cs | Formulario de barra de progreso que hereda de FrmProgres |
| calendargrid/CalendarCell.cs | Celda de DataGridView con soporte de editor DateTime |
| calendargrid/CalendarColumn.cs | Columna de DataGridView para celdas de calendario/datepicker |
| calendargrid/CalendarEditingControl.cs | Control de edicion DateTimePicker (IDataGridViewEditingControl) |
| config_report.cs | Configuracion de reportes Crystal Reports con visor |
| controlesModificados/TexboxDecimal.cs | TextBox personalizado que solo acepta numeros y punto decimal |
| controlesModificados/TexboxSoloLetras.cs | TextBox personalizado que solo acepta caracteres alfabeticos |
| controlesModificados/TexboxSoloNumeros.cs | TextBox personalizado que solo acepta numeros |
| excel_gridviem.cs | Exportador de DataGridView a Excel via COM interop |
| GenerarCodBarras.cs | Generador de codigos de barras Code128 con digito de verificacion |
| Numeros_A_Letras.cs | Convertidor de numeros a texto en espanol (748 lineas) |
| reporte.cs | Wrapper de ReportDocument para cargar archivos .rpt desde ruta de red |
| userControl/UserControlInferiorFormularios.cs | Control de usuario footer para formularios |
| Forms/FormAyuda.cs | Formulario de ayuda/busqueda con tabla y columnas configurables |
| Forms/FrmAyuDocs.cs | Formulario de ayuda/busqueda de documentos |
| Forms/FrmFormPago.cs | Formulario de formas de pago: efectivo, cheques, tarjetas, titulos |
| Forms/FrmProgres.cs | Formulario base de barra de progreso |
| Forms/helptable.cs | Formulario de tabla de busqueda con columnas configurables |
| Forms/imprimir.cs | Formulario visor de Crystal Reports |
| Properties/AssemblyInfo.cs | Metadata del ensamblado |
| Properties/Resources.Designer.cs | Recursos embebidos auto-generados (disco.gif, Salir031.gif) |

**ProjectReference:** Ninguna (usa HintPath a DLLs VB de ConectBd, MsgConfig, msgcop)
**PackageReference:** CrystalDecisions.CrystalReports.Engine (10.5.3700.0), CrystalDecisions.Enterprise.Framework (10.5.3700.0), CrystalDecisions.Enterprise.InfoStore (10.5.3700.0), CrystalDecisions.ReportSource (10.5.3700.0), CrystalDecisions.Shared (10.5.3700.0), CrystalDecisions.Windows.Forms (10.5.3700.0), Microsoft.VisualBasic, Telerik.WinControls (2011.1.11.419), Telerik.WinControls.GridView (2011.1.11.419), Telerik.WinControls.UI (2011.1.11.419), Telerik.WinControls.UI.Design (2011.1.11.419)

---

## 11. Nitgen.CSharp

| Archivo | Descripcion |
|---------|-------------|
| ClsNitgen.cs | Driver de lector de huellas digitales Nitgen con metodos de enrolamiento y verificacion |
| NetBioApi.cs | Constantes/enums para NBioAPI: tipos de dispositivo, codigos de error, opciones |
| Properties/AssemblyInfo.cs | Metadata del ensamblado |

**ProjectReference:** ConectBd.CSharp
**PackageReference:** NITGEN.SDK.NBioBSP (1.1.2.0)

---

## 12. SasToolBar.CSharp

| Archivo | Descripcion |
|---------|-------------|
| SasToolBar.cs | UserControl toolbar personalizado con 8 botones de navegacion y evento ClickEvent |
| SasToolBar.Designer.cs | Codigo designer auto-generado del toolbar |
| Properties/AssemblyInfo.cs | Metadata del ensamblado |

**ProjectReference:** ConectBd.CSharp
**PackageReference:** Ninguno

---

## 13. arnomgen.CSharp

| Archivo | Descripcion |
|---------|-------------|
| inicio.cs | Clase principal para generacion/carga de numeros de nomina con integracion ODBC/Excel |
| Form1.cs | Formulario de inicio vacio (placeholder) |
| Form1.Designer.cs | Designer auto-generado de Form1 |
| num_solicitud.cs | Dialogo simple con boton de cierre |
| num_solicitud.Designer.cs | Designer auto-generado del dialogo |
| Properties/AssemblyInfo.cs | Metadata del ensamblado |

**ProjectReference:** MsgConfig.CSharp, msgcop.CSharp
**PackageReference:** Interop.Excel (1.5.0.0), Interop.Microsoft.Office.Core (2.3.0.0)

---

## 14. mscProcesoProd.CSharp

| Archivo | Descripcion |
|---------|-------------|
| ProcesoProd.cs | Manejador de procesos de produccion con conexion ODBC y dialogos de ayuda MsgSas |
| Properties/AssemblyInfo.cs | Metadata del ensamblado |

**ProjectReference:** ConectBd.CSharp, MsgSas.CSharp
**PackageReference:** Microsoft.VisualBasic

---

## 15. msccircular.CSharp

| Archivo | Descripcion |
|---------|-------------|
| clscircular.cs | Genera circulares de cobro con filtros por empresa/agencia/centro de costo |
| Properties/AssemblyInfo.cs | Metadata del ensamblado |

**ProjectReference:** ConectBd.CSharp, MsgConfig.CSharp, MsgSas.CSharp
**PackageReference:** CrystalDecisions.CrystalReports.Engine (10.5.3700.0), CrystalDecisions.Windows.Forms (10.5.3700.0), Microsoft.VisualBasic

---

## 16. mscext01.CSharp

| Archivo | Descripcion |
|---------|-------------|
| ClsMscext01.cs | Impresion de extractos de cuenta via Crystal Reports con parametro de usuario opcional |
| Properties/AssemblyInfo.cs | Metadata del ensamblado |

**ProjectReference:** Ninguna
**PackageReference:** CrystalDecisions.CrystalReports.Engine (10.5.3700.0), Microsoft.VisualBasic

---

## 17. mscext02.CSharp

| Archivo | Descripcion |
|---------|-------------|
| clsmscext02.cs | Motor de distribucion de reportes por email/PDF/impresion con generacion de extractos |
| Properties/AssemblyInfo.cs | Metadata del ensamblado |

**ProjectReference:** ConectBd.CSharp, MsgConfig.CSharp, MsgSas.CSharp
**PackageReference:** CrystalDecisions.CrystalReports.Engine (10.5.3700.0), CrystalDecisions.Shared (10.5.3700.0), Microsoft.VisualBasic

---

## 18. mscimpcdat.CSharp

| Archivo | Descripcion |
|---------|-------------|
| clsimpcdats.cs | Impresion Crystal Reports de CDTs con busqueda de obligacion y conversion numeros a letras |
| Properties/AssemblyInfo.cs | Metadata del ensamblado |

**ProjectReference:** MsgCdats.CSharp, MsgConfig.CSharp, MsgSas.CSharp
**PackageReference:** CrystalDecisions.CrystalReports.Engine (10.5.3700.0), Microsoft.VisualBasic

---

## 19. mscimpsol.CSharp

| Archivo | Descripcion |
|---------|-------------|
| clsimpsol.cs | Impresion Crystal Reports de formularios de solicitud de credito con planes de pago |
| Properties/AssemblyInfo.cs | Metadata del ensamblado |

**ProjectReference:** ConectBd.CSharp, MsgConfig.CSharp
**PackageReference:** CrystalDecisions.CrystalReports.Engine (10.5.3700.0), CrystalDecisions.Windows.Forms (10.5.3700.0), Microsoft.VisualBasic

---

## 20. mscpagonomi.CSharp

| Archivo | Descripcion |
|---------|-------------|
| Clspagonomi.cs | Procesador de archivos planos de nomina que parsea registros y contabiliza movimientos |
| Properties/AssemblyInfo.cs | Metadata del ensamblado |

**ProjectReference:** MsgConfig.CSharp, msgcop.CSharp, msgdep.CSharp, MsgSas.CSharp
**PackageReference:** Microsoft.VisualBasic

---

## 21. msgcli.CSharp

| Archivo | Descripcion |
|---------|-------------|
| Clsmsgcli.cs | Modulo de clientes con 5 clases: clsmsgbase (ODBC), Clsmsgcli (consultas), Clsmsgconfig (config), ClsMsgProcesos (ventas/facturacion), clsimpresion (Crystal Reports) |
| Properties/AssemblyInfo.cs | Metadata del ensamblado |

**ProjectReference:** ConectBd.CSharp, msgcnt.CSharp, MsgConfig.CSharp, msginv.CSharp, msginvconfig.CSharp, MsgSas.CSharp
**PackageReference:** CrystalDecisions.CrystalReports.Engine (10.5.3700.0), CrystalDecisions.Shared (10.5.3700.0), CrystalDecisions.Windows.Forms (10.5.3700.0), Microsoft.VisualBasic

---

## 22. msgcnt.CSharp

| Archivo | Descripcion |
|---------|-------------|
| ClsContabilidad.cs | Clase contable principal (partial 1/2) con 60+ metodos: asientos, mayor, balance, conciliacion |
| ClsContabilidad.Promedios.cs | Calculos de promedios contables (partial 2/2) para analisis de cartera |
| Forms/FrmCntProgres.cs | Formulario de progreso para operaciones contables de larga duracion |
| Forms/FrmCntProgres.Designer.cs | Designer auto-generado del formulario de progreso |
| Forms/cnt_cuadredoc.cs | Formulario de cuadre/conciliacion de documentos |
| Forms/cnt_cuadredoc.Designer.cs | Designer auto-generado del cuadre |
| Forms/frmMovCiclo.cs | Formulario de movimientos por ciclo/periodo |
| Forms/frmMovCiclo.Designer.cs | Designer auto-generado del ciclo |
| Forms/frmTipodoc.cs | Formulario de configuracion de tipos de documento |
| Forms/frmTipodoc.Designer.cs | Designer auto-generado de tipos doc |
| Forms/frmdocaux.cs | Formulario de documentos auxiliares |
| Forms/frmdocaux.Designer.cs | Designer auto-generado de documentos auxiliares |
| Properties/AssemblyInfo.cs | Metadata del ensamblado |

**ProjectReference:** ConectBd.CSharp, MsgConfig.CSharp, msgliqcre.CSharp, MsgSas.CSharp
**PackageReference:** CrystalDecisions.CrystalReports.Engine (10.5.3700.0), CrystalDecisions.Shared (10.5.3700.0), CrystalDecisions.Windows.Forms (10.5.3700.0), Microsoft.VisualBasic

---

## 23. msgcop.CSharp

| Archivo | Descripcion |
|---------|-------------|
| Clscartera.cs | Clase principal de cartera (partial 1/13) con inicializacion y enums de portafolio |
| Clscartera.Part2.cs | Metodos de cartera (2/13): ActuaEstaAntici, AplicaCptoAfavor |
| Clscartera.Part3.cs | Metodos de cartera (3/13) |
| Clscartera.Part4.cs | Metodos de cartera (4/13) |
| Clscartera.Part5.cs | Metodos de cartera (5/13) |
| Clscartera.Part6.cs | Metodos de cartera (6/13) |
| Clscartera.Part6b.cs | Metodos de cartera (6b/13) |
| Clscartera.Part7.cs | Metodos de cartera (7/13) |
| Clscartera.Part7b.cs | Metodos de cartera (7b/13) |
| Clscartera.Part8.cs | Metodos de cartera (8/13) |
| Clscartera.Part8b.cs | Metodos de cartera (8b/13) |
| Clscartera.Part9.cs | Metodos de cartera (9/13) |
| Clscartera.Part9b.cs | Metodos de cartera (9b/13) |
| Forms/FrmProgres.cs | Formulario de progreso para operaciones de cartera |
| Forms/FrmProgres.Designer.cs | Designer auto-generado |
| Forms/Frmpagodeuda.cs | Formulario de pago de deudas individuales con deducciones |
| Forms/Frmpagodeuda.Designer.cs | Designer auto-generado |
| Forms/FrmpagodeudaEstaCue.cs | Formulario de pago de deuda por estado de cuenta |
| Forms/FrmpagodeudaEstaCue.Designer.cs | Designer auto-generado |
| Forms/Frmreestructura.cs | Formulario de reestructuracion de deuda a largo plazo |
| Forms/Frmreestructura.Designer.cs | Designer auto-generado |
| Forms/Frmcodeuda.cs | Formulario de codigo/parametros de obligacion |
| Forms/Frmcodeuda.Designer.cs | Designer auto-generado |
| Forms/FrmGestionCob.cs | Formulario de gestion de cobro y seguimiento |
| Forms/FrmGestionCob.Designer.cs | Designer auto-generado |
| Forms/cop_cuadredoc.cs | Formulario de cuadre/conciliacion de cartera |
| Forms/cop_cuadredoc.Designer.cs | Designer auto-generado |
| Forms/cop_fconcuope01.cs | Formulario de operaciones concurrentes/estado de cuenta |
| Forms/cop_fconcuope01.Designer.cs | Designer auto-generado |
| Forms/cop_fopciondescuentos.cs | Formulario de opciones de descuento por pago |
| Forms/cop_fopciondescuentos.Designer.cs | Designer auto-generado |
| Forms/frmRecDeudas.cs | Formulario de recuperacion de deudas |
| Forms/frmRecDeudas.Designer.cs | Designer auto-generado |
| Forms/frmlavado.cs | Formulario de control antilavado/cumplimiento AML |
| Forms/frmlavado.Designer.cs | Designer auto-generado |
| Forms/frmcirculares.cs | Formulario de gestion de circulares |
| Forms/frmcirculares.Designer.cs | Designer auto-generado |
| Forms/frmrecaudo.cs | Formulario de recaudo |
| Forms/frmrecaudo.Designer.cs | Designer auto-generado |
| Forms/frmrectarj01.cs | Formulario de recuperacion por tarjeta de credito |
| Forms/frmrectarj01.Designer.cs | Designer auto-generado |
| Forms/cop_frmObligaciones.cs | Formulario de obligaciones/compromisos |
| Forms/cop_frmObligaciones.Designer.cs | Designer auto-generado |
| Forms/FrmGarantias.cs | Formulario de garantias/colateral |
| Forms/FrmGarantias.Designer.cs | Designer auto-generado |
| Forms/FrmSolicitudesUsu.cs | Formulario de solicitudes de usuario |
| Forms/FrmSolicitudesUsu.Designer.cs | Designer auto-generado |
| Forms/cop_lineacobjur01.cs | Formulario de linea de credito juridica |
| Forms/cop_lineacobjur01.Designer.cs | Designer auto-generado |
| Forms/credi_extras.cs | Formulario de creditos adicionales/financiamiento extra |
| Forms/credi_extras.Designer.cs | Designer auto-generado |
| Forms/frmsiplainusuales.cs | Formulario de transacciones SIPLA inusuales |
| Forms/frmsiplainusuales.Designer.cs | Designer auto-generado |
| Properties/AssemblyInfo.cs | Metadata del ensamblado |
| Properties/Resources.Designer.cs | Recursos embebidos auto-generados |

**ProjectReference:** ConectBd.CSharp, msccircular.CSharp, msgcnt.CSharp, MsgConfig.CSharp, msgcre.CSharp, MsgDeb.CSharp, msgdep.CSharp, msgliqcre.CSharp, msgtes.CSharp, SasToolBar.CSharp
**PackageReference:** CrystalDecisions.CrystalReports.Engine (10.5.3700.0), CrystalDecisions.Shared (10.5.3700.0), CrystalDecisions.Windows.Forms (10.5.3700.0), Microsoft.VisualBasic

---

## 24. msgcre.CSharp

| Archivo | Descripcion |
|---------|-------------|
| Clstarjcredito.cs | Configuracion/parametrizacion de tarjetas de credito: banco, agencia, cupos, tasas |
| Properties/AssemblyInfo.cs | Metadata del ensamblado |

**ProjectReference:** ConectBd.CSharp, MsgConfig.CSharp, MsgDeb.CSharp, MsgSas.CSharp
**PackageReference:** Microsoft.VisualBasic

---

## 25. msgdep.CSharp

| Archivo | Descripcion |
|---------|-------------|
| ClsDepositos.cs | Clase principal de depositos (4500+ lineas, 60+ metodos, 7 enums) |
| Var.cs | Modulo estatico con variables compartidas de conexion y resultados |
| AyudaCuen.cs | Formulario de ayuda/busqueda de cuentas |
| AyudaCuen.Designer.cs | Designer auto-generado |
| dep_Frmcuenta.cs | Formulario de gestion de cuentas en modulo de depositos |
| dep_Frmcuenta.Designer.cs | Designer auto-generado |
| dep_fcanje01.cs | Formulario de operaciones de canje |
| dep_ffirmas.cs | Formulario de gestion/validacion de firmas |
| dep_ffirmas.Designer.cs | Designer auto-generado |
| dep_fsello.cs | Formulario de gestion de sellos |
| dep_fsello.Designer.cs | Designer auto-generado |
| dep_fvenfirma.cs | Formulario de vencimiento/vigencia de firmas |
| dep_fvenfirma.Designer.cs | Designer auto-generado |
| dep_fverdocu01.cs | Formulario de visualizacion de documentos |
| dep_fverdocu01.Designer.cs | Designer auto-generado |
| Properties/AssemblyInfo.cs | Metadata del ensamblado |
| Properties/Resources.Designer.cs | Recursos embebidos auto-generados |

**ProjectReference:** ConectBd.CSharp, msgcnt.CSharp, MsgConfig.CSharp, msgliqcre.CSharp, MsgSas.CSharp, SasToolBar.CSharp
**PackageReference:** CrystalDecisions.CrystalReports.Engine (10.5.3700.0), Microsoft.VisualBasic

---

## 26. msgimp.CSharp

| Archivo | Descripcion |
|---------|-------------|
| clsmsgimp.cs | Clase de impresion Crystal Reports con 36 metodos para nomina, certificados, recibos |
| Properties/AssemblyInfo.cs | Metadata del ensamblado |

**ProjectReference:** ConectBd.CSharp, MsgConfig.CSharp, MsgImpCnt.CSharp, msgnomconfig.CSharp, MsgSas.CSharp
**PackageReference:** CrystalDecisions.CrystalReports.Engine (10.5.3700.0), CrystalDecisions.Shared (10.5.3700.0), CrystalDecisions.Windows.Forms (10.5.3700.0), Microsoft.VisualBasic

---

## 27. msginv.CSharp

| Archivo | Descripcion |
|---------|-------------|
| msginv.cs | Clase principal de inventario (partial 1/5) con 78+ metodos de operaciones, ventas, transacciones |
| msginv.Part2.cs | Inventario continuacion (partial 2/5) |
| msginv.Part3.cs | Inventario continuacion (partial 3/5) |
| msginv.Part4.cs | Inventario continuacion (partial 4/5) |
| msginv.Part5.cs | Inventario continuacion (partial 5/5) |
| Forms/frmcantbodega.cs | Formulario de cantidades por bodega |
| Forms/frmcantbodega.Designer.cs | Designer auto-generado |
| Forms/frmclientes.cs | Formulario de seleccion/gestion de clientes |
| Forms/frmclientes.Designer.cs | Designer auto-generado |
| Forms/frmLimiteVentas.cs | Formulario de configuracion de limites de venta |
| Forms/frmLimiteVentas.Designer.cs | Designer auto-generado |
| Forms/Inv_frmforpag.cs | Formulario de formas de pago con opciones de financiamiento |
| Forms/Inv_frmforpag.Designer.cs | Designer auto-generado |
| Forms/inv_frmotroimp.cs | Formulario de opciones alternativas de impresion |
| Forms/inv_frmotroimp.Designer.cs | Designer auto-generado |
| Forms/inv_frmPreProducto.cs | Formulario de vista previa de precios/promociones de productos |
| Forms/inv_frmPreProducto.Designer.cs | Designer auto-generado |
| Properties/AssemblyInfo.cs | Metadata del ensamblado |
| Properties/Resources.Designer.cs | Recursos embebidos auto-generados |

**ProjectReference:** ConectBd.CSharp, msgcnt.CSharp, MsgConfig.CSharp, msgcop.CSharp, msginvconfig.CSharp, msgliqcre.CSharp, MsgSas.CSharp, msgtiket.CSharp, SasToolBar.CSharp
**PackageReference:** CrystalDecisions.CrystalReports.Engine (10.5.3700.0), CrystalDecisions.Windows.Forms (10.5.3700.0), Interop.OpenCajon (5.0.0.0), Microsoft.VisualBasic

---

## 28. msginvconfig.CSharp

| Archivo | Descripcion |
|---------|-------------|
| ClsInvConfig.cs | Configuracion de inventario con 80+ metodos y enum Navega para queries y navegacion |
| Properties/AssemblyInfo.cs | Metadata del ensamblado |

**ProjectReference:** ConectBd.CSharp, msgcnt.CSharp, MsgSas.CSharp
**PackageReference:** Microsoft.VisualBasic

---

## 29. msgliqcre.CSharp

| Archivo | Descripcion |
|---------|-------------|
| ClsLiqcreditos.cs | Clase principal de liquidacion de creditos (partial 1/4) con 78+ metodos y 3 enums |
| ClsLiqcreditos.Part2.cs | Liquidacion de creditos continuacion (partial 2/4) |
| ClsLiqcreditos.Part3.cs | Liquidacion de creditos continuacion (partial 3/4) |
| ClsLiqcreditos.Part4.cs | Liquidacion de creditos continuacion (partial 4/4) |
| SolicitudCredito.cs | Formulario de solicitud de credito |
| SolicitudCredito.Part2.cs | Solicitud de credito continuacion (partial) |
| SolicitudCredito.Designer.cs | Designer auto-generado (462 controles, 119 eventos) |
| frmCapacidadPago.cs | Formulario de evaluacion de capacidad de pago |
| frmCapacidadPago.Designer.cs | Designer auto-generado |
| cop_deduciones.cs | Formulario de configuracion de deducciones de credito |
| cop_deduciones.Designer.cs | Designer auto-generado |
| Frmparviv.cs | Formulario de parametros de vivienda compartida |
| Frmparviv.Designer.cs | Designer auto-generado |
| cop_cuotaextras.cs | Formulario de cuotas extras de pago |
| cop_cuotaextras.Designer.cs | Designer auto-generado |
| frmsugeridos.cs | Formulario de acciones/recomendaciones sugeridas |
| frmsugeridos.Designer.cs | Designer auto-generado |
| frmcptoadicionales.cs | Formulario de conceptos adicionales de cuenta |
| frmcptoadicionales.Designer.cs | Designer auto-generado |
| frmBienes.cs | Formulario de gestion de bienes/activos |
| frmBienes.Designer.cs | Designer auto-generado |
| frmRecDeudas.cs | Formulario de recuperacion de deudas |
| frmRecDeudas.Designer.cs | Designer auto-generado |
| frmBienesExisten.cs | Formulario de bienes existentes |
| frmBienesExisten.Designer.cs | Designer auto-generado |
| Properties/AssemblyInfo.cs | Metadata del ensamblado |
| Properties/Resources.Designer.cs | Recursos embebidos auto-generados |

**ProjectReference:** ConectBd.CSharp, SasToolBar.CSharp
**PackageReference:** CrystalDecisions.CrystalReports.Engine (10.5.3700.0), CrystalDecisions.Shared (10.5.3700.0), CrystalDecisions.Windows.Forms (10.5.3700.0), Microsoft.VisualBasic

---

## 30. msgnom.CSharp

| Archivo | Descripcion |
|---------|-------------|
| msgnom.cs | Clase principal de nomina (partial 1/8) con 136+ metodos: liquidacion, prestaciones, cesantias, vacaciones |
| msgnom.Part2.cs | Nomina continuacion (partial 2/8) |
| msgnom.Part3.cs | Nomina continuacion (partial 3/8) |
| msgnom.Part4.cs | Nomina continuacion (partial 4/8) |
| msgnom.Part5.cs | Nomina continuacion (partial 5/8) |
| msgnom.Part6.cs | Nomina continuacion (partial 6/8) |
| msgnom.Part7.cs | Nomina continuacion (partial 7/8) |
| msgnom.Part8.cs | Nomina continuacion (partial 8/8) |
| Forms/nom_frmayuausen.cs | Formulario de gestion de ausentismos/asistencia |
| Forms/nom_frmayuausen.Designer.cs | Designer auto-generado |
| Forms/nom_frmrescptos.cs | Formulario de acumulados de recibos de nomina |
| Forms/nom_frmrescptos.Designer.cs | Designer auto-generado |
| Properties/AssemblyInfo.cs | Metadata del ensamblado |
| Properties/Resources.Designer.cs | Recursos embebidos auto-generados |

**ProjectReference:** ConectBd.CSharp, msgcnt.CSharp, MsgConfig.CSharp, msgimp.CSharp, msgnomconfig.CSharp, MsgSas.CSharp, SasToolBar.CSharp
**PackageReference:** Microsoft.VisualBasic

---

## 31. msgnomconfig.CSharp

| Archivo | Descripcion |
|---------|-------------|
| msgnomconfig.cs | Configuracion de nomina con enum EstadoPeriodos y gestion de periodos/calendarios |
| frmfiltros.cs | Formulario de opciones de filtrado de nomina |
| frmfiltros.Designer.cs | Designer auto-generado |
| Properties/AssemblyInfo.cs | Metadata del ensamblado |

**ProjectReference:** ConectBd.CSharp, MsgConfig.CSharp, MsgSas.CSharp
**PackageReference:** Ninguno

---

## 32. msgtes.CSharp

| Archivo | Descripcion |
|---------|-------------|
| clstesoreria.cs | Clase de tesoreria con enums (OpPago, OpConPor, Estadofact) y operaciones de pago/liquidez |
| VarIni.cs | Modulo estatico de tesoreria con GrabaFormapago y helpers de formas de pago |
| FrmPagoFact.cs | Formulario de pago de facturas |
| FrmPagoFact.Designer.cs | Designer auto-generado |
| Properties/AssemblyInfo.cs | Metadata del ensamblado |

**ProjectReference:** ConectBd.CSharp, MsgImpCnt.CSharp, msgcnt.CSharp, MsgConfig.CSharp, MsgSas.CSharp
**PackageReference:** Microsoft.VisualBasic

---

## 33. msgtiket.CSharp

| Archivo | Descripcion |
|---------|-------------|
| clsmsgtiket.cs | Impresion de tickets POS (1223 lineas) con struct stdatos y metodos PrintDocument para impresoras termicas |
| Properties/AssemblyInfo.cs | Metadata del ensamblado |

**ProjectReference:** ConectBd.CSharp, msgcnt.CSharp, MsgConfig.CSharp, msginvconfig.CSharp
**PackageReference:** Interop.OpenCajon (5.0.0.0), Microsoft.VisualBasic

---

## Resumen de Dependencias entre Proyectos

```
ConectBd.CSharp ← base (sin dependencias)
SasToolBar.CSharp ← ConectBd
MsgConfig.CSharp ← ConectBd, SasToolBar
MsgSas.CSharp ← (HintPath a VB DLLs, sin ProjectRef directas)
MsgCdats.CSharp ← ConectBd
Nitgen.CSharp ← ConectBd
msgnomconfig.CSharp ← ConectBd, MsgConfig, MsgSas
msginvconfig.CSharp ← ConectBd, msgcnt, MsgSas
MsgDeb.CSharp ← ConectBd, MsgConfig, msgdep
msgcre.CSharp ← ConectBd, MsgConfig, MsgDeb, MsgSas
msgliqcre.CSharp ← ConectBd, SasToolBar
msgcnt.CSharp ← ConectBd, MsgConfig, msgliqcre, MsgSas
msgdep.CSharp ← ConectBd, msgcnt, MsgConfig, msgliqcre, MsgSas, SasToolBar
msgtes.CSharp ← ConectBd, MsgImpCnt, msgcnt, MsgConfig, MsgSas
MsgImpCnt.CSharp ← ConectBd, msgcnt, MsgConfig, MsgSas
MsgImpCop.CSharp ← ConectBd, MsgConfig, msgliqcre, MsgSas
msccircular.CSharp ← ConectBd, MsgConfig, MsgSas
MsgRecaudos.CSharp ← ConectBd, MsgConfig, MsgSas
mscext01.CSharp ← (sin ProjectRef)
mscext02.CSharp ← ConectBd, MsgConfig, MsgSas
mscimpcdat.CSharp ← MsgCdats, MsgConfig, MsgSas
mscimpsol.CSharp ← ConectBd, MsgConfig
mscpagonomi.CSharp ← MsgConfig, msgcop, msgdep, MsgSas
mscProcesoProd.CSharp ← ConectBd, MsgSas
msgcli.CSharp ← ConectBd, msgcnt, MsgConfig, msginv, msginvconfig, MsgSas
msgimp.CSharp ← ConectBd, MsgConfig, MsgImpCnt, msgnomconfig, MsgSas
msginv.CSharp ← ConectBd, msgcnt, MsgConfig, msgcop, msginvconfig, msgliqcre, MsgSas, msgtiket, SasToolBar
msgtiket.CSharp ← ConectBd, msgcnt, MsgConfig, msginvconfig
msgnom.CSharp ← ConectBd, msgcnt, MsgConfig, msgimp, msgnomconfig, MsgSas, SasToolBar
msgcop.CSharp ← ConectBd, msccircular, msgcnt, MsgConfig, msgcre, MsgDeb, msgdep, msgliqcre, msgtes, SasToolBar
MscAplNom.CSharp ← ConectBd, MsgConfig, msgcop, MsgSas
MscImpCert.CSharp ← ConectBd, MsgConfig, msgcop, MsgSas
arnomgen.CSharp ← MsgConfig, msgcop
```

## Estadisticas

| Metrica | Valor |
|---------|-------|
| Total proyectos .CSharp | 32 |
| Proyectos adicionales | 1 (Nitgen.CSharp) |
| Proyecto mas grande | msgcop.CSharp (~55 archivos .cs, 40,158 lineas) |
| Proyecto base (sin deps) | ConectBd.CSharp |
| Proyecto con mas dependencias | msgcop.CSharp (10 ProjectReferences) |
| NuGet mas usado | CrystalDecisions.CrystalReports.Engine (16 proyectos) |
| Framework actual | .NET 3.5 |
