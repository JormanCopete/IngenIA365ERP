using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T002 (decisiones-transversales T18, §2.18), FR-002 y FR-008: las entidades que son
/// <b>hechos</b> (<c>IHechoInmutable</c>: línea de kardex, consumo de capa, mensaje, dependencia,
/// intento, decisión de aprobación, <c>InventoryPosting</c>, instantánea de tercero, línea de
/// impuesto, versión y transmisión electrónica, ancla) no cambian después de insertarse. En el
/// fuente eso se ve en que no tienen <c>set</c> público: sólo <c>init</c> o <c>private set</c>. En
/// ejecución lo hace cumplir <c>ApplicationDbContext.SaveChangesAsync</c>.
///
/// <para>
/// Esqueleto del Setup: <see cref="Hechos"/> (nombres de tipo) empieza vacía y la prueba afirma la
/// regla sobre cada elemento; con la lista vacía pasa porque no hay nada que violar, no por un
/// <c>return</c> temprano. La llena el bloque que crea cada entidad (plataforma, fase 2; base de
/// inventario, fase 3; y las historias que agregan hechos).
/// </para>
/// </summary>
public class LosHechosInmutablesNoSeModifican
{
    /// <summary>Nombres de tipo de las entidades que son hechos inmutables. Los agrega el bloque que las crea.</summary>
    private static readonly string[] Hechos =
    [
        // Plataforma, bandeja de salida (T073): el mensaje y su arista sólo tienen init.
        "IntegrationMessage",
        "IntegrationMessageDependency",
        // Base de inventario, fase 3 (T133): la copia fiscal de la contraparte y la foto tributaria.
        "DocumentPartySnapshot",
        "DocumentTaxLine",
        // US2 (T245): el hecho del kardex y el historial del grupo contable de un producto (US1, T201).
        "KardexEntry",
        "ProductAccountingGroupChange",
        // US11 (T389): las capturas de un conteo físico sólo se agregan (una corrección es otra captura negativa).
        "CountCapture",
        // US7, entrega I2 (T476, T479): el intento de entrega y el recibo contable de un mensaje de Inventario.
        "IntegrationDeliveryAttempt",
        "InventoryPosting",
        // US8, entrega I4 (T671; data-model §18, §26): la versión de un documento electrónico y cada intento ante el canal.
        "ElectronicDocumentVersion",
        "ElectronicDocumentTransmission",
        // US16, entrega I5 (T825; data-model §3.5): el consumo de una capa PEPS. La capa (CostLayer) es proyección y no está aquí:
        // la protege NadieEscribeElKardexFueraDelRegistro.
        "LayerConsumption",
        // Feature 013, fase 2 (T0038): el registro central de valores nacionales y su copia por cooperativa (T37–T41), la revisión de la
        // copia, el perfil regulatorio por versiones (FR-018) y las cargas de listas restrictivas con sus entradas (T70).
        "PlatformNationalValue",
        "PlatformRegulatoryDocument",
        "NationalValue",
        "RegulatoryDocument",
        "NationalValueReplication",
        "NationalValueReview",
        "RegulatoryProfileVersion",
        "RestrictiveListLoad",
        "RestrictiveListEntry",
        // Feature 013, US7 (T0609, T0634): el recibo contable de un hecho de Cartera (Entities/Accounting/Transactions, que además cubre
        // PrincipioXI_ContableImmutable) y el indicador regulatorio, de sólo inserción (corregir es registrar otro que lo reemplaza).
        "LendingPosting",
        "RegulatoryIndicator",
    ];

    /// <summary>
    /// T671 (data-model §18, §26): las propiedades que un hecho admite escribir una sola vez (nulo → valor, <c>[EscrituraUnica]</c>).
    /// En la versión de un documento electrónico son exactamente los cuatro artefactos, que llegan después de numerar; la transmisión no
    /// admite ninguna.
    /// </summary>
    private static readonly Dictionary<Type, string[]> EscrituraUnicaAdmitida = new()
    {
        [typeof(IngenIA365ERP.Domain.Entities.ElectronicInvoicing.Transactions.ElectronicDocumentVersion)] =
        [
            "CanonicalAttachmentPublicId", "SignedXmlAttachmentPublicId", "AttachedDocumentAttachmentPublicId", "GraphicPdfAttachmentPublicId",
        ],
        [typeof(IngenIA365ERP.Domain.Entities.ElectronicInvoicing.Transactions.ElectronicDocumentTransmission)] = [],
    };

