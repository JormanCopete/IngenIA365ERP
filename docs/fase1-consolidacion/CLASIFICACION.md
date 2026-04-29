# Clasificacion por Modulo Funcional Destino (ERP.Core)

> Generado: 2026-03-15
> Criterios de clasificacion:
> - Prefijo del proyecto original (cnt=Contabilidad, cop=Cartera, dep=Ahorro, etc.)
> - Proposito funcional de cada clase
> - Clases de infraestructura/utilidad usadas por multiples modulos → **Compartido**
> - Se proponen modulos adicionales donde el dominio cooperativo lo requiere

## Modulos Propuestos

| # | Modulo | Namespace ERP.Core | Descripcion |
|---|--------|---------------------|-------------|
| 1 | Compartido | ERP.Core.Compartido | Infraestructura, utilidades, controles UI y config usados transversalmente |
| 2 | Contabilidad | ERP.Core.Contabilidad | Asientos, mayor, balance, conciliacion, impresion contable (cnt) |
| 3 | CarteraFinanciera | ERP.Core.CarteraFinanciera | Gestion de prestamos, obligaciones, cobro, circulares, reestructuracion (cop) |
| 4 | Creditos | ERP.Core.Creditos | Liquidacion, solicitud, proyeccion de creditos (liqcre) |
| 5 | Ahorros | ERP.Core.Ahorros | Cuentas de ahorro, depositos, firmas, sellos (dep) |
| 6 | CDT | ERP.Core.CDT | Certificados de Deposito a Termino (cdt/cdats) |
| 7 | TarjetaDebito | ERP.Core.TarjetaDebito | Gestion de tarjetas debito, cupos (deb) |
| 8 | TarjetaCredito | ERP.Core.TarjetaCredito | Configuracion de tarjetas credito (cre) |
| 9 | Inventario | ERP.Core.Inventario | Inventario, punto de venta, tickets POS, clientes (inv) |
| 10 | Nomina | ERP.Core.Nomina | Liquidacion nomina, prestaciones, planilla unica, descuentos (nom) |
| 11 | Tesoreria | ERP.Core.Tesoreria | Pagos, liquidez, formas de pago (tes) |
| 12 | Reportes | ERP.Core.Reportes | Extractos, distribucion de reportes, impresion general |
| 13 | Seguridad | ERP.Core.Seguridad | Autenticacion, biometria, firmas, bloqueos |
| 14 | Recaudos | ERP.Core.Recaudos | Conceptos de recaudo y facturacion de servicios |
| 15 | Produccion | ERP.Core.Produccion | Procesos productivos |

---

## Clasificacion Detallada por Clase

### COMPARTIDO (ERP.Core.Compartido)
> Clases referenciadas por 3+ modulos funcionales distintos. Infraestructura transversal.

