# Quickstart: Cartera financiera — líneas, originación, causación, pagos, riesgo y cobranza

**Feature**: 013 | **Date**: 2026-10-05 | **Rama**: `013-cartera-financiera`

Cómo se prueba y se ensaya la feature, entrega por entrega (K1 a K6b): primero lo automático (casos dorados,
Application, arquitectura, e2e con Testcontainers en los dos motores y volumen), después el ensayo manual en
DEV o QA sobre cooperativas de ensayo, después el ensayo de COOFLOPAL y las condiciones para salir en vivo, y
por último lo que validan el dueño, la contadora y el oficial de cumplimiento. Todo lo que aquí se afirma sobre
un ambiente se comprueba **contra la base del ambiente**, nunca contra el repositorio. Los nombres de tablas,
motores, puertos, comandos, mensajes, permisos, rutas, pantallas, parámetros, alertas, tareas, errores y
pruebas son los de [decisiones-transversales.md](decisiones-transversales.md) (§2, decisiones T1–T102, choques
CH1–CH50, preguntas A1–I17 y E11, y la conciliación C01–C90 de la §7); «DT §n» remite a una sección de ese documento y «§n», a una de éste. Lo que este
documento tuvo que nombrar por su cuenta está al final, en «Nombres fijados en este documento».

Cinco decisiones mandan sobre todo lo que sigue:

- **Sin Recaudo ni Tesorería no hay salida en vivo** (Q1 B, FR-089). En los ambientes de prueba Cartera se
  ejercita con dos **dobles de prueba**, `RecaudadorDePrueba` y `TesoreriaDePrueba`, que sólo existen si
  `Lending:TestDoubles:Enabled = true` **y** el despliegue no es de producción (T20). No son una forma de operar.
- **Tres conceptos distintos con nombres distintos** (CH20): los dobles de prueba (nunca en producción; la API
  no arranca si se piden allí), los **canales simulados** `SIMULADO` (admitidos en producción, antes y después
  de activar, siempre sin efecto legal, T73) y los dobles unitarios (sólo en `tests/`).
- **Modelos de deterioro** (Q2 A): el Anexo 1 en K1, la pérdida esperada en K6a y vivienda en K6b. COOFLOPAL
  no recibe ahorros: no está obligada y sale con el Anexo 1.
- **Las cuotas de servicios a asociados no son cartera** (Q3, D29): Comercial las vende con
  `CreditWithoutLending` (enmienda A6) y no llegan a Cartera.
- **Ninguna fecha fija.** El ensayo y la salida los fija el dueño. Los meses se nombran «M» (el mes que contiene
  la activación del ensayo) y «M+1» (el siguiente); «C» es la fecha de corte de la carga de migración y la
  activación se fecha C+1 (FR-142).

## 1. Pruebas automáticas

### 1.1 Compilar y migrar

```bash
dotnet build IngenIA365ERP.CI.slnf -c Release
pwsh tools/scripts/check-migration-parity.ps1
```

`check-migration-parity.ps1` en verde en los contextos `Admin` y `Application`. Toda migración se genera en par
con `tools/scripts/add-migration.ps1 -Name <Nombre> -Context Application|Admin` y entra en este orden (T88 y DT §2.26,
enmendados por C103; los pares de K1 se regeneran hasta que la última tarea de K1 los congela, C104):

| E | Migración | Tipo | Qué comprobar |
|---|---|---|---|
| K1 | `ValoresNacionales` (Admin) | aditiva | `ADM_NationalValues`, `ADM_RegulatoryDocuments` |
| K1 | `PlataformaParaCartera` | aditiva | tablas `COR_` de DT §2.5, columnas de DT §2.6, fila `lending.process` y semilla del registro de participantes |
| K1 | `NominaRecaudaPorElContrato` | aditiva | `PAY_PayrollLoanDeductions`; `PAY_PayrollRuns.LendingActivationAck*`; `ObligationPublicId` y `ObligationNumber` en `PAY_SettlementDeductions` y `PAY_Novelties` con la copia idempotente desde `LND_LoanPortfolios` |
| K1 | `ComercioConCartera` | aditiva | `INV_DocumentPayments.ReservedLoanNumber`, `CreditLineCode`, `CreditWithoutLending`; `COR_PaymentMeans.CreditWithoutLending` (sujetas a A6); `INV_LendingObligationResponses` |
| K1 | `RetiroDeLaCarteraHeredada` | **destructiva con guarda** | commit propio, cabecera `MIGRACION-DESTRUCTIVA-APROBADA`, guarda con `THROW 50013` en SQL Server; `Down()` recrea vacío (T87) |
| K1 | `CarteraNucleo`, `CarteraRiesgo`, `IntegracionContableDeCartera` | aditivas | tablas `LND_` de K1 (con `LND_IssuedDocument*`, C88, y `LND_ProvisionalCollections`, C44) y `ACC_Lending*`, `ACC_RegulatoryIndicators` |
| K2 | `CarteraLibranza` | aditiva | `LND_PayrollAgreement*`, `LND_PayrollDeductionFile*` |
| K3 | `CarteraNovedades` | aditiva | `LND_LoanNovelties`, `LND_Waivers`, `LND_WriteOffBatch*`, `LND_PortfolioEvaluation*` |
| K4 | `CarteraCobranza` | aditiva | tablas de cobranza de DT §2.3 |
| K5 | `CarteraInformes` | aditiva | `LND_RegulatoryFiles` |
| K6a | `CarteraPerdidaEsperada` | aditiva | `LND_ImpairmentModelElections`, `LND_ArrearsHistoryLoads` |
| K6b | `CarteraVivienda` | aditiva, sólo si hace falta | columnas UVR (`ValorUvr` 18,4) |

Ninguna migración aditiva trae operaciones sobre las 38 tablas conservadas (cambian de espacio de nombres a
`Savings`, no de `ToTable`, T3). `PrincipioXII_MigracionesDestructivas` y `Feature004_MigrationParity` en verde.

### 1.2 Dominio · casos dorados del motor financiero

```bash
dotnet test tests/IngenIA365ERP.Domain.Tests --filter "FullyQualifiedName~Lending|FullyQualifiedName~Common|FullyQualifiedName~Payroll"
```

Los casos viven en `Domain.Tests/Lending/Casos/*.json`, los carga `CasoDoradoDeCartera` y los corre
`CasosDoradosDeCarteraTests` (molde `CasoDoradoDeCosteo`: JSON con comentarios, claves en español y enums por
nombre). Cada caso trae las condiciones selladas de la línea, las operaciones con **fecha valor y fecha de
registro** (R), las series (usura por modalidad certificada, IBR, festivos nacionales) y lo esperado **por día,
cuota y concepto, al centavo**. Se calculan a mano con decimal de 40 dígitos (Python `decimal`, con la hoja
versionada junto a los casos), nunca en Excel (T26), para que la contadora los pueda leer y firmar. Forma de un
caso (los importes van completos en el archivo):

```jsonc
{
  // US5-1: dos cuotas vencidas y un valor libre; cubre completa la más antigua antes de la siguiente.
  "nombre": "Dos cuotas vencidas con valor libre",
  "condiciones": { "sistema": "LevelPayment", "convencion": "Thirty360", "equivalencia": "Compound",
                   "redondeo": { "unidad": 0.01, "modo": "AwayFromZero", "momento": "PerConcept" },
                   "prelacion": "OldestInstallmentFirst", "monto": 1200000, "plazoCuotas": 12, "tasaEA": 0.240000 },
  "series": { "usura": [ { "clave": "CONSUMO", "desde": "2026-09-01", "ea": 0.260000, "registradoEn": "2026-08-31T15:00:00Z" } ],
              "festivos": [ "2026-10-12" ] },
  "operaciones": [
    { "id": "D1", "tipo": "Disbursement", "fechaValor": "2026-08-01", "registradoEn": "2026-08-01T14:00:00Z" },
    { "id": "P1", "tipo": "Payment", "modalidad": "FreeAmount", "valor": "…", "fechaValor": "2026-10-20",
      "registradoEn": "2026-10-20T16:00:00Z",
      "esperado": { "distribucion": [ { "cuota": 1, "concepto": "DefaultInterest", "valor": "…" } ], "excedente": 0 } }
  ],
  "consultas": [ { "fecha": "2026-10-31", "informacionHasta": "2026-10-31T23:59:59Z",
                   "esperado": { "saldos": { "Principal": "…", "CurrentInterest": "…" }, "diasDeMora": 0 } } ]
}
```

**K1, unos treinta casos** (el mínimo de DT §2.25, ampliado con los defectos D1–D14):

| # | Archivo | Qué fija | Criterio |
|---|---|---|---|
| 01 | `01-cuota-fija-30-360` | Cuota fija mensual 30/360: el día 31 no causa, el fin de febrero completa 30; Σ capital = monto y la última cuota absorbe el residuo | SC-017, SC-023 |
| 02 | `02-cuota-fija-real-365-bisiesto` | Real/365 de base fija con el 29-02-2028 causando (D3) | SC-023 |
| 03 | `03-cuota-fija-real-360-usura` | Real/360 sobre una EA sube la efectiva: `ControlDeUsura` compara la TIR diaria con cargos computables, no la nominal, y bloquea al simular | SC-002 |
| 04 | `04-abono-constante` | `ConstantPrincipal` | SC-017 |
| 05 | `05-cuotas-extraordinarias` | Cuota fija con extraordinarias pactadas | SC-017 |
| 06 | `06-pago-unico` | `Bullet` | SC-017 |
| 07 | `07-gracia-a-capital` | `GraceKind.Principal` | SC-017 |
| 08 | `08-gracia-total-primera-cuota` | Intereses de gracia sin capitalizar, cobrados en la primera cuota | SC-017 |
| 09 | `09-gracia-total-distribuida` | Los mismos, distribuidos en las cuotas siguientes | SC-017 |
| 10 | `10-vencimiento-en-festivo` | Art. 829: se paga el siguiente hábil sin mora, el sábado es hábil (D2) y el interés de esos días se cobra con la cuota siguiente | SC-023 |
| 11 | `11-dos-cuotas-vencidas-valor-libre` | Prelación `OldestInstallmentFirst`: gastos informados, seguros, mora con su suspenso, corriente con su suspenso, capital (US5-1) | SC-023 |
| 12 | `12-suspension-y-reanudacion` | Consumo a 60 días: lo nuevo en `CurrentInterestInSuspense`, lo causado antes no se reclasifica; reanuda al ponerse al día (US4-3) | SC-023 |
| 13 | `13-usura-baja` | Usura nueva menor que la pactada: desde ese día causa con la usura, fija y variable (US1-4) | SC-002 |
| 14 | `14-correccion-de-usura` | Usura corregida con `SupersedesPublicId` después de causar: la liquidación con R anterior no cambia; con R posterior, ajuste y saldo a favor (T28) | SC-001, SC-002 |
| 15 | `15-tasa-variable-ibr` | Margen sumado en la expresión del índice, a EA, piso, techo y usura (D10); último índice publicado a la fecha de reajuste | SC-023 |
| 16 | `16-abono-extraordinario-plazo-o-cuota` | Una simulación con los dos planes; se aplica la elección; sin preferencia queda saldo a favor (D24) | SC-003 |
| 17 | `17-anticipo-tres-cuotas` | El anticipo cubre cada cuota al vencer, no reduce intereses y se aplica parcial si no alcanza (D6) | SC-023 |
| 18 | `18-cuota-del-ciclo-antes-del-vencimiento` | No reduce intereses; el resto queda como anticipo de esa cuota (D8) | SC-023 |
| 19 | `19-fecha-valor-anterior` | Pago con fecha valor anterior a otro ya aplicado: redistribuye el posterior (D4) | SC-004 |
| 20 | `20-reverso-con-pago-posterior` | El crédito queda como si el pago no hubiera existido, con el posterior redistribuido (US5-5) | SC-004 |
| 21 | `21-reverso-de-pago-que-cancelo` | El crédito se reabre con sus cuotas, su mora y su altura | SC-004 |
| 22 | `22-dos-dias-sin-causar` | Puesta al día de a un día, con la tasa de cada día y sin duplicar (US4-4) | SC-020 |
| 23 | `23-liquidacion-futura` | Sin pagos intermedios, con la última usura conocida y `ValidaHasta` (D11, US4-5) | SC-001 |
| 24 | `24-tolerancia-de-saldo-residual` | Un vencido menor que `Mora.ToleranciaDeSaldoVencido` no cuenta días | SC-023 |
| 25 | `25-nota-credito-parcial-y-anulacion` | Ajuste de Comercial: baja el capital, reversa el interés de la parte devuelta y recalcula el plan; la anulación con pagos deja saldo a favor (US6-5) | SC-023 |
| 26 | `26-credito-migrado-con-suspenso` | Saldo inicial por concepto al corte, capital por cuota conservado e interés causado por el motor desde el corte (D5) | SC-013 |
| 27 | `27-periodicidad-semanal-y-quincenal` | 7 y 15 salen del valor del enum de periodicidad, nunca como literal (CH39) | SC-017 |
| 28 | `28-seguro-sobre-saldo` | Seguro causado al vencer la cuota, sobre el saldo inicial del período (D9) | SC-023 |
| 29 | `29-equivalencia-lineal` | Equivalencia `Linear` (D1) frente a la compuesta | SC-023 |
| 30 | `30-desembolso-por-tramos` | El plan se regenera sobre lo desembolsado en cada tramo (D12) | SC-017 |
| 31 | `31-abono-mayor-que-el-capital` | Se trata como cancelación y el excedente vuelve al recaudador (D5) | SC-023 |

**Casos de entregas posteriores** (mismo cargador):

| # | Archivo | E | Qué fija |
|---|---|---|---|
| 40 | `40-prorroga` | K3 | Versión nueva del plan; conserva número e historial |
| 41 | `41-cambio-de-fecha-de-pago` | K3 | Ídem |
| 42 | `42-cambio-de-tasa` | K3 | Ídem, con la usura vigente como tope |
| 43 | `43-novacion` | K3 | Crédito enlazado que hereda marca, conteo, calificación e historial |
| 44 | `44-condonacion-de-mora` | K3 | Operación `Waiver` por concepto |
| 45 | `45-castigo-y-recuperacion` | K3 | Conceptos en suspenso en control; el pago sobre el castigado es `Recovery` |
| 50 | `50-descontado-pendiente-de-giro` | K2 | `AlturaDeMora` excluye las cuotas cubiertas por un «descontado pendiente de giro» dentro del umbral y las cuenta al superarlo (SC-016) |
| 51 | `51-pagaduria-con-referencia` | K2 | Fecha valor = la del descuento, sin el límite de D4 (A9) |
| 60 | `60-vivienda-uvr` | K6b | Capital en UVR con su valor diario, tasa fija, sin mora sobre intereses (`RulesVersion` nueva) |

**Propiedades y rendimiento del motor** (sin base, sin reloj real):

| Prueba | Qué fija | Criterio |
|---|---|---|
| `SumaDeCapitalDelPlanTests` | Montos, plazos, sistemas, convenciones, periodicidades y gracias al azar: Σ capital del plan = monto, al centavo | SC-017 |
| `ReproducibilidadDeLiquidacionTests` | Misma entrada → misma huella `HuellaDeCartera` y mismo resultado, a D pasada, de hoy y futura, repetido; con R anterior a un pago posterior, idéntico (US4-6) | SC-001 |
| `ReversoComoSiNoHubieraExistidoTests` | Secuencias de pagos al azar: reversar uno = reconstruir sin él (saldos por concepto, cuotas, días de mora, suspensión), con y sin pagos posteriores | SC-004 |
| `ElReversoEnlazaCadaFilaConSuOriginalTests` | Cada fila propia de la operación reversada recibe su espejo con `ReversesTransactionId`; el desembolso reversado espeja también su causación (T33) | SC-004, FR-002 |
| `CausacionIdempotenteTests` | Causar d dos veces equivale a una; causar d sin `LastAccruedDate = d−1` se rechaza | SC-020 |
| `RendimientoDeCausacionTests` | 50.000 créditos sintéticos, motor solo, con calentamiento: menos de 60 s. **Corre siempre**, sin bandera, como `RendimientoTests` de Nómina (§7.6 de las transversales) | SC-007 (cota del motor) |

### 1.3 Dominio · casos dorados de riesgo

En `Domain.Tests/Lending/Risk/Casos/*.json`, con el cargador `CasoDoradoDeRiesgo` y la clase
`CasosDoradosDeRiesgoTests`. Entran la política efectiva (`PoliticaSarcEfectiva.Combinar` de norma y política),
la altura de mora que entrega la reconstrucción, las garantías y el historial; sale, por crédito, la
calificación con su causa (`RatingCause`), la subcategoría, la calificación contable A–E y el deterioro
desglosado, con la explicación. Mientras rija D19: sin excepción de aportes en el arrastre ni descuento de
aportes en el deterioro.

| # | Archivo | E | Qué fija | Criterio |
|---|---|---|---|---|
| r01 | `r01-rangos-por-modalidad` | K1 | Rangos A–E de consumo, comercial, microcrédito y productivas, con la altura de mora en días hábiles | SC-021 |
| r02 | `r02-subcategorias` | K1 | Subcategorías con su condición tipada | SC-021 |
| r03 | `r03-arrastre-por-modalidad` | K1 | Consumo en C y consumo en A quedan en C; el comercial sigue en A (US9-1) | SC-021 |
| r04 | `r04-arrastre-no-alcanza-codeudas` | K1 | El codeudor que es deudor en mora no arrastra sus codeudas | SC-021 |
| r05 | `r05-reestructurado` | K1 | Calificación al reestructurar, mejora de una categoría por tramo de cuotas consecutivas y recaída (US11-4) | SC-021 |
| r06 | `r06-pago-unico` | K1 | Regla especial de pago único (US9-5) | SC-021 |
| r07 | `r07-credito-nuevo` | K1 | Regla del crédito nuevo | SC-021 |
| r08 | `r08-analisis-solo-peor` | K1 | La calificación por análisis nunca mejora la de las reglas (US9-4) | SC-021 |
| r09 | `r09-insolvencia` | K1 | La insolvencia mantiene la calificación (FR-098) | SC-021 |
| r10 | `r10-deterioro-individual-anexo1` | K1 | Base = capital − garantía admisible × porcentaje por meses de mora; conceptos en suspenso al 100 % | SC-023 |
| r11 | `r11-deterioro-general` | K1 | Mínimo del perfil más el adicional, por dimensión | SC-023 |
| r12 | `r12-politica-mas-estricta` | K1 | La política del consejo endurece celda por celda; si la norma se endurece después, gana la norma | SC-023 |
| r13 | `r13-redondeo-del-deterioro` | K1 | `Riesgo.RedondeoDeterioro` al centavo y al peso (E9) | SC-023 |
| r14 | `r14-homologacion-contable` | K1 | Calificación contable A–E y subcategorías a su letra | SC-021 |
| r15 | `r15-selector-sin-k6a` | K1 | Cooperativa obligada sin K6a: Anexo 1 con la marca `ObligatoryModelUnavailable` (D28) | SC-023 |
| r20 | `r20-evaluacion-de-cartera` | K3 | La evaluación vigente recalifica hasta la siguiente | SC-021 |
| pe01 | `pe01-consumo-con-libranza` | K6a | PI con `FuncionLogistica` en decimal, PDI por días, VEA y deterioro crédito por crédito | SC-023 |
| pe02 | `pe02-consumo-sin-libranza` | K6a | Ídem | SC-023 |
| pe03 | `pe03-comercial-persona-natural` | K6a | Ídem | SC-023 |
| pe04 | `pe04-historial-parcial-mayor-observada` | K6a | Historial de mora parcial con `Riesgo.ReglaMesesFaltantes = MayorObservada` | SC-023 |
| pe05 | `pe05-historial-parcial-del-corte` | K6a | Ídem con `DelCorte` | SC-023 |
| pe06 | `pe06-historial-parcial-fija` | K6a | Ídem con `Fija` | SC-023 |
| pe07 | `pe07-pi-voluntaria` | K6a | PI con la clave `VOLUNTARIA` del documento `SarcAnexo2` | SC-023 |
| pe08 | `pe08-homologacion` | K6a | `HomologacionDeCalificacion` de pérdida esperada a A–E | SC-021 |
| pe09 | `pe09-segmento-fuera-del-modelo` | K6a | En una obligada, los segmentos no cubiertos siguen el Anexo 1 (US9-6) | SC-023 |
| pe10 | `pe10-novacion-hereda-historial` | K6a | `HistorialDeMora.Construir` sigue novaciones y refinanciaciones | SC-023 |

