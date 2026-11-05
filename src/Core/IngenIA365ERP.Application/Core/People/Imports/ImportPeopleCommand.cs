using FluentValidation;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.People.Contracts;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Domain.Common.Identidad;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using P = IngenIA365ERP.Application.Core.People.Imports.PlantillaDePersonas;

namespace IngenIA365ERP.Application.Core.People.Imports;

/// <summary>
/// <c>POST /api/core/people/import?mode=review|apply</c> (<c>Core.People.Create</c>, con clave; feature 016, US1): la plantilla
/// <see cref="PlantillaDePersonas"/> sobre el ejecutor común. (nuevo)
/// </summary>
public sealed record ImportPeopleCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string? Reason = null,
    SiYaExiste Existentes = SiYaExiste.Actualizar)
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportPeopleCommandValidator : AbstractValidator<ImportPeopleCommand>
{
    public ImportPeopleCommandValidator() => RuleFor(x => x.File).NotNull();
}

/// <summary>
/// Las reglas del alta unitaria, fila por fila, con los catálogos cargados en bloque:
/// <list type="bullet">
/// <item>tipo y número por <see cref="UsuarioDeDocumento.Validar"/>; la llave es el usuario derivado, repetida en el archivo es
/// <c>Import.Row.Duplicate</c>;</item>
/// <item>las personas citadas se leen de una vez por usuario, <b>eliminadas incluidas</b>: una eliminada es
/// <c>Person.TaxIdDeleted</c> en la columna del documento (se restaura en Personas, no por archivo);</item>
/// <item>lo que se crea pasa por <see cref="PersonInputValidator"/> y <see cref="PersonFactory.Construir"/>; lo que se actualiza cambia
/// sólo las celdas con valor, se valida con el mismo validador sobre el resultado y deja el cambio campo a campo (exige
/// <c>Core.People.Update</c>);</item>
/// <item>con <see cref="SiYaExiste.Conservar"/> sólo agrega: la persona viva queda sin cambio, sin validar su fila y sin exigir
/// <c>Core.People.Update</c>; una eliminada sigue siendo error, porque tampoco se puede agregar;</item>
/// <item>nunca toca <c>IsAssociate</c>, <c>IsEmployee</c> ni <c>IsSalesperson</c>: las escribe el handler de su fila hija.</item>
/// </list>
/// </summary>
public sealed class ImportPeopleCommandHandler(IApplicationDbContext db, EjecutorDeImportacion ejecutor, PersonFactory personas,
    IDateTimeService reloj, ICurrentUserService usuario)
    : IRequestHandler<ImportPeopleCommand, Result<ImportResultDto>>
{
    public const string PermisoDeActualizar = "Core.People.Update";
    public const string CodigoDigitoDistinto = "Person.CheckDigitMismatch";
    public const string CodigoInvalido = "Person.Invalid";

    private static readonly IReadOnlyDictionary<string, string> Naturalezas = new Dictionary<string, string>
    {
        ["natural"] = P.TipoNatural, ["n"] = P.TipoNatural, ["01"] = P.TipoNatural,
        ["juridica"] = P.TipoJuridica, ["j"] = P.TipoJuridica, ["02"] = P.TipoJuridica,
    };

    /// <summary>La columna de la plantilla de cada propiedad de <see cref="PersonInput"/>, para ubicar los errores del validador.</summary>
    private static readonly IReadOnlyDictionary<string, string> ColumnaDe = new Dictionary<string, string>
    {
        [nameof(PersonInput.IdType)] = P.TipoDocumento, [nameof(PersonInput.TaxId)] = P.Documento,
        [nameof(PersonInput.FirstName)] = P.PrimerNombre, [nameof(PersonInput.LastName)] = P.PrimerApellido,
        [nameof(PersonInput.SecondLastName)] = P.SegundoApellido, [nameof(PersonInput.OtherNames)] = P.OtrosNombres,
        [nameof(PersonInput.BusinessName)] = P.RazonSocial, [nameof(PersonInput.Email)] = P.Correo,
        [nameof(PersonInput.Phone1)] = P.Telefono, [nameof(PersonInput.Phone2)] = P.Telefono2, [nameof(PersonInput.Mobile)] = P.Celular,
        [nameof(PersonInput.Address)] = P.Direccion, [nameof(PersonInput.Gender)] = P.Genero,
        [nameof(PersonInput.MaritalStatus)] = P.EstadoCivil, [nameof(PersonInput.EducationLevel)] = P.NivelEducativo,
        [nameof(PersonInput.DateOfBirth)] = P.FechaNacimiento, [nameof(PersonInput.CiiuCode)] = P.Ciiu,
    };

    public Task<Result<ImportResultDto>> Handle(ImportPeopleCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(P.Definicion, request, (ctx, c) => ProcesarAsync(ctx, request.Existentes == SiYaExiste.Conservar, c), ct);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, bool conservarExistentes, CancellationToken ct)
    {
        var filas = ctx.Datos.Filas;

        // Primera pasada: documento y usuario de cada fila, para leer en bloque las personas citadas.
        var documentos = new Dictionary<FilaDeImportacion, DocumentoValidado>();
        foreach (var fila in filas)
        {
            if (DocumentoDePlantilla.Leer(fila, P.TipoDocumento, P.Documento) is not { } validado) continue;
            documentos[fila] = validado;
        }

        var usuarios = documentos.Values.Select(d => d.Usuario).Distinct().ToList();
        var existentes = usuarios.Count == 0
            ? []
            : await db.People.IgnoreQueryFilters().Where(p => usuarios.Contains(p.Username)).ToListAsync(ct);
        var porUsuario = existentes.GroupBy(p => p.Username).ToDictionary(g => g.Key, g => g.OrderBy(p => p.IsDeleted).First());
        var ciudades = CatalogoCitado<int>.Desde(
            await db.Cities.AsNoTracking().Where(c => !c.IsDeleted).Select(c => new { c.Id, c.DaneCode, c.LegacyCode, c.Name }).ToListAsync(ct),
            c => c.Id, c => c.DaneCode, c => c.LegacyCode, c => c.Name);
        var validador = new PersonInputValidator();
        var puedeActualizar = ctx.TienePermiso(PermisoDeActualizar);

        foreach (var fila in filas)
        {
            if (!documentos.TryGetValue(fila, out var documento)) continue;
            if (!ctx.Datos.LlaveUnica(fila, documento.Usuario, P.Documento)) continue;

            porUsuario.TryGetValue(documento.Usuario, out var existente);
            if (conservarExistentes && existente is { IsDeleted: false })
            {
                ctx.Registrar(fila, documento.Usuario, AccionDeImportacion.Unchanged);
                continue;
            }

            var datos = Leer(fila, documento, ciudades);
            if (fila.TieneErrores || datos is null) continue;

            if (existente is { IsDeleted: true })
            {
                var nombre = PersonFactory.NombreVisible(existente.FirstName, existente.OtherNames, existente.LastName, existente.SecondLastName, existente.BusinessName);
                var eliminada = PersonFactory.DocumentoEliminado(nombre, existente.DeletedAt);
                fila.Error(P.Documento, eliminada.Code, eliminada.Message + " Restáurela en Maestros › Personas.");
                continue;
            }

            if (existente is null)
                Crear(ctx, fila, documento, datos, validador);
            else if (!puedeActualizar)
                fila.Error(P.Documento, ImportErrors.CellPermissionRequired,
                    $"La persona {documento.Usuario} ya existe y actualizarla exige el permiso {PermisoDeActualizar}.");
            else
                Actualizar(ctx, fila, documento, datos, existente, validador);
        }
    }

    // ------------------------------------------------------------------ lectura --

    /// <summary>Lo que trae una fila, ya convertido. Nulo en una celda = la fila no la trae.</summary>
    private sealed record DatosDeFila(
        string TipoDePersona, string? Digito, string? Nombre, string? OtrosNombres, string? Apellido, string? SegundoApellido,
        string? RazonSocial, string? LugarExpedicion, DateOnly? FechaExpedicion, string? Correo, string? Telefono, string? Telefono2,
        string? Celular, string? Direccion, int? CiudadId, DateOnly? FechaNacimiento, string? Genero, string? EstadoCivil,
        string? NivelEducativo, bool? Proveedor, bool? Cliente, PersonInput Tributario);

    private static DatosDeFila? Leer(FilaDeImportacion fila, DocumentoValidado documento, CatalogoCitado<int> ciudades)
    {
        string? tipoDePersona = null;
        var naturaleza = fila.Texto(P.Naturaleza);
        if (naturaleza is not null && !Naturalezas.TryGetValue(TablaLeida.Normalizar(naturaleza), out tipoDePersona))
            fila.Error(P.Naturaleza, ImportErrors.CellFormat, $"«{naturaleza}» no es válido en «{P.Naturaleza}». Escriba natural o juridica.");

        var digito = fila.Texto(P.DigitoVerificacion, 1);
        if (digito is not null && !char.IsAsciiDigit(digito[0]))
            fila.Error(P.DigitoVerificacion, ImportErrors.CellFormat, "El dígito de verificación es un número de 0 a 9.");
        else if (digito is not null && documento.Tipo == TiposDeDocumento.Nit
                 && P.DigitoDeVerificacionDian(documento.Numero) is { } esperado && esperado != digito[0] - '0')
            fila.Aviso(P.DigitoVerificacion, CodigoDigitoDistinto,
                $"El dígito de verificación del NIT {documento.Numero} es {esperado}, no {digito}. Se guarda el del archivo.");

        int? ciudad = null;
        if (fila.Texto(P.Ciudad) is not null)
        {
            var id = fila.Referencia(P.Ciudad, ciudades, "una ciudad", "en Maestros › Ciudades");
            ciudad = id == 0 ? null : id;
        }

        var tributario = new PersonInput
        {
            IsVatResponsible = fila.SiNoIndiferente(P.ResponsableIva),
            IsSelfWithholder = fila.SiNoIndiferente(P.Autorretenedor),
            IsVatWithholdingAgent = fila.SiNoIndiferente(P.AgenteRetencionIva),
            IsSimpleTaxRegime = fila.SiNoIndiferente(P.RegimenSimple),
            IsIncomeTaxFiler = fila.SiNoIndiferente(P.DeclaranteRenta),
            IsObligatedToInvoice = fila.SiNoIndiferente(P.ObligadoFacturar),
            IsObligatedToKeepBooks = fila.SiNoIndiferente(P.ObligadoContabilidad),
            IsLargeContributor = fila.SiNoIndiferente(P.GranContribuyente),
            WithholdingExempt = fila.SiNoIndiferente(P.ExentoRetencion),
            IcaWithholdingExempt = fila.SiNoIndiferente(P.ExentoReteIca),
            CiiuCode = fila.Texto(P.Ciiu, 6),
        };

        var datos = new DatosDeFila(
            tipoDePersona ?? P.TipoNatural, digito,
            fila.Texto(P.PrimerNombre), fila.Texto(P.OtrosNombres), fila.Texto(P.PrimerApellido), fila.Texto(P.SegundoApellido),
            fila.Texto(P.RazonSocial), fila.Texto(P.LugarExpedicion), fila.Fecha(P.FechaExpedicion),
            fila.Texto(P.Correo), fila.Texto(P.Telefono), fila.Texto(P.Telefono2), fila.Texto(P.Celular), fila.Texto(P.Direccion),
            ciudad, fila.Fecha(P.FechaNacimiento),
            Cerrado(fila, P.Genero, P.Generos), Cerrado(fila, P.EstadoCivil, P.EstadosCiviles), Cerrado(fila, P.NivelEducativo, P.NivelesEducativos),
            fila.SiNoIndiferente(P.EsProveedor), fila.SiNoIndiferente(P.EsCliente), tributario);

        if (datos.TipoDePersona == P.TipoJuridica && datos.RazonSocial is null)
            fila.Error(P.RazonSocial, ImportErrors.CellRequired, "La persona jurídica lleva razón social.");
        return fila.TieneErrores ? null : datos;
    }

    /// <summary>Un código de un catálogo cerrado del formulario de persona, por código o por nombre.</summary>
    private static string? Cerrado(FilaDeImportacion fila, string columna, IReadOnlyDictionary<string, string> catalogo)
    {
        var crudo = fila.Texto(columna);
        if (crudo is null) return null;
        var buscado = TablaLeida.Normalizar(crudo);
        foreach (var (codigo, nombre) in catalogo)
        {
            var nombreNormal = TablaLeida.Normalizar(nombre);
            if (TablaLeida.Normalizar(codigo) == buscado || nombreNormal == buscado
                || (buscado.Length >= 4 && nombreNormal.StartsWith(buscado, StringComparison.Ordinal)))
                return codigo;
        }
        fila.Error(columna, ImportErrors.CellFormat,
            $"«{crudo}» no es válido en «{columna}». Admite: {string.Join(", ", catalogo.Select(c => $"{c.Key} ({c.Value})"))}.");
        return null;
    }

    // ------------------------------------------------------------------ crear --

    private void Crear(ContextoDeImportacion ctx, FilaDeImportacion fila, DocumentoValidado documento, DatosDeFila d, PersonInputValidator validador)
    {
        if (d.TipoDePersona == P.TipoNatural)
        {
            if (d.Nombre is null) fila.Error(P.PrimerNombre, ImportErrors.CellRequired, "La persona natural lleva primer nombre.");
            if (d.Apellido is null) fila.Error(P.PrimerApellido, ImportErrors.CellRequired, "La persona natural lleva primer apellido.");
            if (fila.TieneErrores) return;
        }

        // La jurídica sin nombres: el alta exige nombres y apellidos, y la persona se muestra por su razón social.
        var entrada = d.Tributario with
        {
            IdType = documento.Tipo.CodigoDePersona,
            TaxId = documento.Numero,
            TaxIdCheckDigit = d.Digito,
            IdIssuedAt = d.LugarExpedicion,
            IdIssueDate = d.FechaExpedicion,
            FirstName = d.Nombre ?? d.RazonSocial ?? string.Empty,
            LastName = d.Apellido ?? d.RazonSocial ?? string.Empty,
            SecondLastName = d.SegundoApellido,
            OtherNames = d.OtrosNombres,
            BusinessName = d.RazonSocial,
            PersonType = d.TipoDePersona,
            Address = d.Direccion,
            Phone1 = d.Telefono,
            Phone2 = d.Telefono2,
            Mobile = d.Celular,
            Email = d.Correo,
            Gender = d.Genero,
            MaritalStatus = d.EstadoCivil,
            DateOfBirth = d.FechaNacimiento,
            EducationLevel = d.NivelEducativo,
            IsSupplier = d.Proveedor ?? false,
            IsCustomer = d.Cliente ?? false,
        };
        if (!Valida(fila, validador, entrada)) return;

        personas.Construir(entrada, d.CiudadId);
        ctx.Registrar(fila, documento.Usuario, AccionDeImportacion.Create);
    }

    // ------------------------------------------------------------- actualizar --

    private void Actualizar(ContextoDeImportacion ctx, FilaDeImportacion fila, DocumentoValidado documento, DatosDeFila d, Person p,
        PersonInputValidator validador)
    {
        var cambios = new List<CampoCambiadoDto>();

        void Texto(string columna, string? nuevo, Func<string?> leer, Action<string?> escribir)
        {
            if (nuevo is null || string.Equals(leer(), nuevo, StringComparison.Ordinal)) return;
            cambios.Add(new CampoCambiadoDto(columna, leer(), nuevo));
            escribir(nuevo);
        }

        void Fecha(string columna, DateOnly? nueva, Func<DateOnly?> leer, Action<DateOnly?> escribir)
        {
            if (nueva is null || leer() == nueva) return;
            cambios.Add(new CampoCambiadoDto(columna, leer()?.ToString("yyyy-MM-dd"), nueva.Value.ToString("yyyy-MM-dd")));
            escribir(nueva);
        }

        void Marca(string columna, bool? nueva, Func<bool> leer, Action<bool> escribir)
        {
            if (nueva is null || leer() == nueva.Value) return;
            cambios.Add(new CampoCambiadoDto(columna, Si(leer()), Si(nueva.Value)));
            escribir(nueva.Value);
        }

        void MarcaNula(string columna, bool? nueva, Func<bool?> leer, Action<bool> escribir)
        {
            if (nueva is null || leer() == nueva.Value) return;
            cambios.Add(new CampoCambiadoDto(columna, leer() is { } antes ? Si(antes) : null, Si(nueva.Value)));
            escribir(nueva.Value);
        }

        // La naturaleza sólo se cambia si la columna vino: vacía, la persona conserva la suya.
        Texto(P.Naturaleza, fila.Texto(P.Naturaleza) is null ? null : d.TipoDePersona, () => p.PersonType, v => p.PersonType = v);
        Texto(P.DigitoVerificacion, d.Digito, () => p.TaxIdCheckDigit, v => p.TaxIdCheckDigit = v);
        Texto(P.PrimerNombre, d.Nombre, () => p.FirstName, v => p.FirstName = v!);
        Texto(P.OtrosNombres, d.OtrosNombres, () => p.OtherNames, v => p.OtherNames = v);
        Texto(P.PrimerApellido, d.Apellido, () => p.LastName, v => p.LastName = v!);
        Texto(P.SegundoApellido, d.SegundoApellido, () => p.SecondLastName, v => p.SecondLastName = v);
        Texto(P.RazonSocial, d.RazonSocial, () => p.BusinessName, v => p.BusinessName = v);
        Texto(P.LugarExpedicion, d.LugarExpedicion, () => p.IdIssuedAt, v => p.IdIssuedAt = v);
        Fecha(P.FechaExpedicion, d.FechaExpedicion, () => p.IdIssueDate, v => p.IdIssueDate = v);
        Texto(P.Correo, d.Correo, () => p.Email, v => p.Email = v);
        Texto(P.Telefono, d.Telefono, () => p.Phone1, v => p.Phone1 = v);
        Texto(P.Telefono2, d.Telefono2, () => p.Phone2, v => p.Phone2 = v);
        Texto(P.Celular, d.Celular, () => p.Mobile, v => p.Mobile = v);
        Texto(P.Direccion, d.Direccion, () => p.Address, v => p.Address = v);
        if (d.CiudadId is { } ciudad && p.CityId != ciudad)
        {
            cambios.Add(new CampoCambiadoDto(P.Ciudad, p.CityId?.ToString(), fila.Crudo(P.Ciudad)));
            p.CityId = ciudad;
        }
        Fecha(P.FechaNacimiento, d.FechaNacimiento, () => p.DateOfBirth, v => p.DateOfBirth = v);
        Texto(P.Genero, d.Genero, () => p.Gender, v => p.Gender = v);
        Texto(P.EstadoCivil, d.EstadoCivil, () => p.MaritalStatus, v => p.MaritalStatus = v);
        Texto(P.NivelEducativo, d.NivelEducativo, () => p.EducationLevel, v => p.EducationLevel = v);
        Marca(P.EsProveedor, d.Proveedor, () => p.IsSupplier, v => p.IsSupplier = v);
        Marca(P.EsCliente, d.Cliente, () => p.IsCustomer, v => p.IsCustomer = v);
        var t = d.Tributario;
        MarcaNula(P.ResponsableIva, t.IsVatResponsible, () => p.IsVatResponsible, v => p.IsVatResponsible = v);
        MarcaNula(P.Autorretenedor, t.IsSelfWithholder, () => p.IsSelfWithholder, v => p.IsSelfWithholder = v);
        MarcaNula(P.AgenteRetencionIva, t.IsVatWithholdingAgent, () => p.IsVatWithholdingAgent, v => p.IsVatWithholdingAgent = v);
        MarcaNula(P.RegimenSimple, t.IsSimpleTaxRegime, () => p.IsSimpleTaxRegime, v => p.IsSimpleTaxRegime = v);
        MarcaNula(P.DeclaranteRenta, t.IsIncomeTaxFiler, () => p.IsIncomeTaxFiler, v => p.IsIncomeTaxFiler = v);
        MarcaNula(P.ObligadoFacturar, t.IsObligatedToInvoice, () => p.IsObligatedToInvoice, v => p.IsObligatedToInvoice = v);
        MarcaNula(P.ObligadoContabilidad, t.IsObligatedToKeepBooks, () => p.IsObligatedToKeepBooks, v => p.IsObligatedToKeepBooks = v);
        MarcaNula(P.GranContribuyente, t.IsLargeContributor, () => p.IsLargeContributor, v => p.IsLargeContributor = v);
        MarcaNula(P.ExentoRetencion, t.WithholdingExempt, () => p.WithholdingExempt, v => p.WithholdingExempt = v);
        MarcaNula(P.ExentoReteIca, t.IcaWithholdingExempt, () => p.IcaWithholdingExempt, v => p.IcaWithholdingExempt = v);
        Texto(P.Ciiu, t.CiiuCode, () => p.CiiuCode, v => p.CiiuCode = v);

        if (cambios.Count == 0)
        {
            ctx.Registrar(fila, documento.Usuario, AccionDeImportacion.Unchanged);
            return;
        }

        // La persona que resulta tiene que pasar las mismas reglas del alta (largos, correo, fecha de nacimiento, CIIU).
        var resultado = new PersonInput
        {
            IdType = p.IdType, TaxId = p.TaxId, FirstName = p.FirstName, LastName = p.LastName, SecondLastName = p.SecondLastName,
            OtherNames = p.OtherNames, BusinessName = p.BusinessName, Email = p.Email, Phone1 = p.Phone1, Phone2 = p.Phone2,
            Mobile = p.Mobile, Address = p.Address, Gender = p.Gender, MaritalStatus = p.MaritalStatus,
            EducationLevel = p.EducationLevel, DateOfBirth = p.DateOfBirth, CiiuCode = p.CiiuCode,
        };
        if (!Valida(fila, validador, resultado)) return;

        p.UpdatedAt = reloj.UtcNow;
        p.UpdatedBy = usuario.UserName;
        ctx.Registrar(fila, documento.Usuario, AccionDeImportacion.Update, cambios);
    }

    private static bool Valida(FilaDeImportacion fila, PersonInputValidator validador, PersonInput entrada)
    {
        var resultado = validador.Validate(entrada);
        foreach (var falla in resultado.Errors)
            fila.Error(ColumnaDe.GetValueOrDefault(falla.PropertyName, P.Documento), CodigoInvalido, falla.ErrorMessage);
        return resultado.IsValid;
    }

    private static string Si(bool valor) => valor ? "sí" : "no";
}
