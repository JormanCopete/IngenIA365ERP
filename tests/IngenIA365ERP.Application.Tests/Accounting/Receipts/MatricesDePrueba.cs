using System.Security.Cryptography;
using System.Text;
using IngenIA365ERP.Application.Accounting.RecaudoYTesoreria;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Integration;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Tests.Accounting.Receipts;

/// <summary>
/// Lo que comparten las pruebas de las matrices de Recaudo y Tesorería (feature 015, fase 16): cuentas del plan en memoria, reglas con su peso calculado, la matriz
/// vigente sin base de datos y un mensaje entrante armado desde un contrato de hecho, con el contenido serializado como lo guarda la bandeja. (nuevo)
/// </summary>
public static class MatricesDePrueba
{
    public static readonly DateOnly Hoy = new(2026, 11, 3);

    private static int _id = 100;

    public static ChartOfAccount Cuenta(string codigo, AccountingModules modulos = AccountingModules.Receipts | AccountingModules.Treasury, bool tercero = false,
        bool cruce = false) => new()
    {
        Id = Interlocked.Increment(ref _id), Code = codigo, Name = "Cuenta " + codigo, IsMovement = true, IsActive = true, EnabledModules = modulos,
        RequiresThirdParty = tercero, RequiresCrossDocument = cruce,
    };

    public static T Regla<T>(string rol, ChartOfAccount cuenta, string hecho = "*", DateOnly? desde = null, Action<T>? dimensiones = null, bool deResultado = false)
        where T : ReglaContableDeModulo, new()
    {
        var r = new T { FactType = hecho, Role = rol, AccountId = cuenta.Id, ValidFrom = desde ?? Hoy.AddMonths(-1) };
        dimensiones?.Invoke(r);
        r.Recalcular(deResultado);
        return r;
    }

    public static MatrizVigente Matriz(ModuloDeLaMatriz modulo, IEnumerable<ReglaContableDeModulo> reglas, IEnumerable<ChartOfAccount> cuentas,
        IEnumerable<OwnBankAccountLink>? vinculos = null) =>
        new(modulo, reglas.ToList(), cuentas.ToDictionary(c => c.Id), (vinculos ?? []).ToList());

    /// <summary>Un mensaje entrante de <paramref name="modulo"/> con el contenido <paramref name="contenido"/> (un record de hecho).</summary>
    public static MensajeEntrante Mensaje(string modulo, object contenido, string tipo, Guid? origen = null, Guid? sucursal = null, string numero = "R-1",
        DateOnly? fecha = null, Guid? persona = null, string? scheduleKey = null, DocumentRefV1? relacionado = null)
    {
        var bytes = OpcionesDeMensajes.Serializar(contenido);
        var sobre = new IntegrationEnvelopeV1
        {
            MessageId = Guid.NewGuid(), Type = tipo, Version = 1, Kind = IntegrationMessageKind.Business, OriginModule = modulo, OriginEventKey = ClavesDeEvento.Confirmacion,
            Origin = new MessageOriginV1 { Kind = MessageOriginKind.Document, Number = numero, PublicId = origen ?? Guid.NewGuid(), OperationDate = fecha ?? Hoy },
            BranchPublicId = sucursal ?? Guid.NewGuid(), PersonPublicId = persona, Related = relacionado,
            OriginUser = new UserRefV1 { CentralUserId = Guid.NewGuid(), Name = "cajero" }, Payload = contenido,
        };
        return new MensajeEntrante(sobre, contenido, Encoding.UTF8.GetString(bytes), Convert.ToHexStringLower(SHA256.HashData(bytes)), PrevalidationOutcome.Postable,
            new EntregaEntrante(IntegrationDestinations.Accounting, scheduleKey is null ? DeliveryMode.Online : DeliveryMode.Batch,
                scheduleKey is null ? DeliveryStatus.Pending : DeliveryStatus.InBatch, scheduleKey, null, null, 0, null));
    }
}
