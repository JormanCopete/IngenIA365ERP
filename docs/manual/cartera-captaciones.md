# Captaciones y aportes: índice de manuales y lo pendiente de validar

> Feature 014 (Cartera financiera — captaciones), T226. Para la contadora y el jefe de captaciones. Las captaciones (ahorro a la vista,
> permanente y contractual, CDAT) y los aportes sociales viven **dentro de Cartera**, con sus permisos `Lending.*`, y se construyeron
> desde cero en reemplazo de lo heredado (que se retiró entero: ver [captaciones-retiro-heredado.md](../operaciones/captaciones-retiro-heredado.md)).
> Se entregan en cinco tramos, K7 a K11, que continúan la numeración de la 013; con el despliegue de la 014 la entrega vigente de Cartera es
> **K11** (las cinco abiertas juntas). Por decisión del dueño (Q1) **ninguna sale a producción sola ni sin Recaudo y Tesorería reales**: en
> prueba operan el recaudador y la tesorería de prueba. Especificaciones: `specs/014-ahorros-aportes-cdt/`; donde la investigación
> (`research.md`) enmienda a la especificación, manda la investigación y el código.

## Cómo está armado, en siete líneas

1. **Una Cartera, tres naturalezas.** Toda línea es de colocación (crédito), captación o aportes; comparten el catálogo `/cartera/lineas`, el
   alcance por línea y oficina, las actas, la matriz contable y el cierre. Cada naturaleza se **activa por separado** (aportes con K7,
   captaciones con K8), después de los créditos.
2. **Un solo escritor del libro de cuentas** (`RegistroDeCuentas`) y **una sola fuente de saldos**: la reconstrucción de la cuenta a una fecha
   valor D con la información registrada hasta R. Lo que muestra una lista es una foto; la cifra exacta es el saldo a (D, R).
3. **Las cuentas se tocan sólo por el contrato de cuentas** (`IContratoDeCuentas`, versión 1): Recaudo, Nómina, Tesorería y las pagadurías
   consignan, retiran y trasladan por él, en su propia transacción; la pantalla de Cartera sólo retira por orden de giro, traslada y reversa.
4. **Cartera no conoce cuentas contables**: emite hechos a Contabilidad en la misma transacción que la operación y Contabilidad los asienta por
   la matriz de Cartera, que ahora tiene cinco dimensiones y 17 roles más. Comprobantes nuevos: `MA`, `CC`, `TD`, `AO`, `RA`.
5. **Ningún valor legal en el código**: tarifas de retención y de GMF, topes, límite inembargable, componente inflacionario, porcentajes del fondo
   de liquidez y demás son **parámetros con vigencia** (`/cartera/parametros`) o **valores nacionales** que registra el maestro en la central y se
   replican a cada cooperativa. Todo defecto sembrado va «pendiente de validar».
6. **Lo hecho no se edita**: una operación se corrige con su **reverso** (espejo con la misma fecha valor) y una operación nueva; las condiciones
   del CDAT y del contractual se **sellan** en versiones; las decisiones de asamblea, los cierres y los archivos regulatorios se reversan o se
   regeneran, nunca se reescriben.
7. **Plazos regulatorios informativos** (criterio del dueño): los plazos de respuesta al juzgado, de devolución, de reportes y de inactividad
   avisan; no bloquean.

## K7 — aportes

