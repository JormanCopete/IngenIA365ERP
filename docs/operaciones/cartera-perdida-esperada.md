# Runbook: la pérdida esperada de Cartera (Anexo 2, entrega K6a)

> Feature 013, fase 22 (US9 ampliada y US8-10, entrega K6a). Para quien despliega, para el maestro de la plataforma que registra la norma y para
> el jefe de cartera y la contadora de cada cooperativa. Qué hacen las pantallas está en el tema «Pérdida esperada (Anexo 2) y modelo de
> deterioro» del manual en línea (`/cartera/cierres`, pestaña «Modelo de deterioro»); lo que la contadora valida, en
> [cartera-perdida-esperada.md](../manual/cartera-perdida-esperada.md). Contrato: [api.md](../../specs/013-cartera-financiera/contracts/api.md)
> §12.3–§12.5, [data-model.md](../../specs/013-cartera-financiera/data-model.md) §23.2, §23.8, §23.9; decisiones T45–T50, R15, R54, R55, E2, E8, E9.

K6a no trae hechos contables nuevos: el cierre emite los mismos `DeterioroCalculado` (en delta) y `CarteraReclasificadaPorCalificacion` de K1. Lo
nuevo es **con qué se calcula** el deterioro de tres segmentos —consumo con libranza, consumo sin libranza y comercial de persona natural— en las
cooperativas obligadas y en las que lo adoptan: probabilidad de incumplimiento (PI) × pérdida dado el incumplimiento (PDI) × exposición, con la
calificación contable homologada del Anexo 2. Los demás segmentos siguen por el Anexo 1.

## 1. Antes de desplegar

1. Respaldo de cada base de cooperativa (Principio XII). La migración **`CarteraPerdidaEsperada`** es **aditiva**: `LND_ImpairmentModelElections`,
   `LND_ArrearsHistoryLoads`, la FK e índice de `LND_LoanArrearsHistory.ArrearsHistoryLoadId` y siete columnas nulas en `LND_MonthlyCloseLoans`
   (`ExpectedLossZ`, `ExpectedLossScore`, `IsInDefault`, `DaysInDefault`, `ProbabilityOfDefaultKey`, `LossGivenDefaultCollateralType`,
   `IncompleteArrearsHistory`). Va detrás de las de K5 en los dos proyectos; `tools/scripts/check-migration-parity.ps1` verde.
2. `CatalogoDeCartera.EntregaVigente = K6a`: con ella el selector deja de marcar «modelo obligatorio no disponible» a las obligadas y las
   lleva a la pérdida esperada **desde el primer cierre después del despliegue**. Revise antes cuáles cooperativas son obligadas (perfil
   regulatorio: ahorro y crédito, multiactivas e integrales con sección de ahorro, fondos de empleados de categoría plena) y que cada una cumpla
   el §2 y el §3; si no, su cierre se niega con `Lending.MonthlyClose.RegulationMissing` (nunca una tabla supuesta).
3. Los permisos `Lending.ImpairmentModel.Manage` y `Lending.ArrearsHistory.Import` ya existían (K1); los siembra la API al arrancar. El perfil
   sugerido «Secretaría del consejo» trae el primero; el segundo, quien hizo la migración.
4. El parámetro `Riesgo.ReglaMesesFaltantes` (sembrado «MayorObservada», **pendiente de validar**) y, si se elige «Fija», el nuevo
   `Riesgo.MesesFaltantesDiasFijos` (sin valor; «Fija» sin días se rechaza con `Lending.Parameter.FixedDaysRequired`).
5. El dueño de adjuntos nuevo `LendingImpairmentModelElectionSupport` (estudio de impacto y aviso de una propuesta) no necesita nada en el
   bucket: va con el mismo prefijo de la cooperativa.

## 2. El Anexo 2 en la plataforma (maestro)

La semilla central `sarc-anexo-2.json` (código `ANEXO2-2024-07-25`, Circular Externa 68 del 2024-07-24) va **toda con `verified = false`**
(pregunta E8) y se replica a cada cooperativa con los valores nacionales. Mientras no se coteje, **rige** y el cierre lleva la marca
`UnvalidatedParameters`.

**Cotejar** (dueño y contadora, contra la circular consolidada vigente): las ecuaciones Z y sus variables, los cortes del puntaje, las tres tablas
de PI por tipo de organización, la PDI por tipo de garantía y sus tramos de días en incumplimiento, la PDI sin incumplimiento y las dos tablas de
homologación. Lo que la transcripción dejó **abierto**:

| Dato | Estado | Efecto mientras falte |
|---|---|---|
| PI de la aplicación **voluntaria** (CE 100 del 2026-05-12) | no se consiguió el texto | ninguna cooperativa puede adoptar: `Lending.ImpairmentModel.VoluntaryPdMissing` |
| PI de las integrales **con** sección de ahorro (`COOP_INTEGRAL_SECCION_AC`) | la CE 68 no trae la columna | esa cooperativa obligada no activa ni cierra (`RegulationMissing`) |
| Tercer tramo de «sin garantía» | la imagen dice «00»; se puso 90 por analogía | PDI de los créditos sin garantía con mucha mora |
| `COOP_TRABAJO_ASOCIADO`, `COOP_ESPECIALIZADA_SIN_AHORRO`, `COOP_INTEGRAL_SIN_AHORRO` | la PI los usa y `TiposDeOrganizacion` no los tiene | el perfil no los puede elegir |
| Vigencia | verificar si la CE 100 u otra posterior sustituyó el Anexo 2 de la CE 68 | — |