### 1.4 Dominio · los demás motores puros

| Motor | Prueba y casos | Qué fija | Criterio |
|---|---|---|---|
| `EvaluadorDeVotacion` | `CasosDoradosDeVotacionTests` sobre `Domain.Tests/Lending/Sessions/Casos/*.json` | Quórum sobre la composición vigente; mayoría ordinaria; vinculados con 5 × favorables ≥ 4 × composición, con el interesado excluido y la composición como base (US2-7); radicador excluido (US2-6); suplente por principal ausente y, sin actividad financiera, la mayoría de los estatutos (F7) | SC-015 |
| `EnrutadorDeAtribuciones`, `NoExceptuables` | `CasosDoradosDeAtribucionesTests` sobre `Domain.Tests/Lending/Attributions/Casos/*.json` | Nivel efectivo = el mayor entre techo de monto consolidado, reglas, consejo por vinculado y excepción por puntaje o alertas; los niveles no se acumulan; nunca exceptuables la falta de capacidad, de autorización, la coincidencia en listas ni la usura | SC-015 |
| `ReglasDeSegregacion` | `ReglasDeSegregacionTests` | Misma persona con dos usuarios; `Decidir × Desembolsar` activo por defecto | SC-015 |
| `VentanaLegalDeContacto`, `RepartoDeGestores` | `CasosDoradosDeVentanaLegalTests` sobre `Domain.Tests/Lending/Collections/Casos/*.json` | Domingo → lunes 7:00; festivo nacional; sábado hasta 15:00; un contacto por día; tras un contacto directo por teléfono el lunes, WhatsApp el miércoles se excluye (US12-2); el informativo respeta horario y tope y no fija canal (I2); el documento pedido por el titular queda exento (I3); sin autorización del canal, excluido (US12-4); fallecido o en insolvencia, excluido; techo nacional ∩ restricción de la cooperativa. Reparto por número y por saldo (K4) | SC-011 |
| `NovedadesDeLibranza` | `NovedadesDeLibranzaTests` (K2) | Altas, cambios de valor, cancelaciones y sin cambio entre dos períodos | SC-016 |
| `VigenciaDeDocumentos` | `VigenciaDeDocumentosTests` | Un reverso anula todo documento cuyo `InformationCutoff` sea igual o posterior al registro de lo reversado | FR-132 |
| `VigenciaBitemporal` | `VigenciaBitemporalTests` | Versión conocida en R que cubre D; corrección con `SupersedesPublicId`; empate de `ValidFrom` por `Sequence` mayor | SC-001 |
| `CalendarioHabil`, `ContadorDeDias` | `CalendarioHabilTests`, `ContadorDeDiasTests` | Fecha exigible con la lista de no hábiles que recibe; de a excluida a b incluida en las tres convenciones | SC-023 |
| `CoincidenciaDeNombres`, `ReglaDeRevocacion`, `PlazosDelTitular` | `CoincidenciaDeNombresTests`, `ReglaDeRevocacionTests`, `PlazosDelTitularTests` | Nombre normalizado con umbral; revocación que no procede con deber legal; plazos en días hábiles nacionales (F11) | SC-012 |
| `LayoutDeArchivo`, `EscritorPorLayout`, `ValidadorDeLayout` | `LayoutsDeArchivoTests` (K5) | Ancho fijo y delimitado, registros por código, formatos, obligatorios y valores admitidos | SC-019 |
| `CatalogoDeLayoutsDeCartera`, `ProyeccionSicsesCartera`, `ProyeccionCentralDeRiesgo` | `CasosDoradosDeLayoutsDeCarteraTests` (K5, en Application.Tests) sobre `Application.Tests/Lending/Regulatory/Casos/*.esperado.txt` | El archivo del SICSES y el de cada central comparados **byte a byte**; se regeneran sólo a propósito, tras cotejar a mano, con `CARTERA_ESCRIBIR_ESPERADO=1` (molde de la PILA) | SC-019 |

**Nómina con la cuota de Cartera** (DT §2.25, en `Domain.Tests/Payroll/Calculation/Casos/`): cuota dentro del tope;
cuota que excede el tope (lo que no cabe queda no descontado con `DeductionCap`); neto insuficiente
(`InsufficientNet`); la cuota primera entre las autorizadas, después de las de ley (G5). `CasoDoradoLiquidacion`
cambia: `DESC_CARTERA` vuelve a contabilizar contra el puente (T53, enmienda D-08 de la 010).

### 1.5 Application (InMemory, reloj fijo, servicios reales)

```bash
dotnet test tests/IngenIA365ERP.Application.Tests --filter "FullyQualifiedName~Lending|FullyQualifiedName~Common.Integration|FullyQualifiedName~Common.NationalValues|FullyQualifiedName~Common.Calendar|FullyQualifiedName~Common.Messaging|FullyQualifiedName~Common.Compliance|FullyQualifiedName~Common.Parameters|FullyQualifiedName~Saas|FullyQualifiedName~Payroll|FullyQualifiedName~Inventory|FullyQualifiedName~Core"
```

**Lo transaccional no se prueba aquí**: InMemory ignora las transacciones, así que la idempotencia del contrato y
de la reserva, el cerrojo `ICerrojoDeCartera`, la numeración, el `SKIP LOCKED`/`READPAST` del proceso y
`Lending.Contract.TransactionRequired` sólo se prueban con Testcontainers en los dos motores (§1.7). Lo que sí:

| Área | Pruebas | Qué fija |
|---|---|---|
| Libro y motor | `RegistroDeCarteraTests`, `CargadorDeReconstruccionTests`, `FechaContableTests` | Único escritor del libro y de la marca de agua; `Conciliar` registra cada diferencia por (día valor, concepto, cuota) enlazada al reverso; lectura en bloque para 1..N créditos; fecha contable = fecha valor si su mes no tiene cierre y está abierto, si no el primer día del primer mes que cumpla las dos cosas (T36) |
| Pagos | `SimulateLoanPaymentQueryHandlerTests`, `ReverseLoanPaymentCommandHandlerTests` | `Lending.Payment.DistributionChanged` al cambiar la huella (nunca en la forma sin interacción); `.ValueDateOutOfRange`, `.ValueDateBeforeClosing`, `.PreferenceMissing`; `Lending.Reversal.OfReversal` y `.OwnedByCollector` (un usuario de Cartera no reversa un pago de Nómina, T31) |
| Parámetros | `ReglasDeParametrosDeCarteraTests`, `LectorDeParametrosConInformacionHastaTests` | `Lending.Parameter.BelowLegalFloor` con el límite y su fuente; al leer rige lo más estricto; `informacionHasta` compara con `CreatedAt` y deriva `ValidTo` de la versión siguiente conocida en R (T38); sin `informacionHasta`, Inventario igual que hoy |
| Valores nacionales | `ReplicacionDeValoresNacionalesTests`, `EfectoDeReplicacionEnNominaTests`, `EfectoDeReplicacionObligacionSobrevenidaTests`, `LectorDeValoresNacionalesTests`, `ValidadorDeDocumentoNormativoDeCarteraTests` | Réplica idempotente por `Sequence` y `SourcePublicId`; las cuatro reglas de T93 (adopta lo igual, conserva y alerta `Nomina.ValorNacionalDistinto` lo distinto, cierra la vigencia anterior, `Payroll.LegalParameter.NationalValue` para `SMMLV` y `UVT` a mano); `Cartera.ObligacionSobrevenida`; `NationalValues.Missing`; los documentos `SarcAnexo1`, `SarcAnexo2` y `ReglaDePerdidaEsperada` se validan al registrarlos (T45) |
| Mensajes | `EmisorDeMensajesConVariosModulosTests`, `DestinoCarteraTests`, `ContratosDeMensajesTests` (sin cambios) | El módulo lo da el catálogo (CAR, INV, TES, REC), nunca la solicitud; sólo los tipos con `AdmiteProceso` aceptan proceso (`OriginActorKind = Process`); los cinco desenlaces del consumidor (`Processed`, `ValidationFailed`, `Rejected`, `NotIncorporated` con `SinObligacion`, confirmado por excepción); los bytes de los contratos INV no cambian |
| Contabilidad | `ResolutorDeReglasDeCarteraTests`, `ConstructorDeLineasDeCarteraTests`, `AgrupadorDeResumidosDeCarteraTests`, `ReglasDeLaMatrizDeCarteraTests`, `ConciliacionContableDeCarteraTests`, `ClosePeriodConCarteraTests` | Pesos de especificidad (Línea 512 … Sucursal 4); importe negativo invierte el lado; un comprobante por día con la sucursal por línea y una línea por crédito en las cuentas que exigen tercero; `Accounting.LendingRule.Missing`, `.Overlaps`, `.RetroactiveOverPosted`; cada diferencia explicada; `Accounting.Period.LendingNotClosed`, `.LendingPending`, `.LendingLoadWillExpire` (T57) |
| Modo de paso | `ElModoDeLaDimensionCambiadaDependeDeQuienLaEmite`, `ValidacionPreviaContableDeCarteraTests` | `EnLinea` si la emite una operación, `PorLotes` si la emite el proceso (salvo vigencia propia); «Bloquear» sólo muerde en pantallas; en el contrato y los procesos siempre advierte (A8); sin respuesta, D7 |
| Seguridad | `PerfilesSugeridosDeCarteraTests`, `CreateSuggestedLendingRolesCommandHandlerTests`, `AlcanceDeCarteraTests`, `RegistroDeIntervencionesTests`, `ReleaseScreeningMatchCommandHandlerTests` | Las trece plantillas contra el catálogo `Lending.*`; el comando es idempotente; `CompanyAdmin` sin `Lending.Decisions.*` ni `Lending.Sessions.Vote`; el alcance falla cerrado; `Lending.Segregation.Conflict` y `Lending.Security.UserWithoutPerson`; quien radicó no libera una coincidencia |
| Habeas data | `AutorizacionDeDatosPorFinalidadTests`, `SolicitudesDelTitularTests` | `VigenteAsync(persona, finalidad, canal?)`; una autorización migrada sin evidencia bloquea sólo centrales y mensajes de cobranza (FR-006); la revocación pregunta el deber a la consulta de obligaciones |
| Adaptadores | `GuardiaDeCanalesExternosTests`, `RenderizadorDePlantillasDeMensajeTests`; baterías `ConformidadDeMensajeria`, `ConformidadDelDepositoDeValores`, `ConformidadDeCentral`, `ConformidadDeListasRestrictivas` (con su clase por canal simulado; en `Application.Tests`, no en Dominio, §7.6 de las transversales) | `Lending.Adapter.SimulatedInProduction`: en producción un `SIMULADO` no perfecciona pagarés, no es constancia ni evidencia y no envía; sólo variables de la lista blanca, HTML escapado, aviso de largo de SMS; los comportamientos del último dígito de DT §2.22; nunca lanzan |
| Nómina | `PropuestaDeDescuentosDeCarteraTests`, `RecaudoPorNominaTests`; reescritas `SettlementInputLoaderTests`, `ApproveSettlementCommandHandlerTests`, `ReverseSettlementCommandHandlerTests`, `RecalculateSettlementCommandHandlerTests`, `RegisterTerminationCommandHandlerTests`, `CalculatePayrollRunCommandHandlerTests` | La definitiva propone por `IConsultaDeObligaciones` (vivas como deudor, castigadas incluidas, nunca codeudas, G4); `Payroll.Settlement.LoanProposalOutdated` y `.LoanAmountsChanged`; la ordinaria con una novedad `LoanDeduction` por obligación; los descuentos de Cartera ya **sí** contabilizan contra el puente (T53) |
| Comercial y Personas | `EvaluateSaleCreditQueryTests`, `CreditoEnLaVentaTests`, `DestinoInventarioTests`, `DeletePersonCommandHandlerTests` | Origen `Validated` con Cartera real; `Inventory.Credit.LendingBlocked`, `.QuotaExceeded`, `.LendingNoResponse`; `INV_LendingObligationResponses` sin saldos; `CreditWithoutLending` no consulta, no reserva ni emite (A6); `Person.HasActiveLoans` como deudor, como codeudor o «migrada, sin activar» (la rama que hoy falta, T94) |
| Migración y activación | `ApplyMigrationLoadCommandHandlerTests`, `ActivateLendingCommandHandlerTests` | Validación entre partes, todo o nada, cuadre y conciliación de ventas; activación negada si la fecha no es C+1, pasó el plazo, hay diferencias sin aceptar o falta un requisito; **con el ambiente en producción**, exige un recaudador `Receipts` y un destino `Treasury` reales y K6a para una obligada (US8-7, US8-8: no se pueden ensayar en DEV ni QA) |
| Plantillas | las de la 012 (16, sin cambios) más `CatalogoDePlantillasDeCarteraTests` | `RutasDePlantilla.MapPlantilla` generalizada sin cambiar las de Inventario; ninguna plantilla crea personas |
| Riesgo y cierres | `ExecuteMonthlyCloseCommandHandlerTests`, `ReopenMonthlyCloseCommandHandlerTests`, `ValidateRiskPolicyVersionCommandHandlerTests`, `SetAnalysisRatingCommandHandlerTests` | Reclasificación completa y deterioro en delta frente al cierre anterior o al base (T48); sólo se reabre el último y antes del cierre contable (D12); la política sólo endurece; el análisis sólo empeora y exige motivo |
| Procesos | `ProcesadorDeCarteraTests`, `TareasDeCarteraTests` | Recorre sólo cooperativas activadas, respeta presupuesto y paralelismo; cada tarea de DT §2.13 sale si Cartera no está activada (salvo la réplica) y es idempotente por su `DedupKey` |

### 1.6 Arquitectura

```bash
dotnet test tests/IngenIA365ERP.Architecture.Tests
```

**Nuevas** (`tests/IngenIA365ERP.Architecture.Tests/Principles`):

| Prueba | Qué fija | Criterio |
|---|---|---|
| `LaCarteraSoloSeTocaPorSusInterfaces` | La lista cerrada de FR-008: nadie depende de la Cartera nueva salvo por `Common.Integration.Lending`; métodos exactos de los cinco puertos que Cartera ofrece; ninguna FK ni navegación de `PAY_`/`INV_` a `LND_` | SC-018 |
| `CarteraNoConoceContabilidad`, `LosHechosDeCarteraNoLlevanCuentas`, `LoDeCarteraSeCorrigeConComprobanteNuevo` | Frontera contable en las dos direcciones; contratos sin cuentas; la guarda del poster para CAR | SC-018, FR-126 |
| `SoloElEmisorEscribeLaBandeja`, `ElCatalogoDeMensajesEsUnico`, `EmisionesDeProcesoPermitidas`, `UnSoloConsumidorPorDestino` | Bandeja de varios emisores; sólo los tipos CAR marcados admiten proceso; un consumidor por destino | FR-149 |
| `LosDoblesDePruebaNoSeRegistranEnProduccion` | Recorre el DI con el ambiente de producción: sin dobles y, si la configuración los pide, el arranque falla | FR-089 |
| `LosComandosDeCarteraLlevanClave` | Todo comando de pantalla es `IOperacionIdempotente` | SC-010 |
| `LaCarteraNoTieneValoresLegalesFijos` | Los literales de Nómina más `365`; `7` y `10` sólo por el valor del enum; sospechosos del Anexo con exención comentada por archivo | SC-018 |
| `ElMotorDeCarteraEsDecimal`, `NadieEscribeElLibroDeCarteraFueraDelRegistro`, `LosSaldosYTasasDeCarteraTienenSuPrecision`, `LaCarteraUsaLaFechaLocal` | Sin `double`, `float`, `Math.Pow/Exp/Log` en `Domain/Lending`; único escritor del libro; alias de `PrecisionDeCartera`; `HoyLocal`, nunca `TodayUtc` | SC-001, SC-023 |
| `LosValoresNacionalesSeLeenEnUnSoloSitio`, `SoloElReplicadorLeeElRegistroCentral`, `SoloLaReplicaEscribeLosValoresNacionalesDeNomina`, `LosCierresDeCarteraNoSeReescriben` | Tres lectores únicos; sólo el replicador lee `ADM_`; `SMMLV`, `UVT` y festivos `National` sólo por la réplica; cierres e historial inmutables | SC-018, FR-107 |
| `CadaParametroRespetaLaEntregaDeSuModulo` | Ninguna clave de Cartera se ofrece antes de su entrega (`CatalogoDeCartera.EntregaVigente`) | FR-007 |
| `LaCarteraHeredadaNoVuelve`, `NingunaTablaConservadaApuntaALaCarteraRetirada`, `ElEspacioLendingEsSoloCartera` | Ninguno de los 55 nombres vuelve al modelo ni al snapshot; ninguna conservada apunta a una retirada; `Lending` es sólo la Cartera nueva | FR-144 |
| `LasConsultasDeCarteraRespetanElAlcance`, `LasLecturasSensiblesDeCarteraSeAuditan`, `LosCodigosViewDeCarteraNoTienenDatosPersonales` | Toda consulta filtra por `FiltroDeAlcanceDeCartera`; lista fija de lecturas sensibles; la bandeja, los hechos y los archivos regulatorios van con `Read` | SC-014, SC-015 |
| `LosPuertosDeCarteraValidanYAuditanSusEscrituras` | Cada método de escritura de `IContratoDeObligaciones` e `IReservasDeCartera` ejecuta su validador y escribe su evento (T101) | SC-014 |
| `LosProveedoresExternosSoloLosConoceSuAdaptador`, `LasCredencialesDeCanalesExternosNoTocanLaBase`, `LaCobranzaNuncaContactaReferencias`, `LasPantallasDeCarteraEstanEnElMenu`, `TextosDeCarteraTests` | Sólo `Program.cs` llama `AddExternalChannels`; credenciales fuera de la base; ningún destinatario que sea referencia; página, ruta y permiso de cada pantalla; ningún texto con códigos técnicos ni con el nombre del sistema anterior | SC-011, FR-013 |

**Ampliadas**: `El_puerto_de_Cartera_declara_exactamente_sus_dos_consultas` (más los dos métodos de
`IReservasDeCartera`), `InventarioNoConoceContabilidadNiCartera`, `NingunTrabajoDeFondoOperaSinCooperativa` (más
`ProcesadorDeCartera`), `LosComandosDeConsumoNoTienenRuta`, `LosHechosInmutablesNoSeModifican`,
`NingunModuloEscribeMovimientosFueraDelContrato`, `PrincipioXI_ContableImmutable` (se activa sola con
`Entities/Lending/Transactions`), `PrincipioXII_MigracionesDestructivas`, `LosEndpointsProtegidosExigenPermiso`
(más `Endpoints/Lending/*.cs`, con `Endpoints/Savings` exento por nombre), `LaSegregacionNoUsaUserIdDelToken`
(más las carpetas de Cartera), `LasPantallasDicenQueEstanCargando` (`ModulosMigrados` += `Cartera`),
`TodoEnlaceDelMenuTieneSuPagina`, `LosUnicosDeDescuentosSoloCuentanFilasVivasTests` (`ObligationPublicId`),
`LaUvtSeLeeEnUnSoloSitio` (sigue en `LectorDeUvt`) y, en Application, `LosFormatosElegidosPorReferenciaNoCompitenPorVigencia`
(K2) y `ElModoDeLaDimensionCambiadaDependeDeQuienLaEmite`. **Sin cambios y verdes**: `LaContabilidadNoTieneCuentasEnCodigo`,
`LaPersonaSeEscribeEnUnSoloSitio`, `ElTokenDeSesionLoPoneElHandler` (cubre los clientes tipados nuevos de
Cartera), `LosParametrosSeLeenEnUnSoloSitio`, `SoloElNumeradorNumera`, `Feature004_MigrationParity`. Si una de
éstas cae, algo de Cartera tocó lo que no debía.

### 1.7 e2e con Testcontainers, en los dos motores

