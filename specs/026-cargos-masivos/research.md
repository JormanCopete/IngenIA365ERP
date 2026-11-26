# 026 — Investigación y decisiones técnicas

Base de código: `origin/develop` @ `dc7ee87c` (2026-11-25). Rutas relativas a `src/`.

## R1. Cómo nace la obligación sin solicitud

- **Decisión**: un servicio nuevo, `NacimientoDeCargoMasivo`, copia la secuencia de `NacimientoDeObligacionDeVenta.NacerAsync`
  (`Core/IngenIA365ERP.Application/Lending/Integration/Comercial/NacimientoDeObligacionDeVenta.cs:114`) sin las partes de la venta (reserva de
  número, `Source*`, `SaleObligationStatus`, cupo, cobros provisionales). Usa:
  - `Loan { Origin = LoanOrigin.BulkCharge, Status = Active, BulkChargeLineId }`, `numerador.NumerarCreditoAsync(credito, "CR-")`, `registro.IniciarControl`;
  - `registro.RegistrarOperacion` con `Kind = Disbursement`, `DisbursementForm = Charge` (nuevo), `ActorKind = Person` y el usuario que confirmó;
  - `registro.RegistrarTransacciones([+Principal])`;
  - `TramoDeDesembolso.Condiciones(..., TermsVersionCause.BulkCharge)` (nueva causa), `tramos.PlanAsync`, `registro.RegistrarPosicion`;
  - el vínculo de nómina o pagaduría `LoanPayrollLink.AlOriginar` cuando la forma de pago lo pide (como `ExecuteLoanDisbursementCommand.cs:276-303`).
- **Por qué**: es el único camino que ya hace nacer un crédito completo (control, plan, posición) sin solicitud, y lo hace por el único escritor
  del libro (`RegistroDeCartera`).
- **Descartado**: `ExecuteLoanDisbursementCommand` (exige solicitud aprobada, prohíbe `InKind` y siempre emite orden de giro); usar `InKind`
  (fuerza los roles de Comercial y una factura).

## R2. El hecho contable: uno nuevo, no `CreditoDesembolsado`

- **Decisión**: dos hechos nuevos de Cartera:
  - `CargoMasivoRegistrado` (corto `CMS`): débito `Capital` (regla de saldo), crédito el rol nuevo **`ContrapartidaDelCargo`**;
  - `CargoMasivoAnulado` (corto `CMA`): el espejo.
- **Por qué**: `CreditoDesembolsado` sin `InKind` acredita `DesembolsoPorGirar`, cuyos motivos sólo son `Transfer`, `Check` y `ThirdPartyPayment`
  y que Tesorería concilia contra un giro que aquí no existe. Además el tipo de comprobante se mapea por hecho (`ACC_LendingVoucherMappings`):
  reusar `CreditoDesembolsado` obligaría a `DS`.
- Cada hecho nuevo se declara en tres sitios que una prueba cruza: `OperacionesDeCartera` (Application/Accounting/Lending/Reglas),
  `LendingFactTypesSeeder.Datos` (Persistence/Seeding/Parametric) y `CatalogoDeMensajesDeCarteraV1`.
- **Rol `ContrapartidaDelCargo`**: naturaleza crédito, familia puente (no lleva la dimensión del hecho), exigidas `[]`, opcionales `[Línea,
  Sucursal]`, tercero natural `Beneficiario` (el tercero elegido en la corrida). Se agrega en `RolesDeCuentaDeCartera.Todos` antes del bloque de
  captaciones y sube `RolesDeCredito` de 28 a 29.
- **Constructor**: `ConstructorDeLineasDeCartera` gana el caso de `CargoMasivoRegistrado`: débito `Saldo(Principal)` con el deudor; crédito
  `L(R.ContrapartidaDelCargo, …, tercero = CounterpartPersonPublicId)`. El anulado, al revés.

## R3. Un comprobante por corrida

- **Decisión**: la corrida **sella el modo** de sus hechos en vez de resolverlo:
  `ModoDeEntrega.Sellado(DeliveryMode.Batch, "CAR.CMS|CargoMasivo||PorDia", "CargoMasivo:{run}")`, y al terminar la última tanda crea el lote y
  le asigna las entregas de ese alcance, en la misma transacción, como `CierreMensualDeCartera.LoteAsync` (Lending/Risk, :491-509).
- `PostLendingSummaryGroupCommand.Resumen` agrupa por fecha contable y tipo de comprobante: **todos los hechos de una corrida llevan la misma
  fecha contable** (la del cargo), así sale un solo comprobante. La granularidad es `PorDia` (no por sucursal): la sucursal va en cada línea.
  Donde la cuenta exige tercero o cruce, una línea por crédito; si no, una línea por cuenta, sucursal y centro de costo.
