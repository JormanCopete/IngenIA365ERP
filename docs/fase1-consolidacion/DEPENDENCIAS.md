# Analisis de Dependencias - Migracion a ERP.Core

> Generado: 2026-03-15
> Basado en analisis de `using` statements, referencias directas a clases y ProjectReference de los 33 proyectos .CSharp

---

## 1. DEPENDENCIAS CIRCULARES

> Estas son dependencias bidireccionales detectadas a nivel de codigo (using + referencias directas).
> En el proyecto consolidado ERP.Core (un solo ensamblado) dejan de ser problema,
> pero determinan que estos modulos DEBEN migrarse juntos en el mismo lote.

### Ciclos Directos (A ↔ B)

| Clase/Proyecto A | Modulo A | Clase/Proyecto B | Modulo B | Detalle |
|------------------|----------|------------------|----------|---------|
| msgcnt.ClsContabilidad | Contabilidad | msgcop.Clscartera | CarteraFinanciera | cnt usa BuscaAsociado/BuscaLinea de cop; cop usa GrabaMovimiento/BuscaComprobante de cnt |
| msgcnt.ClsContabilidad | Contabilidad | msgtes.clstesoreria | Tesoreria | cnt referencia msgtes; tes usa GrabaMovimiento/BuscaComprobante de cnt |
| msgcop.Clscartera | CarteraFinanciera | msgliqcre.ClsLiqcreditos | Creditos | cop usa ClsLiqcreditos para proyecciones; liqcre usa Clscartera para BuscaAsociado/obligaciones |
| msgcop.Clscartera | CarteraFinanciera | msgdep.ClsDepositos | Ahorros | cop usa BuscarCuentaAhorro de dep; dep usa Clscartera para asociados |
| msgcop.Clscartera | CarteraFinanciera | msgtes.clstesoreria | Tesoreria | cop referencia tes en Parts 3,4; tes referencia cop |
| msgcop.Clscartera | CarteraFinanciera | MsgDeb.ClsMsgDeb | TarjetaDebito | cop usa ClsMsgDeb en Part3/frmrectarj01; deb usa Clscartera |
| msgcop.Clscartera | CarteraFinanciera | msgcre.Clstarjcredito | TarjetaCredito | cop usa Clstarjcredito en frmrectarj01; cre usa Clscartera |
| msgliqcre.ClsLiqcreditos | Creditos | msgdep.ClsDepositos | Ahorros | liqcre usa BuscarCuentaAhorro de dep; dep usa msgliqcre |
| msgliqcre.ClsLiqcreditos | Creditos | MsgImpCop.ImpreDoc | CarteraFinanciera | liqcre referencia MsgImpCop; MsgImpCop referencia msgliqcre |

### Cluster Circular Principal

Estos 8 proyectos forman un **grafo fuertemente conexo** — todos se alcanzan mutuamente a traves de cadenas de dependencia:

```
                    ┌─────────────┐
            ┌──────►│  msgcnt     │◄──────┐
            │       │(Contabilid.)│       │
            │       └──────┬──────┘       │
            │              │              │
            ▼              ▼              │
     ┌──────────┐   ┌──────────┐   ┌─────┴────┐
     │ msgtes   │◄─►│ msgcop   │◄─►│msgliqcre │
     │(Tesorer.)│   │(Cartera) │   │(Creditos)│
     └──────────┘   └────┬─────┘   └────┬─────┘
                         │              │
                    ┌────┴────┐    ┌────┴────┐
                    │ msgdep  │◄──►│MsgImpCop│
                    │(Ahorros)│    │(Cart.Rep│
                    └────┬────┘    └─────────┘
                         │
                  ┌──────┴──────┐
                  │   MsgDeb    │    ┌─────────┐
                  │(Tarj.Deb.)  │    │ msgcre  │
                  └─────────────┘    │(Tarj.Cr)│
                                     └─────────┘
```

**Implicacion para migracion:** Estos 8 proyectos (+ mscimpsol que liqcre referencia) deben compilarse juntos. En ERP.Core esto se resuelve automaticamente al ser un solo ensamblado.

---

## 2. CLASES MAS REFERENCIADAS (Top 20)