| Proyecto Origen | Clase/Archivo | Subcarpeta Destino | Razon |
|-----------------|---------------|---------------------|-------|
| ConectBd.CSharp | ClsConect.cs | Compartido/Datos/ | Conexion ODBC base, usada por TODOS los modulos (24 refs) |
| SasToolBar.CSharp | SasToolBar.cs | Compartido/Controles/ | Toolbar de navegacion, usado por 5+ modulos |
| SasToolBar.CSharp | SasToolBar.Designer.cs | Compartido/Controles/ | Designer del toolbar |
| MsgSas.CSharp | VarIni.cs | Compartido/Utilidades/ | GrabaFormapago, buscaFormapago, wrappers ODBC (15+ refs) |
| MsgSas.CSharp | Ayuda.cs | Compartido/Utilidades/ | Dialogos de ayuda/busqueda usados por todos los modulos |
| MsgSas.CSharp | Barraprogress.cs | Compartido/Controles/ | Barra de progreso generica |
| MsgSas.CSharp | CalendarCell.cs | Compartido/Controles/CalendarGrid/ | Control DataGridView calendario |
| MsgSas.CSharp | CalendarColumn.cs | Compartido/Controles/CalendarGrid/ | Columna DataGridView calendario |
| MsgSas.CSharp | CalendarEditingControl.cs | Compartido/Controles/CalendarGrid/ | Editor DateTimePicker para grid |
| MsgSas.CSharp | config_report.cs | Compartido/Reportes/ | Configuracion Crystal Reports generica |
| MsgSas.CSharp | TexboxDecimal.cs | Compartido/Controles/ | TextBox solo decimales |
| MsgSas.CSharp | TexboxSoloLetras.cs | Compartido/Controles/ | TextBox solo letras |
| MsgSas.CSharp | TexboxSoloNumeros.cs | Compartido/Controles/ | TextBox solo numeros |
| MsgSas.CSharp | excel_gridviem.cs | Compartido/Utilidades/ | Exportacion DataGridView a Excel |
| MsgSas.CSharp | GenerarCodBarras.cs | Compartido/Utilidades/ | Generador codigo barras Code128 |
| MsgSas.CSharp | Numeros_A_Letras.cs | Compartido/Utilidades/ | Conversor numeros a letras en espanol |
| MsgSas.CSharp | reporte.cs | Compartido/Reportes/ | Wrapper ReportDocument generico |
| MsgSas.CSharp | UserControlInferiorFormularios.cs | Compartido/Controles/ | Footer reutilizable para formularios |
| MsgSas.CSharp | FormAyuda.cs | Compartido/Forms/ | Form de ayuda/busqueda generica |
| MsgSas.CSharp | FrmAyuDocs.cs | Compartido/Forms/ | Form ayuda de documentos |
| MsgSas.CSharp | FrmFormPago.cs | Compartido/Forms/ | Form formas de pago (efectivo, cheque, tarjeta) |
| MsgSas.CSharp | FrmProgres.cs | Compartido/Forms/ | Form base barra de progreso |
| MsgSas.CSharp | helptable.cs | Compartido/Forms/ | Form tabla de busqueda configurable |
| MsgSas.CSharp | imprimir.cs | Compartido/Forms/ | Form visor Crystal Reports |
| MsgConfig.CSharp | ParamSys.cs | Compartido/Configuracion/ | Parametros del sistema global (email, archivos, procesos) |
| MsgConfig.CSharp | VarIni.cs | Compartido/Configuracion/ | Variables globales de inicializacion del sistema |
| MsgConfig.CSharp | FrmActividades.cs | Compartido/Forms/ | Gestion de actividades (bienestar social) |
| MsgConfig.CSharp | frmtasas.cs | Compartido/Configuracion/ | Tasas de cambio/interes (transversal a modulos) |
| MsgConfig.CSharp | frmtasacptos.cs | Compartido/Configuracion/ | Tasas por concepto (transversal) |

---

### CONTABILIDAD (ERP.Core.Contabilidad)

| Proyecto Origen | Clase/Archivo | Subcarpeta Destino | Descripcion |
|-----------------|---------------|---------------------|-------------|
| msgcnt.CSharp | ClsContabilidad.cs | Contabilidad/Services/ | Clase contable principal: asientos, mayor, balance, conciliacion (60+ metodos) |
| msgcnt.CSharp | ClsContabilidad.Promedios.cs | Contabilidad/Services/ | Calculo de promedios contables para analisis de cartera |
| msgcnt.CSharp | FrmCntProgres.cs | Contabilidad/Forms/ | Progreso para operaciones contables |
| msgcnt.CSharp | cnt_cuadredoc.cs | Contabilidad/Forms/ | Cuadre/conciliacion de documentos |
| msgcnt.CSharp | frmMovCiclo.cs | Contabilidad/Forms/ | Movimientos por ciclo/periodo |
| msgcnt.CSharp | frmTipodoc.cs | Contabilidad/Forms/ | Configuracion de tipos de documento |
| msgcnt.CSharp | frmdocaux.cs | Contabilidad/Forms/ | Documentos auxiliares |
| MsgImpCnt.CSharp | ImpreDoc.cs | Contabilidad/Reportes/ | Impresion de recibos, comprobantes, cheques contables |
| MsgImpCnt.CSharp | VarIni.cs | Contabilidad/Helpers/ | Variables de inicializacion para impresion contable |
| MsgConfig.CSharp | ParamCnt.cs | Contabilidad/Models/ | Parametros y enums de contabilidad |

---

### CARTERA FINANCIERA (ERP.Core.CarteraFinanciera)

