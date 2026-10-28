using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Checks;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Setup;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Receipts.Activation;
using IngenIA365ERP.Domain.Entities.Receipts.Banks;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Entities.Receipts.Vaults;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;
using P = IngenIA365ERP.Application.Receipts.Imports.PlantillasDePuestaEnMarchaDeRecaudo;

namespace IngenIA365ERP.Application.Receipts.Imports;

/// <summary>
/// <c>POST /api/receipts/imports/{code}/import?mode=review|apply</c> (<c>Receipts.Imports.Run</c>, con clave; feature 015, US13, T0287; FR-157): las seis plantillas de
/// puesta en marcha de Recaudo y la de cifras de referencia sobre <see cref="EjecutorDeImportacion"/> —todo o nada, revisión previa, error por fila y columna,
/// evento resumen—. Un solo comando con el código de la plantilla (la tarea pedía uno por plantilla; el ejecutor y la ruta son los mismos). Escriben en las tablas
/// reales con la marca de la puesta en marcha (<c>IsOpeningLoad</c>, <c>OriginKind = OpeningLoad</c>, movimiento de bóveda <c>OpeningBalance</c>) y <b>ningún
/// hecho contable</b> (FR-158): no llaman a <c>EmisionDeRecaudo</c>. (nuevo)
/// </summary>
public sealed record ImportReceiptsGoLiveTemplateCommand(string Code, ModoDeImportacion? Mode, ArchivoDeImportacion File, string? Reason = null)
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportReceiptsGoLiveTemplateCommandValidator : AbstractValidator<ImportReceiptsGoLiveTemplateCommand>
{
    public ImportReceiptsGoLiveTemplateCommandValidator()
    {
        RuleFor(x => x.Code).Must(c => P.Por(c) is not null).WithMessage("No hay una plantilla de puesta en marcha de Recaudo con ese código.");
        RuleFor(x => x.File).NotNull();
    }
}