> Ordenadas por cantidad de proyectos que las usan directamente en codigo.
> Estas son las candidatas prioritarias para migrar primero a Compartido/ o
> las primeras en estabilizar en sus modulos destino.

| # | Clase | Proyecto Origen | Modulo Destino | Proyectos que la Usan | Usos |
|---|-------|-----------------|----------------|----------------------|------|
| 1 | **ClsConect** + struct **OdbcConect** | ConectBd.CSharp | Compartido/Datos | TODOS | 32 |
| 2 | **ParamSys** (BuscarCompania, BuscaUsuario, enums) | MsgConfig.CSharp | Compartido/Configuracion | msgcop, msgcnt, msgdep, msgliqcre, msgtes, msginv, msgnom, MsgCdats, MsgDeb, msgcre, msgimp, msgcli, mscext02, mscimpcdat, MsgRecaudos, MsgImpCnt, MsgImpCop, MscImpCert, MscAplNom | ~20 |
| 3 | **ParamCop** (Navega enum, BuscaAsociado wrapper) | MsgConfig.CSharp | CarteraFinanciera/Models | msgcop, msgcnt, msgdep, msgliqcre, msgtes, msginv, MsgDeb, msgcre, mscext02, MsgImpCop, MscImpCert, MscAplNom, arnomgen, mscpagonomi | ~15 |
| 4 | **Ayuda** (CargaAyuda, UnloadHelp, ConfiguraForma) | MsgSas.CSharp | Compartido/Utilidades | MsgConfig, msgcop, msgdep, msgliqcre, msginv, msginvconfig, msgnom, msgcre, msgnomconfig, mscProcesoProd | ~15 |
| 5 | **Clscartera** (145 metodos, 11 enums) | msgcop.CSharp | CarteraFinanciera/Services | msgcnt, msgliqcre, msgdep, msgtes, MsgDeb, msgcre, MsgCdats, mscimpcdat, mscimpsol, MscImpCert, MscAplNom, arnomgen, mscpagonomi | ~13 |
| 6 | **Barraprogress** | MsgSas.CSharp | Compartido/Controles | msgcop, msgnom, msginv, msgcli, MscAplNom, mscpagonomi, msgliqcre, msgdep | ~8 |
| 7 | **ClsContabilidad** (GrabaMovimiento, BuscaComprobante, BuscarTercero) | msgcnt.CSharp | Contabilidad/Services | msgcop, msgdep, msgtes, msginv, msginvconfig, msgtiket, msgcli, msgnom, MsgCdats | ~9 |
| 8 | **imprimir** (visor Crystal Reports) | MsgSas.CSharp | Compartido/Forms | msgimp, msgcli, MsgImpCnt, MsgImpCop, mscimpcdat, mscimpsol | ~6 |
| 9 | **reporte** (ReportDocument wrapper) | MsgSas.CSharp | Compartido/Reportes | msgimp, msgcli, msginv, MsgImpCnt, MsgImpCop, mscimpcdat | ~6 |
| 10 | **config_report** (configuracion Crystal) | MsgSas.CSharp | Compartido/Reportes | mscext01, mscext02, MsgImpCnt, MsgImpCop, msgimp | ~5 |
| 11 | **SasToolBar** (toolbar navegacion) | SasToolBar.CSharp | Compartido/Controles | MsgConfig, msgdep, msginv, msgnom, msgliqcre, msgcop | ~6 |
| 12 | **ClsLiqcreditos** (Periodicidad enum, proyeccion, amortizacion) | msgliqcre.CSharp | Creditos/Services | msgcop, msgdep, msgcnt, MsgImpCop, msginv | ~5 |
| 13 | **ClsDepositos** (BuscarCuentaAhorro, Navega enum) | msgdep.CSharp | Ahorros/Services | msgcop, msgliqcre, MsgDeb, mscpagonomi | ~4 |
| 14 | **Numeros_A_Letras** (Num_a_Letras, EnLetras) | MsgSas.CSharp | Compartido/Utilidades | msginv, msgcli, MsgImpCop, mscimpcdat | ~4 |
| 15 | **ClsInvConfig** (Navega enum, 80+ metodos config) | msginvconfig.CSharp | Inventario/Services | msginv, msgcli, msgtiket | 3 |
| 16 | **ImpreDoc** (impresion contable) | MsgImpCnt.CSharp | Contabilidad/Reportes | msgtes, msgimp, msgnom | 3 |
| 17 | **ClsMsgDeb** (tarjetas debito) | MsgDeb.CSharp | TarjetaDebito/Services | msgcop, msgcre | 2 |
| 18 | **clstesoreria** (OpPago, Estadofact enums) | msgtes.CSharp | Tesoreria/Services | msgcop, msgcnt | 2 |
| 19 | **msgnomconfig** (EstadoPeriodos enum) | msgnomconfig.CSharp | Nomina/Services | msgnom, msgimp | 2 |
| 20 | **clsmsgimp** (36 metodos impresion) | msgimp.CSharp | Nomina/Reportes | msgcli, msgnom | 2 |

