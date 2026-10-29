using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Receipts.Paymasters;
using IngenIA365ERP.Application.Tests.Receipts.BankItems;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Entities.Receipts.Paymasters;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Receipts;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Receipts.Paymasters;

/// <summary>
/// El escenario de las pruebas de la fase 12 (feature 015, US7, T0201–T0203) sobre <see cref="BancosDePrueba"/>: una pagaduría (persona jurídica), el tipo <c>RP</c> de
/// recibo de pagaduría, los mensajes entrantes simulados (<see cref="IMensajesEntrantes"/>) y el registro de participantes con las dos versiones de
/// <c>DescuentosPorPagaduria</c> adoptadas. Los archivos se reciben por el consumidor real o se siembran directo con <see cref="Archivo"/>. Las pruebas se escriben ahora y
/// se corren al final del plan.
/// </summary>
internal sealed class PagaduriasDePrueba
{
    public BancosDePrueba B { get; } = new();
    public IMensajesEntrantes Entrantes { get; } = Substitute.For<IMensajesEntrantes>();
    public IRegistroDeIntegracion Registro { get; } = Substitute.For<IRegistroDeIntegracion>();
    public Person Pagaduria { get; }

    private readonly Dictionary<Guid, MensajeEntrante> _mensajes = [];

    public PagaduriasDePrueba(params int[] versionesAdoptadas)
    {
        var e = B.K.C.E;
        Pagaduria = new Person { FirstName = "", LastName = "", BusinessName = "Secretaría de Educación", TaxId = "800100200" };
        e.Db.People.Add(Pagaduria);
        e.Db.ReceiptDocumentTypes.Add(new ReceiptDocumentType { Code = "RP", Name = "Recibo de pagaduría", Kind = ReceiptDocumentKind.PaymasterReceipt });
        e.Db.SaveChanges();
        Entrantes.LeerAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ci =>
            IngenIA365ERP.Application.Common.Models.Result.Success<IReadOnlyList<MensajeEntrante>>(
                ci.ArgAt<IReadOnlyCollection<Guid>>(0).Select(id => _mensajes[id]).ToList()));
        var versiones = versionesAdoptadas.Length == 0 ? new[] { 1, 2 } : versionesAdoptadas;
        Registro.DestinoAsync(IntegrationDestinations.Receipts, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new ParticipanteVigente(Guid.NewGuid(), "REC",
            IntegrationParticipantClass.Receipts, IntegrationParticipantRole.MessageDestination, IntegrationDestinations.Receipts, false, EscenarioDeRecaudo.Hoy.AddYears(-1), null,
            versiones.Select(v => new VersionAdoptada(DescuentosPorPagaduriaV1.Type, v)).ToList()));
    }

    /// <summary>Registra un <c>DescuentosPorPagaduria</c> de Cartera y devuelve el id del mensaje.</summary>
    public Guid Mensaje(object contenido, int version, Guid archivo, string numero = "DES-0001")
    {
        var id = Guid.NewGuid();
        var sobre = new IntegrationEnvelopeV1
        {
            MessageId = id, Type = DescuentosPorPagaduriaV1.Type, Version = version, OriginModule = "CAR", OriginEventKey = "Confirmation",
            Origin = new MessageOriginV1 { PublicId = archivo, Number = numero, OperationDate = EscenarioDeRecaudo.Hoy }, BranchPublicId = B.K.C.E.OficinaA.PublicId,
        };
        _mensajes[id] = new MensajeEntrante(sobre, contenido, "{}", "sha", null,
            new EntregaEntrante(IntegrationDestinations.Receipts, DeliveryMode.Always, DeliveryStatus.Pending, null, null, null, 0, null));
        return id;
    }

    /// <summary>Un archivo v1 de <paramref name="lineas"/> créditos (el A y el B alternados) por <paramref name="valor"/> cada uno.</summary>
    public DescuentosPorPagaduriaV1 V1(int lineas, decimal valor = 100_000m, SupersededDeductionFileV1? reemplaza = null, int version = 1) => new()
    {
        Agreement = new PayrollAgreementRefV1 { PaymasterPersonPublicId = Pagaduria.PublicId, AgreementCode = "SED", AgreementVersion = 1 },
        Period = new PayrollPeriodRefV1 { Year = EscenarioDeRecaudo.Hoy.Year, Month = EscenarioDeRecaudo.Hoy.Month },
        FileVersion = version,
        File = new DeductionFileRefV1 { AttachmentPublicId = Guid.NewGuid(), FileName = "descuentos.txt", Sha256 = "abc123", FormatCode = "SED-IDA" },
        SendBy = EscenarioDeRecaudo.Hoy.AddDays(5),
        Lines = Enumerable.Range(1, lineas).Select(i => new DeductionFileLineV1
        {
            DeductionFileLinePublicId = Guid.NewGuid(),
            Loan = new LoanRefV1 { LoanPublicId = i % 2 == 1 ? B.K.CreditoA : B.K.CreditoB, LoanNumber = i % 2 == 1 ? "CR-A" : "CR-B" },
            DebtorPersonPublicId = B.K.Asociado.PublicId, PayerEmployeeId = "E-1", InstallmentNumber = i, DueDate = EscenarioDeRecaudo.Hoy, NoveltyKind = "Installment",
            Amount = valor,
        }).ToList(),
        Totals = new DeductionFileTotalsV1 { LineCount = lineas, Amount = lineas * valor },
        SupersedesFile = reemplaza,
    };

    /// <summary>Un archivo v2 con un crédito y una cuota de aportes del mismo empleado, con códigos de la pagaduría distintos.</summary>
    public DescuentosPorPagaduriaV2 V2() => new()
    {
        Agreement = new PayrollAgreementRefV1 { PaymasterPersonPublicId = Pagaduria.PublicId, AgreementCode = "SED", AgreementVersion = 2 },
        FileGroup = new DeductionFileGroupRefV2 { Code = "G1", Name = "Grupo 1", Version = 2 },
        Period = new PayrollPeriodRefV1 { Year = EscenarioDeRecaudo.Hoy.Year, Month = EscenarioDeRecaudo.Hoy.Month },
        FileVersion = 1,
        File = new DeductionFileRefV1 { AttachmentPublicId = Guid.NewGuid(), FileName = "g1.txt", Sha256 = "def456", FormatCode = "SED-G1" },
        SendBy = EscenarioDeRecaudo.Hoy.AddDays(5),
        Lines =
        [
            new DeductionFileLineV2
            {
                DeductionFileLinePublicId = Guid.NewGuid(), Kind = "Loan", Loan = new LoanRefV1 { LoanPublicId = B.K.CreditoA, LoanNumber = "CR-A" },
                PersonPublicId = B.K.Asociado.PublicId, PayerEmployeeId = "E-1", PaymasterConceptCode = "120", DueDate = EscenarioDeRecaudo.Hoy, NoveltyKind = "Installment",
                Amount = 100_000m,
            },
            new DeductionFileLineV2
            {
                DeductionFileLinePublicId = Guid.NewGuid(), Kind = "AccountInstallment",
                Account = new IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits.MemberAccountRefV1 { AccountPublicId = B.K.Aportes, AccountNumber = "AP-1" },
                InstallmentPublicId = Guid.NewGuid(), PersonPublicId = B.K.Asociado.PublicId, PayerEmployeeId = "E-1", PaymasterConceptCode = "130",
                DueDate = EscenarioDeRecaudo.Hoy, NoveltyKind = "Installment", Amount = 50_000m,
            },
        ],
        Totals = new DeductionFileTotalsV1 { LineCount = 2, Amount = 150_000m },
    };

    public ConsumePayrollDeductionFileCommandHandler Consumir() => new(B.K.C.E.Db, Entrantes, B.K.C.Cerrojo, B.K.C.E.Alertas, B.K.C.E.Reloj);

    /// <summary>Recibe el archivo por el consumidor y lo devuelve.</summary>
    public async Task<PayrollDeductionFile> RecibirAsync(object contenido, int version, Guid? archivo = null)
    {
        var id = Mensaje(contenido, version, archivo ?? Guid.NewGuid());
        var r = await Consumir().Handle(new ConsumePayrollDeductionFileCommand([id]), default);
        if (r.IsFailure) throw new InvalidOperationException(r.Error.Code);
        return B.K.C.E.Db.ReceiptsPayrollDeductionFiles.Single(f => f.FileMessagePublicId == id);
    }

    public ConfirmPayrollFileSentCommandHandler ConfirmarEnvio() =>
        new(B.K.C.E.Db, B.K.C.E.Alcance, B.K.C.E.Actor, B.K.C.Cerrojo, B.Emision, B.Auditoria, B.K.C.E.Reloj);

    public LoadPaymasterResponseCommandHandler CargarRespuesta() =>
        new(B.K.C.E.Db, B.K.C.E.Alcance, B.K.C.E.Actor, B.K.C.Cerrojo, B.Auditoria, Substitute.For<MediatR.ISender>(), B.K.C.E.Reloj);

    public EmitPaymasterResultCommandHandler Devolver() =>
        new(B.K.C.E.Db, B.K.C.E.Alcance, B.K.C.E.Actor, B.K.C.Cerrojo, Registro, B.Emision, B.Auditoria, B.K.C.E.Reloj);

    public RefreshPaymasterResponseStatusCommandHandler Evaluacion() => new(B.K.C.E.Db, B.K.C.E.Alcance, B.K.C.Cerrojo, B.Auditoria);

    public GirosDePagaduria Giros => new(B.K.C.E.Db, B.K.C.Cerrojo, B.Aplicacion, B.Auditoria, B.K.C.E.Alertas, B.K.C.E.Reloj);

    public RegisterPaymasterRemittanceCommandHandler RegistrarGiro() =>
        new(B.K.C.E.Db, B.K.C.E.Alcance, B.K.C.E.Actor, B.Movimientos, B.K.C.Cerrojo, B.K.C.Numerador, B.Emision, B.Auditoria, B.K.C.E.Reloj);

    public ApplyPaymasterRemittanceCommandHandler AplicarGiro() => new(B.K.C.E.Db, B.K.C.E.Alcance, B.K.C.E.Actor, Giros);

    /// <summary>Un archivo ya respondido y aceptado: cada línea descontada entera con la fecha <paramref name="fecha"/>.</summary>
    public async Task<PayrollDeductionFile> RespondidoAsync(int lineas, decimal valor = 100_000m, DateOnly? fecha = null)
    {
        var f = await RecibirAsync(V1(lineas, valor), 1);
        foreach (var l in B.K.C.E.Db.ReceiptsPayrollDeductionFileLines.Where(x => x.PayrollDeductionFileId == f.Id))
        {
            l.DeductionOutcome = PaymasterDeductionOutcome.Deducted;
            l.DeductedAmount = l.Amount;
            l.DeductionDate = fecha ?? EscenarioDeRecaudo.Hoy.AddDays(-3);
        }
        f.Status = PayrollDeductionFileState.Responded;
        await B.K.C.E.Db.SaveChangesAsync();
        return f;
    }
}
