using System.Text.Json.Nodes;
using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using R = IngenIA365ERP.Application.Accounting.Lending.Reglas.RolesDeCuentaDeCartera;

namespace IngenIA365ERP.Application.Tests.Accounting.Lending;

/// <summary>
/// Feature 013, T0612 (T52, T54, C21, C77, C79, C81, C91, C93; contracts/contabilidad.md §2, §3, §5, §7.3 y §24): los casos dorados del
/// constructor de líneas de Cartera. Cada archivo de <c>Casos/</c> trae la unidad de hechos (sobre y contenidos <c>…V1</c>), lo que agrega a
/// la matriz del escenario y el comprobante esperado, calculado a mano —cuenta, lado, importe, tercero, cruce, sucursal, centro, el rol del
/// que sale cada línea y su mapa hecho/concepto/créditos—, o el recibo sin comprobante (<c>Informational</c>, <c>ZeroValue</c>,
/// <c>SameAccounts</c>). Cubren los quince ejemplos de §24 (casos 01–22), un caso por cada tipo de negocio de <see cref="OperacionesDeCartera"/>
/// (los de K3 y K4 incluidos, 23–37), importe negativo que invierte el lado, ceros descartados y suma de líneas iguales (38), el recibo
/// <c>SameAccounts</c> (39), el tercero natural de cada rol (26, 40, 41), la gracia (42), los movimientos del deterioro y el nivel general o
/// adicional (10, 11, 43, 44), el castigo con su suspenso (19) y con faltante de deterioro (49, T0900) y los contingentes con y sin cruce (34–37).
///
/// <para>
/// <b>Lo que corre en A1</b> (el constructor, <c>ConstructorDeLineasDeCartera</c>, es de T0646): que cada caso esté bien formado —los
/// contenidos son de su <c>…V1</c> sin propiedades desconocidas, el tipo esperado es el del mapeo del hecho principal (C01), las líneas cuadran,
/// cada rol es del catálogo y de un hecho de la unidad y tiene en la matriz una regla con esa cuenta— y que el comprobante esperado pase las
/// reglas 1 a 11 de la 009 (<c>AccountingPoster.ValidarAsync</c>): lo que el constructor tiene que armar, el contrato lo contabiliza.
/// </para>
///
/// <para>
/// <b>Lo que agregó T0646</b> (molde <c>ConstructorDeLineasDeInventarioTests.El_caso_dorado_da_el_comprobante_esperado</c>): una teoría sobre
/// <see cref="Casos"/> que construya la unidad (<see cref="EscenarioContableDeCartera.Unidad"/>, con <c>matrizExtra</c> si la trae) contra el
/// escenario y compare con <c>esperado</c>: sin fallos; tipo, fecha (= <c>origin.operationDate</c>), <c>Kind = Regular</c>, origen
/// <c>("CAR", esperado.origen, origin.publicId)</c>, <c>RegistradoPor</c> (nulo si <c>originUser.centralUserId</c> es nulo, C23); las líneas
/// como conjunto (<see cref="EscenarioContableDeCartera.Linea"/> da la forma), con su mapa; y, en los casos sin comprobante, el motivo del
/// recibo.
/// </para>
/// </summary>
public class ConstructorDeLineasDeCarteraTests
{
    public static IEnumerable<object[]> Casos() => EscenarioContableDeCartera.NombresDeCasos();

    [Fact]
    public void El_escenario_carga_la_matriz_los_mapeos_y_los_meses()
    {
        var e = new EscenarioContableDeCartera();

        e.Matriz.Should().HaveCountGreaterThan(60);
        e.Matriz.Should().OnlyContain(r => r.Operation == "*" || R.Buscar(r.Role)!.AdmiteHecho, "hecho * en los roles que no son de resultado (C77)");
        e.D.Db.LendingVoucherMappings.Count().Should().Be(34,
            "31 hechos de negocio (K6b suma la corrección monetaria UVR y el reparto de un pago corregido por la UVR) y las tres excepciones de Nómina en DN (§5)");
        e.D.Db.AccountingPeriods.Count(p => p.Status == Domain.Enums.Accounting.PeriodStatus.Open && p.Month == 11).Should().BeGreaterThan(0);
        Casos().Should().HaveCountGreaterThanOrEqualTo(46);
    }