---

## 3. DEPENDENCIAS EXTERNAS

> Todas las DLLs, paquetes NuGet y COM references que necesitara ERP.Core consolidado.
> Se excluyen las referencias estandar de .NET Framework (System.*, mscorlib).

### 3.1 Crystal Reports (la dependencia mas critica)

| Paquete | Version | Proyectos que lo Usan | Cantidad |
|---------|---------|----------------------|----------|
| CrystalDecisions.CrystalReports.Engine | 10.5.3700.0 | msgcnt, msgcop, msgliqcre, msginv, msgimp, msgcli, msgdep, mscimpcdat, MsgImpCnt, MsgImpCop, MsgSas, MsgCdats, msccircular, mscext01, mscext02, MscImpCert, MsgRecaudos, mscimpsol | **18** |
| CrystalDecisions.Windows.Forms | 10.5.3700.0 | (subset de los anteriores) | **17** |
| CrystalDecisions.Shared | 10.5.3700.0 | msgcnt, msgcop, msgliqcre, msgimp, msgcli, MsgImpCnt, MsgImpCop, MsgSas, MsgCdats, mscext02, MscImpCert | **14** |
| CrystalDecisions.ReportSource | 10.5.3700.0 | MsgImpCnt, MsgSas | **2** |
| CrystalDecisions.Enterprise.Framework | 10.5.3700.0 | MsgSas | **1** |
| CrystalDecisions.Enterprise.InfoStore | 10.5.3700.0 | MsgSas | **1** |

> **RIESGO:** Crystal Reports 10.5 (VS 2008) no es compatible con .NET 10. Requiere migracion a SAP Crystal Reports runtime for .NET o alternativa (RDLC, FastReport, etc.)

### 3.2 Telerik WinControls

| Paquete | Version | Proyectos | Cantidad |
|---------|---------|-----------|----------|
| Telerik.WinControls | 2011.1.11.419 | MsgSas | **1** |
| Telerik.WinControls.GridView | 2011.1.11.419 | MsgSas | **1** |
| Telerik.WinControls.UI | 2011.1.11.419 | MsgSas | **1** |
| Telerik.WinControls.UI.Design | 2011.1.11.419 | MsgSas | **1** |

> **RIESGO:** Telerik 2011 no soporta .NET 10. Requiere upgrade a Telerik UI for WinForms (actual) o reemplazo con controles nativos.

### 3.3 Microsoft.VisualBasic

| Paquete | Version | Proyectos | Cantidad |
|---------|---------|-----------|----------|
| Microsoft.VisualBasic | (framework) | ConectBd, MsgConfig, MsgSas, msgcnt, msgcop, msgliqcre, msgnom, msgtes, msginvconfig, msgcli, MsgCdats, msgcre, MsgDeb, msccircular, mscext01, mscext02, MsgImpCop, mscimpcdat | **18** |

> **NOTA:** Disponible en .NET 10 via paquete `Microsoft.VisualBasic.Compatibility` o namespace built-in. Las funciones mas usadas: `IsNumeric()`, `Interaction.CreateObject()`, `FileSystem.*`, `Information.*`.

### 3.4 COM Interop / DLLs Externas