| Proyecto Origen | Clase/Archivo | Subcarpeta Destino | Descripcion |
|-----------------|---------------|---------------------|-------------|
| msgcop.CSharp | Clscartera.cs (13 partials) | CarteraFinanciera/Services/ | Clase principal de cartera: 145 metodos, 11 enums, gestion de obligaciones |
| msgcop.CSharp | FrmProgres.cs | CarteraFinanciera/Forms/ | Progreso para operaciones de cartera |
| msgcop.CSharp | Frmpagodeuda.cs | CarteraFinanciera/Forms/ | Pago de deudas individuales |
| msgcop.CSharp | FrmpagodeudaEstaCue.cs | CarteraFinanciera/Forms/ | Pago por estado de cuenta |
| msgcop.CSharp | Frmreestructura.cs | CarteraFinanciera/Forms/ | Reestructuracion de deuda |
| msgcop.CSharp | Frmcodeuda.cs | CarteraFinanciera/Forms/ | Codigo/parametros de obligacion |
| msgcop.CSharp | FrmGestionCob.cs | CarteraFinanciera/Forms/ | Gestion de cobro y seguimiento |
| msgcop.CSharp | cop_cuadredoc.cs | CarteraFinanciera/Forms/ | Cuadre/conciliacion de cartera |
| msgcop.CSharp | cop_fconcuope01.cs | CarteraFinanciera/Forms/ | Operaciones concurrentes/estado de cuenta |
| msgcop.CSharp | cop_fopciondescuentos.cs | CarteraFinanciera/Forms/ | Opciones de descuento por pago |
| msgcop.CSharp | frmRecDeudas.cs | CarteraFinanciera/Forms/ | Recuperacion de deudas |
| msgcop.CSharp | frmlavado.cs | CarteraFinanciera/Forms/ | Control antilavado (AML/SIPLA) |
| msgcop.CSharp | frmcirculares.cs | CarteraFinanciera/Forms/ | Gestion de circulares |
| msgcop.CSharp | frmrecaudo.cs | CarteraFinanciera/Forms/ | Recaudo de cartera |
| msgcop.CSharp | frmrectarj01.cs | CarteraFinanciera/Forms/ | Recuperacion por tarjeta |
| msgcop.CSharp | cop_frmObligaciones.cs | CarteraFinanciera/Forms/ | Obligaciones/compromisos |
| msgcop.CSharp | FrmGarantias.cs | CarteraFinanciera/Forms/ | Garantias/colateral |
| msgcop.CSharp | FrmSolicitudesUsu.cs | CarteraFinanciera/Forms/ | Solicitudes de usuario |
| msgcop.CSharp | cop_lineacobjur01.cs | CarteraFinanciera/Forms/ | Linea de credito juridica |
| msgcop.CSharp | credi_extras.cs | CarteraFinanciera/Forms/ | Creditos adicionales |
| msgcop.CSharp | frmsiplainusuales.cs | CarteraFinanciera/Forms/ | Transacciones SIPLA inusuales |
| msccircular.CSharp | clscircular.cs | CarteraFinanciera/Services/ | Generacion de circulares de cobro |
| MsgImpCop.CSharp | ImpreDoc.cs | CarteraFinanciera/Reportes/ | Impresion de documentos de cooperativa |
| MsgImpCop.CSharp | VarIni.cs | CarteraFinanciera/Helpers/ | Variables y GrabaFormapago para cartera |
| MsgImpCop.CSharp | Ordencomercio.cs | CarteraFinanciera/Forms/ | Ordenes de compra/proveedor |
| MscImpCert.CSharp | ClsImpCert.cs | CarteraFinanciera/Reportes/ | Impresion de certificados por linea de credito |
| MsgConfig.CSharp | ParamCop.cs | CarteraFinanciera/Models/ | Parametros y enums de cartera cooperativa |
| MsgConfig.CSharp | FrmAgregarReferencia.cs | CarteraFinanciera/Forms/ | Agregar referencias financieras (para solicitudes credito) |
| MsgConfig.CSharp | frmreferencias.cs | CarteraFinanciera/Forms/ | Gestion de referencias |
| MsgConfig.CSharp | frmtasasintcred.cs | CarteraFinanciera/Forms/ | Tasas de interes de creditos |
| MscAplNom.CSharp | AplicaDstos.cs | CarteraFinanciera/Services/ | Aplicacion de descuentos de nomina |

---

### CREDITOS (ERP.Core.Creditos)