public sealed class ImportReceiptsGoLiveTemplateCommandHandler(
    IApplicationDbContext db,
    EjecutorDeImportacion ejecutor,
    ParametrosDeCaja parametros,
    ICuentasBancariasPropias cuentasPropias,
    IActorActual actorActual,
    IDateTimeService reloj,
    IngenIA365ERP.Application.Common.Parameters.IReglasDeParametros? reglas = null) : IRequestHandler<ImportReceiptsGoLiveTemplateCommand, Result<ImportResultDto>>
{
    public const string CodigoSinSegundoFactor = "Receipts.Imports.UserWithoutSecondFactor";
    public const string CodigoBovedaYaCargada = "Receipts.Imports.VaultAlreadyLoaded";
    public const string CodigoSaldoNoCuadra = "Receipts.Imports.VaultTotalMismatch";

    public Task<Result<ImportResultDto>> Handle(ImportReceiptsGoLiveTemplateCommand request, CancellationToken ct)
    {
        var plantilla = P.Por(request.Code)!;
        return ejecutor.EjecutarAsync(plantilla, request, (ctx, c) => plantilla.Clave switch
        {
            P.PuntosYCajas => PuntosYCajasAsync(ctx, c),
            P.CajerosYAlcances => CajerosAsync(ctx, c),
            P.MediosPorPunto => MediosAsync(ctx, c),
            P.BovedasAlCorte => BovedasAsync(ctx, c),
            P.PartidasAlCorte => PartidasAsync(ctx, c),
            P.ChequesEnCanje => ChequesAsync(ctx, c),
            P.Convenios => ConveniosAsync(ctx, c),
            // Segunda ronda del 2026-10-27 (D-l): los parámetros RCD por plantilla.
            P.Parametros => IngenIA365ERP.Application.Common.Parameters.PlantillaDeParametrosDeModulo.AplicarAsync(ctx, db, reglas,
                IngenIA365ERP.Domain.Receipts.Parameters.ParametrosDeRecaudoYTesoreria.ModuloRecaudo, c),
            _ => CifrasAsync(ctx, request.File.NombreArchivo, c),
        }, ct, despuesDeGuardar: async (_, c) =>
        {
            foreach (var paso in _despues) paso();
            await Task.CompletedTask;
        });
    }

    /// <summary>Lo que necesita los Id del primer guardado (las cajas de un punto nuevo, las denominaciones del movimiento de bóveda): sólo corre al aplicar.</summary>
    private readonly List<Action> _despues = [];

    private async Task<CatalogoCitado<Domain.Entities.Core.Branch>> OficinasAsync(CancellationToken ct) =>
        CatalogoCitado<Domain.Entities.Core.Branch>.Desde(await db.Branches.AsNoTracking().ToListAsync(ct), b => b, b => b.LegacyCode, b => b.Name);

    private static string Nuevo(string prefijo) => $"{prefijo}-{Guid.NewGuid():N}"[..Math.Min(30, prefijo.Length + 17)];

    // ------------------------------------------------------------------------------------------------------------------ puntos y cajas --
    private async Task PuntosYCajasAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var oficinas = await OficinasAsync(ct);
        var puntos = await db.CollectionPoints.ToListAsync(ct);
        var cajas = await db.CashBoxes.AsNoTracking().Select(c => c.Code).ToListAsync(ct);
        var modoPorDefecto = Enum.TryParse<CashBoxBaseMode>(await parametros.TextoAsync(ParametrosDeRecaudoYTesoreria.CajaBaseModo, hoy, ct), out var m) ? m : CashBoxBaseMode.DailyBase;
        var nuevos = new Dictionary<string, CollectionPoint>(StringComparer.OrdinalIgnoreCase);
        foreach (var fila in ctx.Datos.Filas)
        {
            var oficina = fila.Referencia(P.Oficina, oficinas, "una oficina", "en Maestros → Agencias");
            var codigoPunto = fila.Codigo(P.Punto, 10);
            var nombrePunto = fila.Texto(P.NombrePunto, 150);
            var codigoCaja = fila.Codigo(P.Caja, 10);
            var nombreCaja = fila.Texto(P.NombreCaja, 150);
            var base_ = fila.Monto(P.Base) ?? 0m;
            var modo = fila.EstaVacia(P.Modo) ? modoPorDefecto : fila.Enumeracion<CashBoxBaseMode>(P.Modo);
            var aviso = fila.Monto(P.TopeDeAviso);
            var bloqueo = fila.Monto(P.TopeDeBloqueo);
            var principal = fila.SiNo(P.CajaPrincipal);
            var desde = fila.Fecha(P.VigenteDesde) ?? hoy;
            if (base_ < 0m) fila.Error(P.Base, ImportErrors.CellFormat, "La base no puede ser negativa.");
            if (aviso is { } a && bloqueo is { } b && b < a) fila.Error(P.TopeDeBloqueo, ImportErrors.CellFormat, "El tope de bloqueo no puede ser menor que el de aviso.");
            if (codigoCaja is not null && ctx.Datos.LlaveUnica(fila, codigoCaja, P.Caja) && cajas.Contains(codigoCaja, StringComparer.OrdinalIgnoreCase))
                fila.Error(P.Caja, ImportErrors.RowDuplicate, $"Ya existe una caja con el código «{codigoCaja}».");
            if (fila.TieneErrores || oficina is null || codigoPunto is null || nombrePunto is null || codigoCaja is null || nombreCaja is null || modo is null) continue;

            var accion = AccionDeImportacion.Create;
            if (!nuevos.TryGetValue(codigoPunto, out var punto))
            {
                punto = puntos.FirstOrDefault(p => string.Equals(p.Code, codigoPunto, StringComparison.OrdinalIgnoreCase));
                if (punto is null)
                {
                    punto = new CollectionPoint { BranchId = oficina.Id, Code = codigoPunto.ToUpperInvariant(), Name = nombrePunto, ValidFrom = desde };
                    db.CollectionPoints.Add(punto);
                }
                else accion = AccionDeImportacion.Update;
                nuevos[codigoPunto] = punto;
            }
            if (punto.BranchId != oficina.Id)
            {
                fila.Error(P.Oficina, ImportErrors.CellFormat, $"El punto «{codigoPunto}» es de otra oficina: la oficina de un punto no cambia.");
                continue;
            }
            var caja = new CashBox
            {
                Code = codigoCaja.ToUpperInvariant(), Name = nombreCaja, BaseMode = modo.Value, BaseAmount = base_, CashWarnCap = aviso, CashBlockCap = bloqueo,
                IsMainCashBox = principal, ValidFrom = desde,
            };
            var delPunto = punto;
            if (delPunto.Id > 0)
            {
                caja.CollectionPointId = delPunto.Id;
                db.CashBoxes.Add(caja);
            }
            else
                _despues.Add(() =>
                {
                    caja.CollectionPointId = delPunto.Id;
                    db.CashBoxes.Add(caja);
                });
            ctx.Registrar(fila, $"{codigoPunto}|{codigoCaja}", accion);
        }
    }

    // ---------------------------------------------------------------------------------------------------------------- cajeros y alcances --
    private enum RolDeRecaudo { Cajero = 1, Supervisor = 2, Analista = 3 }

    private async Task CajerosAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var documentos = ctx.Datos.Filas.Select(f => f.Crudo(P.Documento)).OfType<string>().Distinct().ToList();
        var usuarios = await (from u in db.Users.AsNoTracking()
                              join p in db.People.AsNoTracking() on u.PersonId equals p.Id
                              where u.IsActive && documentos.Contains(p.TaxId)
                              orderby u.Id // con dos usuarios de la misma persona, el más antiguo, siempre el mismo (el catálogo citado se queda con el primero)
                              select new { u.Id, p.TaxId, u.IsMfaEnabled }).ToListAsync(ct);
        var porDocumento = CatalogoCitado<(int Id, bool Mfa)>.Desde(usuarios, u => (u.Id, u.IsMfaEnabled), u => u.TaxId);
        var cajas = CatalogoCitado<CashBox>.Desde(await db.CashBoxes.AsNoTracking().ToListAsync(ct), c => c, c => c.Code);
        var puntos = CatalogoCitado<CollectionPoint>.Desde(await db.CollectionPoints.AsNoTracking().ToListAsync(ct), p => p, p => p.Code);
        var asignaciones = await db.CashBoxCashiers.AsNoTracking().ToListAsync(ct);
        var alcancesCaja = await db.UserCashBoxScopes.AsNoTracking().Select(s => new { s.UserId, s.CashBoxId }).ToListAsync(ct);
        var alcancesPunto = await db.UserCollectionPointScopes.AsNoTracking().Select(s => new { s.UserId, s.CollectionPointId }).ToListAsync(ct);
        foreach (var fila in ctx.Datos.Filas)
        {
            var usuario = fila.Referencia(P.Documento, porDocumento, "un usuario con la persona de documento", "en Administración → Usuarios (el usuario tiene que tener su persona)");
            var rol = fila.Enumeracion<RolDeRecaudo>(P.Rol);
            var caja = fila.Referencia(P.Caja, cajas, "una caja", "con la plantilla recaudo.puntos-y-cajas o en Recaudo → Puntos y cajas");
            var punto = fila.Referencia(P.Punto, puntos, "un punto de recaudo", "con la plantilla recaudo.puntos-y-cajas o en Recaudo → Puntos y cajas");
            var desde = fila.Fecha(P.VigenteDesde) ?? hoy;
            if (rol == RolDeRecaudo.Cajero && fila.EstaVacia(P.Caja)) fila.Error(P.Caja, ImportErrors.CellRequired, "El cajero exige su caja.");
            if (rol is RolDeRecaudo.Supervisor or RolDeRecaudo.Analista && fila.EstaVacia(P.Punto)) fila.Error(P.Punto, ImportErrors.CellRequired, "El supervisor y el analista exigen su punto.");
            if (caja is not null && punto is not null && caja.CollectionPointId != punto.Id) fila.Error(P.Punto, ImportErrors.CellFormat, "La caja no es de ese punto.");
            if (fila.TieneErrores || rol is null || usuario == default) continue;
            if (!usuario.Mfa)
                fila.Aviso(P.Documento, CodigoSinSegundoFactor,
                    "El usuario no tiene segundo factor inscrito: es condición de la lista de go/no-go para cajeros y supervisores (FR-176).");
            var nada = true;
            if (caja is not null)
            {
                if (!asignaciones.Any(a => a.CashBoxId == caja.Id && a.UserId == usuario.Id && a.VigenteEn(desde)) && rol == RolDeRecaudo.Cajero)
                {
                    db.CashBoxCashiers.Add(new CashBoxCashier { CashBoxId = caja.Id, UserId = usuario.Id, ValidFrom = desde });
                    nada = false;
                }
                if (!alcancesCaja.Any(a => a.UserId == usuario.Id && a.CashBoxId == caja.Id))
                {
                    db.UserCashBoxScopes.Add(new UserCashBoxScope { UserId = usuario.Id, CashBoxId = caja.Id });
                    nada = false;
                }
            }
            var puntoDelAlcance = punto?.Id ?? caja?.CollectionPointId;
            if (puntoDelAlcance is { } pid && !alcancesPunto.Any(a => a.UserId == usuario.Id && a.CollectionPointId == pid))
            {
                db.UserCollectionPointScopes.Add(new UserCollectionPointScope { UserId = usuario.Id, CollectionPointId = pid });
                nada = false;
            }
            ctx.Registrar(fila, $"{fila.Crudo(P.Documento)}|{rol}|{fila.Crudo(P.Caja) ?? fila.Crudo(P.Punto)}", nada ? AccionDeImportacion.Unchanged : AccionDeImportacion.Create);
        }
    }

    // ------------------------------------------------------------------------------------------------------------------ medios por punto --
    private async Task MediosAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var puntos = CatalogoCitado<CollectionPoint>.Desde(await db.CollectionPoints.AsNoTracking().ToListAsync(ct), p => p, p => p.Code);
        var medios = CatalogoCitado<Domain.Entities.Core.Payments.PaymentMeans>.Desde(await db.PaymentMeans.AsNoTracking().Where(m => m.IsActive).ToListAsync(ct), m => m, m => m.Code);
        var ofrecidos = await db.PaymentMeansCollectionPoints.AsNoTracking().ToListAsync(ct);
        var cheque = await parametros.SiNoAsync(ParametrosDeRecaudoYTesoreria.CajaAdmiteCheque, hoy, ct);
        foreach (var fila in ctx.Datos.Filas)
        {
            var punto = fila.Referencia(P.Punto, puntos, "un punto de recaudo", "con la plantilla recaudo.puntos-y-cajas");
            var medio = fila.Referencia(P.Medio, medios, "un medio de pago activo", "en Maestros → Medios de pago");
            var desde = fila.Fecha(P.VigenteDesde) ?? hoy;
            if (medio is not null && !MediosDeRecaudo.DeR0.Contains(medio.Class))
                fila.TodaviaNoDisponible(P.Medio, "R1");
            else if (medio is not null && medio.Class == Domain.Enums.Core.PaymentMeansClass.Check && !cheque)
                fila.Error(P.Medio, ImportErrors.CellFormat, "La caja no recibe cheques (Caja.AdmiteCheque está apagado).");
            if (punto is not null && medio is not null) ctx.Datos.LlaveUnica(fila, $"{punto.Code}|{medio.Code}", P.Medio);
            if (fila.TieneErrores || punto is null || medio is null) continue;
            if (ofrecidos.Any(o => o.CollectionPointId == punto.Id && o.PaymentMeansId == medio.Id && o.VigenteEn(desde)))
            {
                ctx.Registrar(fila, $"{punto.Code}|{medio.Code}", AccionDeImportacion.Unchanged);
                continue;
            }
            db.PaymentMeansCollectionPoints.Add(new PaymentMeansCollectionPoint { CollectionPointId = punto.Id, PaymentMeansId = medio.Id, ValidFrom = desde });
            ctx.Registrar(fila, $"{punto.Code}|{medio.Code}", AccionDeImportacion.Create);
        }
    }

    // ------------------------------------------------------------------------------------------------------------------ bóvedas al corte --
    private async Task BovedasAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        var oficinas = await OficinasAsync(ct);
        var bovedas = await db.Vaults.AsNoTracking().ToListAsync(ct);
        var cargadas = await db.VaultMovements.AsNoTracking().Where(v => v.Kind == VaultMovementKind.OpeningBalance).Select(v => v.VaultId).ToListAsync(ct);
        var denominaciones = await db.CashDenominations.AsNoTracking().Where(d => d.IsActive).ToListAsync(ct);
        var leidas = new List<(FilaDeImportacion Fila, Vault Boveda, DateOnly Corte, Domain.Entities.Core.Payments.CashDenomination Den, int Cantidad, decimal Declarado)>();
        foreach (var fila in ctx.Datos.Filas)
        {
            var oficina = fila.Referencia(P.Oficina, oficinas, "una oficina", "en Maestros → Agencias");
            var corte = fila.Fecha(P.FechaDeCorte);
            var valor = fila.Monto(P.Denominacion);
            var cantidad = fila.Entero(P.Cantidad);
            var declarado = fila.Monto(P.SaldoDeclarado);
            var boveda = oficina is null ? null : bovedas.FirstOrDefault(v => v.BranchId == oficina.Id);
            if (oficina is not null && boveda is null)
                fila.Error(P.Oficina, ImportErrors.CellNotFound, "La oficina no tiene bóveda configurada: créela en Recaudo → Bóvedas.");
            if (boveda is not null && cargadas.Contains(boveda.Id))
                fila.Error(P.Oficina, CodigoBovedaYaCargada, "La bóveda de esta oficina ya tiene su saldo de puesta en marcha.");
            var den = valor is null ? null : denominaciones.FirstOrDefault(d => d.Value == valor && (corte is null || (d.ValidFrom <= corte && (d.ValidTo == null || d.ValidTo >= corte))));
            if (valor is not null && den is null) fila.Error(P.Denominacion, ImportErrors.CellNotFound, $"No hay una denominación vigente de {valor:N0}.");
            if (cantidad is <= 0) fila.Error(P.Cantidad, ImportErrors.CellFormat, "La cantidad tiene que ser mayor que cero.");
            if (oficina is not null && valor is not null) ctx.Datos.LlaveUnica(fila, $"{oficina.Id}|{valor}", P.Denominacion);
            if (fila.TieneErrores || boveda is null || corte is null || den is null || cantidad is null || declarado is null) continue;
            leidas.Add((fila, boveda, corte.Value, den, cantidad.Value, declarado.Value));
        }
        foreach (var g in leidas.GroupBy(l => l.Boveda.Id))
        {
            var filas = g.ToList();
            if (filas.Select(f => f.Corte).Distinct().Count() > 1 || filas.Select(f => f.Declarado).Distinct().Count() > 1)
            {
                foreach (var f in filas) f.Fila.Error(P.FechaDeCorte, ImportErrors.CellFormat, "La fecha de corte y el saldo declarado son los mismos en todas las filas de la oficina.");
                continue;
            }
            var total = filas.Sum(f => f.Den.Value * f.Cantidad);
            if (total != filas[0].Declarado)
            {
                foreach (var f in filas)
                    f.Fila.Error(P.SaldoDeclarado, CodigoSaldoNoCuadra,
                        string.Create(CultureInfo.InvariantCulture, $"Las denominaciones suman {total:N0} y el saldo declarado es {filas[0].Declarado:N0}."));
                continue;
            }
            if (actor.UserId is not { } usuario) continue;
            var movimiento = new VaultMovement
            {
                Number = Nuevo("PM"), VaultId = g.Key, Kind = VaultMovementKind.OpeningBalance, Amount = total, CustodianUserId = usuario,
                Reason = $"Saldo de la bóveda al corte del {filas[0].Corte:dd/MM/yyyy} (puesta en marcha)", MovedAt = filas[0].Corte.ToDateTime(new TimeOnly(23, 59)),
            };
            db.VaultMovements.Add(movimiento);
            var saldos = await db.VaultDenominationBalances.Where(s => s.VaultId == g.Key).ToListAsync(ct);
            foreach (var f in filas)
            {
                var monto = f.Den.Value * f.Cantidad;
                var detalle = new VaultMovementDenomination { CashDenominationId = f.Den.Id, Quantity = f.Cantidad, Amount = monto };
                _despues.Add(() =>
                {
                    detalle.VaultMovementId = movimiento.Id;
                    db.VaultMovementDenominations.Add(detalle);
                });
                var saldo = saldos.FirstOrDefault(s => s.CashDenominationId == f.Den.Id);
                if (saldo is null) db.VaultDenominationBalances.Add(new VaultDenominationBalance { VaultId = g.Key, CashDenominationId = f.Den.Id, Quantity = f.Cantidad, Amount = monto });
                else
                {
                    saldo.Quantity += f.Cantidad;
                    saldo.Amount += monto;
                }
                ctx.Registrar(f.Fila, $"{g.Key}|{f.Den.Value}", AccionDeImportacion.Create);
            }
        }
    }

    // ------------------------------------------------------------------------------------------------------------------ convenios (R2) --
    private async Task ConveniosAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var cuentas = CatalogoCitado<CuentaBancariaPropiaDto>.Desde(await cuentasPropias.DeRecaudoAsync(null, ct), c => c, c => c.Code);
        var formatos = CatalogoCitado<BankFileFormat>.Desde(await db.BankFileFormats.AsNoTracking()
            .Where(f => f.IsActive && (f.Scope == Domain.Enums.Core.BankFileScope.CollectionAgreementFile || f.Scope == Domain.Enums.Core.BankFileScope.PaymentGatewayFile))
            .ToListAsync(ct), f => f, f => f.Code);
        var documentos = ctx.Datos.Filas.Select(f => f.Crudo(P.Aliado)).OfType<string>().Distinct().ToList();
        var personas = CatalogoCitado<int>.Desde(await db.People.AsNoTracking().Where(p => documentos.Contains(p.TaxId)).Select(p => new { p.Id, p.TaxId }).ToListAsync(ct),
            p => p.Id, p => p.TaxId);
        var oficinas = await db.Branches.AsNoTracking().Select(b => new { b.Id, b.PublicId }).ToListAsync(ct);
        var existentes = await db.CollectionAgreements.AsNoTracking().Select(c => c.Code).ToListAsync(ct);
        var digitoPorDefecto = Enum.TryParse<CheckDigitAlgorithm>(await parametros.TextoAsync(ParametrosDeRecaudoYTesoreria.ReferenciasDigitoDeVerificacion, hoy, ct), out var d)
            ? d
            : CheckDigitAlgorithm.Modulo10;
        foreach (var fila in ctx.Datos.Filas)
        {
            var codigo = fila.Codigo(P.Convenio, 10);
            var nombre = fila.Texto(P.NombreConvenio, 150);
            var clase = fila.Enumeracion<CollectionAgreementKind>(P.ClaseConvenio);
            var cuenta = fila.Referencia(P.CuentaPropia, cuentas, "una cuenta bancaria propia de recaudo", "en Tesorería → Cuentas bancarias");
            var formato = fila.Referencia(P.Formato, formatos, "un formato de archivo de convenio o de pasarela", "en Maestros → Formatos bancarios");
            var modo = fila.EstaVacia(P.ModoDeReferencia) ? PaymentReferenceMode.ByProduct : fila.Enumeracion<PaymentReferenceMode>(P.ModoDeReferencia);
            var longitud = fila.Entero(P.LongitudDeReferencia) ?? 0;
            var digito = fila.EstaVacia(P.Digito) ? digitoPorDefecto : fila.Enumeracion<CheckDigitAlgorithm>(P.Digito);
            int? aliado = fila.EstaVacia(P.Aliado) ? null : fila.Referencia(P.Aliado, personas, "una persona con documento", "en Personas");
            var porPago = fila.Monto(P.ComisionPorPago);
            var porcentaje = fila.Porcentaje(P.ComisionPorcentaje);
            var desde = fila.Fecha(P.VigenteDesde) ?? hoy;
            if (longitud is < 0 or > 40) fila.Error(P.LongitudDeReferencia, ImportErrors.CellFormat, "La longitud va de 0 a 40.");
            if (codigo is not null && ctx.Datos.LlaveUnica(fila, codigo, P.Convenio) && existentes.Contains(codigo, StringComparer.OrdinalIgnoreCase))
                fila.Error(P.Convenio, ImportErrors.RowDuplicate, $"Ya existe un convenio con el código «{codigo}».");
            if (formato is not null && clase is { } k && formato.Scope != Agreements.ConveniosDeRecaudo.AmbitoDe(k))
                fila.Error(P.Formato, ImportErrors.CellFormat, "El formato no es del ámbito de la clase del convenio (archivo de convenio o de pasarela).");
            var oficina = cuenta?.BranchPublicId is { } bp ? oficinas.FirstOrDefault(o => o.PublicId == bp)?.Id : null;
            if (cuenta is not null && oficina is null) fila.Error(P.CuentaPropia, ImportErrors.CellFormat, "La cuenta recaudadora no tiene oficina.");
            if (fila.TieneErrores || codigo is null || nombre is null || clase is null || cuenta is null || formato is null || modo is null || digito is null || oficina is null)
                continue;
            var convenio = new IngenIA365ERP.Domain.Entities.Receipts.Agreements.CollectionAgreement
            {
                Code = codigo.ToUpperInvariant(), Name = nombre, Kind = clase.Value, OwnBankAccountPublicId = cuenta.PublicId, BranchId = oficina.Value, PartnerPersonId = aliado,
                PartnerAgreementCode = fila.Texto(P.CodigoAnteElAliado, 40),
            };
            db.CollectionAgreements.Add(convenio);
            var version = new IngenIA365ERP.Domain.Entities.Receipts.Agreements.CollectionAgreementVersion
            {
                ValidFrom = desde, ReferenceMode = modo.Value, ReferenceLength = longitud, CheckDigit = digito.Value, BankFileFormatId = formato.Id,
                CommissionPerPayment = porPago, CommissionRate = porcentaje,
            };
            _despues.Add(() =>
            {
                version.CollectionAgreementId = convenio.Id;
                db.CollectionAgreementVersions.Add(version);
            });
            ctx.Registrar(fila, codigo, AccionDeImportacion.Create);
        }
    }

    // ------------------------------------------------------------------------------------------------------------------ partidas al corte --
    private async Task PartidasAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var oficinas = await OficinasAsync(ct);
        var cuentas = CatalogoCitado<CuentaBancariaPropiaDto>.Desde(await cuentasPropias.DeRecaudoAsync(null, ct), c => c, c => c.Code);
        var hoy = reloj.HoyLocal;
        foreach (var fila in ctx.Datos.Filas)
        {
            var cuenta = fila.Referencia(P.CuentaPropia, cuentas, "una cuenta bancaria propia de recaudo", "en Tesorería → Cuentas bancarias");
            var oficina = fila.Referencia(P.Oficina, oficinas, "una oficina", "en Maestros → Agencias");
            var fecha = fila.Fecha(P.Fecha);
            var valor = fila.Monto(P.Valor);
            var referencia = fila.Texto(P.Referencia, 100);
            var consignante = fila.Texto(P.Consignante, 200);
            var documento = fila.Texto(P.DocumentoConsignante, 20);
            if (valor is <= 0m) fila.Error(P.Valor, ImportErrors.CellFormat, "El valor tiene que ser mayor que cero.");
            if (fecha is { } f && f > hoy) fila.Error(P.Fecha, ImportErrors.CellFormat, "La fecha del abono no puede ser futura.");
            if (fila.TieneErrores || cuenta is null || oficina is null || fecha is null || valor is null) continue;
            db.UnidentifiedItems.Add(new UnidentifiedItem
            {
                Number = Nuevo("PI-PM"), OwnBankAccountPublicId = cuenta.PublicId, BranchId = oficina.Id, MovementDate = fecha.Value, Amount = valor.Value, Balance = valor.Value,
                Reference = referencia, DepositorName = consignante, DepositorTaxId = documento, Status = UnidentifiedItemState.Open, OriginKind = UnidentifiedItemOrigin.OpeningLoad,
                OpenedOn = fecha.Value,
            });
            ctx.Registrar(fila, $"{cuenta.Code}|{fecha:yyyy-MM-dd}|{valor}|{referencia}", AccionDeImportacion.Create);
        }
    }

    // ------------------------------------------------------------------------------------------------------------------ cheques en canje --
    private static readonly IReadOnlySet<ReceivedCheckState> EstadosDeCanje = new HashSet<ReceivedCheckState>
        { ReceivedCheckState.InCustody, ReceivedCheckState.Deposited, ReceivedCheckState.InClearing };

    private async Task ChequesAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var oficinas = await OficinasAsync(ct);
        var bancos = CatalogoCitado<Bank>.Desde(await db.Banks.AsNoTracking().Where(b => !b.IsDeleted).ToListAsync(ct), b => b, b => b.Name, b => b.LegacyCode, b => b.TransferCode);
        var documentos = ctx.Datos.Filas.Select(f => f.Crudo(P.Pagador)).OfType<string>().Distinct().ToList();
        var personas = CatalogoCitado<int>.Desde(await db.People.AsNoTracking().Where(p => documentos.Contains(p.TaxId)).Select(p => new { p.Id, p.TaxId }).ToListAsync(ct),
            p => p.Id, p => p.TaxId);
        var existentes = await db.ReceivedChecks.AsNoTracking().Select(c => new { c.DraweeBankId, c.AccountNumber, c.CheckNumber }).ToListAsync(ct);
        foreach (var fila in ctx.Datos.Filas)
        {
            var banco = fila.Referencia(P.Banco, bancos, "un banco", "en Maestros → Bancos");
            var numero = fila.Texto(P.Numero, 20);
            var cuenta = fila.Texto(P.Cuenta, 30);
            var girador = fila.Texto(P.Girador, 200);
            var pagador = fila.Referencia(P.Pagador, personas, "una persona con documento", "en Personas");
            var valor = fila.Monto(P.Valor);
            var fecha = fila.Fecha(P.Fecha);
            var estado = fila.Enumeracion<ReceivedCheckState>(P.Estado);
            var oficina = fila.Referencia(P.Oficina, oficinas, "una oficina", "en Maestros → Agencias");
            var mismaPlaza = fila.SiNo(P.MismaPlaza, porDefecto: true);
            if (estado is { } e && !EstadosDeCanje.Contains(e)) fila.Error(P.Estado, ImportErrors.CellFormat, "El cheque en canje está InCustody, Deposited o InClearing.");
            if (valor is <= 0m) fila.Error(P.Valor, ImportErrors.CellFormat, "El valor tiene que ser mayor que cero.");
            if (banco is not null && numero is not null && cuenta is not null)
            {
                ctx.Datos.LlaveUnica(fila, $"{banco.Id}|{cuenta}|{numero}", P.Numero);
                if (existentes.Any(x => x.DraweeBankId == banco.Id && x.AccountNumber == cuenta && x.CheckNumber == numero))
                    fila.Error(P.Numero, ImportErrors.RowDuplicate, "Ese cheque ya está registrado.");
            }
            if (fila.TieneErrores || banco is null || numero is null || cuenta is null || pagador == 0 || valor is null || fecha is null || estado is null || oficina is null)
                continue;
            db.ReceivedChecks.Add(new ReceivedCheck
            {
                DraweeBankId = banco.Id, CheckNumber = numero, AccountNumber = cuenta, DrawerName = girador, PayerPersonId = pagador, IssueDate = fecha.Value,
                Amount = valor.Value, IsThirdParty = false, SamePlace = mismaPlaza, PresentationDeadline = await ChequesRecibidos.PlazoAsync(parametros, fecha.Value, mismaPlaza, hoy, ct),
                BranchId = oficina.Id, Status = estado.Value, IsOpeningLoad = true,
            });
            ctx.Registrar(fila, $"{banco.Name}|{cuenta}|{numero}", AccionDeImportacion.Create);
        }
    }

    // ------------------------------------------------------------------------------------------------------------- cifras de referencia --
    private async Task CifrasAsync(ContextoDeImportacion ctx, string archivo, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        var lote = Guid.NewGuid();
        var leidas = new List<(FilaDeImportacion Fila, DateOnly Corte, string Concepto, string Dimension, decimal Valor)>();
        foreach (var fila in ctx.Datos.Filas)
        {
            var corte = fila.Fecha(P.FechaDeCorte);
            var concepto = fila.Codigo(P.Concepto, 40);
            var dimension = fila.Codigo(P.Dimension, 40);
            var valor = fila.Monto(P.Valor);
            if (corte is not null && concepto is not null && dimension is not null) ctx.Datos.LlaveUnica(fila, $"{corte:yyyy-MM-dd}|{concepto}|{dimension}", P.Dimension);
            if (fila.TieneErrores || corte is null || concepto is null || dimension is null || valor is null) continue;
            leidas.Add((fila, corte.Value, concepto, dimension, valor.Value));
        }
        if (ctx.HayErrores || actor.UserId is not { } usuario) return;
        var cortes = leidas.Select(l => l.Corte).Distinct().ToList();
        var anteriores = await db.ReceiptsReferenceFigures.Where(c => cortes.Contains(c.CutoffDate)).ToListAsync(ct);
        foreach (var l in leidas)
        {
            foreach (var a in anteriores.Where(a => a.CutoffDate == l.Corte && string.Equals(a.Concept, l.Concepto, StringComparison.OrdinalIgnoreCase)
                                                    && string.Equals(a.Dimension, l.Dimension, StringComparison.OrdinalIgnoreCase)))
            {
                a.IsDeleted = true;
                a.DeletedAt = reloj.UtcNow;
                a.DeletedBy = actor.Name;
            }
            db.ReceiptsReferenceFigures.Add(new ReceiptsReferenceFigure
            {
                CutoffDate = l.Corte, Concept = l.Concepto, Dimension = l.Dimension, Amount = l.Valor, LoadedByUserId = usuario, LoadedAt = reloj.UtcNow, ImportBatchPublicId = lote,
                SourceFileName = archivo.Length <= 200 ? archivo : archivo[..200],
            });
            ctx.Registrar(l.Fila, $"{l.Corte:yyyy-MM-dd}|{l.Concepto}|{l.Dimension}", AccionDeImportacion.Create);
        }
        ctx.Extra["batchPublicId"] = lote;
    }
}