    [Fact]
    public void La_version_y_la_transmision_electronicas_son_hechos_con_escritura_unica_solo_en_los_artefactos()
    {
        var infractores = new List<string>();
        foreach (var (tipo, admitidas) in EscrituraUnicaAdmitida)
        {
            if (!typeof(IngenIA365ERP.Domain.Common.IHechoInmutable).IsAssignableFrom(tipo))
                infractores.Add($"{tipo.Name} no es IHechoInmutable");

            var conEscrituraUnica = tipo.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Where(p => p.IsDefined(typeof(IngenIA365ERP.Domain.Common.EscrituraUnicaAttribute), inherit: true))
                .Select(p => p.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
            if (!conEscrituraUnica.SequenceEqual(admitidas.OrderBy(n => n, StringComparer.Ordinal)))
                infractores.Add($"{tipo.Name}: [EscrituraUnica] en {{{string.Join(", ", conEscrituraUnica)}}}; se admite sólo {{{string.Join(", ", admitidas)}}}");

            foreach (var p in tipo.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                         .Where(p => p.DeclaringType == tipo && p.SetMethod is { IsPublic: true }))
            {
                var esInit = p.SetMethod!.ReturnParameter.GetRequiredCustomModifiers()
                    .Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit");
                if (!esInit) infractores.Add($"{tipo.Name}.{p.Name} tiene set público");
            }
        }

        Assert.True(infractores.Count == 0,
            "Los hechos del documento electrónico sólo admiten nulo → valor en sus artefactos (T671, Principio XI):\n  " + string.Join("\n  ", infractores));
    }

    /// <summary>
    /// T117 (T18): la guarda de <c>ApplicationDbContext.SaveChangesAsync</c> no tiene lista que mantener —cubre a todo
    /// tipo por el marcador—, así que lo que se fija es que exista, que el guardado la llame antes de escribir y que mire
    /// los dos marcadores. Si alguien la cambia por una lista, esta prueba tiene que cambiar con ella.
    /// </summary>
    [Fact]
    public void El_guardado_hace_cumplir_los_dos_marcadores_para_todo_tipo()
    {
        var root = RepoPath.FindRepoRoot();
        var carpeta = Path.Combine(root, "src", "Infrastructure", "IngenIA365ERP.Persistence", "DbContext");
        var contexto = File.ReadAllText(Path.Combine(carpeta, "ApplicationDbContext.cs"));
        var guarda = File.ReadAllText(Path.Combine(carpeta, "GuardaDeInmutabilidad.cs"));

        Assert.Matches(@"SaveChangesAsync\(CancellationToken[^)]*\)\s*\{\s*(//[^\n]*\n\s*)*await GuardaDeInmutabilidad\.VerificarAsync\(this", contexto);
        Assert.Contains("case IHechoInmutable", guarda);
        Assert.Contains("case IInmutableTrasConfirmar", guarda);

        var marcados = typeof(IngenIA365ERP.Domain.Common.BaseEntity).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && (typeof(IngenIA365ERP.Domain.Common.IHechoInmutable).IsAssignableFrom(t)
                    || typeof(IngenIA365ERP.Domain.Common.IInmutableTrasConfirmar).IsAssignableFrom(t)))
            .ToList();
        Assert.NotEmpty(marcados);
        Assert.All(marcados.Where(t => typeof(IngenIA365ERP.Domain.Common.IInmutableTrasConfirmar).IsAssignableFrom(t)),
            t => Assert.NotNull(t.GetProperty(nameof(IngenIA365ERP.Domain.Common.IInmutableTrasConfirmar.Status))));
    }