| Tema | Manual | Operación |
|---|---|---|
| Línea de aportes, cuota periódica, recaudo por Nómina y pagadurías, consulta de aportes para créditos, devoluciones y cesiones | [cartera-captaciones-aportes-y-retiro-del-asociado.md](cartera-captaciones-aportes-y-retiro-del-asociado.md) §1–§4 | [captaciones-puesta-en-marcha.md](../operaciones/captaciones-puesta-en-marcha.md) |
| Retiro del asociado: radicación, acta, liquidación con cruce a los créditos, pérdidas, devolución y mínimo irreducible | [cartera-captaciones-aportes-y-retiro-del-asociado.md](cartera-captaciones-aportes-y-retiro-del-asociado.md) §5–§8 | — |
| Grupos de archivo y equivalencias de una pagaduría (aportes y ahorros) | [cartera-libranza-y-pagadurias.md](cartera-libranza-y-pagadurias.md) §9a | — |
| Catálogo común de líneas, apertura de cuentas, titulares, menores y beneficiarios | [cartera-captaciones-lineas-y-apertura.md](cartera-captaciones-lineas-y-apertura.md) | — |
| Seguridad: segregación, alcance, atribuciones con monto y lecturas auditadas | [cartera-captaciones-lineas-y-apertura.md](cartera-captaciones-lineas-y-apertura.md) §6 | — |
| Saldos iniciales y activación por naturaleza | [cartera-captaciones-lineas-y-apertura.md](cartera-captaciones-lineas-y-apertura.md) §7 | [captaciones-puesta-en-marcha.md](../operaciones/captaciones-puesta-en-marcha.md), [captaciones-despliegue.md](../operaciones/captaciones-despliegue.md) |
| Integración contable: hechos, roles, matriz, comprobantes, validación previa y conciliación | [cartera-captaciones-integracion-contable.md](cartera-captaciones-integracion-contable.md) | — |
| Retiro de las tablas y pantallas heredadas de ahorro, aportes y CDT | — | [captaciones-retiro-heredado.md](../operaciones/captaciones-retiro-heredado.md) |

## K8 — ahorro a la vista y permanente

| Tema | Manual | Operación |
|---|---|---|
| Líneas de captación (tasa, tramos, método de saldo, topes, cargos) | [cartera-captaciones-lineas-y-apertura.md](cartera-captaciones-lineas-y-apertura.md) §2 | — |
| Consignaciones, retiros y traslados por el contrato y por pantalla; firmas, topes, canje, reversos | [cartera-captaciones-movimientos-intereses-retencion-y-gmf.md](cartera-captaciones-movimientos-intereses-retencion-y-gmf.md) §1–§3 | [cartera-dobles-de-prueba.md](../operaciones/cartera-dobles-de-prueba.md) |
| Cargos de cuenta y débito automático | [cartera-captaciones-movimientos-intereses-retencion-y-gmf.md](cartera-captaciones-movimientos-intereses-retencion-y-gmf.md) §4–§5 | — |
| Causación diaria, liquidación de intereses y retención en la fuente | [cartera-captaciones-movimientos-intereses-retencion-y-gmf.md](cartera-captaciones-movimientos-intereses-retencion-y-gmf.md) §6–§7 | [cartera-proceso-diario.md](../operaciones/cartera-proceso-diario.md) |
| GMF: cuenta exenta, tope, hechos generadores | [cartera-captaciones-movimientos-intereses-retencion-y-gmf.md](cartera-captaciones-movimientos-intereses-retencion-y-gmf.md) §8 | — |
| Cancelación de una cuenta | [cartera-captaciones-movimientos-intereses-retencion-y-gmf.md](cartera-captaciones-movimientos-intereses-retencion-y-gmf.md) §9 | — |
| Bloqueos, embargos y límite inembargable; respuestas al juzgado | [cartera-captaciones-bloqueos-embargos-y-documentos.md](cartera-captaciones-bloqueos-embargos-y-documentos.md) §1–§4 | — |
| Documentos al titular: extracto, certificados de saldo, tributario y de retención, constancias, lote | [cartera-captaciones-bloqueos-embargos-y-documentos.md](cartera-captaciones-bloqueos-embargos-y-documentos.md) §5–§6 | — |

## K9 — CDAT y ahorro contractual

| Tema | Manual | Operación |
|---|---|---|
| CDAT: constitución, plan de intereses, vencimiento y gracia, prórroga, renovación, redención, título | [cartera-captaciones-cdat-y-contractual.md](cartera-captaciones-cdat-y-contractual.md) §1–§5 | — |
| Tasa negociada sobre la tabla de la línea | [cartera-captaciones-cdat-y-contractual.md](cartera-captaciones-cdat-y-contractual.md) §6 | — |
| Ahorro contractual: contrato sellado, cuotas, bonificación, incumplimiento | [cartera-captaciones-cdat-y-contractual.md](cartera-captaciones-cdat-y-contractual.md) §7–§10 | — |

## K10 — asamblea, inactividad y fallecimiento