    [Fact]
    public void Los_casos_cubren_cada_tipo_de_negocio_y_cada_motivo_sin_comprobante()
    {
        var tipos = Casos().Select(c => EscenarioContableDeCartera.Caso((string)c[0]))
            .SelectMany(c => c["mensajes"]!.AsArray().Select(m => (string)m!["type"]!))
            .ToHashSet(StringComparer.Ordinal);
        tipos.Should().Contain(OperacionesDeCartera.DeNegocio.Select(o => o.Codigo), "un caso por cada tipo de negocio de OperacionesDeCartera (K3 y K4 incluidos)");

        var sinComprobante = Casos().Select(c => EscenarioContableDeCartera.Caso((string)c[0])["esperado"]!["sinComprobante"])
            .Where(n => n is not null).Select(n => (string)n!).ToHashSet();
        sinComprobante.Should().BeEquivalentTo([LendingPosting.SinComprobanteInformativo, LendingPosting.SinComprobanteValorCero, LendingPosting.SinComprobanteMismasCuentas]);
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public async Task El_caso_dorado_esta_bien_formado_y_su_comprobante_pasa_las_reglas_de_la_009(string nombre)
    {
        var e = new EscenarioContableDeCartera();
        var caso = EscenarioContableDeCartera.Caso(nombre);
        if (caso["matrizExtra"] is JsonArray extra) e.AgregarReglas(extra);

        // Los contenidos son de su …V1 y no traen nada que el record no tenga.
        foreach (var m in caso["mensajes"]!.AsArray())
        {
            var registro = EscenarioContableDeCartera.TipoDe((string)m!["type"]!);
            EscenarioContableDeCartera.PropiedadesDesconocidas(m["payload"], registro.Record).Should().BeEmpty($"{nombre}: {registro.Type}");
        }
        var unidad = EscenarioContableDeCartera.Unidad(caso);
        unidad.Should().OnlyContain(m => m.Envelope.OriginModule == "CAR" && m.Envelope.Currency == "COP" && m.OriginDocumentClassText != null);
        var tipos = unidad.Select(m => m.Type).ToList();
        var principal = OperacionesDeCartera.Principal(tipos)!;
        tipos.Where(t => t != principal).Should().OnlyContain(t => OperacionesDeCartera.Buscar(principal)!.Acompanantes.Contains(t),
            "una unidad es una operación y su modo: el principal y sus acompañantes (C01)");

        var esperado = caso["esperado"]!.AsObject();
        if (esperado["sinComprobante"] is { } motivo)
        {
            if ((string)motivo! == LendingPosting.SinComprobanteInformativo)
                unidad.Should().OnlyContain(m => m.Envelope.Kind == IntegrationMessageKind.Informational);
            else
                unidad.Should().OnlyContain(m => m.Envelope.Kind == IntegrationMessageKind.Business);
            return;
        }

        // El tipo es el del mapeo del hecho principal, con la clase de su recaudador (§4.2, C79).
        var mapeos = await new TiposDeComprobanteDeCartera(e.D.Db).CargarAsync(default);
        var tipo = mapeos.Resolver(principal, ClaseDelRecaudador(unidad[0].Payload));
        tipo.IsSuccess.Should().BeTrue(tipo.Error?.Message);
        tipo.Value.VoucherTypeCode.Should().Be((string)esperado["tipo"]!, nombre);
        ((string)esperado["fecha"]!).Should().Be(unidad[0].Envelope.Origin.OperationDate.ToString("yyyy-MM-dd"), "la fecha del comprobante es la fecha contable del hecho (§7.3)");
        ((string)esperado["origen"]!).Should().Be(OrigenesDeCartera.Documento, "C114");

        var lineas = esperado["lineas"]!.AsArray().Select(l => l!.AsObject()).ToList();
        lineas.Sum(l => (decimal)l["debito"]!).Should().Be(lineas.Sum(l => (decimal)l["credito"]!), $"{nombre} cuadra");
        foreach (var l in lineas)
        {
            var rol = R.Buscar((string)l["rol"]!);
            rol.Should().NotBeNull((string)l["rol"]!);
            var hechos = l["mapa"]!["hechos"]!.AsArray().Select(h => (string)h!).ToList();
            hechos.Should().NotBeEmpty().And.OnlyContain(h => tipos.Contains(h), $"{nombre}: el mapa nombra hechos de la unidad");
            hechos.Should().Contain(h => OperacionesDeCartera.Buscar(h)!.TieneRol(rol!.Codigo), $"{nombre}: {rol!.Codigo} es un rol de algún hecho de la línea");
            var cuenta = e.Cuenta((string)l["cuenta"]!);
            e.Matriz.Should().Contain(r => r.Role == rol!.Codigo && r.AccountId == cuenta.Id, $"{nombre}: la matriz lleva {rol!.Codigo} a {cuenta.Code}");
            ((decimal)l["debito"]! == 0m).Should().NotBe((decimal)l["credito"]! == 0m, $"{nombre}: una línea tiene un solo lado");
        }

        var validacion = await e.D.Poster.ValidarAsync(e.ComprobanteEsperado(caso), default);
        validacion.Errores.Select(x => $"línea {x.LineNumber} {x.AccountCode}: {x.Code} {x.Message}").Should().BeEmpty($"{nombre}: el contrato de la 009 lo acepta");
    }

    /// <summary>
    /// T0646: el constructor arma, contra el escenario y la <c>matrizExtra</c> del caso, exactamente el comprobante esperado —tipo, fecha
    /// contable, <c>Regular</c>, origen <c>("CAR", LendingIssuedDocument, origin.publicId)</c>, «registrado por» el usuario de origen o nulo si lo
    /// originó el proceso (C23)— con sus líneas como conjunto (cuenta, lado, importe, tercero, sucursal, centro y cruce) y el mapa de cada una
    /// (rol, hechos, concepto y créditos); o el recibo sin comprobante del caso.
    /// </summary>
    [Theory]
    [MemberData(nameof(Casos))]
    public async Task El_caso_dorado_da_el_comprobante_esperado(string nombre)
    {
        var e = new EscenarioContableDeCartera();
        var caso = EscenarioContableDeCartera.Caso(nombre);
        if (caso["matrizExtra"] is JsonArray extra) e.AgregarReglas(extra);
        var unidad = EscenarioContableDeCartera.Unidad(caso);
        var hechos = unidad.Select(HechoDeCartera.De).ToList();
        var esperado = caso["esperado"]!.AsObject();

        if ((string?)esperado["sinComprobante"] == LendingPosting.SinComprobanteInformativo)
        {
            ConstructorDeLineasDeCartera.EsInformativa(hechos).Should().BeTrue(nombre);
            return;
        }

        var catalogos = await CatalogosDeCartera.CargarAsync(e.D.Db, new ResolutorDeReglasDeCartera(e.D.Db), new TiposDeComprobanteDeCartera(e.D.Db),
            [hechos], e.D.Clock.TodayUtc, default);
        var c = ConstructorDeLineasDeCartera.Construir(hechos, catalogos);
        c.Fallos.Select(f => $"{f.FactType} {f.Role}: {f.Error.Code} {f.Error.Message}").Should().BeEmpty(nombre);

        if ((string?)esperado["sinComprobante"] is { } motivo)
        {
            c.SinLineas.Should().BeTrue(nombre);
            c.SinComprobante.Should().Be(motivo, nombre);
            return;
        }

        var r = c.Request!;
        var sobre = unidad[0].Envelope;
        r.VoucherTypeCode.Should().Be((string)esperado["tipo"]!, nombre);
        r.Date.Should().Be(sobre.Origin.OperationDate, "la fecha del comprobante es la fecha contable del hecho, nunca la de hoy (§7.3)");
        r.Kind.Should().Be(Domain.Enums.Accounting.DocumentKind.Regular);
        r.Origin.Should().Be(new AccountingOrigin("CAR", (string)esperado["origen"]!, sobre.Origin.PublicId));
        if (sobre.OriginUser.CentralUserId is { } central)
            r.RegistradoPor.Should().Be(new UsuarioDeOrigen(central, sobre.OriginUser.Name));
        else
            r.RegistradoPor.Should().BeNull("lo originó el proceso: el comprobante dice «Proceso de integración» (C23)");

        var lineasEsperadas = esperado["lineas"]!.AsArray().Select(l => l!.AsObject()).ToList();
        static string Clave(PostingLine l) =>
            string.Join('|', l.AccountCode, l.Debit.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                l.Credit.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), l.PersonId, l.BranchId, l.CostCenterId, l.CrossDocumentType,
                l.CrossDocumentNumber);
        r.Lines.Select(Clave).Should().BeEquivalentTo(lineasEsperadas.Select(l => Clave(e.Linea(l))), nombre);

        var mapa = r.Lines.Select((l, i) => (Clave: Clave(l), Mapa: c.Mapa[i])).ToDictionary(x => x.Clave, x => x.Mapa);
        foreach (var l in lineasEsperadas)
        {
            var m = mapa[Clave(e.Linea(l))];
            var de = $"{nombre}: {(string)l["rol"]!} {(string)l["cuenta"]!}";
            m.Rol.Should().Be((string)l["rol"]!, de);
            m.AccountCode.Should().Be((string)l["cuenta"]!, de);
            m.Hechos.Should().BeEquivalentTo(l["mapa"]!["hechos"]!.AsArray().Select(h => (string)h!), de);
            m.Concepto.Should().Be((string?)l["mapa"]!["concepto"], de);
            m.Creditos.Should().BeEquivalentTo(l["mapa"]!["creditos"]!.AsArray().Select(h => (string)h!), de);
        }

        var validacion = await e.D.Poster.ValidarAsync(r, default);
        validacion.Errores.Should().BeEmpty($"{nombre}: lo que arma el constructor lo acepta el contrato de la 009");
    }

    /// <summary>La clase del recaudador del hecho principal (la del sobre del pago o la de sus dimensiones), para el mapeo <c>DN</c>.</summary>
    private static string? ClaseDelRecaudador(object? contenido) => contenido switch
    {
        PagoAplicadoV1 p => p.Collector.CollectorClass,
        PagoReversadoV1 p => p.Collector.CollectorClass,
        SaldoAFavorResueltoV1 p => p.Collector.CollectorClass,
        _ => null,
    };
}
