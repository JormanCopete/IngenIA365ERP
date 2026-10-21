using System.Security.Cryptography;
using System.Text;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Lending;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Integration;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Lending;

/// <summary>
/// Escenario de la matriz contable de Cartera (feature 013, T0613): el de <see cref="ContabilidadTestData"/> más cuentas habilitadas para
/// CAR, un doble de <see cref="IDimensionesDeCartera"/> con las líneas <c>LIB01</c> (consumo con libranza), <c>COM01</c> (consumo sin
/// libranza) y la vieja <c>VIE01</c> (cerrada), y ayudantes para sembrar reglas y hechos ya contabilizados. Molde: <c>MatrizDePrueba</c> (012).
/// </summary>
public sealed class MatrizDePruebaDeCartera
{
    public static readonly DateOnly Enero1 = new(2026, 1, 1);

    private const AccountingModules Car = AccountingModules.Accounting | AccountingModules.Lending;

    public ContabilidadTestData D { get; } = new();
    public IDimensionesDeCartera Dimensiones { get; } = Substitute.For<IDimensionesDeCartera>();

    /// <summary>Capital de consumo con libranza en A (exige tercero y cruce).</summary>
    public ChartOfAccount CapitalA { get; }

    /// <summary>Capital de consumo en A, cualquier sub-segmento.</summary>
    public ChartOfAccount CapitalConsumoA { get; }

    /// <summary>Capital de créditos a empleados en A (el CUIF hace que «empleados» gane a la libranza).</summary>
    public ChartOfAccount CapitalEmpleadosA { get; }

    /// <summary>Capital de una línea concreta.</summary>
    public ChartOfAccount CapitalLinea { get; }

    /// <summary>Un deterioro de naturaleza crédito.</summary>
    public ChartOfAccount Deterioro { get; }

    /// <summary>Ingreso por intereses (crédito).</summary>
    public ChartOfAccount Ingreso { get; }

    /// <summary>Ingreso de un hecho concreto (peso 2).</summary>
    public ChartOfAccount IngresoDelAjuste { get; }

    /// <summary>Puente de recaudo (débito, exige tercero).</summary>
    public ChartOfAccount Puente { get; }

    /// <summary>Una cuenta que no está habilitada para Cartera.</summary>
    public ChartOfAccount SinCartera { get; }