| Componente | Version | Tipo | Proyectos | Uso |
|-----------|---------|------|-----------|-----|
| Interop.Excel | 1.5.0.0 | COM Interop | MsgConfig, arnomgen | Exportacion a Excel |
| Interop.Outlook | - | COM Interop | MsgConfig | Envio de email |
| Interop.Microsoft.Office.Core | 2.3.0.0 | COM Interop | arnomgen | Soporte Office |
| Interop.OpenCajon | 5.0.0.0 | COM Interop | msginv, msgtiket | Apertura cajon POS |
| NITGEN.SDK.NBioBSP | 1.1.2.0 | DLL externa | Nitgen | Lector huellas biometrico |
| solido.exe | - | EXE ref | msgliqcre | Referencia al ejecutable principal |

### 3.5 Resumen Consolidado para ERP.Core.csproj

```xml
<!-- Crystal Reports (REQUIERE MIGRACION) -->
<PackageReference Include="CrystalDecisions.CrystalReports.Engine" Version="10.5.3700.0" />
<PackageReference Include="CrystalDecisions.Shared" Version="10.5.3700.0" />
<PackageReference Include="CrystalDecisions.Windows.Forms" Version="10.5.3700.0" />
<PackageReference Include="CrystalDecisions.ReportSource" Version="10.5.3700.0" />
<PackageReference Include="CrystalDecisions.Enterprise.Framework" Version="10.5.3700.0" />
<PackageReference Include="CrystalDecisions.Enterprise.InfoStore" Version="10.5.3700.0" />

<!-- Telerik (REQUIERE MIGRACION) -->
<PackageReference Include="Telerik.WinControls" Version="2011.1.11.419" />
<PackageReference Include="Telerik.WinControls.GridView" Version="2011.1.11.419" />
<PackageReference Include="Telerik.WinControls.UI" Version="2011.1.11.419" />

<!-- VB Compatibility -->
<PackageReference Include="Microsoft.VisualBasic" />

<!-- COM Interop -->
<COMReference Include="Excel" />
<COMReference Include="Outlook" />
<COMReference Include="Microsoft.Office.Core" />
<COMReference Include="OpenCajon" />

<!-- Hardware -->
<Reference Include="NITGEN.SDK.NBioBSP" />
```

---

## 4. ORDEN DE MIGRACION SUGERIDO

> Estrategia: migrar desde las capas sin dependencias hacia las mas acopladas.
> Cada lote se puede compilar y verificar antes de avanzar al siguiente.
> El cluster circular (Lote 4) debe migrarse completo de una vez.

### Lote 0 — Infraestructura Base (0 dependencias internas)

| Prioridad | Proyecto | Modulo Destino | Archivos | Justificacion |
|-----------|----------|----------------|----------|---------------|
| 0.1 | ConectBd.CSharp | Compartido/Datos/ | 1 | Base absoluta: ClsConect + OdbcConect usado por TODO |
| 0.2 | SasToolBar.CSharp | Compartido/Controles/ | 2 | Toolbar sin deps (solo ConectBd en csproj, no en codigo) |

**Verificacion:** Compilar ERP.Core con solo estos 3 archivos. Debe compilar limpio.

---

### Lote 1 — Compartido: Utilidades y Configuracion