    /// <summary>
    /// T117 (T18): fuera de su creación, ningún archivo de <c>Application</c> llama <c>Remove</c>, <c>RemoveRange</c>,
    /// <c>Update</c> ni <c>UpdateRange</c> sobre el <c>DbSet</c> de un hecho o de un documento que se fija al confirmar.
    /// </summary>
    [Fact]
    public void Application_no_borra_ni_actualiza_en_bloque_hechos_ni_documentos()
    {
        var root = RepoPath.FindRepoRoot();
        var conjuntos = typeof(IngenIA365ERP.Application.Common.Interfaces.IApplicationDbContext).GetProperties()
            .Where(p => p.PropertyType.IsGenericType)
            .Where(p =>
            {
                var entidad = p.PropertyType.GetGenericArguments()[0];
                return typeof(IngenIA365ERP.Domain.Common.IHechoInmutable).IsAssignableFrom(entidad)
                    || typeof(IngenIA365ERP.Domain.Common.IInmutableTrasConfirmar).IsAssignableFrom(entidad);
            })
            .Select(p => p.Name)
            .ToList();
        Assert.Contains("DocumentTaxLines", conjuntos);
        Assert.Contains("InventoryDocuments", conjuntos);
        // I5 (T825): los consumos de capa son hechos; nadie los borra ni los actualiza en bloque.
        Assert.Contains("LayerConsumptions", conjuntos);

        var infractores = new List<string>();
        foreach (var archivo in RepoPath.ProductionCSharpFiles()
                     .Where(f => f.Contains($"{Path.DirectorySeparatorChar}IngenIA365ERP.Application{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            var texto = File.ReadAllText(archivo);
            foreach (var conjunto in conjuntos)
            {
                if (Regex.IsMatch(texto, $@"\.{Regex.Escape(conjunto)}\s*\.\s*(Remove|RemoveRange|Update|UpdateRange|ExecuteDelete|ExecuteDeleteAsync|ExecuteUpdate|ExecuteUpdateAsync)\b"))
                    infractores.Add($"{Path.GetRelativePath(root, archivo)}: {conjunto}");
            }
        }

        Assert.True(infractores.Count == 0,
            "Hechos o documentos borrados o actualizados en bloque (Principio XI, T18):\n  " + string.Join("\n  ", infractores));
    }

    /// <summary>
    /// I5, T825 (FR-043; data-model §3.5): el consumo de capa lleva el marcador, así la guarda del guardado rechaza cualquier cambio o baja
    /// después de insertarlo; y la capa no, porque es proyección (su restante se mueve con cada salida y la reconstrucción la corrige).
    /// </summary>
    [Fact]
    public void El_consumo_de_capa_es_un_hecho_y_la_capa_es_proyeccion()
    {
        var hecho = typeof(IngenIA365ERP.Domain.Common.IHechoInmutable);
        Assert.True(hecho.IsAssignableFrom(typeof(IngenIA365ERP.Domain.Entities.Inventory.Transactions.LayerConsumption)),
            "LayerConsumption tiene que ser IHechoInmutable (T825).");
        Assert.False(hecho.IsAssignableFrom(typeof(IngenIA365ERP.Domain.Entities.Inventory.Projections.CostLayer)),
            "CostLayer es proyección del kardex, no un hecho (data-model §3.5).");
        var conSetPublico = typeof(IngenIA365ERP.Domain.Entities.Inventory.Transactions.LayerConsumption)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(p => p.SetMethod is { IsPublic: true }
                && !p.SetMethod.ReturnParameter.GetRequiredCustomModifiers().Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit"))
            .Select(p => p.Name).ToList();
        Assert.True(conSetPublico.Count == 0, "LayerConsumption con set público: " + string.Join(", ", conSetPublico));
    }

    /// <summary>
    /// Feature 013, fase 2 (T0038): los hechos de la plataforma de Cartera llevan el marcador, así la guarda del guardado —también la de
    /// <c>AdminDbContext</c> desde C68— rechaza cualquier cambio o baja después de insertarlos.
    /// </summary>
    [Fact]
    public void Los_hechos_de_la_plataforma_de_Cartera_llevan_el_marcador()
    {
        var hecho = typeof(IngenIA365ERP.Domain.Common.IHechoInmutable);
        var tipos = typeof(IngenIA365ERP.Domain.Common.BaseEntity).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && Hechos.Contains(t.Name))
            .ToList();
        foreach (var nombre in new[] { "PlatformNationalValue", "PlatformRegulatoryDocument", "NationalValue", "RegulatoryDocument",
                     "NationalValueReplication", "NationalValueReview", "RegulatoryProfileVersion", "RestrictiveListLoad", "RestrictiveListEntry" })
        {
            var tipo = tipos.SingleOrDefault(t => t.Name == nombre);
            Assert.True(tipo is not null, $"{nombre} no está en el dominio.");
            Assert.True(hecho.IsAssignableFrom(tipo), $"{nombre} tiene que ser IHechoInmutable (Principio XI).");
        }
        var admin = File.ReadAllText(Path.Combine(RepoPath.FindRepoRoot(), "src", "Infrastructure", "IngenIA365ERP.Persistence", "DbContext", "AdminDbContext.cs"));
        Assert.Contains("GuardaDeInmutabilidad.VerificarAsync", admin, StringComparison.Ordinal);
    }

    /// <summary>
    /// Feature 013, T0144 (T0136; DT §2.3, C42, C44, C75, C112): los hechos de Cartera y las únicas propiedades que admiten escribirse una
    /// vez (nulo → valor). La guarda los cubre por el marcador; esta lista fija cuáles son, para que una entidad que deje de ser hecho
    /// o un <c>set</c> nuevo no pasen sin decidirlo.
    /// </summary>
    private static readonly Dictionary<string, string[]> HechosDeCartera = new(StringComparer.Ordinal)
    {
        ["LoanTransaction"] = [], ["LoanOperation"] = ["ReversedByOperationId"], ["LoanTermsVersion"] = [], ["RepaymentScheduleVersion"] = [],
        ["RepaymentInstallment"] = [], ["LoanDailyPosition"] = [], ["CreditDecision"] = [], ["SessionVote"] = ["ReplacedAt"],
        ["CreditScore"] = [], ["CreditApplicationCheck"] = [], ["CreditLineVersion"] = [], ["CreditLineRateBand"] = [],
        ["CreditLineDocumentRequirement"] = [], ["CreditLineInsurance"] = [], ["CreditLineCollateralRule"] = [],
        ["CommercialTermEquivalence"] = [], ["ContributionBalanceLoad"] = [], ["AttributionMatrix"] = [], ["AttributionLevel"] = [],
        ["AttributionRule"] = [], ["GoverningBodyRuleVersion"] = [], ["CaseParticipation"] = [], ["PrepaymentPreference"] = [],
        ["CreditLimit"] = [], ["BureauQuery"] = [], ["ScreeningResult"] = ["ReleasedAt", "ReleasedByUserId", "ReleaseReason"],
        ["DebtorBlock"] = ["LiftedOn", "LiftedAt", "LiftedByUserId", "LiftReason"], ["DisbursementOrderResult"] = [],
        ["DisbursementFile"] = ["VoidedAt", "VoidedByUserId", "VoidReason"], ["DisbursementFileLine"] = [], ["InstallmentNonDeduction"] = [],
        ["LendingInboundMessageEvaluation"] = [], ["AccountingFactPart"] = [], ["DebtorMessage"] = [], ["DebtorMessageEvent"] = [],
        ["PriorNotice"] = [], ["IssuedLendingDocument"] = [], ["IssuedDocumentLoan"] = [], ["IssuedDocumentEvent"] = [],
        ["MigrationLoadDifference"] = ["AcceptedAt", "AcceptedByUserId", "AcceptanceReason", "SessionItemId"],
        ["ProvisionalCollection"] = ["LoanId", "OperationId", "AppliedAt", "MessagePublicId"], ["LendingActivation"] = [],
        ["ConnectedGroup"] = ["EndedAt", "EndReason"], ["ConnectedGroupMember"] = ["EndedAt"],
        // US9 (T0674; data-model §23, R61): el cierre, sus líneas, su general, su reapertura y el historial de mora no admiten ninguna escritura
        // después de insertarse; la versión de la política sólo su validación con el punto de acta (C52).
        ["MonthlyClose"] = [], ["MonthlyCloseLoan"] = [], ["MonthlyCloseGeneralImpairment"] = [], ["MonthlyCloseReopening"] = [],
        ["LoanArrearsHistoryEntry"] = [], ["RiskPolicyVersion"] = ["ValidatedAt", "ValidatedByUserId", "SessionItemId"],
        // K2 (T0824; data-model §24.7, §24.8, §24.10): la línea del archivo de descuentos, el resultado de la pagaduría y el seguimiento de un
        // no aplicado no admiten ninguna escritura después de insertarse (un resultado posterior reemplaza al anterior con SupersedesResultId). El
        // archivo no es hecho: su contenido es de sólo init y sólo avanza el estado.
        ["PayrollDeductionFileLine"] = [], ["PayrollDeductionResult"] = [], ["NonDeductionFollowUp"] = [],
        // Feature 014 (T091, D36): la cuota de cuenta del archivo de un grupo, su resultado y la fila de la respuesta que no se aplicó tampoco (un
        // resultado posterior reemplaza al anterior con SupersedesResultId).
        ["PayrollDeductionFileAccountLine"] = [], ["PayrollDeductionAccountResult"] = [], ["PayrollDeductionUnmatchedLine"] = [],
        // K4 (T0936, T0932; data-model §26.5–§26.10): la historia de etapas, la asignación, la gestión (una corrección es otra gestión), la
        // promesa y la cuota del acuerdo no admiten ninguna escritura; la actuación jurídica, sólo la operación Charge que la cobra al deudor,
        // que llega cuando el aviso del gasto salió (T77).
        ["CollectionStageHistoryEntry"] = [], ["CollectorAssignment"] = [], ["CollectionAction"] = [], ["PaymentPromise"] = [],
        ["PaymentAgreementInstallment"] = [], ["LegalProcessAction"] = ["ChargeOperationId"],
        // K5 (T0997; data-model §28.2): la línea del archivo de una central no admite ninguna escritura después de insertarse. El archivo no es
        // hecho: su contenido es de sólo init y sólo avanzan el estado, el reemplazo y la carga con su radicado.
        ["RegulatoryFileLine"] = [],
        // K6a (T1031; data-model §23.8, §23.9): la elección voluntaria del modelo (un cambio es otra elección) y la carga complementaria del
        // historial (volver a cargar es otra carga) no admiten ninguna escritura después de insertarse.
        ["ImpairmentModelElection"] = [], ["ArrearsHistoryLoad"] = [],
        // Feature 014 (T021–T025; data-model §4.1–§4.3): el libro de cuentas y su posición no admiten ninguna escritura; la operación, sólo la
        // liberación del canje y el reverso que la anula.
        ["MemberAccountTransaction"] = [], ["MemberAccountOperation"] = ["ClearedAt", "ReversedByOperationId"], ["MemberAccountDailyPosition"] = [],
        // Feature 014, US1 (T060–T062; data-model §2.1, §2.2, §2.5): las versiones de las líneas de captación y de aportes y sus tramos; una versión
        // nueva las corrige.
        ["DepositLineVersion"] = [], ["DepositLineRateTier"] = [], ["ContributionLineVersion"] = [],
        // Feature 014, US3 (T081; data-model §3.5, §3.7): la cuota exigible sólo admite su anulación (una vez); lo no descontado, nada.
        ["AccountInstallment"] = ["CancelledAt", "CancelReason", "CancelledByOperationId"], ["AccountInstallmentNonDeduction"] = [],
        // Feature 014, US3 (T087; data-model §1.5.1): las cuentas que cubre un documento emitido no cambian.
        ["IssuedDocumentAccount"] = [],
        // Feature 014, US10 (T096; data-model §6.2): las líneas de una versión de la liquidación del retiro; la reliquidación escribe otra versión.
        ["MembershipWithdrawalLine"] = [],
        // Feature 014, US12 (T202; data-model §6.4): las líneas de una decisión de asamblea se escriben una vez, al aplicar, con todo.
        ["AssemblyDistributionLine"] = [],
        ["AssemblyReturnBase"] = [],
        // Feature 014, US11 (T187; data-model §5.2.1): una respuesta enviada al juzgado no cambia; la siguiente es otra fila.
        ["GarnishmentResponse"] = [],
        // Feature 014, US4 (T110, T114; data-model §2.3, §4.6): el uso del GMF del mes (un reverso escribe su espejo) y el catálogo de cargos de una
        // versión de la línea (una versión nueva lo corrige).
        ["GmfMonthlyUsage"] = [], ["DepositLineCharge"] = [],
        // US5 (T130): la liquidación de intereses (sólo su reverso, una vez) y la retención acumulada por titular.
        ["InterestSettlement"] = ["ReversedByOperationId"], ["WithholdingAccumulation"] = [],
        // US6 (T169): las condiciones selladas del CDAT, su plan y las reglas de tasa negociada (una versión nueva las corrige o las termina).
        ["MemberAccountTermsVersion"] = [], ["TermDepositScheduleItem"] = [], ["NegotiatedRateRule"] = [],
        // US15 (T215; data-model §6.7): la foto del fondo de liquidez de una fecha no cambia (pedirla otra vez devuelve la misma).
        ["LiquidityFundSnapshot"] = [],
    };

    /// <summary>
    /// Feature 015, T0020 (FR-002; Principio XI; data-model §0.2): los hechos de Recaudo y de la Tesorería nueva (las entidades marcadas H) y las
    /// únicas propiedades que admiten escribirse una vez. Nace vacía: la llena cada fase que crea entidades H (recibos, medios, aplicaciones,
    /// anulaciones, movimientos, arqueos cerrados, líneas de extracto, archivos de pago, resultados…). Recorre <c>Domain/Entities/Receipts</c> y
    /// los hijos de <c>Domain/Entities/Treasury</c>, sin la raíz heredada.
    /// </summary>
    private static readonly Dictionary<string, string[]> HechosDeRecaudoYTesoreria = new(StringComparer.Ordinal)
    {
        // Fase 5, US2 (T0089; data-model §2): el arqueo se inserta completo al registrar lo contado; sólo la aprobación de la diferencia de una línea se
        // escribe después, una vez. Movimientos de caja y de bóveda, ajustes y cierres del día no cambian: una corrección es otro documento.
        ["CashCount"] = [], ["CashCountDenomination"] = [], ["CashCountTerminalBatch"] = [], ["CashCountReference"] = [],
        ["CashCountLine"] = ["ApprovalRequestPublicId", "ApprovedByUserId", "ApprovedAt", "ApprovalReason"],
        ["CashMovement"] = [], ["CashMovementDenomination"] = [], ["CashSessionAdjustment"] = [],
        ["DayClose"] = [], ["DayCloseLine"] = [], ["VaultMovement"] = [], ["VaultMovementDenomination"] = [],
    };

    [Fact]
    public void Los_hechos_de_Recaudo_y_Tesoreria_llevan_el_marcador_y_solo_sus_columnas_de_escritura_unica_tienen_set()
    {
        var hecho = typeof(IngenIA365ERP.Domain.Common.IHechoInmutable);
        var tipos = typeof(IngenIA365ERP.Domain.Common.BaseEntity).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.Namespace is { } ns
                && (ns.StartsWith("IngenIA365ERP.Domain.Entities.Receipts", StringComparison.Ordinal)
                    || ns.StartsWith("IngenIA365ERP.Domain.Entities.Treasury.", StringComparison.Ordinal)))
            .ToList();
        var infractores = new List<string>();
        foreach (var (nombre, unicas) in HechosDeRecaudoYTesoreria)
        {
            var tipo = tipos.SingleOrDefault(t => t.Name == nombre);
            if (tipo is null) { infractores.Add($"{nombre}: no está en Entities/Receipts ni en Entities/Treasury"); continue; }
            if (!hecho.IsAssignableFrom(tipo)) infractores.Add($"{nombre}: no es IHechoInmutable");
            var marcadas = tipo.GetProperties().Where(p => p.IsDefined(typeof(IngenIA365ERP.Domain.Common.EscrituraUnicaAttribute), true))
                .Select(p => p.Name).OrderBy(n => n).ToList();
            if (!marcadas.SequenceEqual(unicas.OrderBy(n => n)))
                infractores.Add($"{nombre}: [EscrituraUnica] [{string.Join(", ", marcadas)}]; se esperaba [{string.Join(", ", unicas)}]");
        }
        var otrosHechos = tipos.Where(t => hecho.IsAssignableFrom(t) && !HechosDeRecaudoYTesoreria.ContainsKey(t.Name)).Select(t => t.Name).ToList();
        Assert.True(otrosHechos.Count == 0, "Hechos de Recaudo o Tesorería que no están en la lista (decidir su escritura única): " + string.Join(", ", otrosHechos));
        Assert.True(infractores.Count == 0, "Hechos de Recaudo o Tesorería que se pueden modificar (Principio XI):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Los_hechos_de_Cartera_llevan_el_marcador_y_solo_sus_columnas_de_escritura_unica_tienen_set()
    {
        var hecho = typeof(IngenIA365ERP.Domain.Common.IHechoInmutable);
        var tipos = typeof(IngenIA365ERP.Domain.Common.BaseEntity).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && t.Namespace?.StartsWith("IngenIA365ERP.Domain.Entities.Lending", StringComparison.Ordinal) == true)
            .ToList();
        var infractores = new List<string>();

        foreach (var (nombre, unicas) in HechosDeCartera)
        {
            var tipo = tipos.SingleOrDefault(t => t.Name == nombre);
            if (tipo is null) { infractores.Add($"{nombre}: no está en Entities/Lending"); continue; }
            if (!hecho.IsAssignableFrom(tipo)) infractores.Add($"{nombre}: no es IHechoInmutable");

            var conSet = tipo.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
                .Where(p => p.SetMethod is { IsPublic: true }
                    && !p.SetMethod.ReturnParameter.GetRequiredCustomModifiers().Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit"))
                .Select(p => p.Name).ToList();
            var marcadas = tipo.GetProperties().Where(p => p.IsDefined(typeof(IngenIA365ERP.Domain.Common.EscrituraUnicaAttribute), true))
                .Select(p => p.Name).ToList();
            if (!conSet.OrderBy(n => n).SequenceEqual(unicas.OrderBy(n => n)) || !marcadas.OrderBy(n => n).SequenceEqual(unicas.OrderBy(n => n)))
                infractores.Add($"{nombre}: set público [{string.Join(", ", conSet)}], [EscrituraUnica] [{string.Join(", ", marcadas)}]; se esperaba [{string.Join(", ", unicas)}]");
        }

        var otrosHechos = tipos.Where(t => hecho.IsAssignableFrom(t) && !HechosDeCartera.ContainsKey(t.Name) && !t.Name.StartsWith("Migration", StringComparison.Ordinal))
            .Select(t => t.Name).ToList();
        Assert.True(otrosHechos.Count == 0, "Hechos de Cartera que no están en la lista (decidir su escritura única): " + string.Join(", ", otrosHechos));
        Assert.True(infractores.Count == 0, "Hechos de Cartera que se pueden modificar (Principio XI, C42):\n  " + string.Join("\n  ", infractores));
    }

    /// <summary>
    /// US17 (T0263, T0273; C42, C75, C108): los hechos de gobierno y de la cola de mensajes al deudor están en <see cref="HechosDeCartera"/>
    /// con su escritura única: la matriz con sus niveles y reglas, la decisión, la versión de reglas del órgano, la intervención en un
    /// expediente, el mensaje y su historia sin ninguna; el voto, sólo <c>ReplacedAt</c> (un voto mal registrado se reemplaza, nunca se edita).
    /// Si alguno sale de la lista o cambia su escritura única, la prueba de arriba deja de mirarlo: ésta lo impide.
    /// </summary>
    [Fact]
    public void Los_hechos_de_gobierno_y_de_la_cola_de_mensajes_estan_en_la_lista()
    {
        var esperados = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["AttributionMatrix"] = [], ["AttributionLevel"] = [], ["AttributionRule"] = [], ["CreditDecision"] = [],
            ["GoverningBodyRuleVersion"] = [], ["SessionVote"] = ["ReplacedAt"], ["CaseParticipation"] = [],
            ["DebtorMessage"] = [], ["DebtorMessageEvent"] = [],
        };
        foreach (var (nombre, unicas) in esperados)
        {
            Assert.True(HechosDeCartera.TryGetValue(nombre, out var declaradas), $"{nombre} no está en HechosDeCartera (T0263).");
            Assert.Equal(unicas, declaradas);
        }
    }

    /// <summary>
    /// K4 (T0932; data-model §26): los hechos de la cobranza están en <see cref="HechosDeCartera"/> con su escritura única y sus conjuntos son
    /// de hechos; la etapa, la regla de reparto, el acuerdo, el proceso jurídico y la campaña no son hechos (cierran vigencia o avanzan de
    /// estado).
    /// </summary>
    [Fact]
    public void Los_hechos_de_la_cobranza_estan_en_la_lista()
    {
        var esperados = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["CollectionStageHistoryEntry"] = [], ["CollectorAssignment"] = [], ["CollectionAction"] = [], ["PaymentPromise"] = [],
            ["PaymentAgreementInstallment"] = [], ["LegalProcessAction"] = ["ChargeOperationId"],
        };
        foreach (var (nombre, unicas) in esperados)
        {
            Assert.True(HechosDeCartera.TryGetValue(nombre, out var declaradas), $"{nombre} no está en HechosDeCartera (T0932).");
            Assert.Equal(unicas, declaradas);
        }
        var hecho = typeof(IngenIA365ERP.Domain.Common.IHechoInmutable);
        foreach (var t in new[] { typeof(IngenIA365ERP.Domain.Entities.Lending.Collections.CollectionStage),
                     typeof(IngenIA365ERP.Domain.Entities.Lending.Collections.CollectorAssignmentRule),
                     typeof(IngenIA365ERP.Domain.Entities.Lending.Collections.PaymentAgreement),
                     typeof(IngenIA365ERP.Domain.Entities.Lending.Collections.LegalProcess),
                     typeof(IngenIA365ERP.Domain.Entities.Lending.Collections.MessageCampaign) })
            Assert.False(hecho.IsAssignableFrom(t), $"{t.Name} no es hecho: cierra vigencia o avanza de estado.");

        var conjuntos = typeof(IngenIA365ERP.Application.Common.Interfaces.IApplicationDbContext).GetProperties()
            .Where(p => p.PropertyType.IsGenericType && hecho.IsAssignableFrom(p.PropertyType.GetGenericArguments()[0]))
            .Select(p => p.Name)
            .ToList();
        foreach (var conjunto in new[] { "CollectionStageHistory", "CollectorAssignments", "CollectionActions", "PaymentPromises",
                     "PaymentAgreementInstallments", "LegalProcessActions" })
            Assert.Contains(conjunto, conjuntos);
    }

    /// <summary>
    /// US1 (T0341; C42): la versión de una línea con sus cuatro hijas y las equivalencias de Comercial son hechos sin escritura única: se
    /// corrigen con una versión nueva (la anterior rige hasta la víspera), nunca editándolas.
    /// </summary>
    [Fact]
    public void Las_versiones_de_lineas_sus_hijas_y_las_equivalencias_estan_en_la_lista()
    {
        foreach (var nombre in new[] { "CreditLineVersion", "CreditLineRateBand", "CreditLineDocumentRequirement", "CreditLineInsurance",
                     "CreditLineCollateralRule", "CommercialTermEquivalence" })
        {
            Assert.True(HechosDeCartera.TryGetValue(nombre, out var declaradas), $"{nombre} no está en HechosDeCartera (T0341).");
            Assert.Empty(declaradas);
        }
    }

    /// <summary>
    /// US2 (T0382; C42): los hechos de la originación están en <see cref="HechosDeCartera"/> con su escritura única —la validación, el
    /// puntaje, la consulta a centrales, la versión del cupo, la preferencia de abono y la carga de saldos de aportes sin ninguna; el resultado
    /// de listas sólo con su liberación y el bloqueo sólo con su levantamiento—. Un cupo, una preferencia o un saldo se corrigen con otra
    /// versión o carga, nunca editándolos.
    /// </summary>
    [Fact]
    public void Los_hechos_de_la_originacion_estan_en_la_lista()
    {
        var esperados = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["CreditApplicationCheck"] = [], ["CreditScore"] = [], ["BureauQuery"] = [], ["CreditLimit"] = [], ["PrepaymentPreference"] = [],
            ["ContributionBalanceLoad"] = [], ["ScreeningResult"] = ["ReleasedAt", "ReleasedByUserId", "ReleaseReason"],
            ["DebtorBlock"] = ["LiftedOn", "LiftedAt", "LiftedByUserId", "LiftReason"],
        };
        foreach (var (nombre, unicas) in esperados)
        {
            Assert.True(HechosDeCartera.TryGetValue(nombre, out var declaradas), $"{nombre} no está en HechosDeCartera (T0382).");
            Assert.Equal(unicas, declaradas);
        }

        var conjuntos = typeof(IngenIA365ERP.Application.Common.Interfaces.IApplicationDbContext).GetProperties()
            .Where(p => p.PropertyType.IsGenericType
                && typeof(IngenIA365ERP.Domain.Common.IHechoInmutable).IsAssignableFrom(p.PropertyType.GetGenericArguments()[0]))
            .Select(p => p.Name)
            .ToList();
        foreach (var conjunto in new[] { "CreditApplicationChecks", "CreditScores", "ScreeningResults", "BureauQueries", "CreditLimits",
                     "DebtorBlocks", "PrepaymentPreferences", "ContributionBalanceLoads" })
            Assert.Contains(conjunto, conjuntos);
    }