    public MatrizDePruebaDeCartera()
    {
        CapitalA = D.Cuenta("14410501", modulos: Car, tercero: true, cruce: true);
        CapitalConsumoA = D.Cuenta("14420501", modulos: Car, tercero: true, cruce: true);
        CapitalEmpleadosA = D.Cuenta("14690501", modulos: Car, tercero: true, cruce: true);
        CapitalLinea = D.Cuenta("14410599", modulos: Car, tercero: true, cruce: true);
        Deterioro = D.Cuenta("14450501", AccountNature.Credit, Car);
        Ingreso = D.Cuenta("41502501", AccountNature.Credit, Car);
        IngresoDelAjuste = D.Cuenta("41502509", AccountNature.Credit, Car);
        Puente = D.Cuenta("11059501", modulos: Car, tercero: true);
        SinCartera = D.Cuenta("51050501");
        D.Db.SaveChanges();

        Dimensiones.CatalogoAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new CatalogoDeDimensionesDeCarteraDto(
            [
                new("LIB01", "Libranza", "CONSUMO", "ConLibranza", true),
                new("COM01", "Comercio", "CONSUMO", "SinLibranza", true),
                new("VIE01", "Línea vieja", "CONSUMO", "SinLibranza", false),
            ],
            [
                new("CONSUMO", "Consumo", ["ConLibranza", "SinLibranza"]),
                new("COMERCIAL", "Comercial", ["PersonaNatural", "PersonaJuridica"]),
            ],
            RolesDeCuentaDeCartera.Conceptos,
            ["Receipts", "Payroll", "Test"],
            OperacionesDeCartera.Todas.Select(o => o.Codigo).ToList())));
        Dimensiones.CombinacionesEnUsoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CombinacionDeCarteraEnUsoDto>>([]));
    }

    public ReglasDeLaMatrizDeCartera Reglas() => new(D.Db, Dimensiones, new ResolutorDeReglasDeCartera(D.Db));

    /// <summary>Una regla sembrada directo (sin las validaciones del alta).</summary>
    public LendingPostingRule Regla(string rol, ChartOfAccount cuenta, string operacion = LendingPostingRule.Cualquiera, DateOnly? desde = null,
        string? linea = null, string? modalidad = null, string? subsegmento = null, string? categoria = null, string? concepto = null,
        bool? empleado = null, bool? gracia = null, bool? porCuenta = null, bool? reestructurado = null, string? clase = null, string? motivo = null,
        int? sucursal = null, int? centro = null, DateOnly? hasta = null)
    {
        var regla = new LendingPostingRule(operacion, rol, cuenta.Id, desde ?? Enero1, linea, modalidad, subsegmento, categoria, concepto, empleado,
            gracia, porCuenta, reestructurado, clase, motivo, sucursal, centro, hasta, "prueba") { CreatedBy = "test" };
        D.Db.LendingPostingRules.Add(regla);
        D.Db.SaveChanges();
        return regla;
    }

    /// <summary>Un hecho de Cartera ya contabilizado (con comprobante), con su contenido JSON, en la bandeja y con su recibo.</summary>
    public IntegrationMessage Contabilizado(string tipo, DateOnly fecha, string payloadJson, string numero = "000123", Guid? sucursal = null)
    {
        var mensaje = new IntegrationMessage
        {
            Type = tipo, Version = 1, Kind = IntegrationMessageKind.Business, OriginModule = "CAR", OriginKind = MessageOriginKind.Operation,
            OriginPublicId = Guid.NewGuid(), OriginDocumentClass = "PaymentApplication", OriginDocumentTypeCode = null, OriginNumber = "OP-" + numero,
            OriginEventKey = "Confirmation", ChainRootPublicId = Guid.NewGuid(), OperationDate = fecha,
            BranchPublicId = sucursal ?? D.Principal.PublicId, Currency = "COP", ExchangeRate = 1m, PayloadJson = payloadJson,
            PayloadSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson))),
            OriginUserCentralId = Guid.NewGuid(), OriginUserName = "Ana", EmittedAt = ContabilidadTestData.Ahora, CreatedBy = "test",
        };
        D.Db.IntegrationMessages.Add(mensaje);
        D.Db.LendingPostings.Add(new LendingPosting
        {
            MessagePublicId = mensaje.PublicId, MessageType = tipo, MessageKind = IntegrationMessageKind.Business, SourceModule = "CAR",
            SourcePublicId = mensaje.OriginPublicId, SourceDocumentClass = "PaymentApplication", SourceDocumentNumber = "OP-" + numero,
            LoanNumber = numero, OperationDate = fecha, AccountingDocumentId = 1, OriginUserName = "Ana", ActorName = "Proceso de integración",
            ProcessedAt = ContabilidadTestData.Ahora, CreatedBy = "test",
        });
        D.Db.SaveChanges();
        return mensaje;
    }

    /// <summary>El contenido mínimo de un pago con sus dimensiones y sus conceptos.</summary>
    public static string Pago(string linea = "LIB01", string modalidad = "CONSUMO", string subsegmento = "ConLibranza", string categoria = "A",
        string clase = "Receipts", params string[] conceptos) =>
        $$"""{"loan":{"loanNumber":"000123"},"dimensions":{"creditLineCode":"{{linea}}","modalityCode":"{{modalidad}}","subsegmentCode":"{{subsegmento}}","riskCategory":"{{categoria}}","collectorClass":"{{clase}}"},"amounts":[{{string.Join(',', (conceptos.Length == 0 ? ["Principal"] : conceptos).Select(c => $$"""{"concept":"{{c}}","amount":100}"""))}}]}""";
}