| Proyecto Origen | Clase/Archivo | Subcarpeta Destino | Descripcion |
|-----------------|---------------|---------------------|-------------|
| msgliqcre.CSharp | ClsLiqcreditos.cs (4 partials) | Creditos/Services/ | Liquidacion de creditos: 78+ metodos, proyeccion, amortizacion |
| msgliqcre.CSharp | SolicitudCredito.cs (+Part2) | Creditos/Forms/ | Formulario de solicitud de credito (462 controles) |
| msgliqcre.CSharp | frmCapacidadPago.cs | Creditos/Forms/ | Evaluacion de capacidad de pago |
| msgliqcre.CSharp | cop_deduciones.cs | Creditos/Forms/ | Deducciones de credito |
| msgliqcre.CSharp | Frmparviv.cs | Creditos/Forms/ | Parametros de vivienda |
| msgliqcre.CSharp | cop_cuotaextras.cs | Creditos/Forms/ | Cuotas extras de pago |
| msgliqcre.CSharp | frmsugeridos.cs | Creditos/Forms/ | Recomendaciones sugeridas |
| msgliqcre.CSharp | frmcptoadicionales.cs | Creditos/Forms/ | Conceptos adicionales |
| msgliqcre.CSharp | frmBienes.cs | Creditos/Forms/ | Gestion de bienes/activos del solicitante |
| msgliqcre.CSharp | frmRecDeudas.cs | Creditos/Forms/ | Recuperacion de deudas |
| msgliqcre.CSharp | frmBienesExisten.cs | Creditos/Forms/ | Bienes existentes del solicitante |
| mscimpsol.CSharp | clsimpsol.cs | Creditos/Reportes/ | Impresion solicitud de credito con plan de pagos |

---

### AHORROS (ERP.Core.Ahorros)

| Proyecto Origen | Clase/Archivo | Subcarpeta Destino | Descripcion |
|-----------------|---------------|---------------------|-------------|
| msgdep.CSharp | ClsDepositos.cs | Ahorros/Services/ | Clase principal depositos: 60+ metodos, 7 enums |
| msgdep.CSharp | Var.cs | Ahorros/Helpers/ | Variables compartidas de conexion y resultados |
| msgdep.CSharp | AyudaCuen.cs | Ahorros/Forms/ | Ayuda/busqueda de cuentas de ahorro |
| msgdep.CSharp | dep_Frmcuenta.cs | Ahorros/Forms/ | Gestion de cuentas de ahorro |
| msgdep.CSharp | dep_fcanje01.cs | Ahorros/Forms/ | Operaciones de canje |
| msgdep.CSharp | dep_ffirmas.cs | Ahorros/Forms/ | Gestion de firmas de cuentas |
| msgdep.CSharp | dep_fsello.cs | Ahorros/Forms/ | Gestion de sellos |
| msgdep.CSharp | dep_fvenfirma.cs | Ahorros/Forms/ | Vencimiento/vigencia de firmas |
| msgdep.CSharp | dep_fverdocu01.cs | Ahorros/Forms/ | Visualizacion de documentos |

---

### CDT (ERP.Core.CDT)

| Proyecto Origen | Clase/Archivo | Subcarpeta Destino | Descripcion |
|-----------------|---------------|---------------------|-------------|
| MsgCdats.CSharp | ClsMsgCdats.cs | CDT/Services/ | Gestion de CDTs: pago, interes, liquidacion |
| mscimpcdat.CSharp | clsimpcdats.cs | CDT/Reportes/ | Impresion Crystal Reports de certificados CDT |
| MsgConfig.CSharp | ParamCdt.cs | CDT/Models/ | Parametros y enum Navega para CDT |

---

### TARJETA DEBITO (ERP.Core.TarjetaDebito)

| Proyecto Origen | Clase/Archivo | Subcarpeta Destino | Descripcion |
|-----------------|---------------|---------------------|-------------|
| MsgDeb.CSharp | ClsMsgDeb.cs | TarjetaDebito/Services/ | Gestion tarjetas debito: movimientos, navegacion |
| MsgDeb.CSharp | FrmCupoTarj.cs | TarjetaDebito/Forms/ | Cupos/limites de tarjeta debito |

---

### TARJETA CREDITO (ERP.Core.TarjetaCredito)

| Proyecto Origen | Clase/Archivo | Subcarpeta Destino | Descripcion |
|-----------------|---------------|---------------------|-------------|
| msgcre.CSharp | Clstarjcredito.cs | TarjetaCredito/Services/ | Config tarjetas credito: banco, agencia, cupos, tasas |

---

### INVENTARIO (ERP.Core.Inventario)