    /// <summary>
    /// US4 (T0480): los hechos del proceso diario —el libro, la operación (sólo su reverso se escribe una vez), las condiciones, el plan y sus
    /// cuotas, la posición diaria versionada y las partes de los hechos contables— están en <see cref="HechosDeCartera"/> y en
    /// <c>Entities/Lending/Transactions</c>, <c>Projections</c> e <c>Integration</c>; sus conjuntos los cubre la regla genérica. La corrida y sus
    /// errores no son hechos: la corrida avanza y el error se resuelve.
    /// </summary>
    [Fact]
    public void Los_hechos_del_proceso_diario_estan_en_la_lista()
    {
        var esperados = new Dictionary<string, (string EspacioDeNombres, string[] Unicas)>(StringComparer.Ordinal)
        {
            ["LoanTransaction"] = ("IngenIA365ERP.Domain.Entities.Lending.Transactions", []),
            ["LoanDailyPosition"] = ("IngenIA365ERP.Domain.Entities.Lending.Projections", []),
            ["AccountingFactPart"] = ("IngenIA365ERP.Domain.Entities.Lending.Integration", []),
            ["LoanOperation"] = ("IngenIA365ERP.Domain.Entities.Lending.Operations", ["ReversedByOperationId"]),
            ["LoanTermsVersion"] = ("IngenIA365ERP.Domain.Entities.Lending.Loans", []),
            ["RepaymentScheduleVersion"] = ("IngenIA365ERP.Domain.Entities.Lending.Schedules", []),
            ["RepaymentInstallment"] = ("IngenIA365ERP.Domain.Entities.Lending.Schedules", []),
        };
        var tipos = typeof(IngenIA365ERP.Domain.Common.BaseEntity).Assembly.GetTypes();
        foreach (var (nombre, (espacio, unicas)) in esperados)
        {
            Assert.True(HechosDeCartera.TryGetValue(nombre, out var declaradas), $"{nombre} no está en HechosDeCartera (T0480).");
            Assert.Equal(unicas, declaradas);
            Assert.Contains(tipos, t => t.Name == nombre && t.Namespace == espacio);
        }
        Assert.False(typeof(IngenIA365ERP.Domain.Common.IHechoInmutable).IsAssignableFrom(typeof(IngenIA365ERP.Domain.Entities.Lending.Integration.LendingProcessRun)));

        var conjuntos = typeof(IngenIA365ERP.Application.Common.Interfaces.IApplicationDbContext).GetProperties()
            .Where(p => p.PropertyType.IsGenericType
                && typeof(IngenIA365ERP.Domain.Common.IHechoInmutable).IsAssignableFrom(p.PropertyType.GetGenericArguments()[0]))
            .Select(p => p.Name)
            .ToList();
        foreach (var conjunto in new[] { "LoanTransactions", "LoanDailyPositions", "AccountingFactParts", "LoanTermsVersions", "RepaymentScheduleVersions",
                     "RepaymentInstallments" })
            Assert.Contains(conjunto, conjuntos);
    }