Docker Desktop encendido (está apagado por defecto en la máquina de desarrollo). La fixture es la única,
`CentralIdentityApiFixture`, con los trabajos de fondo apagados y conducidos a mano, como en la 012, más tres
ajustes de esta feature: `Lending:Process:Enabled = false` (las pruebas corren cada tanda con
`RunDailyAccrualCommand` y avanzan días con el reloj de la suite, `RelojDeLaSuite`),
`Lending:TestDoubles:Enabled = true` (el host de pruebas no es de producción) y el doble contable
`ContabilidadParaCarteraConDoble`, que sólo muerde en la cooperativa que una prueba marca (sin respuesta para
D7, molde `ContabilidadParaInventarioConDoble`). Los canales externos van con `SIMULADO` y la carga de listas
con `CARGA-PROPIA`, sin archivo de credencial.

Colección **«Cartera e2e»**: un host, cooperativas aisladas para todo lo que activa Cartera, carga una
migración o cierra un mes (`CarteraE2E.CooperativaAisladaAsync`, molde `ContabilidadE2E.CooperativaAisladaAsync`).
Colección **«Valores nacionales e2e»** (§7.6 de las transversales): otro host para lo que registra o corrige valores
en la central —`ValoresNacionalesYReplicaTests` y `ReplicaSobreCooperativaSembradaTests`—, porque la central es una
por host y todas sus cooperativas la replican. Lo que necesita valores ficticios en la central se ensaya aquí o en
local, nunca en DEV ni QA. SC-005 de punta a punta (cinco segundos con el despachador encendido) se mide en QA
(§3.7); la e2e, con el despachador apagado, mide el consumo con la pasada a mano. Mientras una entrega no exista, su filtro no
encuentra pruebas y la corrida no falla por eso.

```bash
F_K1="FullyQualifiedName~PlataformaParaCarteraTests|FullyQualifiedName~RetiroDeLaCarteraHeredadaTests|FullyQualifiedName~CarteraDesplegadaSinActivarTests|FullyQualifiedName~NominaRecaudaPorElContratoTests|FullyQualifiedName~CreditoConCarteraTests|FullyQualifiedName~ConcurrenciaPagoYCausacionTests|FullyQualifiedName~IdempotenciaDeCarteraTests|FullyQualifiedName~LineasDeCreditoYUsuraTests|FullyQualifiedName~OriginacionConAtribucionesTests|FullyQualifiedName~DesembolsoYRecogidaTests|FullyQualifiedName~CausacionYLiquidacionTests|FullyQualifiedName~ContratoDeObligacionesTests|FullyQualifiedName~ContabilizacionDeCarteraPorMensajesTests|FullyQualifiedName~MigracionYActivacionDeCarteraTests|FullyQualifiedName~CierreMensualDeCarteraTests|FullyQualifiedName~DocumentosDeCarteraTests|FullyQualifiedName~SeguridadYHabeasDataDeCarteraTests"
F_VN="FullyQualifiedName~ValoresNacionalesYReplicaTests|FullyQualifiedName~ReplicaSobreCooperativaSembradaTests|FullyQualifiedName~UsuraNuevaCierraLineasTests"
F_K2="FullyQualifiedName~LibranzaYArchivoDeDescuentosTests"
F_K3="FullyQualifiedName~NovedadesYCondonacionesTests|FullyQualifiedName~CastigoYRecuperacionTests"
F_K4="FullyQualifiedName~CobranzaYAvisoPrevioTests"
F_K5="FullyQualifiedName~InformesYTableroDeCarteraTests|FullyQualifiedName~SicsesYCentralesTests"
F_K6="FullyQualifiedName~PerdidaEsperadaTests|FullyQualifiedName~ViviendaConUvrTests"

DB_PROVIDER=PostgreSql dotnet test tests/IngenIA365ERP.API.IntegrationTests --filter "$F_K1|$F_VN|$F_K2|$F_K3|$F_K4|$F_K5|$F_K6"
DB_PROVIDER=SqlServer  dotnet test tests/IngenIA365ERP.API.IntegrationTests --filter "$F_K1|$F_VN|$F_K2|$F_K3|$F_K4|$F_K5|$F_K6"
```

(No se filtra por `~Lending` ni por `~Integration`: el espacio del proyecto ya contiene «IntegrationTests».)

Lo que recorre cada clase, por HTTP con tokens reales y nada escrito por debajo:

| Historia | Clase | E | Qué recorre | Criterio |
|---|---|---|---|---|
| — | `RetiroDeLaCarteraHeredadaTests` | K1 | Baja a la migración anterior; una fila en una tabla retirada y otra en `LND_SavingsAccounts`; la guarda nombra la retirada y su cantidad; con `LND.RetiroHeredado.Aprobado` pasa; la de ahorros sigue (T87) | FR-144 |
| — | `CarteraDesplegadaSinActivarTests` | K1 | Definitiva y ordinaria sin descuentos de Cartera; con una carga aplicada, exigen `AcknowledgeLendingPendingActivation`; borrar a un deudor de la carga → `Person.HasActiveLoans`; venta a crédito → `NotEnabled` y crédito provisional; contrato → «Cartera no activada»; Contabilidad cierra como hoy y avisa `LendingLoadWillExpire` (T85) | FR-142, FR-146 |
| US1 | `LineasDeCreditoYUsuraTests` | K1 | Plantilla con un error: nada guardado, fila y columna; línea sobre la usura rechazada con usura, modalidad y diferencia; versión nueva que no toca la tasa de los vivos; sin usura del día, simular y aprobar bloquean y alertan; usura nueva menor: causación topada desde ese día, `Cartera.UsuraTopada` y `Cartera.LineaCerradaPorUsura`; línea de origen comercial sin concepto tributario → `Lending.CreditLine.InterestTaxConceptMissing`; parámetro menos estricto que su límite → `Lending.Parameter.BelowLegalFloor` | SC-002, SC-018 |
| US2 | `OriginacionConAtribucionesTests` | K1 | Simular sin escribir; documento faltante; coincidencia en listas que sólo libera el oficial, nunca quien radicó; centrales sin autorización; tres montos a tres niveles; comité con quórum, votos, radicador excluido (`Lending.Session.VoterExcluded`) y acta con SHA-256; vinculado con 4/5 de la composición; concentración con límite y exceso; sin patrimonio técnico vigente → `Lending.Concentration.TechnicalCapitalUnavailable`; usuario sin persona | SC-015, SC-022 |
| US3 | `DesembolsoYRecogidaTests` | K1 | Pagaré con firma pendiente (`DepositoDeValoresSimulado`); aprobación vencida; recogida de un crédito al día con su comprobante, neto y enlace; recogida en mora → «requiere reestructuración»; usura menor el día del desembolso; transferencia rechazada por `TesoreriaDePrueba` → `DesembolsoReversado` sin intereses; `Decidir × Desembolsar` | SC-002 |
| US4 | `CausacionYLiquidacionTests` | K1 | Tandas a mano con el reloj de la suite; repetir la tanda no duplica; la vencida causa sólo mora; suspensión con `CausacionSuspendida`; un día sin correr y la puesta al día; liquidación pasada, de hoy y futura con `date` y `recordedAsOf` (C83); `NationalValues.Missing` frena un crédito y no a los demás; `VerifyLendingLedgerIntegrityQuery`; la auditoría de la corrida queda en la base de auditoría de su cooperativa | SC-001, SC-020 |
| US5 | `ContratoDeObligacionesTests` | K1 | Por `RecaudadorDePrueba` (claves `TST:{guid:N}`, C35): listar y simular con `Answered` (C28), las siete modalidades, abono con dos planes, excedente, saldo a favor y su resolución, distribución cambiada, anticipo, reverso con un pago posterior (vista previa y resultado), `PagoReversadoPorCartera`, reverso de un reverso, fecha valor fuera de rango y antes de un cierre, recaudador fuera del registro, contrato sin transacción en curso | SC-003, SC-004 |
| US6 | `CreditoConCarteraTests` | K1 | Consulta de cupo y estado; reserva repetida con el mismo número; v2 y `ObligacionCreada` con el número; validación fallida que bloquea crédito nuevo sin anular la venta; nota crédito parcial; mensaje repetido; venta que excede la concentración listada como exceso; `CreditWithoutLending` no llega; el consumo de una venta, medido desde la pasada del despachador | SC-005 (consumo), SC-022 |
| US7 | `ContabilizacionDeCarteraPorMensajesTests` | K1 | Matriz y `DS` con cruce `PG`; validación previa que bloquea en pantalla y advierte en el contrato; sin respuesta de Contabilidad (D7); lote resumido `CA` por día; rechazo, bandeja y reproceso; reclasificación A→C; dimensión cambiada; cuenta por cobrar de Comercial reclasificada sin duplicar; `ReverseDocumentCommand` sobre un `CAR` → `ModuleOwned`; los hechos `INV` siguen igual | SC-006 |
| US8 | `MigracionYActivacionDeCarteraTests` | K1 | Partes con errores por fila y columna; aplicar sólo guarda; cuadre por concepto, modalidad, sub-segmento, categoría y cuenta; activación negada (`Lending.Activation.DifferencesNotAccepted`, `.CutoffInvalid`, `.DeadlinePassed`, `.RequirementsMissing`); aceptación por punto de acta y acta de activación obligatoria (C61); carga reemplazada; activación que hace nacer todo en una transacción con guardados por tandas (C60), con cierre base, no aplicados `Activation` y cobros provisionales guardados que el consumidor de Comercial aplica al nacer la obligación (C62); venta fechada hasta el corte que llega después de activar → `NotIncorporated` (`Lending.Sale.NotIncorporated`, C63); respuestas a Comercial (migrada, cancelada antes del corte, aceptada sin migrar, D29); cupo por revalidar | SC-013 |
| US9 | `CierreMensualDeCarteraTests` | K1 | Orden 202 y corrida por tandas; arrastre; reporte de cambios; análisis; pago único; primer cierre en delta frente al base; pago con fecha valor posterior que no reescribe; reapertura del último y `Version + 1`; reapertura de uno que no es el último, rechazada; marca de D28; conciliación cero; `Accounting.Period.LendingNotClosed` | SC-006, SC-013, SC-021 |
| US10 | `NominaRecaudaPorElContratoTests` | K1 | Ordinaria con la cuota de Cartera, tope y no descontados (`LND_InstallmentNonDeductions`, origen `Contract`); definitiva todo o nada; reversos por el contrato; un usuario de Cartera no reversa un pago de Nómina (US10-6) | FR-088, FR-112 |
| US10 | `LibranzaYArchivoDeDescuentosTests` | K2 | Capacidad de descuento; archivo de 20 obligaciones con huella; envío confirmado por el doble; respuesta con tres no aplicados; pendiente de giro sin mora dentro del umbral; traslado; `Lending.PayrollFile.AlreadySent` | SC-016 |
| US11 | `NovedadesYCondonacionesTests` | K3 | Prórroga clasificada como reestructuración; codeudor sin consentimiento; misma obligación o crédito enlazado; mejora por tramos y recaída; condonación al nivel siguiente; insolvencia; fallecimiento con reclamación cerrada por el pago de la aseguradora (T102) | SC-021 |
| US12 | `CobranzaYAvisoPrevioTests` | K4 | Etapas y gestor; domingo → siguiente ventana; canal de la semana; promesa cumplida e incumplida; campaña que excluye al no autorizado; aviso previo por episodio y con dos comunicaciones; gasto de cobranza sólo tras su aviso; reclamo con aceptación tácita | SC-011, SC-012 |
| US13 | `DocumentosDeCarteraTests` | K1 / K5 | Plan de pagos, paz y salvo (negado con saldo y con una venta en reproceso; con mención de la aceptada sin migrar) y certificado de saldo con código; verificación; anulación por reverso; en K5, extracto y certificado tributario | FR-131, FR-132 |
| US14 | `CastigoYRecuperacionTests` | K3 | Asociado vigente rechazado con el requisito; lote aprobado con acta; `CreditoCastigado`; recuperación visible | FR-095 |
| US15 | `InformesYTableroDeCarteraTests` | K1 / K5 | Vistas de K1 y K5 con alcance; tablero de una oficina; cosecha frente a un cálculo a mano; concentración con su porcentaje; una auditoría `Lending.Report.Exported` por exportación | FR-133–FR-135 |
| US16 | `SicsesYCentralesTests` | K5 | Validación que señala el dato inválido antes de generar; `LayoutSinCotejar`; exclusiones sin aviso y sin autorización listadas; archivos dorados | SC-012 |
| US17 | `SeguridadYHabeasDataDeCarteraTests` | K1 | Fuera de oficina o línea → el 404 genérico y `Lending.Access.OutOfScope`; sin permiso por llamada directa → 404 y `Lending.Access.Denied`; revisor fiscal sólo lee; rechazo de forma auditado como `Lending.Request.Rejected`; revocación que no procede con constancia; autorización migrada sin evidencia | SC-014, SC-015 |
| — | `ConcurrenciaPagoYCausacionTests` | K1 | Un pago mientras la tanda causa el mismo crédito: uno espera, ninguno se pierde; dos recaudadores a la vez sobre la misma obligación: uno aplica y el otro recibe «la distribución cambió»; reserva y desembolso del mismo deudor no se pasan del cupo (T34). Una ronda corta en cada corrida | SC-010 |
| — | `IdempotenciaDeCarteraTests` | K1 | Tres veces cada desembolso, pago, reverso, reserva y mensaje con la misma clave: cero duplicados y la misma respuesta; otra carga con la misma clave → `Lending.Contract.KeyReused`, `Lending.Reservation.KeyReused` u `Operation.KeyReused` | SC-010 |
| — | `ValoresNacionalesYReplicaTests` («Valores nacionales e2e») | K1 | El maestro registra con segundo factor; la réplica copia en orden y es idempotente; la cooperativa nueva nace con su copia; una corrección de usura encola la corrida `NationalValueCorrection` y deja ajuste y saldo a favor; `Cartera.NormativaPendienteDeValidar` no bloquea | FR-015, SC-002 |
| US1 | `UsuraNuevaCierraLineasTests` («Valores nacionales e2e») | K1 | Usura de microcrédito del mes y línea al 40 %; una usura menor desde el mes siguiente: la réplica bloquea la línea desde ese día (`usuryBlockedSince`) y alerta `Cartera.LineaCerradaPorUsura` una vez; una versión bajo la usura limpia el bloqueo; un mes sin usura → `NationalValues.Missing` y `Cartera.ValorNacionalFaltante` aunque el comando falle (US1-3, US1-5) | SC-002 |
| — | `ReplicaSobreCooperativaSembradaTests` («Valores nacionales e2e») | K1 | La réplica adopta el SMMLV, la UVT y los festivos que Nómina ya tenía; lo distinto se conserva con `Nomina.ValorNacionalDistinto` (T93) | FR-147 |
| — | `PerdidaEsperadaTests` | K6a | Activación de una obligada que lista lo que falta (US8-10); cierre con segmentos cubiertos y rechazo de dejarlos en Anexo 1 (US9-6); adopción y desistimiento voluntarios (US9-7); obligación sobrevenida con K6a (US9-9) | SC-023 |
| — | `ViviendaConUvrTests` | K6b | Antes de K6b vivienda no se origina ni se migra; después, en UVR con tasa fija y fuera de la mora sobre intereses | — |

Las e2e de la obligación sobrevenida usan el **perfil** de una cooperativa aislada, no una regla
`ReglaDePerdidaEsperada` nueva en la central, que tocaría a todo el host.

### 1.8 Volumen, con `RUN_PERF_TESTS=1`

```bash
RUN_PERF_TESTS=1 DB_PROVIDER=PostgreSql dotnet test tests/IngenIA365ERP.API.IntegrationTests --filter "FullyQualifiedName~ActivacionDeCarteraPerformanceTests|FullyQualifiedName~CausacionDiariaPerformanceTests|FullyQualifiedName~ConsultasYSimulacionesPerformanceTests|FullyQualifiedName~ArchivoDeDescuentosPerformanceTests"
```

Llevan `[FactDeRendimiento]`: sin la variable se reportan **omitidas**, nunca con un `return` al principio (que
cuenta como aprobada sin haber comprobado nada). Se corren también con `DB_PROVIDER=SqlServer` antes de cada
promoción.

| Prueba | Escenario | Umbral | Lo que deja medido | Criterio |
|---|---|---|---|---|
| `ActivacionDeCarteraPerformanceTests` (C60) | Una carga aplicada de 50.000 créditos sintéticos (≈1,5 millones de cuotas) con su cuadre aceptado en una cooperativa aislada; `ActivateLendingCommand` | Termina sin agotar la memoria del host y **todo o nada**: una falla provocada en la última tanda deja la base como antes (ningún crédito, cierre base ni destino `Lending` activo) | Tiempo total y por tanda de 1.000 créditos; memoria máxima del proceso; filas nacidas por tabla | SC-013 |
| `CausacionDiariaPerformanceTests` | 50.000 créditos vivos sintéticos cargados por la migración en una cooperativa aislada; una pasada completa del ciclo diario, tanda por tanda con `RunDailyAccrualCommand` como la conduce el `ProcesadorDeCartera`; mientras corre, otra cooperativa del mismo host aplica pagos por el contrato y consulta cupos | Termina en **< 30 min**; en la otra cooperativa, el p95 de pagos y consultas sigue dentro de SC-008 | Tandas, partes y mensajes emitidos; líneas del comprobante resumido del día (pregunta C5, ~20.000 esperadas); filas nuevas de `LND_LoanDailyPositions` (D13) y de `COR_IntegrationMessages` (B3); un día saltado y su puesta al día sin duplicados; el cierre mensual sobre la misma cartera (medido, sin umbral) | SC-007, SC-020 |
| `ConsultasYSimulacionesPerformanceTests` | Sobre la misma cartera: 1.000 consultas de cupo y estado por la consulta de Comercial; 500 simulaciones de plan y 500 de pago | p95 **< 1 s** en cupo y estado; p95 **< 2 s** en simulaciones | La confirmación del POS con reserva de número dentro de su transacción (pregunta G6) | SC-008 |
| `ArchivoDeDescuentosPerformanceTests` (K2) | Una pagaduría con 5.000 obligaciones; respuesta con no aplicados; ciclos diarios a lo largo de la ventana de giro | Archivo en **< 1 min**; el 100 % de los no aplicados con su motivo; ningún deudor con mora imputada dentro de la ventana | — | SC-016 |

### 1.9 Lo que debe seguir verde

Sin cambios de comportamiento: las colecciones «Nomina e2e», «Contabilidad e2e» e «Inventario e2e», salvo las dos que se nombran abajo; en
particular `ContabilizacionPorMensajesTests`, `ContabilizacionDeVentasPorMensajesTests`,
`EntregaGarantizadaTests`, `LotesProgramadosTests`, `IdempotenciaDeOperacionesTests`,
`ElProcesoEnSegundoPlanAuditaEnSuCooperativaTests`, `IntegridadDeAuditoriaTests` y `AltaDePersonaEnUnPasoTests`.
Con cambios esperados y reescritas en la misma tarea que las provoca: `CreditoProvisionalTests` (sigue verde al
cerrar el paso 1 de T88 y después cubre `NotEnabled`), `LiquidacionDefinitivaTests` (propone y recauda por el
contrato) y las de Application de §1.5 que sembraban `LoanPortfolio`.

**Línea base** (T0010, 2026-10-05, rama `013-cartera-financiera` sobre `e15249fa`, antes de cualquier código de
Cartera nueva):

| Proyecto | Pasan | Omitidas | Notas |
|---|---|---|---|
| Domain.Tests | 1.134 | 0 | |
| Application.Tests | 3.294 | 0 | |
| Architecture.Tests | 394 | 0 | incluye los 32 esqueletos de la fase 1 (listas vacías) y la prueba de `catch` vacío en `.razor` (T0008) |
| Shared.Tests | 340 | 0 | |
| Load.Tests | 2 | 0 | |
| API.IntegrationTests (PostgreSQL) | 457 | 9 | 2026-10-06, tras las fases 1 y 2; 1 falla previa a la 013 (abajo) |
| API.IntegrationTests (SQL Server) | 457 | 9 | ídem |

Falla previa a la 013, no se cuenta como regresión: `ContabilidadNiifTests.La_digitacion_valida_campo_a_campo_guarda_descuadrado_numera_sin_repetir_y_reversa_a_cero` (falla igual en `e15249fa`; tarea aparte).

Siguen contando como «pasadas» sin comprobar nada las siete de CLAUDE.md que hacen `return` según una variable de
entorno (`AuditPerformanceTests`, `LoginThroughputFact`, cuatro de multi-tenancy y `PrincipioVI_PublicIdOnly`).