- Con contabilidad sin iniciar o el hecho en «No pasa», `ModoDePasoDeCartera` diría `NoPasa`: la corrida lo consulta una vez
  (`ResolverAsync` sobre un hecho de muestra) y si es `NoPasa` sella `NoPasa` en vez de `Batch`.
- **Descartado**: dejar el modo de fábrica (`EnLinea` → un comprobante por crédito) o un lote programado por hora (podría mezclar corridas).

## R4. Tipo de comprobante: `CO`, no `CM`

- `CM` ya existe en `voucher-types.json` («Causación de interés de mora»). Se siembra **`CO` «Cargos masivos»** (`usage: Module`,
  `module: CAR`) y los mapeos `CargoMasivoRegistrado → CO` y `CargoMasivoAnulado → CO` en `lending-voucher-mappings.json`. Se cambia como
  cualquier tipo de Cartera en `/contabilidad/cartera/tipos-de-comprobante`. Ajusta D2 del dueño sólo en el código.

## R5. Ejecución: corrida de Cartera en segundo plano

- **Decisión**: encabezado y borrador al estilo del proceso de causación (`AccrualProcess`), y la ejecución como **`LendingProcessRun`** con
  `LendingProcessKind.BulkCharge = 9` y `BulkChargeVoid = 10`, conducida por `ConductorDeCorridasDeCartera` (agregar las dos clases a `Clases`,
  :60-68) con un `IEjecutorDeCorridaDeCartera` que toma N líneas pendientes por cursor y crea cada obligación en su propia
  `TransaccionExplicita`, como `CicloDiarioDeCartera.cs:188-214`.
- **Por qué**: reanuda entre pasadas y réplicas (arrendamiento `lending.process` y cursor), reintenta con la ruta existente
  `/process-runs/{id}/retry` y no depende de que la pestaña siga abierta.
- **Actor**: el conductor corre como proceso; la operación se sella con `ActorKind = Person` y el usuario que confirmó
  (`RequestedByUserId`), como `TramoDeDesembolso.RegistrarAsync`, para que la segregación y la auditoría vean a la persona.
- **Descartado**: crear todo en la transacción de confirmar (2.000 créditos en una transacción) y el `/next` por tandas desde la pantalla de
  Transporte (se detiene si se cierra la pestaña).

## R6. Unicidad por línea, asociado y período

- Índice único filtrado en `LND_BulkChargeLines (CreditLineId, PersonId, Period)` donde `HoldsPeriod = 1`. `HoldsPeriod` es verdadero mientras la
  línea está pendiente o creada, y falso al omitirse, fallar o anularse. Una carrera entre dos corridas se traduce a omitido «ya tiene el cargo».

## R7. Población y exclusiones

- Activo = `COR_Associates.Status = "A"` sin `WithdrawalDate` vencida (o con `RejoinDate` posterior), la misma regla de
  `PersonaDeOriginacion` (Lending/Origination/PersonaDeOriginacion.cs:24-42), que se reusa.
- Exclusiones (D3): `Person.IsDeceased`; un `DeceasedHolderCase.Vivo`; un `MembershipWithdrawal` en `Vivos`.
- Filtros: agencia (`Associate.BranchId`), forma de pago de la ficha (`DeductionType`: `C` caja, `N` nómina), período de descuento
  (`DeductionPeriod`), pagaduría (empresa empleadora, ver R8).

## R8. Forma de pago y vínculo de nómina o pagaduría

- No hay mapa de la ficha a `RepaymentChannel`. «La de cada asociado»: `DeductionType = "N"` → `OwnPayroll` si la línea la admite; si el asociado
  tiene empresa empleadora con convenio de pagaduría vigente (`EmployerCompanyId` → `TaxId` del pagador del `PayrollAgreement`, como
  `LineasDeGrupoDeDescuento.cs:82-87`) → `PaymasterDeduction`; si no, `Cashier`. Si la línea no admite la resultante, su forma por defecto, con
  aviso.
- `OwnPayroll` y `PaymasterDeduction` crean el `LoanPayrollLink` con el convenio (propio o de la pagaduría); sin convenio, la línea se omite
  «sin pagaduría» o «sin convenio de nómina propia».

## R9. Anulación

- `ReversoDeDesembolso` no sirve: exige una orden de giro y deja el crédito en `PendingDisbursement`. `VoidPendingLoanCommand` sólo anula
  créditos sin desembolsar.