    /// <summary>
    /// US9 (T0674; data-model §23; R61): los hechos del riesgo y los cierres están en <see cref="HechosDeCartera"/> con su escritura única —ninguna,
    /// salvo la validación de la política—, viven en <c>Entities/Lending/Risk</c> y sus conjuntos son de hechos; la calificación por análisis no es
    /// hecho (se levanta una vez) y el cierre no tiene estado guardado ni <c>SupersededAt</c>: su estado se deriva de la reapertura.
    /// </summary>
    [Fact]
    public void Los_hechos_del_riesgo_y_los_cierres_estan_en_la_lista()
    {
        var esperados = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["MonthlyClose"] = [], ["MonthlyCloseLoan"] = [], ["MonthlyCloseGeneralImpairment"] = [], ["MonthlyCloseReopening"] = [],
            ["LoanArrearsHistoryEntry"] = [], ["RiskPolicyVersion"] = ["ValidatedAt", "ValidatedByUserId", "SessionItemId"],
        };
        var tipos = typeof(IngenIA365ERP.Domain.Common.BaseEntity).Assembly.GetTypes();
        foreach (var (nombre, unicas) in esperados)
        {
            Assert.True(HechosDeCartera.TryGetValue(nombre, out var declaradas), $"{nombre} no está en HechosDeCartera (T0674).");
            Assert.Equal(unicas, declaradas);
            Assert.Contains(tipos, t => t.Name == nombre && t.Namespace == "IngenIA365ERP.Domain.Entities.Lending.Risk");
        }
        Assert.False(typeof(IngenIA365ERP.Domain.Common.IHechoInmutable).IsAssignableFrom(typeof(IngenIA365ERP.Domain.Entities.Lending.Risk.AnalysisRating)),
            "La calificación por análisis no es hecho: se levanta (data-model §23.6).");
        var cierre = typeof(IngenIA365ERP.Domain.Entities.Lending.Risk.MonthlyClose);
        foreach (var prohibida in new[] { "Status", "SupersededAt", "ReopenedAt" })
            Assert.True(cierre.GetProperty(prohibida) is null, $"MonthlyClose no guarda {prohibida}: el estado se deriva de la reapertura (R61).");