## 2. La preparación del ensayo en DEV o QA

Todo esto es **dato y configuración**, no código. Se hace en DEV o QA (o en local) sobre cooperativas creadas
para el ensayo, cada una con su base (Principio IV). Nada del ensayo llega a una cooperativa de producción.

### 2.1 Ambiente, cooperativas y configuración técnica

1. **Confirmar que el ambiente no corre como producción** (pregunta B4): `ASPNETCORE_ENVIRONMENT` de la API de DEV
   y de QA distinto de `Production`. Si QA corriera como producción, allí no habría dobles y el ensayo de pagos
   y giros sólo se haría en DEV.
2. Configuración técnica de la API del ambiente (no es de la cooperativa, DT §2.20):

   | Clave | Valor en el ensayo | Para |
   |---|---|---|
   | `Lending:TestDoubles:Enabled` | `true` | `RecaudadorDePrueba` y `TesoreriaDePrueba` |
   | `Lending:Process:Enabled`, `StartTimeLocal`, `IntervalSeconds`, `BudgetSeconds`, `LeaseTtlSeconds`, `MaxParallelCooperatives`, `BatchSize` | `true`, «00:30», 30, 60, 120, 2, 250 (defectos) | ciclo diario, cierre y reapertura |
   | sección `ExternalChannels` | `SIMULADO` para SMS, WhatsApp, depósito de valores y centrales; `CARGA-PROPIA` para listas | adaptadores sin contrato |
   | `CanalesExternosOptions.AmbienteDePruebas` | `true` (sale de `!IsProduction()`) | todo documento lleva «AMBIENTE DE PRUEBAS – SIN VALIDEZ» |

   Cuando Recaudo o Tesorería reales existan en ese ambiente, `Lending:TestDoubles:Enabled` pasa a `false`: dos
   consumidores del mismo destino hacen fallar el arranque con `Integration.Destination.DuplicateConsumer` (T15).
3. El maestro crea dos cooperativas desde la consola SaaS (`POST /api/saas/tenants/with-admin`):

   | Cooperativa | Para qué | Perfil regulatorio (§2.5) |
   |---|---|---|
   | `coop_ensayo` | El recorrido completo, con datos parecidos a los de COOFLOPAL | Multiactiva sin sección de ahorro y crédito: **no obligada**, Anexo 1 |
   | `coop_obligada` | D28 sin K6a, la pérdida esperada desde K6a, vinculados con 4/5 y la concentración sin patrimonio técnico | Cooperativa de ahorro y crédito: **obligada** en consumo con y sin libranza y comercial de persona natural |

4. Migrar (en QA y PDN lo hace el Job PreSync de Argo, con `AutoMigrate` apagado):

   ```bash
   dotnet run --project tools/IngenIA365ERP.DbMigrator -- migrate --scope admin
   dotnet run --project tools/IngenIA365ERP.DbMigrator -- migrate --scope cooperativas
   ```

   Comprobar las tablas contra la base del ambiente, no contra el repositorio.

### 2.2 El retiro de lo heredado (FR-144, T86–T89)

1. **Diagnóstico** `specs/013-cartera-financiera/diagnostico-cartera-heredada.sql` (sólo lectura, bloques
   PostgreSQL y SQL Server) en cada base de cooperativa y en `ingenia365erp`: filas de las 55 tablas que se
   retiran, de las 38 que se conservan (informativo), `LND_SiplaGroupParameters`, `PAY_SettlementDeductions` y
   `PAY_Novelties` con `LoanPortfolioId` no nulo, `ACC_Documents` con `OriginModule = 'CAR'` y si ya existe la
   aprobación. Se repite el día de cada promoción; en PDN, sólo con autorización expresa del dueño.
2. **La guarda muerde**: en una copia de DEV con una fila en una tabla retirada, `RetiroDeLaCarteraHeredada` se
   niega nombrando la tabla y la cantidad (PostgreSQL `RAISE EXCEPTION`, SQL Server `THROW 50013`) y no toca
   nada. Sólo pasa con la fila `COR_SystemSettings.SettingKey = 'LND.RetiroHeredado.Aprobado'`, que se inserta a
   mano después del respaldo `<base>-AAAAMMDD-pre-f013.dump` (también de la base administrativa) y con el segundo
   revisor anotado. Runbook: `docs/operaciones/cartera-retiro-heredado.md`.
3. **Después del retiro**:
   - el menú muestra el grupo **«Cartera»** con las pantallas nuevas y el grupo **«Ahorros y aportes»** con los 16
     enlaces conservados, en sus mismas rutas de pantalla `/cartera/ahorros*`, `/cartera/aportes`, etc.;
     ninguna pantalla dice el nombre del sistema anterior (`TodoEnlaceDelMenuTieneSuPagina`);
   - lo conservado responde en `/api/savings/*` y `/api/reports/savings/{person-portfolio, savings-statement}`
     (enmienda A10); `person-portfolio` ya no trae créditos;
   - `LND_SiplaGroupParameters.CreditLineId` queda como dato, sin FK;
   - `PAY_SettlementDeductions` y `PAY_Novelties` conservan la historia en `ObligationPublicId` y
     `ObligationNumber`;
   - los usuarios con `ui.pagina-inicio = /cartera/recaudos` se revisan (T89).

### 2.3 Semillas, permisos y plantillas de rol

Los **permisos** los siembra la API al arrancar (`LendingPermissionCatalogSeeder` con **todo** `Lending.*`, también
lo de K2–K6b, y los de los otros prefijos de DT §2.14: `Accounting.LendingRules.*`, `Accounting.LendingBatches.*`,
`Accounting.RegulatoryIndicators.*`, `Core.IntegrationParticipants.*`, `Core.NationalValues.*`,
`Core.RegulatoryProfile.*`, `Core.ExternalChannels.*` y, en `DomainPermissionCatalogSeeder`,
`Compliance.RestrictiveLists.*` y `Compliance.DataSubjectRequests.*`), **nunca el DbMigrator**. El bloque `Lending` de `IdentitySeedData` y su rol
`LoanOfficer` ya no existen. Las semillas paramétricas dejan:

| Order | Semilla | Qué comprobar |
|---|---|---|
| 6 (Admin) | `PlatformNationalValuesSeeder` | Los documentos `CatalogoDeModalidades`, `TiposDeOrganizacion`, `SarcAnexo1` y `ReglaDePerdidaEsperada` (y `SarcAnexo2` desde K6a) desde JSON embebidos, y los festivos `FestivosLey51` en la central; todo «pendiente de validar» |
| 60 | `VoucherTypesSeeder` | `CN` nuevo junto a `DS`, `RC`, `DN`, `CA`, `PV` (módulo CAR) |
| 71, 74 | `PayrollLegalParametersSeeder`, `HolidaysSeeder` | Ya no siembran SMMLV, UVT ni festivos Ley 51 (T93) |
| 83 | `AlertTypesSeeder` | Los tipos `Cartera.*` de DT §2.18, `Nomina.ValorNacionalDistinto` y `Contabilidad.PeriodoInexistente` con sus destinatarios |
| 88 | `NationalValuesReplicaSeeder` | La cooperativa nace con su copia de los valores nacionales y adopta lo que Nómina ya tenía |
| 89 | `LendingVoucherMappingsSeeder` | Operación y clase de recaudador → comprobante y cruce (`PG`; `FV` en los roles de Comercial), «pendiente de validar» |
| 90 | `LendingFactTypesSeeder` | `LND_FactTypes` (ámbito de parámetro `LendingFactType`) |

La migración `PlataformaParaCartera` deja la fila `lending.process` en `COR_BackgroundLeases` y el registro de
participantes: `Accounting`, `Inventory` y `Payroll` (recaudador, contrato v1) activos; `Lending` (destino)
inactivo hasta la activación.

```bash
H="Authorization: Bearer $TOKEN"; API=https://<host-del-ambiente>
curl -s -H "$H" "$API/api/admin/permissions/mine" | jq '[.[] | select(startswith("Lending."))] | length'
curl -s -H "$H" "$API/api/admin/roles/templates?module=Lending" | jq '.[].key'
curl -s -H "$H" "$API/api/core/integration-participants" | jq '.[] | {code, class, role, isActive}'
```

La segunda devuelve las trece plantillas: `cartera.administrador`, `cartera.asesor`, `cartera.analista`,
`cartera.jefe`, `cartera.comite-credito`, `cartera.comite-riesgos`, `cartera.gerente`, `cartera.consejo`,
`cartera.gestor-cobranza`, `cartera.oficial-cumplimiento`, `cartera.contador`, `cartera.auditor`,
`cartera.revisor-fiscal`. Con el token del administrador de la cooperativa (`CompanyAdmin`), la primera no
incluye `Lending.Decisions.*` ni `Lending.Sessions.Vote`; la plantilla `cartera.administrador` tampoco (C86:
administrar no es aprobar crédito).

### 2.4 Valores nacionales y normativa (el maestro, una vez por ambiente)

La central es **una por ambiente**: lo que registra el maestro lo replican todas las cooperativas de DEV o de QA.
Por eso en DEV y QA sólo se registran **valores reales**; las usuras ficticias, las correcciones y los días sin
usura se ensayan en local (con su propia base administrativa) o en las e2e de «Valores nacionales e2e».

1. En `/admin/valores-nacionales` (rutas `/api/saas/national-values`, con segundo factor; la plantilla de índices
   por `import`) registrar, para M−1 a M+2:
   - `USURA` por modalidad certificada (clave = modalidad del `CatalogoDeModalidades`, incluidas las productivas
     según la certificación vigente, pregunta E4), en `EA`;
   - `IBR` e `IPC` (clave = plazo o serie), `SMMLV`, `UVT`;
   - los `LIMITE` del `CatalogoDeLimitesLegales` (códigos de `data-model.md` §12.5, C71): `AVISO_PREVIO_DIAS`,
     `COBRANZA_HORA_INICIO_LV`, `COBRANZA_HORA_FIN_LV`, `COBRANZA_HORA_INICIO_SAB`, `COBRANZA_HORA_FIN_SAB`,
     `COBRANZA_CONTACTOS_DIA`, `LIBRANZA_CAPACIDAD_DESCUENTO`, `PREPAGO_TOPE_SMMLV`, `CONCENTRACION_INDIVIDUAL`,
     `CONCENTRACION_INDIVIDUAL_CON_GARANTIA`, `DETERIORO_GENERAL_MINIMO`, `VINCULADOS_FRACCION_CALIFICADA` y los
     plazos del titular (`TITULAR_CONSULTA_DIAS`, `TITULAR_RECLAMO_DIAS` y sus prórrogas);
   - `FESTIVO` ya viene sembrado (2026–2028).
2. Documentos normativos (`/api/saas/regulatory-documents`): al registrarlos, `IValidadorDeDocumentoNormativo`
   valida su contenido; `Verified` es el cotejo de la plataforma. Paquete entre ambientes por
   `/api/saas/national-values/export` e `import` (pregunta B5).
3. **Réplica**: la tarea `valores-nacionales.replicar` corre en cada pasada. En cada cooperativa:
   `COR_NationalValueReplications` con el tramo copiado, `/api/core/national-values` con las copias y su
   `KnownAt`. En Nómina, la proyección: la fila de `SMMLV` y `UVT` de `PAY_LegalParameters` con fuente «Valor
   nacional {SourcePublicId}» y los festivos `National` en `PAY_Holidays`; si la fila que Nómina tenía difiere,
   se conserva y se alerta `Nomina.ValorNacionalDistinto`. Registrar a mano un `SMMLV` en Parámetros legales
   responde `Payroll.LegalParameter.NationalValue`.
4. **Revisión de la contadora** (`/api/core/national-values/{id}/review`, `Core.NationalValues.Review`): sin ella el
   valor queda «pendiente de validar», se alerta `Cartera.NormativaPendienteDeValidar` y **nada se bloquea**
   (T41), salvo la activación en producción, que exige el Anexo 1 vigente y la usura de las modalidades de las
   líneas activas.

### 2.5 Perfil regulatorio

En `/admin/perfil-regulatorio` (`/api/core/regulatory-profile/versions`, `Core.RegulatoryProfile.Manage`), una
versión con vigencia por cooperativa: tipo de organización, actividad financiera, nivel de supervisión,
categoría (fondos de empleados), grupo NIIF y registro como operador de libranza con número y fecha.

1. `coop_ensayo`: sin actividad financiera, operador de libranza registrado. El selector responde Anexo 1 en
   todos los segmentos y las mayorías para vinculados son las de los estatutos (F7).
2. `coop_obligada`: con actividad financiera. Sin K6a desplegada, el selector responde Anexo 1 con la marca
   «modelo obligatorio no disponible» en los tres segmentos cubiertos.
3. El grupo NIIF se siembra desde `AccountingSetup.NiifGroup`; si se registra uno distinto, alerta (E3).
4. Sin el registro de operador de libranza, una solicitud por libranza no se radica (FR-108).

### 2.6 Contabilidad de ensayo

La activación, el cuadre y la conciliación comparan contra los libros (receta
`docs/operaciones/contabilidad-primer-ejercicio.md`):

1. Contabilidad iniciada con el CUIF; sucursales contables SUC1 y SUC2; centros de costo; períodos de M y M+1 abiertos.
2. En `/maestros/agencias`, las oficinas O1 → SUC1 y O2 → SUC2 (`COR_Branches.TenantBranchPublicId`). La oficina O3
   queda **sin** sucursal contable a propósito (CH49).
3. Apertura `AP` con fecha C y los saldos de las cuentas de cartera **iguales a los de la carga de §3.3**, salvo el
   capital de consumo con libranza en categoría A, que se deja **150.000 por debajo** para el cuadre.
4. Matriz de Cartera en `/contabilidad/cartera/matriz` desde la plantilla `/api/accounting/lending/rules/template.xlsx`
   (`ImportLendingPostingRulesCommand`), con los roles de T52 y `CollectorClass` en las de `RecaudoPorAplicar`
   (`Payroll`, `Receipts`, `Test`, `OwnLoanPayoff`); tipos de comprobante en `/contabilidad/cartera/tipos-de-comprobante`;
   `/contabilidad/cartera/completitud` vacía salvo el hueco deliberado de §3.10.
5. `DESC_CARTERA` con cuentas en Nómina (`GET /api/payroll/concept-definitions/DESC_CARTERA/accounts`); sin ellas la
   primera nómina con descuentos de Cartera no se aprueba: la definitiva responde
   `Payroll.Settlement.ConceptAccountsMissing` y la ordinaria `Payroll.ConceptWithoutAccounts`, los dos ya existentes
   (§7.6 de las transversales; no hay código nuevo).
6. Patrimonio técnico en `/contabilidad/indicadores-regulatorios` (`ACC_RegulatoryIndicators`, con fuente y soporte)
   en `coop_ensayo`. En `coop_obligada` se deja **sin** indicador vigente para §3.5.

### 2.7 Usuarios, roles, alcance, atribuciones y órganos

Roles desde las plantillas con `CreateSuggestedLendingRolesCommand` (paso del asistente de activación; correrlo dos
veces no crea nada). Todo usuario que radique, decida, vote o desembolse tiene `SEC_Users.PersonId`. Alcance de
Cartera por usuario en `/api/lending/scopes/users/{userPublicId}` (oficinas por `SEC_UserBranchAssignments`, líneas
por `LND_UserCreditLineScopes`); montos operativos en `/api/lending/amount-limits`.

| Usuario | Plantilla | Oficinas / líneas | Ajuste para el ensayo |
|---|---|---|---|
| `admin` | `cartera.administrador` | todas | parámetros, alcances, plantillas, migración y activación |
| `asesor.o1` | `cartera.asesor` | O1 / `CONLIB`, `CONCAJA`, `ROTATIVO` | — |
| `asesor.o2` | `cartera.asesor` | O2 / todas | para los accesos fuera de alcance |
| `analista` | `cartera.analista` | todas | nivel individual hasta 5.000.000 |
| `jefe` | `cartera.jefe` | `Lending.Scope.AllOffices`, `Lending.Scope.AllLines` | `Disbursements.Execute` hasta 50.000.000; `Payments.BackdateBeforeClose` |
| `gerente` | `cartera.gerente` | todas | nivel de gerencia hasta 20.000.000 |
| `comite.1`, `comite.2`, `comite.3` | `cartera.comite-credito` | — | miembros del Comité de crédito; `comite.1` además `cartera.asesor` (US2-6) |
| `secretaria` | `cartera.consejo` | — | registra asistencia y votos del consejo (`Sessions.RecordVotes`, F8) |
| `gestor` | `cartera.gestor-cobranza` | O1 | K4 |
| `oficial` | `cartera.oficial-cumplimiento` | todas | listas restrictivas y solicitudes del titular |
| `contadora` | `cartera.contador` + roles contables de la 009 | todas | `Accounting.LendingRules.Manage`, `Accounting.LendingBatches.Run`, `Accounting.RegulatoryIndicators.Manage`, `Core.NationalValues.Review` |
| `auditor`, `revisor` | `cartera.auditor`, `cartera.revisor-fiscal` | todas, sólo lectura | `AuditLog.VerifyIntegrity` para `auditor` |
| `pruebas` | `Lending.TestDoubles.Operate` | — | opera `/cartera/dobles-de-prueba` |
| `sinpersona` | `cartera.analista` sin `SEC_Users.PersonId` | todas | para `Lending.Security.UserWithoutPerson` |
| `lectura` | rol integrado `ReadOnly` | — | ninguno |

Además, con `admin`:

1. **Matriz de atribuciones** (`/cartera/atribuciones`, `/api/lending/attribution-matrices`), versión 1: analista
   (`Decisions.Analyst`) hasta 5.000.000; gerencia (`Decisions.Management`) hasta 20.000.000; Comité de crédito
   (colegiado) hasta 100.000.000; Consejo (colegiado) por encima y siempre que haya un vinculado; nivel de
   excepción por puntaje bajo.
2. **Órganos** (`/api/lending/governing-bodies`): Comité de crédito (tres principales, quórum y mayoría ordinaria),
   Consejo de administración (cinco principales y dos suplentes, personas del maestro), Comité de riesgos.
3. **Vinculados** con la plantilla `lending.related-parties`: el consejero D (`BoardMember`) y su cónyuge
   (`Spouse`).
4. **Segregación**: `Segregacion.Incompatibilidades` con su defecto `Decidir × Desembolsar`.

### 2.8 Líneas y parámetros

Plantilla `lending.credit-lines` en `/cartera/plantillas` (hojas `Lineas`, `Tasas`, `Documentos`, `Seguros`,
`Garantias`, `Equivalencias`), con códigos de catálogo de la cooperativa:

| Código | Modalidad / sub-segmento | Tasa | Sistema y convención | Lo particular |
|---|---|---|---|---|
| `CONLIB` | `CONSUMO` / `ConLibranza` | fija, cerca de la usura | cuota fija, 30/360, mensual y quincenal | nómina propia; pagaduría desde K2; seguro de vida deudores sobre saldo |
| `CONCAJA` | `CONSUMO` / `SinLibranza` | fija | abono constante, real/365 | «exige consulta a centrales»; tolerancia de mora de 1.000 |
| `VARIBR` | `CONSUMO` / `SinLibranza` | variable: IBR + margen, piso y techo | cuota fija, 30/360 | reajuste mensual |
| `MICRO` | `MICROCREDITO` / `Empresarial` | fija | cuota fija, quincenal | gracia a capital |
| `COMER` | `COMERCIAL` / `PersonaNatural` | fija | pago único | garantía exigida por rango |
| `ROTATIVO` | `CONSUMO` / `SinLibranza`, origen comercial | fija mayor que cero | cuota fija | cupo rotativo; concepto tributario del interés con su soporte (D20); equivalencias de Comercial |
| `EMPLEA` | `CONSUMO` / `Empleados` | fija | cuota fija, quincenal | sólo empleados (dimensión «deudor empleado») |

Parámetros en `/cartera/parametros` (`Lending.Parameters.Manage`, motivo obligatorio); los demás con su defecto:

| Clave | Valor en el ensayo | Para |
|---|---|---|
| `Pagos.DiasMaximosFechaValor` | 5 (D4) | §3.9 y plazo de la activación |
| `Mora.ToleranciaDeSaldoVencido` | 1.000 en `CONCAJA` (ámbito `CreditLine`) | §3.8 |
| `Contabilidad.ModoDePaso` | defectos de D8 (T56) | §3.10 |
| `Contabilidad.FaltaDeRegla` | `Bloquear` | §3.10: muerde en pantallas, advierte en el contrato |
| `Contabilidad.PoliticaSinRespuesta` | `ConfirmarConPendiente` (D7) | §3.10 |
| `Riesgo.CierreProgramado` / `Riesgo.DiaLimiteCierre` | `false` / 10 (E6) | §3.11 |
| `Originacion.DiasVigenciaDeAprobacion` | 2 | §3.6 (aprobación vencida) |
| `Reservas.DiasDeVigencia` · `Giros.DiasSinResultado` | 1 · 1 | §3.7, §3.6 |
| `Causacion.ContinuaTrasFallecimiento` | `true` | §5 |
| `Documentos.Firmante.Nombre` / `.Cargo` · `Documentos.PazYSalvoIncluyeCodeudas` | los del ensayo · `false` | §3.13 |

En `/inventario/parametros` (Comercial) se dejan, hasta la activación, `Cartera.IntegracionHabilitadaDesde` vacío,
`Cartera.PoliticaSinRespuesta`, `Cartera.ConsultaSegundos` y `Cartera.CuentaPorCobrarRegistradaPor =
Contabilidad`.

### 2.9 Personas del ensayo

Todas desde el diálogo único (`PersonaDialog`); **ninguna plantilla crea personas** (I11):

| Persona | Papel en el ensayo |
|---|---|
| A | Empleado de la cooperativa con crédito `EMPLEA` por nómina propia (§3.9) |
| B | Asociado al día en `CONCAJA` (recogida, §3.6) |
| C | Deudor con dos créditos de consumo (uno a 95 días al corte) y uno comercial al día (arrastre, §3.11) |
| D | Consejero (vinculado) |
| E | Coincide con la lista restrictiva cargada (§3.5) |
| F | Sin autorización de consulta a centrales; su crédito migrado trae la autorización sin evidencia |
| G | Asociado con cupo rotativo de 500.000 para las ventas de Comercial (§3.7) |
| H | Deudor migrado con 59 días de mora al corte en `CONCAJA` (suspensión, §3.8) |
| J | Deudor con un crédito castigado migrado (recuperación, §3.9) |
| L | Codeudor de B que a la vez es deudor en mora |
| P · S · W | Pagaduría (persona jurídica, K2) · aseguradora (persona jurídica) · abogado (K4) |
| la cooperativa | Como persona jurídica, para el convenio de su nómina propia (I13, K2) |

## 3. K1 · El recorrido del ensayo

**Orden y fechas.** §2 → §3.1 → §3.2 → §3.3 → §3.4 (activación) → §3.5 a §3.9 en los días siguientes, con §3.10 al
paso de cada operación → §3.11 al terminar M → §3.12 a §3.15 en cualquier momento. El corte C es un día de M que
**no** es fin de mes (T49: el primer cierre es el de M y toma el cierre base como anterior); la activación se
registra entre C+1 y C+5 con fecha C+1. Una primera carga con corte C−10 sirve de ensayo y queda reemplazada.
Todo en `coop_ensayo`, salvo lo que dice `coop_obligada`.

### 3.1 Cartera desplegada y sin activar (FR-142, T85)

Antes de cargar nada, y otra vez con la carga de §3.3 aplicada y sin activar:

1. **Contrato** (en `/cartera/dobles-de-prueba`, con `pruebas`): listar y simular responden `Answered` (C28; no
   `Applied`) sin obligaciones y con el motivo «Cartera no activada»; aplicar, reversar y resolver saldos se
   rechazan con `Lending.NotActivated`.
2. **Comercial**: una venta a crédito en `/pos` recibe `NotEnabled` y sigue como crédito provisional, igual que en la
   012; su mensaje se acumula en la bandeja sin intentos ni alertas.
3. **Nómina**: la ordinaria (`/nomina/liquidacion`) y la definitiva (`/nomina/liquidacion-definitiva`) se aprueban
   sin descuentos de Cartera. Con la carga aplicada, si incluyen a un deudor de la carga, exigen reconocer «Cartera
   por activar» (`AcknowledgeLendingPendingActivation`).
4. **Personas**: con la carga aplicada, borrar a un deudor de la carga responde `Person.HasActiveLoans`, y su ficha
   lo muestra «migrado, sin activar».
5. **Contabilidad** cierra sus meses como hoy; con la carga aplicada, cerrar el mes que contiene C+1 avisa
   `Accounting.Period.LendingLoadWillExpire`.
6. Antes de activar sólo se puede parametrizar, simular, migrar, otorgar cupos (evaluados contra la carga como si
   ya hubiera nacido), registrar autorizaciones y vinculados.

### 3.2 Líneas y usura (US1; FR-019–FR-033; SC-002, SC-018)

1. La plantilla de §2.8 con un error en una fila (una modalidad inexistente): `?mode=review` no guarda nada y lista
   `errors[] { sheet, row, column, code, message }`; `?mode=apply` con el error responde 422 `Import.Invalid`
   (US1-6). Corregida, todo entra.
2. Crear a mano una línea cuya tasa efectiva, con los cargos que computan, supera la usura de su modalidad:
   `Lending.Usury.Exceeded` con la usura vigente, la modalidad y la diferencia; no se guarda nada (US1-1).
3. `ROTATIVO` sin el concepto tributario del interés no se activa (`Lending.CreditLine.InterestTaxConceptMissing`).
4. Versión nueva de `CONLIB` desde el mes siguiente con otra tasa: queda versión; los créditos vivos de §3.4
   conservan su tasa pactada (US1-2).
5. `Cobranza.HorarioLunesAViernes` más amplio que el techo nacional: `Lending.Parameter.BelowLegalFloor` con el
   límite y su fuente (SC-018).
6. Usura que baja por debajo de una tasa pactada, línea cerrada y día sin usura (US1-3 a US1-5): en DEV y QA sólo
   si la usura real del mes siguiente lo produce; si no, quedan cubiertos por `LineasDeCreditoYUsuraTests` y los
   casos 13 y 14 (§2.4).

### 3.3 Migración de prueba y cuadre (US8-1 a US8-3, US8-9, US6-6; FR-139–FR-143; SC-013)

La carga trae unos 100 créditos de todas las líneas de §2.8 salvo vivienda (fuera hasta K6b, así se declara en el
acta), con: alturas de mora 0, 15, 45, 59, 95 y 200 días; reestructurados con conteo 1 y 2; codeudores
(L), garantías, pagaré físico; la pagaduría P y la etapa de cobranza como dato de K2 y K4; un castigado (J);
avisos previos y un reclamo en trámite; autorizaciones por finalidad (la de F sin evidencia); cupos y un
embargo; vinculados; historial de mora; y, por factura, las ventas de Comercial hasta C: tres migradas, una
cancelada antes del corte y una no migrada con saldo, además de una venta de **servicios** confirmada antes de
`CreditWithoutLending`.

1. `/cartera/migracion` → carga nueva con corte C−10 (`/api/lending/migration/loads`); cada parte se baja de
   `{id}/parts/{parte}/template.xlsx` (plantillas `lending.migration.{parte}`) y se importa con revisión previa.
   Una fila cuyo plan no suma el capital (`Lending.Schedule.PrincipalMismatch`) y un deudor que no existe en
   `COR_People` quedan marcados con fila y columna; nada se aplica (US8-1). Un error entre partes al aplicar responde
   `Lending.Migration.CrossPartErrors` con parte, fila y columna.
2. Aplicarla (`{id}/apply`) sólo guarda la carga, su revisión, su cuadre y su conciliación (T83). Su cuadre sale con
   diferencias, porque la apertura contable es de C; no importa: es un ensayo y se reemplaza.
3. Otra carga con corte C y aplicarla: la anterior queda **`Replaced`**, no se borra (US8-9).
4. **Cuadre** (`{id}/balance-check`): por concepto, modalidad, sub-segmento, categoría y cuenta, contra la
   contabilidad al corte; aparece la diferencia de 150.000 de §2.6 (US8-2).
5. **Conciliación de ventas** (`{id}/reconciliation`): las ventas acumuladas hasta C, listadas **sin consumirlas**,
   quedan migradas, canceladas antes del corte o no migradas con saldo. La venta de servicios se marca desde la
   lista como «aceptada fuera de Cartera (D29)», con motivo y punto de acta (G11).
6. Intentar activar con las diferencias sin aceptar: negado con `Lending.Activation.DifferencesNotAccepted`, que dice
   cuáles faltan (US8-3).
7. Una sesión del órgano que la política designe aprueba un punto `Decision` que acepta la diferencia y la venta no
   migrada; cada aceptación referencia ese `SessionItem` (D17, T64). Con eso el cuadre queda aceptado. La misma u
   otra sesión aprueba el punto de la **activación**: el acta es obligatoria (`LND_Activations.SessionItemId` no
   nulo, C61) y sin ella no se activa.

### 3.4 Activación con los dobles de prueba (US8-4, US8-9; FR-142; T16, T20, T84)

1. **Registro** en `/admin/participantes-de-integracion` (`RegisterIntegrationParticipantCommand`): el recaudador
   de prueba (`Class = Test`, `Role = ObligationsCollector`, adopción «ProveedorDeObligaciones» v1) y la tesorería
   de prueba (`Class = Test`, `Role = MessageDestination`, adopción «OrdenDeGiroEmitida» v1). El destino `Lending`
   sigue inactivo.
2. **Asistente** `/cartera/activacion`: lista lo que falta antes de dejar activar:
   - usuarios con roles de Cartera sin alcance (F4);
   - la oficina O3 con créditos y sin sucursal contable: se vincula a SUC2 (CH49);
   - la regla `RecaudoPorAplicar` con `CollectorClass = Payroll`;
   - el aviso de `DESC_CARTERA` sin cuentas, que la pantalla pregunta a Nómina;
   - lo que Comercial debe ajustar en sus parámetros;
   - el punto de acta de la activación (C61).

   Activar con algo de esa lista pendiente responde `Lending.Activation.RequirementsMissing` con `data.missing[]`.
3. **Fecha distinta de C+1**: negada con `Lending.Activation.CutoffInvalid` (US8-9); pasado el plazo o con el mes
   cerrado en Contabilidad, `Lending.Activation.DeadlinePassed`.
4. **Cobros provisionales** (C62): la plantilla `lending.provisional-collections` con los cobros de ventas
   confirmadas después de C que se registraron por fuera (factura, valor, fecha valor, referencia contable). La
   activación los **valida y guarda** en `LND_ProvisionalCollections` con la revisión previa, que deja un adjunto;
   nada nace todavía como pago.
5. **Activar** (`ActivateLendingCommand`, `Lending.Activation.Execute`). En **una transacción con guardados por
   tandas** de 1.000 créditos (C60; nada queda a medias si falla), comprobar:
   - recalcula cuadre y conciliación (si difieren de lo aceptado, pide aceptar otra vez);
   - nacen los créditos con una transacción `OpeningBalance` por concepto y **sin hechos contables**;
   - nace el cierre base (`MonthlyClose` `Kind = Base`) fechado en C, con la calificación y el deterioro migrados;
   - nacen cupos, bloqueos, autorizaciones, avisos, reclamos y vinculados;
   - un cupo otorgado a mano que excede la concentración queda bloqueado «cupo por revalidar»
     (`Cartera.CupoPorRevalidar`);
   - las cuotas de nómina propia de A vencidas entre C+1 y el registro quedan en `LND_InstallmentNonDeductions`
     con origen `Activation` y motivo «Cartera sin activar»;
   - el destino `Lending` queda activo, en la misma transacción, y sale el informativo `CarteraActivada` (US8-4).

   Los cobros provisionales guardados los aplica el **consumidor de Comercial** cuando nace la obligación de su venta,
   con clave idempotente (C62): nacen como pagos con su fecha valor y emiten `CobroProvisionalIncorporado`.
   Reprocesar ese mensaje no los aplica dos veces.
6. **Comercial** el mismo día: `Cartera.IntegracionHabilitadaDesde = C+1`, revisar `Cartera.PoliticaSinRespuesta`
   y fijar `Cartera.CuentaPorCobrarRegistradaPor`. Cartera responde a las ventas conciliadas: la migrada,
   `ObligacionCreada` con estado `Migrated`; la cancelada, obligación en cero con estado `CancelledBeforeCutoff`;
   la aceptada sin migrar y la de servicios, `ObligacionRechazada` no reprocesable. Ninguna se procesa como
   mensaje en vivo; la venta muestra el número (`INV_LendingObligationResponses`).
   **Venta fechada hasta C que llega después de activar** (C63; «anterior a la activación» de FR-143 = anterior al
   corte): queda `NotIncorporated`, con `ObligacionRechazada` no reprocesable (`Lending.Sale.NotIncorporated`) y
   alerta `Cartera.VentaNoIncorporada` (C99). Es la propuesta por defecto de la pregunta **I16**; si el dueño elige la espera reprocesable, este paso
   cambia.
7. **Liquidar** cada crédito migrado a la fecha C (`GET /api/lending/loans/{id}/settlement?date=C`): coincide con los
   saldos cargados.

### 3.5 Originación con comité (US2; FR-034–FR-042; SC-008, SC-009, SC-015, SC-022)

1. `/cartera/simulador` (`asesor.o1`): plan cuota a cuota, tasa efectiva y nominal, costo total y validación de
   usura, sin registrar nada (US2-1). Imprimir el PDF del simulador: no lleva número ni código.
2. `/cartera/solicitudes/nueva` desde la simulación, con `PersonSearchPicker`, las autorizaciones por finalidad
   (`CreditProcessing`, `CreditBureauQuery`) y la preferencia de abono: sin un documento obligatorio de la línea
   no se envía a análisis y se lista lo que falta (US2-2).
3. **Listas**: el `oficial` carga `compliance.restrictive-lists` en `/compliance/listas-restrictivas`; radicar a E
   deja la solicitud bloqueada y alerta `Cartera.CoincidenciaEnListas`; la libera sólo el oficial con motivo
   (`/api/lending/screening-results/{id}/release`); el asesor que radicó no puede (US2-3).
4. **Centrales**: pedir la consulta de F sin autorización: se impide y se explica cómo registrarla (US2-4). Con
   autorización, `CentralDeRiesgoSimulada` responde por el último dígito del documento: 1 reporte negativo, 2 sin
   historia, 3 puntaje bajo (lleva al nivel de excepción), 5 no disponible. En DEV y QA el resultado sirve; en
   producción un simulado nunca es evidencia para aprobar (T73).
5. **Tres solicitudes** de 3.000.000, 15.000.000 y 60.000.000 van al analista, a la gerencia y al comité. Si el
   monto consolidado cambia (por ejemplo, otra deuda del deudor), la solicitud se reenruta.
6. **Comité** en `/cartera/actas`: sesión con asistencia y quórum, un punto por solicitud, votos; el voto de
   `comite.1`, que radicó, se rechaza (`Lending.Session.VoterExcluded`, US2-6). Al cerrar, el acta queda con
   SHA-256 y PDF (`minutes.pdf`) y la decisión `ApproveWithConditions` con sus condiciones; un acta cerrada no
   cambia (`Lending.Session.Closed`) y se corrige con otra sesión (US2-5).
7. **Vinculado**: la solicitud de D va al consejo aunque el monto sea pequeño. En `coop_obligada` (con actividad
   financiera) exige 4 de 5 favorables de la composición, sin que D vote (US2-7); en `coop_ensayo`, la mayoría de
   los estatutos.
8. **Concentración**: una solicitud que lleva al deudor por encima de `CONCENTRACION_INDIVIDUAL` sobre el patrimonio
   técnico se bloquea con el límite y el exceso (`Lending.Concentration.LimitExceeded`, US2-8, SC-022). En `coop_obligada`, sin indicador vigente, se
   bloquea con `Lending.Concentration.TechnicalCapitalUnavailable` y se alerta `Cartera.PatrimonioTecnicoNoDisponible`.
9. **Segregación**: quien radicó no decide (`Lending.Segregation.Conflict`); `sinpersona` no decide
   (`Lending.Security.UserWithoutPerson`) (SC-015).
10. **Cupo rotativo** de 500.000 para G en `/cartera/cupos` (`CreditLimits.Assign`, con su monto operativo).
11. El puntaje muestra la explicación por variable (FR-037). Tiempos en QA: simulación p95 < 2 s (SC-008).

### 3.6 Desembolso (US3; FR-043–FR-048)

1. Pagaré desmaterializado por `DepositoDeValoresSimulado` con documento terminado en 3 (firma pendiente): el
   desembolso se impide hasta firma y custodia (US3-1); la tarea `cartera.pagares` actualiza el estado. La
   alternativa del pagaré físico queda registrada.
2. Una aprobación de hace más de `Originacion.DiasVigenciaDeAprobacion` exige una nueva (US3-2).
3. **Recogida**: desembolsar un crédito nuevo de B que recoge su crédito al día. La recogida entra por el contrato
   con `ClaseDeRecaudador.OwnLoanPayoff` y la clave `CAR:{operaciónDeDesembolso:N}:{obligationPublicId:N}`; el
   recogido queda cancelado con su comprobante de aplicación, se informa el neto a girar y los dos quedan
   enlazados; Contabilidad recibe `CreditoDesembolsado` y `PagoAplicado` (US3-3).
4. Recoger el crédito de H (en mora): la simulación advierte «requiere reestructuración» y hasta K3 no se tramita
   (`Lending.Payoff.RequiresRestructuring`, US3-4).
5. **Transferencia**: la orden de giro sella beneficiario y cuenta (I10); `/api/lending/disbursement-files` genera el
   archivo con `BankFileScope.LoanDisbursement`, la cuenta de origen elegida entre `CuentasBancariasAsync`, SHA-256 y
   enlace de descarga; sale `OrdenDeGiroEmitida` hacia `Treasury`.
6. En `/cartera/dobles-de-prueba`, la tesorería de prueba responde `ResultadoDelGiro`:
   - «girada»: la orden queda `Disbursed`;
   - «rechazada»: el desembolso se reversa desde su origen, sin intereses, con `DesembolsoReversado` y filas espejo,
     y el crédito queda pendiente de un giro nuevo o anulado (US3-6);
   - «cheque no cobrado» sobre otra orden.

   Una orden sin resultado pasado `Giros.DiasSinResultado` alerta `Cartera.GiroSinResultado`.
7. El `gerente` que aprobó no puede desembolsar (`Decidir × Desembolsar`); el `jefe` por encima de su monto
   operativo, tampoco.
8. Usura del día del desembolso menor que la del día de aprobación (US3-5): sólo si la real lo produce; si no, la
   e2e.

### 3.7 Ventas desde Comercial (US6, US7-6; FR-049–FR-060; SC-005, SC-008, SC-022)

1. `cajero.1` (usuario de Comercial, como en el ensayo de la 012) vende en `/pos` a crédito a G 300.000 a 6 cuotas
   en la línea `ROTATIVO`. Comprobar:
   - la consulta de cupo y estado responde en menos de un segundo con el disponible, el estado y las líneas, con su
     plazo máximo, cuotas máximas y periodicidades (US6-1);
   - la reserva de número, repetida para el mismo documento, da el mismo número (US6-2);
   - la factura lleva el número y sale `VentaACreditoRegistrada` v2;
   - con el despachador encendido y sin cola, el crédito existe en **menos de cinco segundos** con el número, la
     fecha y la referencia de la factura, y la venta muestra el número al llegar `ObligacionCreada` (US6-3, SC-005).
2. Un borrador con reserva se descarta: la reserva se libera. Otra sin confirmar ni liberar pasa a `Expired` tras
   `Reservas.DiasDeVigencia` con `Cartera.ReservaVencida`, y su número nunca se reasigna.
3. Cupo insuficiente → `Inventory.Credit.QuotaExceeded`; deudor bloqueado o con más mora que
   `Cupo.MoraMaximaParaVender` → `Inventory.Credit.LendingBlocked`.
4. Una venta que excede el límite de concentración llega y queda con validación fallida, listada como exceso
   (SC-022). La validación fallida bloquea crédito nuevo al deudor, alerta en Comercial y en Cartera y no anula la
   venta (US6-4); el `jefe` la confirma por excepción (`ValidationFailures.Confirm`, B8).