- **Decisión**: `AnulacionDeCargoMasivo` registra una operación `DisbursementReversal` con los espejos del principal
  (`EspejosDelReverso.DelDesembolso`) por `RegistroDeCartera`, pasa el crédito de `Active` a `Voided` con el motivo (`CambiarEstado`, transición ya
  permitida) y emite `CargoMasivoAnulado`. **Sólo si el libro del crédito no tiene otra cosa** que su desembolso: ni pagos, ni causación con
  valor, ni novedades. Si la tiene, se informa «tiene movimientos» y se corrige con las herramientas de siempre (FR-023).
- La anulación de la corrida entera corre como `LendingProcessRun` `BulkChargeVoid` y sella sus hechos con el alcance
  `CargoMasivoAnulacion:{run}:{n}` (un comprobante por anulación); la de un asociado es síncrona y va en línea.

## R10. Centrales y cobranza

- Centrales: `ProyeccionCentralDeRiesgo.cs:361-396`, motivo nuevo `BulkChargeNotReported` en `MotivosDeExclusionDeCentral` cuando el crédito es
  de origen `BulkCharge` y su versión de línea tiene `BulkChargeReportsToBureaus = false`.
- Cobranza: `CobranzaDelCicloDiario.ExcluidosAsync` (:313-322) y los filtros del reparto manual (:436) y de la agenda (:558) excluyen los de origen
  `BulkCharge` cuya versión tiene `BulkChargeEntersCollection = false`.

## R11. La línea

- `CreditLineVersion` gana `AllowsBulkCharge`, `BulkChargeDefaultAmount`, `BulkChargeDefaultInstallments`, `BulkChargeReportsToBureaus`,
  `BulkChargeEntersCollection`. Periodicidad y forma de pago por defecto reusan `DefaultPeriodicity` y `DefaultRepaymentChannel`.
- Se agregan a la huella con `.Si(b.AllowsBulkCharge, …)` (como `AsksFirstDueDate`, :554), así las versiones guardadas no cambian de huella.
- `Validar`: con la marca, nada de codeudores ni garantías (`CreditLineCollateralRule`), documentos, `RequiresEmployment` ni
  `RequiresBureauQuery`; valor por defecto dentro de `MinAmount`–`MaxAmount`.
- Recorrido de una marca: input y DTO (`CreditLineDtos.cs`), borrador (`BorradoresDeLinea.cs`), comandos, configuración EF con
  `HasDefaultValue`, editor (`EditorDeVersionDeLinea.razor`), resumen, DTO cliente y plantilla `lending.credit-lines`.

## R12. Lugares que miran `LoanOrigin`

- `ListLoansQuery` y el filtro `origin` de `LoansEndpoints`; `GetLoanQuery`; `TextosDeCreditos.cs:59-60` (etiqueta «Cargo masivo») y la causa
  (:179-180); `CreditoDetalle.razor:44` (acciones). `VintagesReportQuery` lo incluye solo (`!= Migration`). `CambioDePlazo` lo trata como
  originación.

## R13. Plano

- Plantilla `lending.bulk-charges` en `CatalogoDePlantillasDeCartera.Todas`, con la mecánica de `ImportAssemblyReturnBasesCommand`: el
  «aplicar» no crea obligaciones, **escribe las líneas del borrador** de una corrida en modo plano; la corrida se previsualiza y se confirma como
  cualquier otra. Rutas con `MapPlantilla` en el grupo de la corrida.

## R14. Gobierno

- Permisos en `LendingPermissionCatalogSeeder.Catalog`: `Lending.BulkCharges.View`, `.Run`, `.Void` (sube el total de 169 a 172 en
  `SeguridadYHabeasDataDeCarteraTests` y `PermisosDeCarteraTests`). Perfiles sugeridos: Administrador de Cartera los tres; Analista View y Run.
- Parámetro `Segregacion.CargoMasivoPorOtraPersona` (`SiNo`, falso) en `ParametrosDeCartera`, comprobado al confirmar contra quien creó el
  borrador.
- Auditoría: `Lending.BulkCharge.Confirmed`, `.Completed`, `.Voided`, `.LineVoided` por `LendingAuditEmitter`.
- Clave de operación en crear, confirmar, anular y descartar; el archivo de endpoints entra a `LosComandosDeCarteraLlevanClave`.

## R15. Migración

- Un par nuevo de cooperativa, `CargosMasivos`, posterior a `EdicionDeSolicitudRadicada` (20261125171011 / 20261125170921), con
  `tools/scripts/add-migration.ps1 -Name CargosMasivos -Context Application` y paridad por `check-migration-parity.ps1`. Aditiva: dos tablas y
  columnas nulas o con valor por defecto.
