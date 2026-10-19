using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Contributions;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using R = IngenIA365ERP.Application.Accounting.Lending.Reglas.RolesDeCuentaDeCartera;

namespace IngenIA365ERP.Application.Tests.Accounting.Lending.Captaciones;

/// <summary>
/// Feature 014, US8 (T143, T146, T148; SC-009; contabilidad.md §8.1 de la 014): del libro de cuentas a Contabilidad sin perder un peso. Sobre el escenario
/// real de la Cartera (<see cref="AportesDePrueba"/>: consignar, retirar con GMF, trasladar entre cuentas y reversar por el recaudador una consignación ya
/// usada), cada operación emite sus hechos a Contabilidad, ninguno trae una cuenta del plan, todos se desglosan sin fallos y lo que el constructor lleva al
/// capital y al GMF de cada cuenta es exactamente lo que dice el libro: la conciliación de la Cartera con Contabilidad da cero. Los saldos que concilia
/// <see cref="SaldosContablesDeCaptaciones"/> son los mismos del libro, con la combinación de la cuenta.
/// </summary>
public sealed class HechosDelLibroDeCuentasTests : IDisposable
{
    private readonly AportesDePrueba _a = new();

    public HechosDelLibroDeCuentasTests()
    {
        _a.G.Valores.LeerAsync(CodigosDeValorNacional.Uvt, Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ValorNacionalVigente(Guid.NewGuid(), Guid.NewGuid(), CodigosDeValorNacional.Uvt, string.Empty, 49_799m, "Pesos", null,
                new DateOnly(2026, 1, 1), null, null, "Resolución DIAN de prueba", 1, IngenIA365ERP.Application.Tests.Lending.Sessions.GobiernoDePrueba.Ahora.AddYears(-1), EstadoDeRevision.Validada)));
        _a.C.Cerrojo.BloquearCuentasAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<ModoDeEspera>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<IReadOnlyList<int>>(ci.ArgAt<IReadOnlyCollection<int>>(0).ToList()));
    }

    public void Dispose() => _a.Dispose();

    private async Task<(MemberAccountDto Origen, MemberAccountDto Destino)> OperarAsync()
    {
        var linea = await _a.C.LineaDeCaptacionAsync("AHVISTA", CuentasDePrueba.ALaVista());
        var origen = await _a.C.AbiertaAsync(_a.C.Apertura(linea, _a.C.Ana));
        var otra = await _a.C.LineaDeCaptacionAsync("AHPROG", CuentasDePrueba.ALaVista());
        var destino = await _a.C.AbiertaAsync(_a.C.Apertura(otra, _a.C.Ana));

        var clave = AportesDePrueba.Clave();
        await _a.ConsignadaAsync(_a.Consignacion(origen, 1_000_000m, ModalidadDeConsignacion.Free, clave: clave, medio: ClaseDeMedioDeRecaudo.Cash));
        await _a.ConsignadaAsync(_a.Consignacion(origen, 200_000m, ModalidadDeConsignacion.Free, medio: ClaseDeMedioDeRecaudo.Cash));
        var retiro = await _a.Contrato().RetirarAsync(new SolicitudDeRetiro(AportesDePrueba.Prueba, AportesDePrueba.Clave(), origen.PublicId, 400_000m, _a.G.HoyLocal,
            ClaseDeMedioDeRecaudo.Cash, [origen.Holder.PersonPublicId], origen.Holder.PersonPublicId,
            new ReferenciaDeRecibo("RC-9", _a.G.HoyLocal, null, null, "Recibo")), default);
        retiro.Result.Should().Be(ResultadoDelContrato.Applied, $"{retiro.Code}: {retiro.Message}");
        _a.Db.ChangeTracker.Clear();
        var traslado = await _a.Contrato().TrasladarAsync(new SolicitudDeTraslado(AportesDePrueba.Prueba, AportesDePrueba.Clave(), origen.PublicId, destino.PublicId,
            300_000m, _a.G.HoyLocal, [origen.Holder.PersonPublicId], new ReferenciaDeRecibo("TR-1", _a.G.HoyLocal, null, null, "Traslado")), default);
        traslado.Result.Should().Be(ResultadoDelContrato.Applied, $"{traslado.Code}: {traslado.Message}");
        _a.Db.ChangeTracker.Clear();
        var reverso = await _a.Contrato().ReversarAsync(new SolicitudDeReversoDeCuenta(AportesDePrueba.Prueba, clave, "Cheque devuelto"), default);
        reverso.Result.Should().Be(ResultadoDelContrato.Applied, $"{reverso.Code}: {reverso.Message}");
        _a.Db.ChangeTracker.Clear();
        return (origen, destino);
    }

    private async Task<IReadOnlyList<IReadOnlyList<HechoDeCartera>>> UnidadesAsync()
    {
        var mensajes = await _a.Db.IntegrationMessages.AsNoTracking()
            .Where(m => m.OriginModule == ModulosDeOrigen.Cartera && m.Kind == IntegrationMessageKind.Business).OrderBy(m => m.Id).ToListAsync();
        return mensajes.Where(m => OperacionesDeCartera.Buscar(m.Type) is not null)
            .Select(m =>
            {
                var contenido = MensajesEntrantes.Deserializar(m);
                return (Clave: (m.OriginPublicId, m.OriginEventKey), Hecho: new HechoDeCartera(MensajesEntrantes.Sobre(m, contenido), contenido, m.OriginDocumentClass));
            })
            .GroupBy(x => x.Clave).Select(g => (IReadOnlyList<HechoDeCartera>)g.Select(x => x.Hecho).ToList()).ToList();
    }

    /// <summary>Lo que las partidas llevan a un rol de naturaleza crédito en una cuenta: el crédito suma, el débito resta.</summary>
    private static decimal Neto(IEnumerable<ConstructorDeLineasDeCartera.Partida> partidas, string rol, string cuenta) =>
        partidas.Where(p => p.Rol == rol && p.Cuenta == cuenta).Sum(p => (p.Importe > 0m == p.DebitoSiPositivo ? -1m : 1m) * Math.Abs(p.Importe));

    private async Task<decimal> LibroAsync(MemberAccountDto cuenta, MemberAccountConcept concepto)
    {
        var id = await _a.Db.MemberAccounts.Where(a => a.PublicId == cuenta.PublicId).Select(a => a.Id).SingleAsync();
        return await _a.Db.MemberAccountTransactions.AsNoTracking().Where(t => t.MemberAccountId == id && t.Concept == concepto && !t.IsDeleted).SumAsync(t => t.Amount);
    }

    [Fact]
    public async Task SC_009_cada_operacion_emite_sus_hechos_sin_cuentas_y_el_constructor_cuadra_peso_a_peso_con_el_libro()
    {
        var (origen, destino) = await OperarAsync();

        var unidades = await UnidadesAsync();
        var tipos = unidades.SelectMany(u => u).Select(h => h.Tipo).ToList();
        tipos.Should().Contain([ConsignacionEnCuentaAplicadaV1.Type, RetiroDeCuentaAplicadoV1.Type, GmfRetenidoV1.Type, TrasladoEntreCuentasAplicadoV1.Type,
            OperacionDeCuentaReversadaV1.Type]);
        var operaciones = await _a.Db.MemberAccountOperations.AsNoTracking().Select(o => o.PublicId).ToListAsync();
        var conHecho = unidades.SelectMany(u => u).Select(h => h.Sobre.Origin.PublicId).ToHashSet();
        operaciones.Should().OnlyContain(o => conHecho.Contains(o), "un hecho por cada cambio de saldo (FR-096)");

        var partidas = new List<ConstructorDeLineasDeCartera.Partida>();
        foreach (var unidad in unidades)
        {
            var d = ConstructorDeLineasDeCartera.Desglosar(unidad);
            d.Fallos.Select(f => $"{f.FactType}: {f.Error.Code} {f.Error.Message}").Should().BeEmpty();
            partidas.AddRange(d.Partidas);
        }

        var reversado = unidades.SelectMany(u => u).Single(h => h.Tipo == OperacionDeCuentaReversadaV1.Type).Contenido.Should().BeOfType<OperacionDeCuentaReversadaV1>().Subject;
        reversado.CollectorClass.Should().Be(nameof(ClaseDeRecaudador.Test), "el reverso nombra la contrapartida de su original (research E106)");

        Neto(partidas, R.CapitalDeCaptacion, origen.Number).Should().Be(await LibroAsync(origen, MemberAccountConcept.Principal),
            "el capital que Contabilidad lleva de la cuenta es el del libro: la conciliación da cero");
        Neto(partidas, R.CapitalDeCaptacion, destino.Number).Should().Be(await LibroAsync(destino, MemberAccountConcept.Principal));
        Neto(partidas, R.GmfPorPagar, origen.Number).Should().Be(await LibroAsync(origen, MemberAccountConcept.Gmf));

        var saldos = await new SaldosContablesDeCaptaciones(_a.Db).MovimientosAsync(_a.G.HoyLocal, null, default);
        saldos.Where(m => m.Clave.Rol == R.CapitalDeCaptacion && m.Clave.Linea == "AHVISTA").Sum(m => m.Monto)
            .Should().Be(await LibroAsync(origen, MemberAccountConcept.Principal));
        saldos.Should().OnlyContain(m => m.Clave.Naturaleza == "Deposit" && m.Clave.ClaseDeCaptacion == "Demand" && m.Clave.Marca == "Standard");
        saldos.Should().NotContain(m => m.Clave.Rol == "GmfPorPagar", "el GMF por pagar se concilia con la declaración, no con el libro de la cuenta");
    }
}