| Tema | Manual | Operación |
|---|---|---|
| Revalorización de aportes, distribución de excedentes y amortización | [cartera-captaciones-asamblea-inactividad-y-fallecimiento.md](cartera-captaciones-asamblea-inactividad-y-fallecimiento.md) §1–§4 | [valores-nacionales.md](../operaciones/valores-nacionales.md) (IPC) |
| Inactividad y reactivación | [cartera-captaciones-asamblea-inactividad-y-fallecimiento.md](cartera-captaciones-asamblea-inactividad-y-fallecimiento.md) §5 | [cartera-proceso-diario.md](../operaciones/cartera-proceso-diario.md) |
| Saldos no reclamados | [cartera-captaciones-asamblea-inactividad-y-fallecimiento.md](cartera-captaciones-asamblea-inactividad-y-fallecimiento.md) §6 | — |
| Fallecimiento del titular y entrega a beneficiarios | [cartera-captaciones-asamblea-inactividad-y-fallecimiento.md](cartera-captaciones-asamblea-inactividad-y-fallecimiento.md) §7 | [valores-nacionales.md](../operaciones/valores-nacionales.md) (tope sin sucesión) |

## K11 — liquidez, informes y reportes regulatorios

| Tema | Manual | Operación |
|---|---|---|
| Fondo de liquidez (Decreto 976 de 2026), lo constituido y las fotos | [cartera-captaciones-liquidez-e-informes.md](cartera-captaciones-liquidez-e-informes.md) §1–§3 | [captaciones-despliegue.md](../operaciones/captaciones-despliegue.md) |
| Riesgo de liquidez (IRL y brecha) y depósitos asegurados | [cartera-captaciones-liquidez-e-informes.md](cartera-captaciones-liquidez-e-informes.md) §4–§5 | — |
| Informes de gestión de captaciones y aportes | [cartera-captaciones-liquidez-e-informes.md](cartera-captaciones-liquidez-e-informes.md) §6 | — |
| SICSES (9026, 9068, 9013, 169, 9067) y exógena (1019, 1020) | [cartera-captaciones-liquidez-e-informes.md](cartera-captaciones-liquidez-e-informes.md) §7 | [cartera-sicses-y-centrales.md](../operaciones/cartera-sicses-y-centrales.md) (la mecánica común) |

## Pantallas y roles sugeridos

Todo está en el grupo de menú **«Captaciones y aportes»** (configuración, cuentas, puesta en marcha); las fichas (`/cartera/ahorros/{id}`,
`/cartera/aportes/{persona}`, `/cartera/cdat/{id}`, `/cartera/retiros-asociado/{id}`) se abren desde sus listas. Los informes están en
`/cartera/informes` y en el centro de reportes («Captaciones y aportes»). Cuatro roles sugeridos de captaciones, que se crean con los de créditos
o aparte (el requisito de activación `DepositRolesCreated` los exige): **Jefe de captaciones** (`cartera.jefe-captaciones`), **Asesor de
captaciones** (`cartera.asesor-captaciones`), **Analista de aportes** (`cartera.analista-aportes`) y **Tesorero del fondo de liquidez**
(`cartera.tesorero-liquidez`). El administrador de la empresa **no** recibe `Lending.NegotiatedRates.Approve` (D17).

## Pendiente de validar por la contadora

Todo lo que sigue funciona hoy con el defecto indicado; cada valor es un parámetro de Cartera con vigencia (`/cartera/parametros`), un valor
nacional de la central (lo registra el maestro, [valores-nacionales.md](../operaciones/valores-nacionales.md)) o una condición de la línea. Al
validarlo, la cooperativa registra una vigencia nueva; nada cambia hacia atrás. Lo marcado «abogado» lo valida también el abogado de la
cooperativa. Las preguntas de norma de la especificación siguen abiertas: **P2, P3, P8 a P12 y P26** (P4 a P7 con el abogado y el oficial de
cumplimiento).

### Retención en la fuente (P2, P27)

