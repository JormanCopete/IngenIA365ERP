# Analisis del Cluster Circular - Lote 5

> Generado: 2026-03-15
> Pre-requisito para migrar los 12 proyectos del cluster circular a ERP.Core.

---

## 1. INVENTARIO DE LOS 12 PROYECTOS

| # | Proyecto | Archivos .cs | Lineas | Clase principal | Partials |
|---|----------|-------------|--------|-----------------|----------|
| 1 | **msgcop.CSharp** | 54 | 40,202 | Clscartera | 13 partes + 20 Forms(x2) |
| 2 | **msgliqcre.CSharp** | 27 | ~28,000 | ClsLiqcreditos | 4 partes + SolicitudCredito(3) + 8 Forms |
| 3 | **msgdep.CSharp** | 16 | 7,338 | ClsDepositos | 6 Forms(x2) |
| 4 | **msgtes.CSharp** | 4 | 2,916 | clstesoreria | FrmPagoFact(x2) |
| 5 | **MsgDeb.CSharp** | 3 | 1,859 | ClsMsgDeb | FrmCupoTarj(x2) |
| 6 | **msgcre.CSharp** | 1 | 1,588 | Clstarjcredito | Ninguno |
| 7 | **MsgImpCop.CSharp** | 4 | 1,704 | ImpreDoc | Ordencomercio(x2) |
| 8 | **MscImpCert.CSharp** | 1 | 176 | ClsImpCert | Ninguno |
| 9 | **mscimpsol.CSharp** | 1 | 795 | clsimpsol | Ninguno |
| 10 | **mscimpcdat.CSharp** | 1 | 226 | clsimpcdats | Ninguno |
| 11 | **MsgCdats.CSharp** | 1 | 1,078 | ClsMsgCdats | Ninguno |
| 12 | **MscAplNom.CSharp** | 1 | 753 | AplicaDstos | Ninguno |
| | **TOTAL** | **114** | **~86,635** | | |

---

## 2. MATRIZ DE DEPENDENCIAS (dentro del cluster)

> Filas = "este proyecto DEPENDE DE" → Columnas = "este proyecto"
> X = dependencia directa en codigo. Celdas vacias = sin dependencia.
> Nota: dependencias a Compartido, Contabilidad y modulos ya migrados NO se muestran.

| Depende de ↓ \ Usa → | cop | liqcre | dep | tes | Deb | cre | ImpCop | ImpCert | impsol | impcdat | Cdats | AplNom |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| **msgcop** | — | X | X | X | X | X | | | | | | |
| **msgliqcre** | X | — | X | | | | X | | X | | | |
| **msgdep** | X | X | — | | | | | | | | | |
| **msgtes** | X | | | — | | | | | | | | |
| **MsgDeb** | X | | X | | — | | | | | | | |
| **msgcre** | X | | | | X | — | | | | | | |
| **MsgImpCop** | | X | | | | | — | | | | | |
| **MscImpCert** | X | | | | | | | — | | | | |
| **mscimpsol** | X | | | | | | | | — | | | |
| **mscimpcdat** | X | | | | | | | | | — | X | |
| **MsgCdats** | X | | | | | | | | | | — | |
| **MscAplNom** | X | | | | | | | | | | | — |

### Conteo de dependencias:

| Proyecto | Depende de (sale) | Es usado por (entra) |
|----------|-------------------|---------------------|
| **msgcop** | 5 (liqcre, dep, tes, Deb, cre) | **10** (todos menos si mismo y MsgImpCop) |
| **msgliqcre** | 4 (cop, dep, ImpCop, impsol) | 3 (cop, dep, MsgImpCop) |
| **msgdep** | 2 (cop, liqcre) | 3 (cop, liqcre, MsgDeb) |
| **msgtes** | 1 (cop) | 1 (cop) |
| **MsgDeb** | 2 (cop, dep) | 2 (cop, cre) |
| **msgcre** | 2 (cop, Deb) | 1 (cop) |
| **MsgImpCop** | 1 (liqcre) | 1 (liqcre) |
| **MscImpCert** | 1 (cop) | 0 |
| **mscimpsol** | 1 (cop) | 1 (liqcre) |
| **mscimpcdat** | 2 (cop, Cdats) | 0 |
| **MsgCdats** | 1 (cop) | 1 (impcdat) |
| **MscAplNom** | 1 (cop) | 0 |

---

## 3. CICLOS EXACTOS