**Registrar una versión cotejada o corregida**: `POST /api/saas/regulatory-documents` con `kind = SarcAnexo2`, un código nuevo, `validFrom`, el
contenido completo (el esquema de `Anexo2`, `Domain/Lending/Risk/Normativa/Anexo2.cs`), `verified` y la fuente. El validador de Cartera rechaza
un documento mal formado (cortes no crecientes, PI sin todas sus columnas, homologación que no cubre el rango). Rige desde su `validFrom` para los
cierres posteriores; **nunca cambia un cierre hecho**. Después, la réplica (la tarea `valores-nacionales.replicar`, que también corren el ciclo diario y el cierre de Cartera).

La PI **voluntaria** se agrega como columnas con `organizationType = "VOLUNTARIA"` en `probabilityOfDefault`, una por segmento. La e2e
`PerdidaEsperadaTests` registra en su host un Anexo 2 **de prueba** (`ANEXO2-E2E-PRUEBA`) con una PI voluntaria copiada de la de ahorro y
crédito: **no** es la de la CE 100 y nunca se registra en un ambiente real.

## 3. El historial de mora (obligadas)

La PI usa la mora máxima de cada mes de los últimos 36 (`ReglaDePerdidaEsperada.historyMonths`) o desde el desembolso si el crédito es más joven.
La migración trae la mora **al corte**; los meses anteriores los trae la **carga complementaria** (plantilla `lending.arrears-history`:
`numeroDeCredito`, `mes` AAAA-MM, `moraMaximaDias`, `moraAlCierreDias`, `fuente`).

1. `/cartera/cierres` → «Modelo de deterioro» → «Historial de mora» → «Revisar»: los créditos vivos sin historial completo y los meses que faltan.
   Antes de activar, la misma lista sale en la revisión de la carga (`extra.completeness[]`) y en los requisitos de la activación.
2. Descargar la plantilla, llenar los meses con el reporte de mora del sistema anterior (la `fuente`, la misma en todas las filas) e importar con
   revisión previa. Todo o nada; errores por fila y columna: `Lending.ArrearsHistory.LoanNotInLoad`, `.MonthClosed` (el mes ya tiene cierre de
   Cartera), `.LoadClosed` (ya pasó el primer cierre en pérdida esperada), `.NoAppliedLoad`.
3. Se carga **antes o después de activar**: antes, la carga guarda sus filas y la activación las lleva al historial; después, va directo a
   `LND_LoanArrearsHistory` con origen `ComplementaryLoad`. Volver a cargar un mes no borra nada: manda la carga más reciente. No crea
   transacciones ni hechos.
4. **Activación de una obligada** (US8-10, en todos los ambientes): sin el Anexo 2 con la PI de su tipo, sin la regla de meses faltantes (o sin
   sus días con «Fija»), sin el SMMLV si es fondo de empleados, o con créditos migrados cubiertos sin historial completo, responde
   `Lending.Activation.RequirementsMissing` con `data.missing[]` (`ExpectedLossParameters`, `ArrearsHistoryComplete` con la lista y
   `Lending.Migration.ArrearsHistoryRequired`). Completada la carga, activa.
5. Si llega el primer cierre en pérdida esperada con meses faltantes, **el cierre no se niega**: aplica la regla de meses faltantes, marca el
   crédito (`IncompleteArrearsHistory`), el encabezado (`IncompleteHistory` con `IncompleteHistoryJson`: regla, días y créditos) y lo lista en el
   reporte de cambios.

## 4. Adoptar o desistir (no obligadas)

El orden importa: **acta, soportes, registro**.

1. «Proponer la adopción» (o «el desistimiento», según la última elección) genera el identificador de la propuesta.
2. El consejo decide en una sesión con un punto `Decision` de sujeto «Cambio del modelo de deterioro» (`ImpairmentModelChange`) y ese identificador.
   El acta se cierra.
3. Se suben a la propuesta el estudio de impacto y el soporte del aviso a la Superintendencia (dueño `LendingImpairmentModelElectionSupport`).
4. Se registra: fecha del aviso, desde cuándo rige, segmentos (por defecto los tres) y el punto del acta. Rechazos: `.NoticeOutOfTerm` (rige antes
   del aviso más la antelación de la regla vigente —15 días en la semilla, pregunta E2—, con `earliestValidFrom`), `.RetroactiveValidFrom`
   (no después del último cierre), `.ValidFromTaken`, `.NotAlternating`, `.Obligated` (una obligada nunca adopta ni desiste), `.VoluntaryPdMissing`,
   `.IncompleteHistory` (créditos vivos de los segmentos sin historial a la fecha del primer cierre que la usa), `.SupportInvalid` (el estudio o el
   aviso no son adjuntos disponibles de la propuesta), `Lending.Session.ItemMismatch`, `Lending.Security.UserWithoutPerson`.
