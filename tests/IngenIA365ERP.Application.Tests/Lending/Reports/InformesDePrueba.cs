using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Reports;
using IngenIA365ERP.Application.Lending.Reports.Vistas;
using IngenIA365ERP.Application.Lending.Risk;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Lending.Payments;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Parameters;

namespace IngenIA365ERP.Application.Tests.Lending.Reports;

/// <summary>
/// El escenario del centro de informes de Cartera (feature 013, US15, K1, T0771, T0772): el de <see cref="PagosDePrueba"/> —InMemory, hoy local
/// 2026-12-01, la línea <c>CONLIB</c> al 20 % EA con usura de consumo 25 %, el crédito de Ana desembolsado hoy por 3.000.000 a 24 cuotas por el
/// registro del libro (fase 3), Cartera activada, el contrato de obligaciones real— con el Anexo 1 sembrado y el límite del deterioro general, para
/// que la calificación informativa use el mismo motor del cierre. Arma los siete handlers de K1 con el alcance de <c>G.AlcanceActual</c>. (nuevo)
/// </summary>
internal sealed class InformesDePrueba : IDisposable
{
    public static readonly DateOnly Hoy = PagosDePrueba.Hoy;

    private InformesDePrueba(PagosDePrueba p) => P = p;

    public static async Task<InformesDePrueba> CrearAsync()
    {
        var p = await PagosDePrueba.CrearAsync();
        await p.C.AnexoAsync(new DateOnly(2026, 1, 1));
        await p.C.SembrarTiposDeHechoAsync();
        p.G.Limite(CatalogoDeLimitesLegales.DeterioroGeneralMinimo, 0.01m, "Circular Básica Contable y Financiera, Anexo 1");
        return new InformesDePrueba(p);
    }

    public PagosDePrueba P { get; }
    public Disbursements.DesembolsoDePrueba D => P.D;
    public Sessions.GobiernoDePrueba G => P.G;
    public TestApplicationDbContext Db => P.Db;
    public Loan Credito => P.Credito;

    public void Dispose() => P.Dispose();

    // ------------------------------------------------------------------------------------------------------------ piezas --

    public CalificacionInformativa Informativa() => new(Db,
        new LectorDelContextoDeRiesgo(new LectorDeDocumentoNormativo(Db), G.Valores, D.Parametros(),
            new LectorDeIndicadoresDeLaEntidad(D.Contabilidad, D.Parametros()), G.Perfil, new PoliticaSarcVigente(Db)),
        new CargadorDeRiesgo(Db, P.C.Cargador()));

    public AgingReportQueryHandler Edades() => new(Db, G.Alcance, D.Parametros(), Informativa(), G.Reloj);
    public PlacementReportQueryHandler Colocacion() => new(Db, G.Alcance, G.Reloj);
    public ReceiptsReportQueryHandler Recaudo() => new(Db, G.Alcance, G.Reloj);
    public TransactionsReportQueryHandler Movimientos() => new(Db, G.Alcance, G.Reloj);
    public UsuryReportQueryHandler Usura() => new(Db, G.Alcance, D.Parametros(), new ReglasDeLineaDeCredito(D.O.L.Normativa(), D.O.L.Valores(), G.Perfil), G.Reloj);
    public DebtorMessagesReportQueryHandler Mensajes() => new(Db, G.Alcance, G.Reloj);
    public MigrationReconciliationReportQueryHandler Migracion() => new(Db, G.Alcance);

    // -------------------------------------------------------------------------------------------------------- escenario --

    /// <summary>Un alcance de sólo una oficina que no es la del crédito de Ana (y todas las líneas).</summary>
    public static AlcanceDeCartera OtraOficina(int oficinaDelCredito) =>
        new(false, new HashSet<int> { oficinaDelCredito + 1000 }, null, true, new HashSet<int>());

    /// <summary>Un alcance de la oficina del crédito y todas las líneas.</summary>
    public static AlcanceDeCartera SoloLaOficina(int oficina) => new(false, new HashSet<int> { oficina }, oficina, true, new HashSet<int>());

    public async Task ParametroAsync(string clave, string valor)
    {
        Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = clave, ScopeKind = ParameterScopeKind.None, Value = valor, ValidFrom = new DateOnly(2026, 1, 1),
            Reason = "prueba", CreatedAt = G.UtcNow.AddDays(-10),
        });
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
    }

    public static TablaExportable Tabla(Result<TablaExportable> r)
    {
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    /// <summary>El índice de una columna por su nombre (visible) o su clave (oculta).</summary>
    public static int Indice(TablaExportable t, string nombre)
    {
        for (var i = 0; i < t.Columnas.Count; i++)
            if (t.Columnas[i].Nombre == nombre || t.Columnas[i].Clave == nombre) return i;
        throw new InvalidOperationException($"La tabla «{t.Titulo}» no tiene la columna {nombre}.");
    }

    public static object? Valor(TablaExportable t, FilaExportable f, string columna) => f.Valores[Indice(t, columna)];
}