        var conjuntos = typeof(IngenIA365ERP.Application.Common.Interfaces.IApplicationDbContext).GetProperties()
            .Where(p => p.PropertyType.IsGenericType
                && typeof(IngenIA365ERP.Domain.Common.IHechoInmutable).IsAssignableFrom(p.PropertyType.GetGenericArguments()[0]))
            .Select(p => p.Name)
            .ToList();
        foreach (var conjunto in new[] { "MonthlyCloses", "MonthlyCloseLoans", "MonthlyCloseGeneralImpairments", "MonthlyCloseReopenings",
                     "LoanArrearsHistory", "RiskPolicyVersions" })
            Assert.Contains(conjunto, conjuntos);
    }

    /// <summary>
    /// US8 (bloque 13C, T0728; data-model §29.3): las 17 filas de preparación de la carga (<c>Migration…Row</c>) son hechos sin ninguna escritura
    /// después de insertarse —reemplazar una parte deja la tanda <c>Superseded</c> y conserva sus filas—; la diferencia, el cobro provisional y la
    /// activación están en <see cref="HechosDeCartera"/> con su escritura única (aceptación con el acta; aplicación del cobro una vez; ninguna).
    /// </summary>
    [Fact]
    public void Las_filas_de_la_carga_de_migracion_son_hechos_sin_escritura()
    {
        var hecho = typeof(IngenIA365ERP.Domain.Common.IHechoInmutable);
        var filas = typeof(IngenIA365ERP.Domain.Common.BaseEntity).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.Namespace == "IngenIA365ERP.Domain.Entities.Lending.Migration"
                && t.Name.StartsWith("Migration", StringComparison.Ordinal) && t.Name.EndsWith("Row", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(17, filas.Count);
        var infractores = new List<string>();
        foreach (var tipo in filas)
        {
            if (!hecho.IsAssignableFrom(tipo)) infractores.Add($"{tipo.Name}: no es IHechoInmutable");
            var conSet = tipo.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
                .Where(p => p.SetMethod is { IsPublic: true }
                    && !p.SetMethod.ReturnParameter.GetRequiredCustomModifiers().Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit"))
                .Select(p => p.Name).ToList();
            if (conSet.Count > 0) infractores.Add($"{tipo.Name}: set público [{string.Join(", ", conSet)}]");
        }
        foreach (var nombre in new[] { "LendingActivation", "MigrationLoadDifference", "ProvisionalCollection" })
            Assert.True(HechosDeCartera.ContainsKey(nombre), $"{nombre} no está en HechosDeCartera (T0728).");
        Assert.True(infractores.Count == 0, "Filas de la carga de migración que se pueden modificar (Principio XI):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Application_no_borra_ni_actualiza_en_bloque_los_hechos_de_Cartera()
    {
        var conjuntos = typeof(IngenIA365ERP.Application.Common.Interfaces.IApplicationDbContext).GetProperties()
            .Where(p => p.PropertyType.IsGenericType
                && typeof(IngenIA365ERP.Domain.Common.IHechoInmutable).IsAssignableFrom(p.PropertyType.GetGenericArguments()[0]))
            .Select(p => p.Name)
            .ToList();

        // La regla genérica de arriba recorre todo DbSet de un hecho: con éstos, cubre también el libro y los demás hechos de Cartera.
        Assert.Contains("LoanTransactions", conjuntos);
        Assert.Contains("LoanOperations", conjuntos);
        Assert.Contains("CreditDecisions", conjuntos);
        Assert.Contains("SessionVotes", conjuntos);
        Assert.Contains("LendingActivations", conjuntos);
        // US1 (T0341): las versiones de líneas, sus hijas y las equivalencias de Comercial.
        Assert.Contains("CreditLineVersions", conjuntos);
        Assert.Contains("CreditLineRateBands", conjuntos);
        Assert.Contains("CreditLineInsurances", conjuntos);
        Assert.Contains("CommercialTermEquivalences", conjuntos);
    }

    private static readonly Regex SetPublico =new(@"public\s+[^;{=]+\{\s*get;\s*set;", RegexOptions.Compiled);

    [Fact]
    public void Ningun_hecho_expone_un_set_publico()
    {
        var root = RepoPath.FindRepoRoot();
        var fuentes = RepoPath.ProductionCSharpFiles().Select(f => (Archivo: f, Texto: File.ReadAllText(f))).ToList();
        var infractores = new List<string>();

        foreach (var hecho in Hechos)
        {
            var declaracion = new Regex($@"\bclass\s+{Regex.Escape(hecho)}\b", RegexOptions.Compiled);
            var (archivo, texto) = fuentes.FirstOrDefault(f => declaracion.IsMatch(f.Texto));

            if (archivo is null)
                infractores.Add($"{hecho}: no se encontró su declaración (si se renombró, actualizá Hechos)");
            else if (SetPublico.IsMatch(texto))
                infractores.Add($"{Path.GetRelativePath(root, archivo)}: {hecho} tiene una propiedad con set público");
        }

        Assert.True(infractores.Count == 0,
            "Hechos inmutables que se pueden modificar (FR-002, Principio XI):\n  " + string.Join("\n  ", infractores));
    }
}