5. Nota crédito por devolución parcial: baja el capital, se reversa el interés de la parte devuelta y el plan se
   recalcula (US6-5). Una nota mayor que el saldo deja saldo a favor visible en el listado del contrato.
6. Reprocesar desde la bandeja un mensaje ya procesado: la misma respuesta y ningún crédito nuevo (US6-7).
7. Una venta de servicios con un medio `CreditWithoutLending` no consulta, no reserva ni emite a Cartera (A6).
8. En Contabilidad, la venta cuya cuenta por cobrar registró Contabilidad se reclasifica a cartera sin duplicarla
   (rol `CuentaPorCobrarComercial`, US7-6).

### 3.8 Causación diaria y liquidación (US4; FR-062–FR-070; SC-001, SC-002, SC-007, SC-020)

1. Cada mañana, en `/api/lending/process-runs` (las cuatro clases de corrida, C59: `DailyCycle`, `MonthlyClose`,
   `CloseReopen` y `NationalValueCorrection`), la corrida `DailyCycle` del día queda `Completed`; los
   `InteresCausado`, `MoraCausada` y `SeguroCausado` salen por partes (fecha × sucursal × tipo × tanda) y, en
   `PorLotes` con `FinDeCausacion`, forman **un comprobante `CA` por día** con la sucursal en cada línea y la lista
   de créditos (US7-3).
2. Pedir otra corrida (`RequestLendingProcessRunCommand`, 202): no causa nada dos veces (US4-1); mientras corre,
   otra pedida responde `Lending.Process.AlreadyRunning`.
3. Un crédito con una cuota vencida: la porción vencida causa sólo mora y la no vencida sólo corriente; la suma no
   supera la usura del día (US4-2).
4. H cruza los 60 días: lo nuevo va a suspenso, sale `CausacionSuspendida` (informativo, sin comprobante) y la
   liquidación a la fecha sigue incluyendo ese interés (US4-3).
5. **Un día sin causación**: con `Lending:Process:Enabled = false` un día, la tarea `cartera.vigilancia` alerta
   `Cartera.ProcesoDiarioNoCorrio`; al volver a `true`, la pasada siguiente causa los dos días, cada uno con su
   tasa y sin duplicar (US4-4, SC-020).
6. **Liquidación** (`/cartera/creditos/{id}`, `GET /api/lending/loans/{id}/settlement?date=&recordedAsOf=`; C83: en
   `/loans/*`, `/risk/ratings` y `/api/core/national-values`, `date` es D y `recordedAsOf` es R; en informes y tablero,
   `asOf` es la fecha de corte):
   - a una fecha futura se proyecta sin pagos intermedios y dice hasta cuándo vale (US4-5);
   - una a fecha pasada con «información hasta ayer», repetida hoy después de registrar un pago con fecha valor
     anterior, da **idéntico al centavo** (US4-6, SC-001);
   - cada lectura queda auditada con D, R y la huella.
7. `VARIBR` sin IBR para su fecha de reajuste (cuando ocurra en el ensayo, o en local): ese crédito no causa
   (`NationalValues.Missing`), su marca de agua no avanza, queda en `LND_ProcessRunErrors` y se alerta
   `Cartera.ValorNacionalFaltante`; los demás siguen.
8. `POST /api/lending/integrity/verify` (`Integrity.Verify`): el libro de cada crédito es igual a su reconstrucción.
9. El tiempo de la corrida, escalado a la cartera del ensayo, da la primera señal de SC-007; la medición formal es
   `CausacionDiariaPerformanceTests`.

### 3.9 Pagos por el contrato (US5, US10-6; FR-071–FR-089; SC-003, SC-004, SC-010)

Con el recaudador de prueba en `/cartera/dobles-de-prueba` (`pruebas`), salvo Nómina. Su código de participante es
`TST` y su clave de pago, `TST:{guid:N}`: la pantalla **propone una nueva** en cada operación y **deja repetirla** a
mano, para probar la idempotencia (C35; ya no existe el prefijo `TEST:`).

1. **Listar** a C como deudor y como codeudor (resultado `Answered`, C28): número, línea, estado, días de mora,
   exigible hoy, cuota del ciclo, vencido, total para cancelar, anticipo, saldo a favor, modalidades y bloqueos
   (FR-084). Simular también responde `Answered`; sólo aplicar responde `Applied`.
2. **Valor libre** sobre dos cuotas vencidas: cubre completa la más antigua y, dentro de cada cuota, gastos,
   seguros, mora, corriente y capital (US5-1).
3. **Abono extraordinario**: una sola simulación muestra los dos planes; se aplica la elección del deudor con el
   identificador de la simulación (SC-003, US5-2). Sin interacción y con preferencia registrada, se aplica la
   preferencia y se le informa (D24); sin preferencia, queda saldo a favor, y la resolución
   (`ResolverSaldoAFavorAsync`) lo aplica como abono con la opción que el deudor elija.
4. **Pago mayor que el total para cancelar**: el crédito queda cancelado y el excedente vuelve al recaudador (US5-3).
5. **Idempotencia**: repetir tres veces la clave `TST:{guid:N}` que propuso la pantalla devuelve el comprobante
   original y no aplica dos veces (US5-4, SC-010); la misma clave con otro valor responde
   `Lending.Contract.KeyReused`.
6. **Distribución cambiada**: simular, dejar correr la causación u otro pago y aplicar con la simulación vieja:
   `Lending.Payment.DistributionChanged` con la nueva (US5-6).
7. **Anticipo** de tres cuotas: se informa que no reduce intereses y se ofrece el abono a capital (US5-7); al vencer
   cada cuota sale `AnticipoAplicado`.
8. **Reverso** desde `/cartera/creditos/{id}` (`ReverseLoanPaymentCommand`, `Payments.Reverse`) del primero de dos
   pagos: muestra la redistribución del posterior; al confirmar, saldos, cuotas, días de mora, causación y
   calificación diaria quedan como si el primero no hubiera existido (US5-5, SC-004). El recaudador recibe
   `PagoReversadoPorCartera`; reversar el reverso responde `Lending.Reversal.OfReversal`.
9. **Fecha valor**: 5 días atrás se acepta; 6 días, `Lending.Payment.ValueDateOutOfRange`; después del cierre de
   §3.11, una fecha valor de M sin la atribución responde `Lending.Payment.ValueDateBeforeClosing`.
10. **Recuperación**: un pago a J (castigado) emite `RecuperacionDeCastigada`.
11. **Nómina ordinaria** de A, quincenal (US10-6, FR-112):
    - la cuota llega como novedad `LoanDeduction` por obligación desde `CuotasDeNominaAsync`;
    - una cuota que no cabe en el tope queda no descontada con `DeductionCap`;
    - al aprobar, el lote se aplica por el contrato antes del NM, dentro de la transacción de la aprobación;
    - `/api/payroll/runs/{runId}/loan-deductions` muestra esperado, descontado, no descontado y motivo;
    - el no descontado queda en `LND_InstallmentNonDeductions` (origen `Contract`), la cuota sigue exigible y se
      alerta `Cartera.CuotaNoDescontadaPorNomina`;
    - una cuota ya pagada por otro medio vuelve a Nómina como excedente;
    - el comprobante `DN` lleva `DESC_CARTERA` contra el puente;
    - reversar la nómina reversa sus recaudos por el contrato;
    - el `jefe` no puede reversar ese pago desde Cartera (`Lending.Reversal.OwnedByCollector`).
12. **Definitiva** de A: propone por la consulta de obligaciones (total para cancelar o cuotas causadas, según la
    política); si entre la propuesta y la aprobación entra un pago, `Payroll.Settlement.LoanProposalOutdated`; al
    aprobar, todo o nada.

### 3.10 Hechos contables y validación previa (US7-1, US7-2, US7-5; FR-125–FR-130)

1. El desembolso de §3.6 produce en Contabilidad un `DS` con las cuentas de su línea, modalidad, sub-segmento y
   concepto, y el número del crédito como cruce `PG` (US7-1).
2. **Hueco deliberado** en la matriz: sin la regla de `CreditoDesembolsado` para `MICRO`, el desembolso por pantalla
   se bloquea diciendo qué regla falta (`Contabilidad.FaltaDeRegla = Bloquear`, US7-2); un pago por el contrato de
   un crédito `MICRO` advierte, sella `PrevalidationOutcome`, alerta y se aplica (A8).
3. Un hecho que Contabilidad no puede contabilizar queda `Rejected` con su motivo en `/cartera/bandeja-de-mensajes`
   (`Lending.Messages.Read`); se completa la regla y se reprocesa (`Messages.Reprocess`). La operación de Cartera no
   se deshace.
4. `/contabilidad/cartera/lotes`: el lote `CA` de cada día y, tras §3.11, el `PV` del cierre.
5. **Conciliación** (`/cartera/conciliacion-contable`, `/api/lending/accounting/reconciliation?date=`): por fecha
   contable, capital e intereses por modalidad, sub-segmento y categoría, deterioro, anticipos, saldos a favor,
   suspenso, castigada y puentes; cada diferencia explicada por los hechos pendientes, en lote, rechazados o que no
   pasan (US7-5). Una diferencia sin explicar alerta `Cartera.DescuadreContable`.
6. **Dimensión cambiada**: A deja de ser empleado; el ciclo siguiente emite `DimensionContableCambiada` (por lotes).
   Un cambio de oficina hecho por una operación sale en línea (T56).
7. En Contabilidad, `ReverseDocumentCommand` sobre un comprobante de Cartera responde `ModuleOwned`: lo de Cartera
   se corrige con comprobante nuevo.

### 3.11 Cierre mensual, conciliación y cierre contable (US9, US7-4, US8-5; FR-099–FR-107, FR-146; SC-006, SC-013, SC-021)

Al empezar M+1:

1. Contabilidad intenta cerrar M: `Accounting.Period.LendingNotClosed` (T57).
2. Antes de cerrar, el `analista` califica peor un crédito por análisis con motivo; uno mejor se rechaza (US9-4).
3. `/cartera/cierres` → ordenar el cierre de M (`OrderMonthlyCloseCommand`, 202). Exige el cierre base como anterior
   (T49), el período contable abierto, la causación al día hasta fin de mes y la réplica al día. La corrida
   `MonthlyClose` avanza por tandas; el encabezado nace en la transacción final con R sellado.
4. **Arrastre** de C: los dos créditos de consumo quedan en C y el comercial sigue en A (US9-1).
5. **Reporte de cambios**: cada crédito que cambió de categoría con la anterior, la nueva y la causa (US9-3); el de
   pago único pasado de sus días queda en la categoría de su regla (US9-5).
6. **Hechos**: `CarteraReclasificadaPorCalificacion` de todo saldo con categoría (capital, intereses, deterioro y
   suspenso) de A a C (US7-4) y `DeterioroCalculado` como **diferencia frente al cierre base**: el deterioro migrado
   no se reconoce otra vez (US8-5, SC-013); salen por partes y forman el lote `PV`.
7. **Conciliación** a fin de M: cero por modalidad, sub-segmento, categoría y concepto, descontados los mensajes
   pendientes (SC-006). La contadora la firma.
8. **Reabrir**, con el período contable de M todavía abierto: `ReopenMonthlyCloseCommand` sobre el último cierre,
   con motivo y atribución, emite `ReversoDeCierre` por partes y `CierreDeCarteraReabierto`; cerrar de nuevo crea
   `Version + 1` y la conciliación vuelve a dar cero.
9. Contabilidad cierra M; si quedan hechos de Cartera pendientes con fecha en M, avisa
   `Accounting.Period.LendingPending`. Desde ahí, reabrir el cierre de M se rechaza, igual que reabrir uno que no
   es el último (D12).
10. **Pago con fecha valor de M** después del cierre (dentro de `Pagos.DiasMaximosFechaValor`), con
    `Payments.BackdateBeforeClose`: el cierre de M no cambia y el efecto se reconoce en M+1 (US9-2).
11. **`coop_obligada`** (activada en DEV o QA sin K6a): su cierre usa el Anexo 1, lleva la marca «modelo obligatorio
    no disponible» y alerta (US9-8, D28).
12. La calificación diaria informativa (`/api/lending/loans/{id}/rating?date=`) usa el mismo motor y no se guarda.

### 3.12 Mensajes al deudor de K1 (FR-112, FR-122; D24)

1. El abono a capital aplicado de §3.9 genera el informativo por correo (`CanalDeCorreo`), despachado por
   `cartera.mensajes` dentro del horario y con la autorización del canal; queda en `/cartera/mensajes` con su
   plantilla, versión y canal sellados (`LND_DebtorMessages`, inmutable).
2. Por SMS o WhatsApp, `MensajeriaSimulada` responde por el último dígito: 1 número inválido, 2 entregado en la
   consulta siguiente (`cartera.entregas`), 3 fallo de entrega (`Cartera.MensajeAlDeudorFallido`), 5 no disponible
   (el circuito alerta `Cartera.CanalExternoNoDisponible`). En DEV y QA nada sale por SMS ni WhatsApp.
3. Un envío manual fuera de horario: `Lending.DebtorMessage.OutsideLegalWindow` con `nextAllowedAt`; a quien no es
   deudor ni codeudor obligado: `Lending.DebtorMessage.RecipientNotObligated`.

### 3.13 Documentos de K1 (US13-1, US13-2, US13-4; FR-131, FR-132; pregunta I5, C88)

1. Plan de pagos, paz y salvo y certificado de saldo desde `/cartera/documentos` (`IssueLendingDocumentCommand`), en
   un paso, con número, firma por parámetro, código `XXXX-XXXX-XXXX-XXXX` y QR a `/cartera/verificar?codigo=`; en DEV y
   QA, con «AMBIENTE DE PRUEBAS – SIN VALIDEZ».
2. Paz y salvo general de C: negado, diciendo qué obligación tiene saldo (`Lending.Document.ClearanceDenied`, US13-1).
   El de un deudor con una venta en reproceso se niega nombrando la factura; el de quien tiene una venta aceptada
   sin migrar se emite mencionándola fuera de Cartera (US13-4).
3. Verificar el código con un usuario de la cooperativa: auténtico y sus datos esenciales (US13-2).
4. Reversar el pago que canceló el crédito: el paz y salvo emitido después verifica como **anulado**
   (`VigenciaDeDocumentos`). Reimprimir es descargar el mismo PDF guardado.

### 3.14 Seguridad, habeas data y auditoría (US17; FR-003–FR-006; SC-014, SC-015)

1. `asesor.o2` abre por URL un crédito de O1: el 404 genérico, y la auditoría registra `Lending.Access.OutOfScope`.
2. `lectura` llama por `curl` a `POST /api/lending/disbursements`: 404 y `Lending.Access.Denied` (US17-1).
3. El `analista` intenta decidir una solicitud por encima de su atribución: no puede; queda auditado.
4. `revisor` ve créditos, actas y auditoría y no puede modificar nada (US17-2).
5. Un comando con un campo inválido queda como `Lending.Request.Rejected` (campos y códigos, sin valores) (F9).
6. `/admin/auditoria`, pestaña «Integridad» (`POST /api/audit/integrity/verify`, `auditor`): sin alteraciones. La
   detección de una alteración se prueba sólo en local, alterando un evento en Mongo (US17-3).
7. **Revocación** de F con una obligación viva en `/compliance/solicitudes-del-titular`: se responde que no procede
   mientras exista el deber, con constancia y plazo en días hábiles (US17-5).
8. La autorización migrada sin evidencia de F deja su crédito vivo y cobrable, y bloquea la consulta y el reporte a
   centrales y los mensajes de cobranza (`Lending.Authorization.WithoutEvidence`, FR-006).
9. **Informes de K1** (`/cartera/informes?vista=`, `/api/reports/lending/{vista}?format=`): `aging`, `placement`,
   `receipts`, `transactions`, `usury`, `debtor-messages`, `migration-reconciliation`, con el alcance del usuario;
   exportar exige `Reports.Export` (y `Reports.ExportPersonalData` para las columnas con datos personales) y deja
   una auditoría `Lending.Report.Exported`.

### 3.15 Valores nacionales en el tiempo (T28, T39, T40)

1. La usura real del mes siguiente llega por la réplica sin intervención; si alguna línea queda por encima, se
   comprueban US1-4 y US1-5 (§3.2, paso 6).
2. Una cooperativa con migraciones pendientes se salta en la réplica y queda visible con
   `Cartera.ReplicaDeValoresNacionalesAtrasada`; un índice viejo alerta `Cartera.IndiceDesactualizado`.
3. **Sólo en local**: corregir una usura ya usada (`CorrectNationalValueCommand`, `/api/saas/national-values/{id}/corrections`).
   El primer paso del ciclo siguiente encola la corrida `NationalValueCorrection`, que nadie ordena a mano, deja
   `AjusteDeCausacionPorFechaValor` y, si se cobró de más, `SaldoAFavorReconocido`; la liquidación con R anterior a
   la corrección no cambia (SC-001, SC-002).

## 4. K2 · Libranza (US10; FR-108–FR-116; SC-016)

Preparación: la pagaduría P (persona jurídica) con convenio en `/cartera/pagadurias` (`PayrollAgreements.Manage`):
vigencia, fechas de corte, envío y pago, contactos, códigos de respuesta, tope, reglas de novedades, ventana de
giro (`Libranza.VentanaDeGiroDias` = 3) y umbral (`Libranza.UmbralDeMoraDePagaduria`); sus formatos de ida y vuelta
en `/maestros/formatos-bancarios` con los ámbitos `PayrollDeductionRequest` y `PayrollDeductionResult`, elegidos por
referencia desde la versión del convenio (no compiten por vigencia: `LosFormatosElegidosPorReferenciaNoCompitenPorVigencia`).
El convenio de la nómina propia, con la cooperativa como persona jurídica (I13).

1. Una solicitud por libranza cuyo descuento total supera el tope legal sobre el salario después de los descuentos
   de ley se rechaza mostrando la capacidad disponible, contra la certificación adjunta
   (`LendingPayrollCertification`) (US10-1).
2. **Archivo** del período de P con 20 obligaciones (`/cartera/archivos-de-descuento`, `PayrollDeductionFiles.Generate`):
   cuotas exigibles en el formato vigente, con altas, cambios y cancelaciones (`NovedadesDeLibranza`), inmutable con
   su huella; sale `DescuentosPorPagaduria` hacia `Receipts`. El recaudador de prueba confirma el envío
   (`EnvioDeDescuentosConfirmado`) y el archivo queda `Sent` (US10-2). Regenerarlo antes del envío crea versión y
   deja la anterior `Superseded`; después, `Lending.PayrollFile.AlreadySent`.
3. **Respuesta**: en el recaudador de prueba se carga un archivo con 17 descontados y 3 no aplicados; sale
   `ResultadoDeDescuentos` (`ConsumePayrollDeductionResultCommand`): 17 quedan «pendiente de giro» y 3 no aplicados
   con su motivo (retiro, saldo insuficiente, prelación) y la acción siguiente según el convenio; se alerta
   `Cartera.NoAplicadoDePagaduria` y se informa al deudor por sus canales (US10-4).
4. Pasa el vencimiento con cuotas pendientes de giro: dentro del umbral, ni días de mora ni cambio de calificación ni
   gastos ni reporte (US10-3). Vencida la ventana, `Cartera.GiroDePagaduriaAtrasado`; superado el umbral, el deudor
   se informa y se califica individualmente.
5. El giro de P entra por el contrato con la referencia de la línea pendiente: levanta el pendiente, fecha valor la
   del descuento sin el límite de D4 (A9) y la pagaduría como tercero. Un pago de P sin referencia se aplica como
   `ExternalPaymasterDeduction` sin levantar pendientes.
6. **Traslado**: el deudor pasa a otra pagaduría con convenio y la libranza conserva su fecha original para la
   prelación (US10-5); retirado sin nuevo empleador pasa a caja con aviso; el cambio con o sin libranza emite
   `DimensionContableCambiada`.
7. El reporte `payroll-deduction-reconciliation` cuadra enviado, aplicado, pendiente de giro y no aplicado; las
   tareas `cartera.archivos-de-descuento` y `cartera.giros-de-pagaduria` avisan lo que toca.
8. Los no aplicados de la nómina propia siguen ahora la regla de su convenio (FR-115) y se gestionan en
   `/cartera/no-aplicados`.