| Proyecto Origen | Clase/Archivo | Subcarpeta Destino | Descripcion |
|-----------------|---------------|---------------------|-------------|
| msginv.CSharp | msginv.cs (5 partials) | Inventario/Services/ | Clase principal inventario: 78+ metodos, ventas, transacciones |
| msginv.CSharp | frmcantbodega.cs | Inventario/Forms/ | Cantidades por bodega |
| msginv.CSharp | frmclientes.cs | Inventario/Forms/ | Seleccion de clientes |
| msginv.CSharp | frmLimiteVentas.cs | Inventario/Forms/ | Limites de venta |
| msginv.CSharp | Inv_frmforpag.cs | Inventario/Forms/ | Formas de pago con financiamiento |
| msginv.CSharp | inv_frmotroimp.cs | Inventario/Forms/ | Opciones alternativas de impresion |
| msginv.CSharp | inv_frmPreProducto.cs | Inventario/Forms/ | Vista previa precios/promociones |
| msginvconfig.CSharp | ClsInvConfig.cs | Inventario/Services/ | Configuracion inventario: 80+ metodos, enum Navega |
| msgtiket.CSharp | clsmsgtiket.cs | Inventario/Services/ | Impresion tickets POS en impresoras termicas |
| msgcli.CSharp | Clsmsgcli.cs (5 clases) | Inventario/Services/ | Modulo clientes: consultas, config, ventas/facturacion, impresion |

---

### NOMINA (ERP.Core.Nomina)

| Proyecto Origen | Clase/Archivo | Subcarpeta Destino | Descripcion |
|-----------------|---------------|---------------------|-------------|
| msgnom.CSharp | msgnom.cs (8 partials) | Nomina/Services/ | Clase principal: 136+ metodos, liquidacion, prestaciones, cesantias, vacaciones |
| msgnom.CSharp | nom_frmayuausen.cs | Nomina/Forms/ | Gestion de ausentismos/asistencia |
| msgnom.CSharp | nom_frmrescptos.cs | Nomina/Forms/ | Acumulados de recibos de nomina |
| msgnomconfig.CSharp | msgnomconfig.cs | Nomina/Services/ | Config nomina: periodos, calendarios, enum EstadoPeriodos |
| msgnomconfig.CSharp | frmfiltros.cs | Nomina/Forms/ | Filtros de nomina |
| mscpagonomi.CSharp | Clspagonomi.cs | Nomina/Services/ | Procesador archivos planos de pago nomina |
| arnomgen.CSharp | inicio.cs | Nomina/Services/ | Generacion/carga numeros de nomina con Excel |
| arnomgen.CSharp | num_solicitud.cs | Nomina/Forms/ | Dialogo numero de solicitud |
| arnomgen.CSharp | Form1.cs | Nomina/Forms/ | Formulario inicio (placeholder) |
| msgimp.CSharp | clsmsgimp.cs | Nomina/Reportes/ | Impresion Crystal Reports: 36 metodos (nomina, certificados, recibos) |

---

### TESORERIA (ERP.Core.Tesoreria)

| Proyecto Origen | Clase/Archivo | Subcarpeta Destino | Descripcion |
|-----------------|---------------|---------------------|-------------|
| msgtes.CSharp | clstesoreria.cs | Tesoreria/Services/ | Operaciones de pago, liquidez, enums (OpPago, OpConPor, Estadofact) |
| msgtes.CSharp | VarIni.cs | Tesoreria/Helpers/ | GrabaFormapago y helpers de tesoreria |
| msgtes.CSharp | FrmPagoFact.cs | Tesoreria/Forms/ | Pago de facturas |
| MsgConfig.CSharp | ParamTes.cs | Tesoreria/Models/ | Parametros de tesoreria |

---

### REPORTES (ERP.Core.Reportes)

| Proyecto Origen | Clase/Archivo | Subcarpeta Destino | Descripcion |
|-----------------|---------------|---------------------|-------------|
| mscext01.CSharp | ClsMscext01.cs | Reportes/Services/ | Impresion extractos de cuenta via Crystal Reports |
| mscext02.CSharp | clsmscext02.cs | Reportes/Services/ | Distribucion de reportes por email/PDF/impresion |

---

### SEGURIDAD (ERP.Core.Seguridad)