### Ciclos directos (A ↔ B):

| Ciclo | Proyecto A → Proyecto B | Proyecto B → Proyecto A |
|-------|------------------------|------------------------|
| **C1** | msgcop → msgliqcre (7 refs: ClsLiqcreditos en Part4,5,6b,7,8) | msgliqcre → msgcop (30+ refs: Clscartera en LiqPart2,3,4) |
| **C2** | msgcop → msgdep (4 refs: ClsDepositos en Part4) | msgdep → msgcop (10 refs: Clscartera en ClsDepositos, Forms) |
| **C3** | msgcop → msgtes (5 refs: clstesoreria en Part3,4, cop_cuadredoc) | msgtes → msgcop (5 refs: Clscartera en FrmPagoFact, clstesoreria) |
| **C4** | msgcop → MsgDeb (2 refs: ClsMsgDeb en Part3, frmrectarj01) | MsgDeb → msgcop (1 ref: Clscartera en ClsMsgDeb) |
| **C5** | msgcop → msgcre (2 refs: Clstarjcredito en frmrectarj01) | msgcre → msgcop (14 refs: Clscartera en Clstarjcredito) |
| **C6** | msgliqcre → msgdep (2 refs: ClsDepositos en Part3) | msgdep → msgliqcre (4 refs: ClsLiqcreditos en ClsDepositos) |
| **C7** | msgliqcre → MsgImpCop (3 refs: ImpreDoc en Part3, SolicitudCredito) | MsgImpCop → msgliqcre (1 ref: ClsLiqcreditos en ImpreDoc) |

### Ciclos de 3+ (A → B → C → A):

| Ciclo | Cadena |
|-------|--------|
| **T1** | msgcop → msgliqcre → msgdep → msgcop |
| **T2** | msgcop → MsgDeb → msgdep → msgcop |
| **T3** | msgcop → msgcre → MsgDeb → msgcop |
| **T4** | msgcop → msgliqcre → MsgImpCop → msgliqcre (no vuelve a cop, es sub-ciclo) |

---

## 4. CLASES ESPECIFICAS QUE CAUSAN CIRCULARIDAD

### Ciclo C1: msgcop ↔ msgliqcre

```
msgcop.Clscartera (Part4.cs:36)   usa   msgliqcre.ClsLiqcreditos (new)
msgcop.Clscartera (Part5.cs)      usa   msgliqcre.ClsLiqcreditos.Periodicidad (enum)
msgcop.Clscartera (Part7.cs)      usa   msgliqcre.ClsLiqcreditos.GeneraProyeccion()
---
msgliqcre.ClsLiqcreditos (Part2)  usa   msgcop.Clscartera.BuscaAsociado()
msgliqcre.ClsLiqcreditos (Part3)  usa   msgcop.Clscartera.GrabaMovimiento()
msgliqcre.ClsLiqcreditos (Part4)  usa   msgcop.Clscartera.BuscaLinea()
```

### Ciclo C2: msgcop ↔ msgdep

```
msgcop.Clscartera (Part4.cs:36)   usa   msgdep.ClsDepositos (new)
msgcop.Clscartera (Part4.cs:934)  usa   msgdep.ClsDepositos.BuscarCuentaAhorro()
msgcop.Clscartera (Part4.cs:159)  usa   msgdep.ClsDepositos.ElimanarCanje()
---
msgdep.ClsDepositos                usa   msgcop.Clscartera.BuscaAsociado()
msgdep.ClsDepositos                usa   msgcop.Clscartera.BuscaLinea()
```

### Ciclo C3: msgcop ↔ msgtes

```
msgcop.Clscartera (Part3.cs:1904) usa   msgtes.clstesoreria (new)
msgcop.Clscartera (Part4.cs:26)   usa   msgtes.clstesoreria (new)
msgcop.cop_cuadredoc               usa   msgtes.clstesoreria (new)
---
msgtes.clstesoreria                usa   msgcop.Clscartera.BuscaAsociado()
msgtes.FrmPagoFact                 usa   msgcop.Clscartera (new)
```

### Ciclo C4: msgcop ↔ MsgDeb

```
msgcop.Clscartera (Part3.cs:1916) usa   MsgDeb.ClsMsgDeb (new)
msgcop.frmrectarj01                usa   MsgDeb.ClsMsgDeb (new)
---
MsgDeb.ClsMsgDeb                   usa   msgcop.Clscartera (enums, metodos)
```