| Qué | Parámetro o valor | Defecto |
|---|---|---|
| Tarifa del ahorro a la vista | `Retencion.TarifaAhorro` (general y por línea) | 0,07 |
| Tarifa del CDAT (Decreto 572 de 2025) | `Retencion.TarifaCdat` | 0,04 |
| Tarifa del contractual y del permanente | `Retencion.TarifaContractual`, `Retencion.TarifaPermanente` | vacío: rige la del ahorro a la vista |
| Umbral diario (DUR 1625 art. 1.2.4.2.6) | `Retencion.UmbralDiarioUvt` | 0,055 UVT, medido por cuenta y por día |
| **P2** · ¿el umbral alcanza también al CDAT? | `Retencion.AlcanceDelUmbral` | `ExceptTermDeposit` (el CDAT retiene sin umbral) |
| Componente inflacionario | `Retencion.ComponenteInflacionarioAplica`; valor nacional `COMPONENTE_INFLACIONARIO` (por año gravable) | sí; 2025 = 0,5543 (Decreto 898 de 2026). Hoy se descuenta a **toda** persona natural: el perfil no distingue a la «obligada a llevar contabilidad» (pregunta abierta al dueño) |
| Redondeo de la retención y del componente | `Retencion.Redondeo` | al peso, mitad hacia arriba |
| Lo que está en canje gana interés | regla del motor | sí |
| El interés migrado se reparte por igual entre los días que cubre para medir el umbral | regla de la migración | sí |

### GMF (P3)

| Qué | Parámetro o valor | Defecto |
|---|---|---|
| Tarifa (admite cero desde una fecha) | `Gmf.Tarifa` | 0,004 |
| Tope mensual exento por persona | `Gmf.TopeExencionUvt` | 350 UVT |
| **P3** · Política de exención (fondos de empleados; reparto entre varias cuentas, ET art. 881-1) | `Gmf.PoliticaDeExencion` | `SingleMarkedAccount` (una cuenta marcada por persona) |
| Hechos generadores | `Gmf.HechosGeneradores` | gravados: retiro, traslado a otro titular, traslado entre ahorro y CDAT, redención de CDAT abonada en cuenta, cancelación y débito automático; exento: traslado entre cuentas de ahorro del mismo titular |
| Redondeo | `Gmf.Redondeo` | al peso |
| Canje | `Captaciones.DiasDeCanjePorClaseDeMedio` | `{"Check": 3, "*": 0}` días hábiles |
| GMF de los intereses del CDAT abonados en cuenta | regla del CDAT | se cobra, como redención abonada |
| GMF de la consignación al juzgado, de la entrega por fallecimiento y del pago de no reclamados | — | hoy **no** se cobra |
| Reparto del GMF entre cotitulares en el certificado tributario | regla del certificado | por la proporción de cada titular |

### Contabilidad (P1, ya configurable)

- Las cuentas CUIF sugeridas de la plantilla de la matriz (`sugerencias-cuif-captaciones.json`), en especial el ahorro permanente (213005 o
  213010), la bonificación (615015, la misma del gasto por intereses del contractual), los aportes revalorizados (310505, la misma de los
  ordinarios), los no reclamados por pagar (246595) y los roles sin sugerencia (ingreso por cargos, cargo por cobrar, embargos por consignar,
  ingreso por penalidad). La lista completa, en [cartera-captaciones-integracion-contable.md](cartera-captaciones-integracion-contable.md) §2.
- Si la cuenta exenta de GMF o la embargada van a otra cuenta contable que la estándar (entonces la reclasificación mueve saldos).
- El residuo de centavos entre la causación diaria y el interés liquidado al peso: ajustarlo en la liquidación o dejarlo en el interés por pagar.
- La conciliación de la retención y del GMF por pagar contra la declaración (fuera de Cartera).
- Asamblea: fondo de revalorización 3310, fondo de excedentes 3505 o la reserva del acta, retornos por pagar 246515, amortización contra 246505;
  el traslado del fondo de amortización (3305) a aportes amortizados (311010) no lo emite Cartera.

### Aportes y retiro del asociado (P12)