| Proyecto Origen | Clase/Archivo | Subcarpeta Destino | Descripcion |
|-----------------|---------------|---------------------|-------------|
| Nitgen.CSharp | ClsNitgen.cs | Seguridad/Biometria/ | Driver lector huellas Nitgen: enrolamiento y verificacion |
| Nitgen.CSharp | NetBioApi.cs | Seguridad/Biometria/ | Constantes/enums NBioAPI |
| MsgConfig.CSharp | FrmLogeo.cs | Seguridad/Forms/ | Login/autenticacion con validaciones |
| MsgConfig.CSharp | sys_ffirmas.cs | Seguridad/Forms/ | Gestion de firmas/aprobaciones |
| MsgConfig.CSharp | frmempbloq.cs | Seguridad/Forms/ | Gestion de empresas bloqueadas |

---

### RECAUDOS (ERP.Core.Recaudos)

| Proyecto Origen | Clase/Archivo | Subcarpeta Destino | Descripcion |
|-----------------|---------------|---------------------|-------------|
| MsgRecaudos.CSharp | msgrecaudos.cs | Recaudos/Services/ | CRUD y navegacion de conceptos de tipo recaudo |

---

### PRODUCCION (ERP.Core.Produccion)

| Proyecto Origen | Clase/Archivo | Subcarpeta Destino | Descripcion |
|-----------------|---------------|---------------------|-------------|
| mscProcesoProd.CSharp | ProcesoProd.cs | Produccion/Services/ | Procesos de produccion con ODBC y dialogos ayuda |

---

## Resumen de Clasificacion

| Modulo Destino | Proyectos Origen | Clases .cs | Lineas Aprox |
|----------------|------------------|------------|--------------|
| Compartido | ConectBd, SasToolBar, MsgSas, MsgConfig (parcial) | ~30 | ~10,000 |
| Contabilidad | msgcnt, MsgImpCnt, MsgConfig.ParamCnt | ~13 | ~8,500 |
| CarteraFinanciera | msgcop, msccircular, MscAplNom, MsgImpCop, MscImpCert, MsgConfig (parcial) | ~58 | ~42,000 |
| Creditos | msgliqcre, mscimpsol | ~27 | ~28,500 |
| Ahorros | msgdep | ~17 | ~5,500 |
| CDT | MsgCdats, mscimpcdat, MsgConfig.ParamCdt | ~3 | ~1,000 |
| TarjetaDebito | MsgDeb | ~2 | ~500 |
| TarjetaCredito | msgcre | ~1 | ~300 |
| Inventario | msginv, msginvconfig, msgtiket, msgcli | ~22 | ~15,000 |
| Nomina | msgnom, msgnomconfig, mscpagonomi, arnomgen, msgimp | ~18 | ~15,500 |
| Tesoreria | msgtes, MsgConfig.ParamTes | ~5 | ~2,500 |
| Reportes | mscext01, mscext02 | ~2 | ~600 |
| Seguridad | Nitgen, MsgConfig (parcial) | ~5 | ~1,500 |
| Recaudos | MsgRecaudos | ~1 | ~300 |
| Produccion | mscProcesoProd | ~1 | ~200 |
| **TOTAL** | **33 proyectos** | **~205** | **~132,000** |

## Notas y Decisiones Pendientes

1. **MsgConfig.CSharp se descompone entre 6 modulos**: Sus 15 archivos se distribuyen en Compartido (ParamSys, VarIni, frmtasas, frmtasacptos, FrmActividades), Contabilidad (ParamCnt), CarteraFinanciera (ParamCop, frmreferencias, FrmAgregarReferencia, frmtasasintcred), CDT (ParamCdt), Tesoreria (ParamTes), Seguridad (FrmLogeo, sys_ffirmas, frmempbloq).

2. **CarteraFinanciera vs Creditos**: Son modulos muy acoplados (msgcop referencia msgliqcre). Considerar si fusionarlos en un solo modulo `CarteraYCreditos` o mantenerlos separados para claridad.

3. **MsgSas.CSharp va completo a Compartido**: Sus 22 archivos son utilidades/controles usados transversalmente. No tiene sentido dividirlo.

4. **msgimp.CSharp (impresion) va a Nomina**: Aunque su nombre es generico, depende de msgnomconfig y MsgImpCnt, y sus 36 metodos imprimen documentos de nomina/personal.

5. **Archivos Designer.cs y Properties/**: Se mueven junto con su .cs padre. No se listan individualmente para brevedad pero se incluyen.

6. **Dependencias circulares potenciales**: msgcop ← msgliqcre y msgcnt ← msgliqcre. Revisar si al consolidar se crean ciclos entre Contabilidad, CarteraFinanciera y Creditos.