### Ciclo C5: msgcop ↔ msgcre

```
msgcop.frmrectarj01 (line 14)     usa   msgcre.Clstarjcredito (new)
---
msgcre.Clstarjcredito              usa   msgcop.Clscartera.BuscaAsociado() (14 refs)
```

### Ciclo C6: msgliqcre ↔ msgdep

```
msgliqcre.ClsLiqcreditos (Part3)  usa   msgdep.ClsDepositos.BuscarCuentaAhorro()
---
msgdep.ClsDepositos (lines 2352+) usa   msgliqcre.ClsLiqcreditos (new, 4 refs)
```

### Ciclo C7: msgliqcre ↔ MsgImpCop

```
msgliqcre.ClsLiqcreditos (Part3)  usa   MsgImpCop.ImpreDoc (new, 3 refs)
msgliqcre.SolicitudCredito         usa   MsgImpCop.ImpreDoc (new)
---
MsgImpCop.ImpreDoc (line 17)       usa   msgliqcre.ClsLiqcreditos (new)
```

---

## 5. CLASIFICACION EN NIVELES

### NIVEL 3 — NUCLEO CIRCULAR (6 proyectos, deben coexistir)

Estos 6 proyectos tienen dependencias bidireccionales entre si.
**No se puede migrar uno sin el otro.** Deben ir todos juntos.

| Proyecto | Lineas | Rol en el ciclo |
|----------|--------|-----------------|
| **msgcop** | 40,202 | Hub central — 10 proyectos dependen de el |
| **msgliqcre** | ~28,000 | Segundo hub — liquidacion de creditos |
| **msgdep** | 7,338 | Ahorros — ciclos con cop y liqcre |
| **msgtes** | 2,916 | Tesoreria — ciclo con cop |
| **MsgDeb** | 1,859 | Tarjeta debito — ciclo con cop |
| **msgcre** | 1,588 | Tarjeta credito — ciclo con cop via MsgDeb |

### NIVEL 2 — SATELITES CON CICLO (1 proyecto)

Tienen un ciclo con un proyecto del nucleo pero son mas pequenos.

| Proyecto | Lineas | Ciclo con |
|----------|--------|-----------|
| **MsgImpCop** | 1,704 | msgliqcre (ciclo C7) |

### NIVEL 1 — HOJAS (5 proyectos, sin ciclos entre si)

Solo dependen de proyectos del nucleo. Nadie dentro del cluster depende de ellos
(excepto mscimpcdat que depende de MsgCdats).

| Proyecto | Lineas | Solo depende de |
|----------|--------|-----------------|
| **MscImpCert** | 176 | msgcop |
| **mscimpsol** | 795 | msgcop |
| **MsgCdats** | 1,078 | msgcop |
| **mscimpcdat** | 226 | msgcop + MsgCdats |
| **MscAplNom** | 753 | msgcop |

---

## 6. ESTRATEGIA DE MIGRACION RECOMENDADA

Dado que todo va a UN SOLO ensamblado (ERP.Core), los ciclos no causan
errores de compilacion. El orden importa solo para verificacion incremental.

### Sub-lote 5A: NUCLEO + SATELITES (7 proyectos, ~83K lineas)
Migrar juntos en una sola operacion:
1. msgcop → CarteraFinanciera/Services/ + Forms/
2. msgliqcre → Creditos/Services/ + Forms/
3. msgdep → Ahorros/Services/ + Forms/
4. msgtes → Tesoreria/Services/ + Forms/
5. MsgDeb → TarjetaDebito/Services/ + Forms/
6. msgcre → TarjetaCredito/Services/
7. MsgImpCop → CarteraFinanciera/Reportes/

### Sub-lote 5B: HOJAS (5 proyectos, ~3K lineas)
Migrar despues, una vez que el nucleo compile:
8. MscImpCert → CarteraFinanciera/Reportes/
9. mscimpsol → Creditos/Reportes/
10. MsgCdats → CDT/Services/
11. mscimpcdat → CDT/Reportes/
12. MscAplNom → CarteraFinanciera/Services/

### Resultado esperado:
- Al completar 5A+5B: **0 errores** (todos los ciclos se resuelven en el mismo ensamblado)
- El error preexistente de FrmFormPago.cs (msgcop.Clscartera) se resuelve
- El error de cnt_cuadredoc.cs (msgtes.clstesoreria) se resuelve
- **25 de 33 proyectos absorbidos** (76%)