| Qué | Parámetro o valor | Defecto |
|---|---|---|
| Tope de la cuota de un fondo de empleados | `Aportes.TopeCuotaFondoEmpleados`; límite `APORTES_TOPE_CUOTA_FONDO_EMPLEADOS` | 0,10 del ingreso (D29; pregunta abierta: ¿la entidad puede superar el límite?) |
| Parte mínima de la cuota que va a aportes | `Aportes.FraccionMinima`; límite `APORTES_FRACCION_MINIMA` | vacío; el límite está en 0,10 |
| **P12** · Límite individual de participación | `Aportes.LimiteParticipacionPersonaNatural` y `…PersonaJuridica`; límites `APORTES_LIMITE_PARTICIPACION_PN`/`_PJ` | vacíos, sin valor nacional |
| Mínimo irreducible de la entidad | `Aportes.MinimoIrreducibleEntidad` | vacío (lo fijan los estatutos) |
| Plazo estatutario de devolución | `Captaciones.DiasPlazoDevolucionAportes` | vacío |
| De qué fondos sale primero el cruce al retiro | `Captaciones.OrdenDeFondosAlRetiro` | `AportesPrimero` |
| Motivo del retiro para Asociados | `Captaciones.MotivosDeRetiroDeAsociados` | `{}` (sin motivo) |
| Aportes en el deterioro (Anexo 1 num. 5.1; Anexo 2 num. 4.2) | `Riesgo.DescuentoAportesDeterioro` | falso; falta validar los indicadores del num. 5.1 y descontar el ahorro permanente (D39) |
| Mayoría de edad | `Captaciones.MayoriaDeEdadAnios` | 18 |
| Fecha valor hacia atrás | `Captaciones.DiasMaximosFechaValor` | 5 días (D11) |
| Declaración de origen de fondos en efectivo (P4, oficial de cumplimiento) | `Efectivo.UmbralDeclaracion` | vacío: no se exige |

### CDAT (P1, D17, D18)

- El plazo de emisión por días con meses de 30 (menos de 180; 180 a 360; más de 360 y menos de 540; 540 o más) y la frontera de 12 meses
  (CUIF 211010).
- La penalidad por reliquidación como menor gasto de intereses y lo pagado de más descontado del capital (hoy contra el ingreso por penalidad).
- El interés anticipado con el interés por pagar en débito durante el período (o un diferido).
- Los defectos de la propuesta: prórroga automática por el mismo plazo, a la tasa vigente de la tabla, **sin** capitalizar, 3 días hábiles de gracia
  sin causación (D17); redención anticipada no admitida (D18). Son condiciones de cada línea.

### Ahorro contractual (P26)

| Qué | Parámetro o condición | Defecto |
|---|---|---|
| **P26** · Bonificación al cumplir o provisionada durante el contrato | condición de la línea `BonusRecognition` | `AtFulfillment` u `Provisioned`, a elegir en cada línea |
| Gracia de una cuota antes de contar como atrasada | `Captaciones.DiasDeGraciaDeCuotaContractual` (general y por línea) | 5 días |
| La bonificación como gasto financiero con la retención del contractual | rol `GastoPorBonificacion` | así |
| La penalidad por incumplimiento descuenta capital y va al ingreso por penalidad | rol `IngresoPorPenalidad` | así (alternativa: menor gasto de intereses) |
| Plazo de emisión por meses (menos de 6; 6 a 12; 12 a 18; 18 o más) | regla del motor | así |
| La diferencia de la convención 30/360 entre el libro y la cifra del reglamento | — | es de la convención, no un error |

### Embargos (P5, contadora y abogado)

| Qué | Parámetro o valor | Defecto |
|---|---|---|
| **P5** · Límite inembargable de los depósitos de ahorro (CGP art. 594 num. 2) | valor nacional `LIMITE_INEMBARGABLE` | sin semilla: lo registra el maestro con su fuente cada período (para oct-2025 a sep-2026, 55.099.308 por la Circular 058 de 2025 de la Superfinanciera) |
| Cobro coactivo (ET art. 837-1) | límite `EMBARGO_COACTIVO_SMMLV` | 25 SMMLV en la cuenta más antigua |
| Clases protegidas por el límite | `Embargos.ClasesProtegidas` | a la vista, contractual y permanente (el CDAT no) |
| Clases inembargables por estatuto | `Embargos.ClasesInembargables` | ninguna (en un fondo de empleados, el permanente) |
| Aplicación del límite por autoridad | `Embargos.AplicacionDelLimite` | judicial sobre la suma; coactivos sobre la cuenta más antigua |
| Sólo personas naturales | `Embargos.LimiteSoloPersonasNaturales` | sí (D20) |
| Orden de las cuentas | `Embargos.OrdenDeCuentas` | vacío: por antigüedad |
| Días hábiles para responder (informativo) | `Embargos.DiasHabilesParaResponder` | 3 |
| Textos de las cinco respuestas | `Embargos.PlantillasDeRespuesta` | `{}`: rigen los estándar |