5. Registrada, sus soportes quedan bloqueados (`Attachments.OwnerLocked`) y la auditoría lleva `Lending.ImpairmentModel.ElectionRegistered`.
   **Ningún cierre se recalcula**: el primero que cae en la vigencia aplica el modelo nuevo y emite sólo la diferencia frente al anterior.

## 5. La obligación sobrevenida

Si una versión del **perfil regulatorio** de la cooperativa (o una `ReglaDePerdidaEsperada` nueva en la plataforma) la vuelve obligada después de
activar, el registro **nunca se niega** por el historial: responde `expectedLoss.obligated = true` y levanta `Cartera.ObligacionSobrevenida` y, si
hay créditos sin historial completo a la fecha del primer cierre en pérdida esperada, `Cartera.HistorialDeMoraIncompleto` con la lista (al contador
y al jefe de cartera). Completar con la carga complementaria antes de ese cierre; si no, el cierre aplica la regla de meses faltantes (§3.5). Una
obligada no puede desistir.

## 6. Leer el cierre

`GET /api/lending/risk/monthly-closes/{id}/loans` (y la pantalla del cierre, «Por qué») trae por crédito `impairmentModel` y, en pérdida esperada,
`expectedLoss`: segmento del Anexo 2, Z y puntaje (redondeado a la escala del documento, E9), calificación del modelo, incumplimiento y días, la
clave de la tabla de PI (`VOLUNTARIA` en una adopción), la PI, el tipo de garantía con que se tomó la PDI (con varias garantías, la más
conservadora: decisión pendiente de validar), la PDI y la exposición, más la explicación por componente. La categoría contable es la homologada.
El encabezado sella `SarcAnexo2` con su SHA-256 en los documentos usados.

## 7. SICSES por pérdida esperada (9083, y los cambios del 9027 y del 9081)

La Supersolidaria crea el formato **9083** (cálculo de deterioro de cartera) y cambia el 9027 y el 9081. **Sin instructivo no se construyó
ningún layout.** Cuando llegue:

1. Las columnas ya están en `LND_MonthlyCloseLoans`: `ImpairmentModel`, `ExpectedLossSegmentCode`, `ExpectedLossZ`, `ExpectedLossScore`,
   `ModelRating`, `IsInDefault`, `DaysInDefault`, `ProbabilityOfDefaultKey`, `ProbabilityOfDefault`, `LossGivenDefaultCollateralType`,
   `LossGivenDefault`, `ExposureAtDefault`, `AccountingRating`, `TotalImpairment` (y por concepto) e `IncompleteArrearsHistory`. Si el
   instructivo pide algo que no está, es una **migración nueva** aditiva (las de K1 y K6a no se regeneran).
2. **9027 y 9081**: una versión nueva del JSON en `src/Core/IngenIA365ERP.Application/Lending/Regulatory/Layouts/` con su `validFrom`
   (`sicses-cartera-2-{aaaa-mm-dd}.json`, código `SICSES-CARTERA-v2`) y **todos sus campos con `verified = false`** y la fuente «por cotejar»; la
   anterior queda para los períodos que cubre. Las rutas nuevas (`PerdidaEsperada.*`) se agregan a `CreditoDelSicses` y a `FilasDelSicses.Fila`
   (`ProyeccionSicsesCartera.cs`), leyendo esas columnas.
3. **9083**: es otro formato, no otra versión del 9027. `CatalogoDeLayoutsDeCartera` sólo admite `sicses-cartera-*` para la clase `Sicses`: hace
   falta una clase nueva en `RegulatoryFileKind` (o un prefijo propio, p. ej. `sicses-deterioro-{versión}-{fecha}.json`), su proyección
   (una fila por crédito en pérdida esperada del cierre) y su ruta de generación con las mismas reglas de K5 (validar no guarda, bloqueantes,
   advertencia `Lending.Regulatory.LayoutSinCotejar`, marcar cargado).
4. Los casos dorados del layout se regeneran sólo a propósito con `CARTERA_ESCRIBIR_ESPERADO=1` (como en
   [cartera-sicses-y-centrales.md](cartera-sicses-y-centrales.md)).

## 8. Verificar

- `dotnet test tests/IngenIA365ERP.Domain.Tests` (casos pe01–pe10, `FuncionLogisticaTests`, `Anexo2Tests`) y Application (`ExecuteMonthlyCloseConPerdidaEsperadaTests`,
  `RegisterImpairmentModelElectionCommandHandlerTests`, `ImportArrearsHistoryCommandTests`, `ActivacionConPerdidaEsperadaTests`).
- Con Docker, la e2e `PerdidaEsperadaTests` (colección «Cartera e2e», `DB_PROVIDER` de los dos motores).
- En DEV y QA: una cooperativa de ahorro y crédito de ensayo con cartera migrada; revisar que el primer cierre en pérdida esperada cuadre con el
  cálculo a mano de la contadora para tres créditos (quickstart §12).