| Prioridad | Proyecto | Modulo Destino | Archivos | Depende de |
|-----------|----------|----------------|----------|------------|
| 1.1 | MsgSas.CSharp (completo) | Compartido/* | ~22 | ConectBd |
| 1.2 | MsgConfig.CSharp (parcial: ParamSys, VarIni, frmtasas, frmtasacptos, FrmActividades) | Compartido/Configuracion/ | 5 | ConectBd, SasToolBar, MsgSas.Ayuda |

**Verificacion:** ~30 archivos. Todo Compartido/ debe compilar. Estos son los cimientos que todos los demas modulos necesitan.

---

### Lote 2 — Modulos Isla (sin dependencias circulares)

| Prioridad | Proyecto | Modulo Destino | Archivos | Depende de |
|-----------|----------|----------------|----------|------------|
| 2.1 | Nitgen.CSharp | Seguridad/Biometria/ | 2 | ConectBd |
| 2.2 | MsgConfig → FrmLogeo, sys_ffirmas, frmempbloq | Seguridad/Forms/ | 3 | Compartido |
| 2.3 | msgnomconfig.CSharp | Nomina/Services/ + Forms/ | 3 | Compartido |
| 2.4 | msccircular.CSharp | CarteraFinanciera/Services/ | 1 | Compartido |
| 2.5 | MsgRecaudos.CSharp | Recaudos/Services/ | 1 | Compartido |
| 2.6 | mscext01.CSharp | Reportes/Services/ | 1 | MsgSas |
| 2.7 | mscext02.CSharp | Reportes/Services/ | 1 | Compartido |
| 2.8 | mscProcesoProd.CSharp | Produccion/Services/ | 1 | Compartido |

**Verificacion:** ~13 archivos adicionales. Cada uno compila independientemente contra Lotes 0-1.

---

### Lote 3 — Contabilidad (base para el cluster circular)

| Prioridad | Proyecto | Modulo Destino | Archivos | Depende de |
|-----------|----------|----------------|----------|------------|
| 3.1 | MsgConfig → ParamCnt | Contabilidad/Models/ | 1 | Compartido |
| 3.2 | msgcnt.CSharp (ClsContabilidad + Promedios + 5 Forms) | Contabilidad/Services/ + Forms/ | 12 | Compartido, ParamCnt |
| 3.3 | MsgImpCnt.CSharp | Contabilidad/Reportes/ | 2 | Compartido, Contabilidad |

> **NOTA:** ClsContabilidad tiene refs a msgcop, msgtes, msgliqcre pero como todo va al mismo ensamblado, se migra primero y las refs se resuelven cuando lleguen los Lotes 4-5.

**Verificacion:** Compilara con warnings de refs faltantes hasta que se complete Lote 4. Alternativa: migrar junto con Lote 4.

---

### Lote 4 — Cluster Circular (MIGRAR TODO JUNTO)

> Este es el lote critico. Estos 8 proyectos tienen dependencias bidireccionales
> y DEBEN migrarse en una sola operacion para que compilen.

| Prioridad | Proyecto | Modulo Destino | Archivos | Deps Circulares Con |
|-----------|----------|----------------|----------|--------------------|
| 4.1 | MsgConfig → ParamCop, ParamCdt, ParamTes | Varios/Models/ | 3 | - |
| 4.2 | MsgConfig → frmreferencias, FrmAgregarReferencia, frmtasasintcred | CarteraFinanciera/Forms/ | 3 | - |
| 4.3 | msgliqcre.CSharp | Creditos/ | ~27 | msgcop, msgdep, MsgImpCop |
| 4.4 | msgcop.CSharp | CarteraFinanciera/ | ~55 | msgcnt, msgliqcre, msgdep, msgtes, MsgDeb, msgcre |
| 4.5 | msgdep.CSharp | Ahorros/ | ~17 | msgcop, msgcnt, msgliqcre |
| 4.6 | MsgImpCop.CSharp | CarteraFinanciera/Reportes/ | 3 | msgliqcre |
| 4.7 | mscimpsol.CSharp | Creditos/Reportes/ | 1 | msgcop |
| 4.8 | MsgDeb.CSharp | TarjetaDebito/ | 2 | msgcop, msgdep |
| 4.9 | msgcre.CSharp | TarjetaCredito/ | 1 | msgcop, MsgDeb |
| 4.10 | MsgCdats.CSharp | CDT/Services/ | 1 | msgcop, msgcnt |
| 4.11 | mscimpcdat.CSharp | CDT/Reportes/ | 1 | msgcop, MsgCdats |
| 4.12 | MscImpCert.CSharp | CarteraFinanciera/Reportes/ | 1 | msgcop |
| 4.13 | MscAplNom.CSharp | CarteraFinanciera/Services/ | 1 | msgcop |
| 4.14 | msgtes.CSharp | Tesoreria/ | 4 | msgcnt, msgcop, MsgImpCnt |

**Total Lote 4:** ~120 archivos .cs — el corazon del sistema.

**Verificacion:** Compilar ERP.Core completo con Lotes 0-4. DEBE compilar limpio aqui. Si hay errores, son de namespace/using que se corrigen ajustando los `using` al nuevo patron `ERP.Core.[Modulo]`.

---

### Lote 5 — Inventario + POS

| Prioridad | Proyecto | Modulo Destino | Archivos | Depende de |
|-----------|----------|----------------|----------|------------|
| 5.1 | msginvconfig.CSharp | Inventario/Services/ | 1 | Compartido, Contabilidad |
| 5.2 | msginv.CSharp | Inventario/Services/ + Forms/ | ~19 | Compartido, Contabilidad, CarteraFinanciera, Creditos, msginvconfig |
| 5.3 | msgtiket.CSharp | Inventario/Services/ | 1 | Compartido, Contabilidad, msginvconfig, msginv |
| 5.4 | msgcli.CSharp | Inventario/Services/ | 1 | Compartido, Contabilidad, msginv, msginvconfig |

**Verificacion:** Compilar. Todo Inventario/ debe funcionar.

---

### Lote 6 — Nomina

| Prioridad | Proyecto | Modulo Destino | Archivos | Depende de |
|-----------|----------|----------------|----------|------------|
| 6.1 | msgimp.CSharp | Nomina/Reportes/ | 1 | Compartido, Contabilidad, msgnomconfig |
| 6.2 | msgnom.CSharp | Nomina/Services/ + Forms/ | ~14 | Compartido, Contabilidad, msgnomconfig, msgimp |
| 6.3 | mscpagonomi.CSharp | Nomina/Services/ | 1 | Compartido, CarteraFinanciera, Ahorros |
| 6.4 | arnomgen.CSharp | Nomina/Services/ + Forms/ | 4 | Compartido, CarteraFinanciera |

**Verificacion:** Compilar. Todo Nomina/ debe funcionar.

---

### Lote 7 — Verificacion Final

| Paso | Accion |
|------|--------|
| 7.1 | Compilacion completa de ERP.Core |
| 7.2 | Verificar que no quedan referencias a namespaces viejos (ConectBd, MsgConfig, msgcop, etc.) |
| 7.3 | Verificar que todos los recursos (.resx, .gif, .png) estan copiados a Properties/ |
| 7.4 | Ejecutar busqueda de `TODO` y `HACK` pendientes |
| 7.5 | Validar que solido.exe puede cargar ERP.Core.dll como plugin |

---

## Resumen Visual del Orden

```
Lote 0  ██  ConectBd + SasToolBar                    [3 archivos]
         │
Lote 1  ████  MsgSas + MsgConfig(parcial)            [~30 archivos]
         │
Lote 2  ██████  Modulos isla (8 proyectos simples)   [~13 archivos]
         │
Lote 3  ████  Contabilidad (msgcnt + MsgImpCnt)      [~15 archivos]
         │
Lote 4  ████████████████████████████████████████████  [~120 archivos]
        Cluster circular: msgcop + msgliqcre + msgdep + msgtes
        + MsgDeb + msgcre + MsgCdats + MsgImpCop + mscimpsol
        + mscimpcdat + MscImpCert + MscAplNom
         │
Lote 5  ████████  Inventario (msginv + config + tiket + cli)  [~22 archivos]
         │
Lote 6  ██████  Nomina (msgnom + config + imp + pago)  [~20 archivos]
         │
Lote 7  ██  Verificacion final
```

**Total estimado:** ~205 archivos .cs + ~30 .resx + ~7 recursos graficos

---

## Riesgos Principales de la Migracion

| # | Riesgo | Impacto | Mitigacion |
|---|--------|---------|------------|
| 1 | **Crystal Reports 10.5 incompatible con .NET 10** | ALTO - 18 proyectos afectados | Evaluar SAP Crystal Runtime .NET o migrar a RDLC/FastReport |
| 2 | **Telerik 2011 incompatible con .NET 10** | MEDIO - solo MsgSas | Upgrade a Telerik UI for WinForms moderno o reemplazar 3 controles |
| 3 | **COM Interop (Excel, Outlook, OpenCajon)** | MEDIO - 4 proyectos | Usar OpenXML para Excel, MailKit para email, verificar driver POS |
| 4 | **Cluster circular de 8 proyectos** | ALTO - 120 archivos | Migrar todo junto en Lote 4, no intentar hacerlo incremental |
| 5 | **Referencia a solido.exe desde msgliqcre** | BAJO - 1 proyecto | Extraer interfaz o eliminar referencia directa al .exe |
| 6 | **Microsoft.VisualBasic (18 proyectos)** | BAJO - compatible | Disponible en .NET 10 via paquete NuGet, migrar gradualmente a equivalentes C# |
| 7 | **NITGEN SDK biometrico** | BAJO - 1 proyecto | Verificar que el SDK tiene version .NET 10 o usar wrapper |