### Documentos (P10)

| Qué | Parámetro | Defecto |
|---|---|---|
| **P10** · ¿El certificado tributario incluye aportes y revalorización? | `Documentos.CertificadoTributarioIncluyeAportes` | falso |
| Leyenda del certificado de retención (ET art. 381; DUR art. 1.6.1.12.12) | `Documentos.LeyendaCertificadoRetencion` | texto estándar con la norma; la ciudad es la de la cooperativa |
| Periodicidad de los extractos del lote | `Documentos.PeriodicidadDeExtractos` | `Mensual` |
| Texto propio por clase de documento | `Documentos.TextosDeCaptaciones` | `{}` |

### Asamblea (P11)

| Qué | Parámetro o valor | Defecto |
|---|---|---|
| **P11** · Base de la revalorización | `Asamblea.BaseDeRevalorizacion` | saldo promedio diario del ejercicio (D13) |
| **P11** · Límite: el IPC del año anterior (Decreto 3081 de 1990) | `Asamblea.SerieDelIpc`; valor nacional `IPC` | serie `ANUAL`, el valor que rige el 31 de enero siguiente al ejercicio |
| Redondeo | `Asamblea.Redondeo` | al peso |
| Destino y clase de los excedentes | `Asamblea.DestinoDeExcedentes`, `Asamblea.ClaseDeAporteDeExcedentes` | a aportes ordinarios |
| La amortización deja fuera a los deudores | `Asamblea.AmortizacionExcluyeDeudores` | sí |
| Retención sobre la revalorización y los retornos (E260, respuesta 89 B) | `Asamblea.Retencion.Concepto`, `.TarifaRevalorizacion`, `.TarifaExcedentes`, `.BaseMinimaUvt` | sin tarifa no se retiene; la contadora fija concepto, tarifas y base mínima en UVT |
| Base de los retornos (E261, respuesta 90 C) | la decide la asamblea; opción «Base subida por plantilla» con la plantilla «Base de los retornos de la asamblea» | — |

### Inactividad, no reclamados y fallecimiento (P6, P7, contadora y abogado)

| Qué | Parámetro, valor o condición | Defecto |
|---|---|---|
| Días de inactividad y si causa inactiva | condición de la línea | 360 días, sigue causando (D21) |
| Aviso previo de inactividad | `Captaciones.DiasDeAvisoPrevioDeInactividad` | 30 días |
| **P7** · Traslado de cuentas abandonadas (Ley 1777 de 2016) | `CuentasAbandonadas.TrasladoHabilitado`, `CuentasAbandonadas.DiasSinMovimiento` | apagado; 1.095 días si se enciende |
| Días para pasar lo devuelto y no cobrado a no reclamados | `Captaciones.DiasParaNoReclamados` | vacío: no se traslada solo |
| **P6** · Tope de la entrega sin juicio de sucesión (EOSF art. 127) | valor nacional `TOPE_ENTREGA_SIN_SUCESION` | sin semilla |
| Soportes de la entrega sin sucesión | `Fallecimiento.SoportesDeEntregaSinSucesion` | vacío (al menos uno) |
| Textos de los avisos `PREINACTIV`, `INACTIVA`, `NORECLAMA` | `/cartera/plantillas-de-mensaje` | estándar |

### Fondo y riesgo de liquidez