## 5. K3 · Novedades, condonaciones, insolvencia y castigo (US11, US14; FR-090–FR-098)

1. Prórroga pedida por dificultades de pago sobre un crédito en mora: se clasifica como reestructuración y muestra el
   plan nuevo y la calificación resultante (US11-1).
2. **Reestructurar** el crédito de C (en C): sin la aceptación firmada de L (`LendingNoveltyConsent`), el aprobador
   decide con motivo no aplicarla o aplicarla con esa garantía extinguida, y se recalculan cobertura y deterioro
   (US11-2). Aplicada por prórroga, fecha o tasa conserva número e historial con el plan anterior como versión; por
   novación o refinanciación nace un crédito enlazado que hereda marca, conteo, calificación e historial; en los dos
   casos queda marcado reestructurado y la calificación no mejora (US11-3). Emite `CreditoReestructurado`,
   `CreditoModificado` o `CreditoNovado` (comprobante `CN`).
3. La mejora de una categoría por tramo de cuotas consecutivas y la recaída se ven en los cierres siguientes (US11-4).
4. Condonación de mora por encima de la atribución del gerente: va al nivel siguiente y exige motivo (US11-5);
   aplicada, `CondonacionAprobada`.
5. La recogida de un crédito en mora en un desembolso ya se tramita como reestructuración.
6. **Insolvencia** de un deudor: `LND_InsolvencyProceedings` en `Negotiating` suspende causación y cobro y mantiene la
   calificación; el acuerdo (una reestructuración) o el fracaso lo cierran.
7. **Fallecimiento**: bloquea cobranza, mensajes y reporte negativo y abre la reclamación en `InProcess`; la
   causación sigue (`Causacion.ContinuaTrasFallecimiento`). El pago de la aseguradora S por el recaudador de prueba,
   con S como pagadora, cierra la reclamación (`Paid`) en la misma transacción (T102).
8. **Castigo** (US14): un lote con un deudor que sigue siendo asociado se rechaza diciendo el requisito
   (`Lending.WriteOff.RequirementMissing`, US14-1); el
   consejo aprueba el lote con acta y cada crédito queda castigado con referencia al punto, con `CreditoCastigado`
   (`PV`) y, si la entidad está obligada a reportar castigos, la alerta del plazo (US14-2); un pago posterior queda
   como recuperación y se ve en `charge-offs` (US14-3).
9. **Evaluación de cartera** (`/cartera/evaluaciones`, `PortfolioEvaluations.Prepare` y `.Apply`) aprobada por el
   consejo: recalifica hasta la siguiente.
10. Vistas `restructured` y `charge-offs` del centro de informes.

## 6. K4 · Cobranza y aviso previo (US12; FR-117–FR-124; SC-011, SC-012)

1. Etapas en `/cartera/etapas-de-cobranza`: preventiva, administrativa, prejurídica y jurídica. Un crédito que pasa
   de 30 días cambia de etapa en el ciclo siguiente y se asigna a un gestor por la regla (`RepartoDeGestores`)
   (US12-1).
2. Un mensaje de cobranza programado para un domingo sale el lunes a la primera hora permitida, con el evento
   `Deferred` como constancia (US12, prueba independiente).
3. Contacto directo por teléfono el lunes: un WhatsApp el miércoles de la misma semana se impide (US12-2).
4. Promesa de pago para el viernes: llega el pago y queda cumplida sola; sin pago, incumplida y de vuelta en la
   agenda (US12-3).
5. Campaña por WhatsApp con un deudor que no lo autorizó: se le excluye con el motivo (`Excluded`) (US12-4).
6. **Aviso previo**: un crédito que llega al reporte negativo sin aviso con constancia no se reporta; la constancia
   es el evento `Sent` del mensaje o el registro manual con la guía adjunta (`LendingPriorNoticeEvidence`); bajo el
   umbral exige dos comunicaciones en días distintos; quien se puso al día y vuelve a caer en mora necesita un aviso
   nuevo para este episodio (US12-5). Un aviso `SIMULADO` en producción nunca es constancia.
7. Un gasto de cobranza se causa sólo después del `Sent` o `Delivered` de su aviso `CollectionFeeNotice`
   (`GastoDeCobranzaCausado`).
8. Acuerdo de pago sobre lo vencido sin cambiar condiciones; incumplido, vuelve a su etapa. Cobro jurídico con el
   abogado W; la insolvencia lo suspende.
9. Reclamo sobre el reporte: «reclamo en trámite» y, vencido el plazo sin respuesta, aceptado por el ciclo diario.
10. Ninguna pantalla ni campaña deja elegir una referencia personal como destinatario
    (`LaCobranzaNuncaContactaReferencias`).
11. SMS y WhatsApp reales sólo con contrato de la cooperativa y plantillas aprobadas (`ProviderTemplateId`) (I1, I15).

## 7. K5 · Informes, documentos restantes, SICSES y centrales (US13-3, US15, US16; FR-131–FR-138; SC-012, SC-019)

1. Extracto de un período y certificado tributario del año: intereses pagados por crédito, con los de vivienda
   separados, y el saldo al 31 de diciembre (US13-3). Antes de K5, pedirlos responde
   `Lending.Document.KindNotAvailable` (C88); las otras tres clases ya existen desde K1 (§3.13).
2. Tablero `/cartera/tablero` (`/api/lending/dashboard`): un usuario con alcance de una oficina sólo ve su oficina
   (US15-1).
3. **Cosecha** por mes de desembolso: la mora a 30, 60 y 90 días comparada con un cálculo a mano sobre la cartera
   del ensayo (US15, prueba independiente); la e2e lo hace con seis meses de reloj.
4. **Concentración**: el deudor cerca del límite aparece señalado con su porcentaje (US15-2).
5. **SICSES** (`/cartera/sicses`, `ValidateSicsesFileQuery`, `GenerateSicsesFileCommand`): con un dato inválido, la
   validación lo señala y la generación deja una versión `Validated` sin archivo (`Lending.Regulatory.BlockingIssues`)
   (US16, prueba independiente); con el layout sin cotejar, la advertencia `Lending.Regulatory.LayoutSinCotejar`; la
   marca de D28 como advertencia, nunca dentro del archivo; SHA-256; `MarkRegulatoryFileUploadedCommand` con el
   radicado. El dueño pasa el archivo por el validador de la Superintendencia (SC-019). **Condicionado a E11**: si
   el primer vencimiento del formato de cartera del SICSES de COOFLOPAL llega antes que K5, la propuesta por defecto
   adelanta a K1 **sólo** la proyección y el archivo del formato de cartera, y este paso se ensaya en §3 (después
   del primer cierre de §3.11) en vez de aquí; si el dueño responde que no, rige el plan de §11.1, punto 2.
6. **Centrales** (`/cartera/centrales`, `GenerateCreditBureauFileCommand`): un dato negativo sin aviso con constancia
   se excluye y queda listado (US16-2); un deudor sin autorización de reporte no se incluye (US17-4); los reclamos en
   trámite van con su marca; un reporte hecho sin aviso se retira en el siguiente.
7. Vistas `quality`, `vintages`, `concentration`, `issued-documents` y `bureau-exclusions`.

## 8. K6a · Pérdida esperada (US8-10, US9-6, US9-7, US9-9; FR-105, FR-106; SC-023)

En `coop_obligada` y, para la adopción voluntaria, en `coop_ensayo`, con `CatalogoDeCartera.EntregaVigente = K6a`:

1. El maestro registra `SarcAnexo2`; las tablas transcritas de las imágenes de la circular van con `verified = false`
   hasta el cotejo del dueño y la contadora (E8).
2. **Activación de una obligada** sin los parámetros de FR-106 vigentes a la fecha del primer cierre que los use, o
   con créditos migrados de los segmentos cubiertos sin historial completo: negada, con la lista (US8-10). La carga
   complementaria `lending.arrears-history` (`ImportArrearsHistoryCommand`, `ArrearsHistory.Import`) completa el
   historial sin transacciones ni hechos.
3. **Cierre** de la obligada: consumo con libranza, consumo sin libranza y comercial de persona natural por pérdida
   esperada, los demás por el Anexo 1; intentar dejar uno cubierto en el Anexo 1 se rechaza citando la norma (US9-6).
4. **Adopción voluntaria** en `coop_ensayo` (`RegisterImpairmentModelElectionCommand`, `ImpairmentModel.Manage`) con
   punto de acta, estudio de impacto y aviso a la Superintendencia: el primer cierre de su vigencia usa la pérdida
   esperada en los segmentos cubiertos, el anterior no cambia y el hecho de deterioro es la diferencia; el
   desistimiento vuelve al Anexo 1 desde su fecha sin recalcular cierres (US9-7). Se niega sin K6a, con historial
   incompleto o con el aviso fuera de plazo.
5. **Obligación sobrevenida con K6a**: una versión del perfil que obliga a `coop_ensayo` desde una fecha posterior;
   el primer cierre de esa vigencia usa la pérdida esperada, los anteriores no cambian y se listan los créditos sin
   historial completo, con la regla de meses faltantes (US9-9).

## 9. K6b · Vivienda (FR-024, D18)

1. Antes de K6b: una solicitud o una fila de migración de modalidad `VIVIENDA` se rechaza, y sus cuentas quedan
   fuera del cuadre de activación con la constancia en el acta.
2. Con K6b: valores `UVR` y `TOPE_TASA_VIVIENDA` registrados y replicados; un crédito de vivienda en UVR con tasa fija
   bajo el tope, sin mora sobre intereses (`Mora.SobreIntereses` nunca en vivienda) y con su `RulesVersion` nueva;
   la migración de vivienda y su cuadre; E1/E2 por tiempo en E.

## 10. QA por rol

Con los usuarios de §2.7. Los permisos se leen por `GET /api/admin/permissions/mine` y `PermissionGate` esconde lo
que no se puede, pero la puerta es el servidor. La tabla es lo que se espera de las plantillas antes de editarlas;
su contenido exacto lo fija `PerfilesSugeridos` en `data-model.md`, cotejado con los destinatarios de las alertas
(C74). `cartera.administrador` no decide ni vota (C86):

| Acción (permiso) | Admin. | Asesor | Analista | Jefe | Comité / Consejo | Gerente | Gestor | Oficial | Contador | Auditor / Revisor |
|---|---|---|---|---|---|---|---|---|---|---|
| Ver créditos y liquidar (`Loans.Read`, `Payoffs.Read`) | sí | su alcance | sí | sí | sí | sí | su alcance | sí | sí | sí |
| Simular (`Simulations.Run`) | sí | sí | sí | sí | no | sí | no | no | no | no |
| Radicar (`Applications.Create`, `.Submit`) | no | sí | no | sí | no | no | no | no | no | no |
| Analizar (`Applications.Analyze`) | no | no | sí | sí | no | no | no | no | no | no |
| Decidir en nivel individual (`Decisions.*`) | **no** | no | `Analyst` | `Coordinator` | no | `Management` | no | no | no | no |
| Votar en sesión (`Sessions.Vote`) | **no** | no | no | no | sí | no | no | no | no | no |
| Desembolsar (`Disbursements.Execute`) | no | no | no | hasta su monto | no | no | no | no | no | no |
| Liberar una coincidencia (`Compliance.RestrictiveLists.Release`) | no | no | no | no | no | no | no | sí | no | no |
| Reversar un pago (`Payments.Reverse`) | no | no | no | sí | no | no | no | no | no | no |
| Cambiar la preferencia de prepago del deudor (`PrepaymentPreferences.Manage`, C85) | no | sí | no | sí | no | no | no | no | no | no |
| Ver condonaciones (`/api/lending/waivers`, con `Loans.Read`, C85) | sí | su alcance | sí | sí | sí | sí | su alcance | sí | sí | sí |
| Conciliación contable y cuadre de una carga (`Reconciliation.View`, C85) | sí | no | no | sí | no | no | no | no | sí | sí |
| Layouts y archivos regulatorios (`RegulatoryFiles.Read`, C85) | no | no | no | no | no | no | no | no | sí | sí |
| Cerrar y reabrir el mes (`MonthlyClose.Execute`, `.Reopen`) | no | no | no | sí | no | no | no | no | no | no |
| Matriz y lotes contables (`Accounting.LendingRules.*`, `.LendingBatches.*`) | no | no | no | no | no | no | no | no | sí | no |
| SICSES y centrales (`RegulatoryReports.Generate`, `CreditBureauFiles.Generate`) | no | no | no | no | no | no | no | no | sí | no |
| Gestiones y promesas (`Collections.RecordActivity`) | no | no | no | sí | no | no | sí | no | no | no |
| Exportar con datos personales (`Reports.ExportPersonalData`) | no | no | no | sí | no | sí | no | sí | sí | no |
| Parámetros, alcances, plantillas (`Parameters.Manage`, `Scopes.Manage`) | sí | no | no | no | no | no | no | no | no | no |
| Migrar y activar (`Migration.Load`, `Activation.Execute`) | sí | no | no | sí | no | no | no | no | no | no |
| Verificar el libro / la auditoría (`Integrity.Verify` / `AuditLog.VerifyIntegrity`) | sí / no | no | no | sí / no | no | no | no | no | no | sí / sí |

Además:

1. **Por `curl`**: toda acción que la tabla dice «no» responde el 404 genérico, también fuera de alcance, y queda
   auditada.
2. **Pasos** de las operaciones frecuentes de FR-013 (SC-009), verificados uno por uno:

   | Operación | Pantalla | Pasos |
   |---|---|---|
   | Simular | `/cartera/simulador` | 1 |
   | Radicar con los datos ya capturados | `/cartera/solicitudes/nueva`, desde la simulación | ≤ 3 |
   | Aprobar | `/cartera/aprobaciones` | ≤ 2 |
   | Desembolsar | `/cartera/desembolsos` | ≤ 3 |
   | Simular un pago | `/cartera/creditos/{id}` | ≤ 2 |
   | Consultar estado y liquidación | `/cartera/creditos/{id}` | 1 |
   | Emitir un documento | `/cartera/documentos` o desde el crédito | ≤ 2 |

   Y un asesor con la capacitación básica, sin experiencia en el módulo, radica su primera solicitud completa en
   menos de diez minutos (medido en QA).
3. **Cambio de cooperativa** en la misma sesión: los botones se recalculan sin F5 y no aparece un crédito, un
   mensaje ni un documento de otra cooperativa.
4. **App MAUI** en Windows y Android: las pantallas de Cartera, el simulador y la votación en presencia con passkey o
   TOTP se prueban a mano (no hay pruebas de navegador en el repositorio).
5. **Indicador de carga** en toda grilla, formulario y panel, sin colores literales ni `<style>`; mensajes en español
   claro, sin códigos técnicos.

## 11. COOFLOPAL: el ensayo antes de salir en vivo y las condiciones de salida

Sólo con autorización expresa del dueño y en las fechas que él defina, con la evidencia de cada paso en las notas de
release. COOFLOPAL no recibe ahorros: su perfil la registra **no obligada** a la pérdida esperada, sale con el Anexo
1 y no necesita K6a. Su cartera es de créditos y aportes; las cuotas de servicios a asociados no entran (D29), y
lo que se deba por servicios al corte queda en los saldos de apertura de Contabilidad.

### 11.1 Lo que tiene que existir antes

1. **Recaudo y Tesorería** especificadas, construidas y en producción, con sus e2e contra Cartera verdes en los dos
   motores. En la base de COOFLOPAL, el registro de participantes tiene un recaudador `Receipts`
   (`ObligationsCollector`, adopción «ProveedorDeObligaciones» en una versión vigente para Cartera) y un destino
   `Treasury` (`MessageDestination`, adopción «OrdenDeGiroEmitida» vigente), los dos **sin** `Class = Test`.
2. K1 desplegada en producción, con el retiro hecho (§11.4, paso 1). K2 sólo si COOFLOPAL descuenta por pagadurías
   externas con archivo; la nómina propia funciona desde K1 por el contrato. K5 antes del primer vencimiento del
   formato de cartera del SICSES que le toque por su nivel de supervisión; si no alcanza, rige la pregunta **E11**
   (propuesta por defecto: adelantar a K1 sólo el formato de cartera del SICSES, si la fecha lo exige), y la salida
   incluye ese archivo validado (§7, paso 5).
3. Las decisiones del dueño que bloquean K1 (§12) respondidas o aceptadas por defecto, en particular A1, A6–A11, B1,
   B2, C1, C3, C9, C11, F1–F8, G6, G11, H1–H7, I4, I5, I10 e I11.
4. Sin contratos externos, COOFLOPAL opera con los canales `SIMULADO` sin efecto legal (T73): pagaré físico, líneas
   sin «exige consulta a centrales» (las que la exigen no aprueban hasta tener canal real), mensajes al deudor por
   correo y, desde K4, aviso previo con constancia manual.

### 11.2 El ensayo general en QA

1. Una cooperativa de QA con la **parametrización de COOFLOPAL**: sus líneas, la matriz contable de su contadora con
   la completitud vacía, atribuciones, órganos con sus miembros reales, vinculados, plantillas de mensaje
   validadas, roles y alcances de sus usuarios, y su perfil regulatorio.
2. Una contabilidad de ensayo con la apertura de sus saldos reales a un corte pasado, y la carga de migración con sus
   créditos reales a ese corte (las personas se registran antes: no hay plantilla de personas, I11).
3. El recorrido de §3 completo con sus datos y los dobles de prueba, hasta el primer cierre mensual con la
   conciliación en cero.
4. **Comparación con cifras de referencia**: para una muestra de créditos de cada línea, la liquidación de Cartera al
   corte y a fin de mes frente a lo que informa la cooperativa (saldo por concepto, cuota, días de mora,
   calificación). Cada diferencia con su causa, aprobada por el jefe de cartera; la cuota total puede diferir en
   centavos porque el interés lo causa el motor desde el corte (D5), y así se declara en el acta de activación.

### 11.3 El ensayo en producción, sin activar

Desplegada K1 en producción, COOFLOPAL parametriza en su propia base (noviembre, según su plan) y aplica una
**carga de ensayo** con un corte anterior: valida personas, cuadre contra su contabilidad real y conciliación contra
los mensajes de venta acumulados por Comercial, sin consumirlos. Al aplicar la carga real, la de ensayo queda
`Replaced` (FR-142). Mientras haya una carga aplicada sin activar:

- cada nómina quincenal y cada definitiva con deudores de la carga exige reconocer «Cartera por activar»;
- Personas no deja borrar a esos deudores;
- Contabilidad avisa al cerrar el mes que contiene el día siguiente al corte (`Accounting.Period.LendingLoadWillExpire`).

Hay que coordinarlo con Nómina y Contabilidad de COOFLOPAL antes de aplicarla.

### 11.4 La salida, paso a paso

1. **Retiro**: el diagnóstico de §2.2 en PDN con autorización expresa; respaldo `<base>-AAAAMMDD-pre-f013.dump` de
   cada base (también la administrativa) y segundo revisor nombrado (H7), porque la migración es destructiva aunque
   las tablas estén vacías (Principio XII); si alguna base tiene filas en lo heredado, además la fila
   `LND.RetiroHeredado.Aprobado` en esa base. Migraciones por el Job PreSync de Argo, base por base; tablas y
   permisos comprobados contra la base.
2. **Valores nacionales** de PDN al día (usuras de las modalidades de sus líneas, IBR si hay líneas variables,
   `SMMLV`, `UVT`, festivos, límites) y el Anexo 1 vigente; revisados por la contadora. Nómina con la proyección
   adoptada y sin `Nomina.ValorNacionalDistinto` pendientes.
3. **Perfil regulatorio** de COOFLOPAL registrado; el selector responde Anexo 1 en todos los segmentos.
4. **Seguridad**: roles creados con `CreateSuggestedLendingRolesCommand`, alcances, `SEC_Users.PersonId` de todo el
   que radique, decida, vote o desembolse; matriz de atribuciones y órganos.
5. **Contabilidad**: matriz con la completitud vacía; `CN` y los mapeos de comprobante validados; `DESC_CARTERA` con
   cuentas; la regla `RecaudoPorAplicar` con `CollectorClass = Payroll` resuelta; patrimonio técnico registrado en
   `ACC_RegulatoryIndicators` (sin él, el límite de concentración bloquea desembolsos, cupos y novedades).