| Qué | Parámetro o valor | Defecto |
|---|---|---|
| Tabla por categoría (Decreto 976 de 2026) | `FondoDeLiquidez.PorcentajesPorCategoria` | general 10 % y 2 %; plena por madurez 10/5/0 % (bandas de 30 y 540 días), permanente sólo al retiro 5 %, intereses causados y por causar, fin de la transición 2030-02-06 |
| Pisos legales | límites `FONDO_LIQUIDEZ_DEPOSITOS`, `_PERMANENTE_SOLO_RETIRO`, `_PLENA_BANDA_MEDIA`, `_PLENA_PERMANENTE_SOLO_RETIRO` | 0,10; 0,02; 0,05; 0,05 |
| Seguro de depósitos (Fogacoop) | valor nacional `TOPE_SEGURO_DEPOSITOS` | 50.000.000 desde 2024-08-19 (falta el número de la resolución) |
| Colchón de alerta y periodicidad de la foto | `FondoDeLiquidez.MargenDeAlerta`, `FondoDeLiquidez.Periodicidad` | 0,05; `Semanal` |
| IRL mínimo y plan de ajuste | `RiesgoDeLiquidez.UmbralIrl` (límite `IRL_MINIMO`), `RiesgoDeLiquidez.UmbralDePlanDeAjuste` | 1 (100 %); 1,10 |
| Bandas | `RiesgoDeLiquidez.BandasIrl`, `RiesgoDeLiquidez.BandasDeBrecha` | [15, 30, 60, 90]; [30, 60, 90, 180, 270, 360, abierta] |
| Retiros esperados | `RiesgoDeLiquidez.FraccionDeRetiroALaVista`, `…FraccionMensualDeRetiroDeAportes` | 0,10; 0,01 al mes |
| Índice de mora de las entradas | `RiesgoDeLiquidez.IndiceDeMora` | vacío: sin descuento |
| Descuento por clase de inversión | `RiesgoDeLiquidez.DescuentosPorInstrumento` | 0 en cuentas; 0,20 en lo demás |

### Reportes regulatorios (P8, P9)

- **P8 y P9** · Cotejar los **siete layouts** (SICSES 9026, 9068, 9013, 169 y 9067; exógena 1019 y 1020) contra el instructivo vigente y un
  archivo aceptado por el validador: orden, largo y formato de cada campo, separador, fechas y códigos. Mientras tanto cada generación lleva la
  advertencia «formato sin cotejar».
- Si los aportes mínimos van dentro de los ordinarios en el 9013 y si el saldo de depósitos del 9067 lleva los intereses causados.
- El umbral de la 1019 (límite `EXOGENA_1019_UMBRAL_UVT`, 24 UVT, «por persona sumando sus cuentas») y si el contractual y el permanente van en la
  1019 (hoy sí).

### Migración

- El reparto del interés migrado entre los días que cubre para medir el umbral diario.
- Las cifras de referencia por línea y concepto que entrega la cooperativa al corte y las diferencias de cuadre que se aceptan con acta.

## Lo que decide el dueño

**Ya respondidas y construidas** (2026-10-21 y 2026-10-22, detalle en
[cartera-captaciones-respuestas-del-dueno.md](cartera-captaciones-respuestas-del-dueno.md)): la fecha valor en un período ya liquidado (E256),
cancelar con cargos por cobrar (E255), el embargo que se recalcula en cada consignación (E254), el GMF en la entrega por fallecimiento y en no
reclamados (E253), los excedentes de quien no tiene cuenta de ahorro (E257), volver a girar un excedente o una entrega por fallecimiento rechazados
(E258), los retirados durante el ejercicio (E259), la retención y la base de los retornos de la asamblea (E260, E261), la inactivación de las
cuentas migradas (E262), retirar la instrucción de vencimiento del contractual (E263) y los avisos al titular por correo electrónico real, con SMS y WhatsApp a la espera de proveedor (E264). El título de
los CDAT migrados se emite al activar.

**Siguen abiertas** (detalle en `research.md` §13.4 a §25.1): el reverso del cruce al retiro en un solo comprobante; el alcance del destino
de un traslado; el CDAT embargado al vencer; la bonificación del contractual con cuotas pagadas tarde; el traslado al ICETEX; el F29 de riesgo de
liquidez y el código contable del 9026. Las demás preguntas de esa lista (persona natural obligada a llevar contabilidad, aprobador con alcance,
tasa pactada en la prórroga, giro y título de los CDAT migrados, concurrencia de embargos, lotes de documentos programados, activos líquidos,
retiro esperado del ahorro a la vista, cotitulares en el seguro de depósitos y detalle de las inversiones del fondo) se respondieron el 2026-10-21.