6. **Comercial**: el medio a crédito de servicios con `CreditWithoutLending` (A6); las ventas de servicios anteriores
   se resolverán en la conciliación como «aceptadas fuera de Cartera (D29)» (G11).
7. **Corte C = víspera de la activación.** Entre C+1 y el registro de la activación, Recaudo conserva los pagos que el
   contrato rechaza con «Cartera no activada» y Comercial sigue con su crédito provisional.
8. **Carga real** aplicada; cuadre en cero o diferencias aceptadas por punto de acta (D17); conciliación de ventas
   sin «no migradas con saldo» pendientes.
9. **Activación** con fecha C+1, registrada dentro de `Pagos.DiasMaximosFechaValor` días desde C, con el mes abierto
   en Contabilidad y con su **punto de acta** (obligatorio, C61); la plantilla de cobros provisionales si los hubo,
   que la activación guarda y el consumidor de Comercial aplica al nacer cada obligación (C62). Recaudo aplica los
   pagos conservados con su fecha valor. Una venta fechada hasta C que llegue después queda fuera de Cartera con
   la alerta `Cartera.VentaNoIncorporada` (C63, C99, pregunta I16).
10. **Comercial**, el mismo día: `Cartera.IntegracionHabilitadaDesde` igual a la fecha de activación,
    `Cartera.PoliticaSinRespuesta` revisada y `Cartera.CuentaPorCobrarRegistradaPor` fijada.
11. **Primeros días**: corridas diarias completas, alertas de `cartera.vigilancia`, bandeja de mensajes sin
    rechazos sin explicar y conciliación diaria.
12. **Primer cierre**: el del mes que contiene la activación, contra el cierre base (T49), antes de
    `Riesgo.DiaLimiteCierre`; conciliación en cero; después cierra Contabilidad.

### 11.5 Condiciones de salida

| # | Condición | Se comprueba con |
|---|---|---|
| 1 | Recaudo y Tesorería reales registrados en COOFLOPAL con versiones vigentes; ningún doble | `/api/core/integration-participants`; la activación lo exige en producción (US8-7) |
| 2 | K6a sólo si el perfil la hace obligada (no es el caso de COOFLOPAL) | perfil y selector; la activación lo exige (US8-8) |
| 3 | Retiro con diagnóstico, respaldo, segundo revisor y aprobación por base | runbook `docs/operaciones/cartera-retiro-heredado.md` |
| 4 | Anexo 1 vigente y usura de las modalidades de las líneas activas | la activación lo exige (T41) |
| 5 | Cuadre en cero o aceptado con acta; conciliación de ventas resuelta, incluidas las de servicios | `{id}/balance-check`, `{id}/reconciliation` |
| 6 | Toda oficina con créditos vinculada a una sucursal contable | la activación lo exige (CH49) |
| 7 | Roles, alcances y personas vinculadas a los usuarios que deciden | asistente de activación |
| 8 | Matriz completa, `RecaudoPorAplicar` de Nómina resuelta, `DESC_CARTERA` con cuentas, patrimonio técnico vigente | `/contabilidad/cartera/completitud`, asistente |
| 9 | Líneas de origen comercial con el concepto tributario del interés (D20, C11) | la línea no se activa sin él |
| 10 | Parámetros legales y plantillas de mensaje validados por la contadora y el oficial | `COR_NationalValueReviews`, parámetros sin «pendiente de validar» en lo que COOFLOPAL usa |
| 11 | Ensayo de §11.2 terminado con el primer cierre en cero y las diferencias de la comparación aprobadas | acta del ensayo |
| 12 | QA por rol (§10), pasos de SC-009 y la app MAUI probados a mano | notas de release |

## 12. Qué valida cada quien

Las preguntas son las de DT §5; si nadie responde, **rige la propuesta por defecto**. Los
valores legales se siembran «pendiente de validar» y mandan los vigentes que confirmen la contadora y el oficial.

**El dueño**

| Qué | Referencia | Bloquea |
|---|---|---|
| Las enmiendas a la spec y a otras features | A1–A11, T97 | Contratos de K1, `LaCarteraSoloSeTocaPorSusInterfaces`, `ComercioConCartera` |
| Que el proceso origine mensajes, el nombre de Recaudo y el ambiente de QA | B1, B2, B4, B5 | Hechos automáticos, dobles de prueba |
| El modelo propio de decisiones, plantillas, segregación, sesiones y votos | F1–F8 | Originación de K1 |
| Nómina, Personas y Comercial: propuesta, prelación, pagador, reserva, servicios a crédito | G2–G11 | E-02, E-03 |
| El retiro: tablas compartidas, `ACC_Documents` de origen CAR, proyecto Legacy, segundo revisor y diagnóstico en PDN | H1–H7 | La promoción |
| Proveedores de SMS y WhatsApp, el simulado en producción, documentos en K1, firma, layouts del SICSES y de centrales, layouts de pagadurías, volumen de la migración, listas | I1–I15 | K1, K2, K4, K5 |
| Con el abogado: la lectura de la Ley 2032 sin interacción (D24) y el canal semanal de la Ley 2300 | D24, I2, I3 | Casos dorados de `VentanaLegalDeContacto` |
| Pasar el archivo del SICSES por el validador de la Superintendencia | SC-019 | K5 (K1 si E11 lo adelanta) |
| Adelantar el formato de cartera del SICSES a K1 si su primer vencimiento llega antes que K5 | E11 (DT §7.7) | Salida en vivo de COOFLOPAL |
| Venta fechada hasta el corte que llega después de activar; cobros externos por factura en la migración | I16, I17 (DT §7.7) | Consumidor de ventas y plantilla `sales` (K1) |
| Las fechas del ensayo, de la carga y de la salida | — | §11 |

**La contadora**

| Qué | Referencia | Bloquea |
|---|---|---|
| Firmar los casos dorados del motor y de riesgo | §1.2, §1.3, SC-023 | La confianza en el cálculo |
| La matriz contable, los roles, los tipos de comprobante y el cruce | C4, C6, C7, C8, T52, T54 | Hechos y conciliación de K1 |
| `DESC_CARTERA` contra el puente de Nómina | C1, T53 | Pagos por nómina |
| Disminución del deterioro, patrimonio técnico y solvencia, comprobante resumido, comisiones, IVA del interés | C2, C3, C5, C10, C11 | Cierre, FR-041, líneas de origen comercial |
| Equivalencia lineal, sábado hábil, real/365 bisiesto, plan de los migrados, seguros, tasa variable, mora sobre intereses | D1–D3, D5, D9, D10, D14 | Casos dorados de K1 |
| Anexo 1 y Anexo 2 transcritos, redondeo del deterioro, sub-segmentos del microcrédito, modalidades productivas | E4, E8–E10 | Semillas de K1 y K6a |
| La revisión de los valores nacionales, la salvedad NIIF y el grupo NIIF | T41, FR-017, E3 | Ninguno (marca y alerta) |
| El cuadre de activación, el acta (vivienda fuera, cuota en centavos) y la conciliación de cada cierre | FR-141, SC-006, SC-013 | La activación y cada cierre |

**El oficial de cumplimiento**

| Qué | Referencia | Bloquea |
|---|---|---|
| La carga de listas restrictivas, su umbral de nombre y el re-escaneo | I12, T70 | Radicación con listas |
| Los textos de autorización por finalidad y sus versiones de política | T69, FR-006 | Radicación y centrales |
| Los plazos del titular y la regla de revocación | F11, T69 | Solicitudes del titular |
| Horarios, tope de contactos y canales de la Ley 2300; aviso previo y su umbral de dos comunicaciones | D14, T75, T77 | K4 |
| Las plantillas de mensaje sembradas | T76 | Mensajes de K1 y K4 |

## 13. Qué prueba cada criterio

| SC | Automática | En el ensayo |
|---|---|---|
| SC-001 | `ReproducibilidadDeLiquidacionTests`; casos 14 y 23; `CausacionYLiquidacionTests` | §3.8 paso 6, §3.15 |
| SC-002 | Casos 03, 13 y 14; `LineasDeCreditoYUsuraTests`; `DesembolsoYRecogidaTests`; `ValoresNacionalesYReplicaTests` | §3.2, §3.6 |
| SC-003 | Caso 16; `ContratoDeObligacionesTests` | §3.9 paso 3 |
| SC-004 | `ReversoComoSiNoHubieraExistidoTests`, `ElReversoEnlazaCadaFilaConSuOriginalTests`; casos 19–21; `ContratoDeObligacionesTests` | §3.9 paso 8 |
| SC-005 | `CreditoConCarteraTests` (consumo con la pasada a mano) | §3.7 paso 1 (despachador real) |
| SC-006 | `ContabilizacionDeCarteraPorMensajesTests`, `CierreMensualDeCarteraTests`; `ConciliacionContableDeCarteraTests` | §3.10, §3.11 |
| SC-007 | `CausacionDiariaPerformanceTests`; `RendimientoDeCausacionTests` | §3.8 paso 9 |
| SC-008 | `ConsultasYSimulacionesPerformanceTests` | §3.5, §3.7 |
| SC-009 | — | §10, punto 2 |
| SC-010 | `IdempotenciaDeCarteraTests`, `ConcurrenciaPagoYCausacionTests`, `CausacionIdempotenteTests`; `LosComandosDeCarteraLlevanClave` | §3.7, §3.9 paso 5 |
| SC-011 | `CasosDoradosDeVentanaLegalTests`; `CobranzaYAvisoPrevioTests`; `LaCobranzaNuncaContactaReferencias` | §3.12, §6 |
| SC-012 | `CobranzaYAvisoPrevioTests`, `SicsesYCentralesTests`, `OriginacionConAtribucionesTests` | §3.5 paso 4, §6, §7 |
| SC-013 | `MigracionYActivacionDeCarteraTests`, `CierreMensualDeCarteraTests`; caso 26 | §3.3, §3.4, §3.11 |
| SC-014 | `SeguridadYHabeasDataDeCarteraTests`; `LasLecturasSensiblesDeCarteraSeAuditan`, `LosPuertosDeCarteraValidanYAuditanSusEscrituras` | §3.14 |
| SC-015 | `SeguridadYHabeasDataDeCarteraTests`, `OriginacionConAtribucionesTests`; `LasConsultasDeCarteraRespetanElAlcance`, `LosEndpointsProtegidosExigenPermiso`; casos de votación y atribuciones | §3.5, §3.14, §10 |
| SC-016 | `ArchivoDeDescuentosPerformanceTests`, `LibranzaYArchivoDeDescuentosTests`; caso 50 | §4 |
| SC-017 | `SumaDeCapitalDelPlanTests`; casos 01–09, 27 y 30 | §3.5 paso 1 |
| SC-018 | `LaCarteraSoloSeTocaPorSusInterfaces`, `LaCarteraNoTieneValoresLegalesFijos`; `ReglasDeParametrosDeCarteraTests` | §3.2 paso 5 |
| SC-019 | `LayoutsDeArchivoTests`, `CasosDoradosDeLayoutsDeCarteraTests` | §7 paso 5 (el dueño) |
| SC-020 | `CausacionIdempotenteTests`; caso 22; `CausacionYLiquidacionTests`; `CausacionDiariaPerformanceTests` | §3.8 paso 5 |
| SC-021 | Casos r01–r09, r14, r20 y pe08 | §3.11 |
| SC-022 | `OriginacionConAtribucionesTests`, `CreditoConCarteraTests` | §3.5 paso 8, §3.7 paso 4 |
| SC-023 | Casos dorados del motor y de riesgo, incluidos r10–r15 y pe01–pe10 | §3.11, §8 |

Historias: US1 §3.2 · US2 §3.5 · US3 §3.6 · US4 §3.8 · US5 §3.9 · US6 §3.3, §3.4, §3.7 · US7 §3.7, §3.8,
§3.10, §3.11 · US8 §3.1, §3.3, §3.4, §8 · US9 §3.11, §8 · US10 §3.9 (US10-6), §4 · US11 §5 · US12 §6 · US13 §3.13,
§7 · US14 §5 · US15 §3.14, §7 · US16 §7 · US17 §3.5, §3.14.

## Nombres fijados en este documento

Con las reglas T1–T4 (pruebas en español; carpetas y valores en inglés), porque ninguna transversal los nombraba:

- **Colección e2e** «Valores nacionales e2e» (otro host, para lo que registra o corrige en la central).
- **Ayudantes e2e**: `CarteraE2E` (con `CooperativaAisladaAsync`), `ContabilidadParaCarteraConDoble`.
- **Clases e2e**: `LineasDeCreditoYUsuraTests`, `OriginacionConAtribucionesTests`, `DesembolsoYRecogidaTests`,
  `CausacionYLiquidacionTests`, `ContratoDeObligacionesTests`, `ContabilizacionDeCarteraPorMensajesTests`,
  `MigracionYActivacionDeCarteraTests`, `CierreMensualDeCarteraTests`, `IdempotenciaDeCarteraTests`,
  `SeguridadYHabeasDataDeCarteraTests`, `DocumentosDeCarteraTests`, `ValoresNacionalesYReplicaTests`,
  `LibranzaYArchivoDeDescuentosTests`, `NovedadesYCondonacionesTests`, `CastigoYRecuperacionTests`,
  `CobranzaYAvisoPrevioTests`, `InformesYTableroDeCarteraTests`, `SicsesYCentralesTests`, `PerdidaEsperadaTests`,
  `ViviendaConUvrTests`.
- **Volumen**: `ActivacionDeCarteraPerformanceTests` (C60), `ConsultasYSimulacionesPerformanceTests`,
  `ArchivoDeDescuentosPerformanceTests`.
- **Dominio**: `CasoDoradoDeRiesgo` y `CasosDoradosDeRiesgoTests`; `CasosDoradosDeVotacionTests` sobre
  `Domain.Tests/Lending/Sessions/Casos/`; `CasosDoradosDeAtribucionesTests` sobre
  `Domain.Tests/Lending/Attributions/Casos/`; `CasosDoradosDeVentanaLegalTests`; `ReglasDeSegregacionTests`,
  `NovedadesDeLibranzaTests`, `VigenciaDeDocumentosTests`, `VigenciaBitemporalTests`, `CalendarioHabilTests`,
  `ContadorDeDiasTests`, `CoincidenciaDeNombresTests`, `ReglaDeRevocacionTests`, `PlazosDelTitularTests`,
  `LayoutsDeArchivoTests`; los nombres de archivo de los casos (`01-…` a `60-…`, `r01-…` a `r20-…`, `pe01-…` a
  `pe10-…`).
- **Archivos dorados de layouts**: `CasosDoradosDeLayoutsDeCarteraTests` sobre
  `Application.Tests/Lending/Regulatory/Casos/*.esperado.txt` y la variable `CARTERA_ESCRIBIR_ESPERADO=1`.
- **Application**: `RegistroDeCarteraTests`, `CargadorDeReconstruccionTests`, `FechaContableTests`,
  `SimulateLoanPaymentQueryHandlerTests`, `ReverseLoanPaymentCommandHandlerTests`, `ReglasDeParametrosDeCarteraTests`,
  `LectorDeParametrosConInformacionHastaTests`, `ReplicacionDeValoresNacionalesTests`,
  `EfectoDeReplicacionEnNominaTests`, `EfectoDeReplicacionObligacionSobrevenidaTests`, `LectorDeValoresNacionalesTests`,
  `ValidadorDeDocumentoNormativoDeCarteraTests`, `EmisorDeMensajesConVariosModulosTests`, `DestinoCarteraTests`,
  `ResolutorDeReglasDeCarteraTests`, `ConstructorDeLineasDeCarteraTests`, `AgrupadorDeResumidosDeCarteraTests`,
  `ReglasDeLaMatrizDeCarteraTests`, `ConciliacionContableDeCarteraTests`, `ClosePeriodConCarteraTests`,
  `ValidacionPreviaContableDeCarteraTests`, `PerfilesSugeridosDeCarteraTests`,
  `CreateSuggestedLendingRolesCommandHandlerTests`, `AlcanceDeCarteraTests`, `RegistroDeIntervencionesTests`,
  `ReleaseScreeningMatchCommandHandlerTests`, `AutorizacionDeDatosPorFinalidadTests`, `SolicitudesDelTitularTests`,
  `GuardiaDeCanalesExternosTests`, `RenderizadorDePlantillasDeMensajeTests`, `PropuestaDeDescuentosDeCarteraTests`,
  `RecaudoPorNominaTests`, `CreditoEnLaVentaTests`, `DestinoInventarioTests`, `DeletePersonCommandHandlerTests`,
  `ApplyMigrationLoadCommandHandlerTests`, `ActivateLendingCommandHandlerTests`, `CatalogoDePlantillasDeCarteraTests`,
  `ExecuteMonthlyCloseCommandHandlerTests`, `ReopenMonthlyCloseCommandHandlerTests`,
  `ValidateRiskPolicyVersionCommandHandlerTests`, `SetAnalysisRatingCommandHandlerTests`, `ProcesadorDeCarteraTests`,
  `TareasDeCarteraTests`.
- **Ensayo**: las cooperativas `coop_ensayo` y `coop_obligada`; los usuarios de §2.7; los códigos de línea `CONLIB`,
  `CONCAJA`, `VARIBR`, `MICRO`, `COMER`, `ROTATIVO`, `EMPLEA`; las oficinas O1 a O3 y las sucursales SUC1 y SUC2.

## Conciliación

Los puntos que este documento dejó para conciliar se decidieron en `decisiones-transversales.md` §7; el cuerpo ya
aplica cada decisión.

| Punto | Decisión |
|---|---|
| QS-1 · baterías de conformidad y `TextosDeCarteraTests` | §7.6: conformidad en `Application.Tests`; `TextosDeCarteraTests` en `Architecture.Tests/Principles` (§1.5, §1.6) |
| QS-2 · `ReplicaSobreCooperativaSembradaTests` | §7.6: con `ValoresNacionalesYReplicaTests` en «Valores nacionales e2e» (§1.7) |
| QS-3 · códigos para negar migración y activación | C71: los de `data-model.md` §10, usados literales (`Lending.Activation.*`, `Lending.Migration.CrossPartErrors`, `Lending.Payoff.RequiresRestructuring`…; §3.3–§3.6) |
| QS-4 · clave del recaudador de prueba | C35: `TST:{guid:N}`, la pantalla la propone y deja repetirla (§3.9) |
| QS-5 · `RendimientoDeCausacionTests` | §7.6: corre siempre (§1.2) |
| QS-6 · SC-005 con el despachador apagado | §7.6: de punta a punta en QA; la e2e mide el consumo con la pasada a mano (§1.7, §3.7) |
| QS-7 · lo que no se ensaya en DEV ni QA | §7.6: lo que necesita valores ficticios en la central va en local y en las e2e (§2.4, §3.15) |
| QS-8 · contenido de las trece plantillas | C74: `PerfilesSugeridos` en `data-model.md` (§10) |
| QS-9 · documentos en K1 | C88: K1, `LND_IssuedDocument*` en `CarteraNucleo`; extracto y certificado tributario en K5 (§1.1, §3.13, §7) |
| QS-10 · cinco puertos | C27: cinco, en `Common.Integration.Lending` (§1.6) |
| QS-11 · `Payroll.Settlement.ConceptAccountsMissing` | §7.6: ya existe; la ordinaria usa `Payroll.ConceptWithoutAccounts` (§2.6) |
| QS-12 · el SICSES y la salida | E11 (DT §7.7): propuesta de adelantar sólo el formato de cartera a K1 (§7, §11.1, §12) |
| QS-13 · carpeta y variable de los dorados de layouts | §7.6: `Application.Tests/Lending/Regulatory/Casos/`, `CARTERA_ESCRIBIR_ESPERADO` (§1.4) |
| — · resultados de listar y simular | C28: `Answered` (§3.1, §3.9) |
| — · clases de corrida | C59: `DailyCycle`, `MonthlyClose`, `CloseReopen`, `NationalValueCorrection` (§3.8) |
| — · activación | C60 (transacción con guardados por tandas, `ActivacionDeCarteraPerformanceTests`), C61 (acta obligatoria), C62 (cobros provisionales), C63 (venta hasta el corte que llega tarde, I16) (§1.7, §1.8, §3.3, §3.4, §11.4) |
| — · `asOf` | C83: `date` y `recordedAsOf` en `/loans/*` (§3.8) |
| — · permisos y plantillas | C85, C86 (§2.3, §10) |

