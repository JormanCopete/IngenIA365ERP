using System.Globalization;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Rates;

namespace IngenIA365ERP.Application.Lending.Configuration;

/// <summary>
/// Los errores de las líneas de crédito, su usura, su puntaje y las equivalencias de Comercial (feature 013, US1; api.md §1.2, §3.1–§3.3;
/// data-model §1.2–§1.9, §10.1). <c>Lending.CreditLine.NotFound</c> es 404 —también sin permiso o fuera del alcance de líneas, con el
/// mismo mensaje—; los demás, 422 con <c>data</c> para corregir; <c>Validation.Invalid</c> (400) lleva <c>data.errors[{ field, code,
/// message }]</c> para que la pantalla marque el campo. Los mensajes van en español claro, dicen qué hacer y nunca muestran el código
/// (FR-013). (nuevo)
/// </summary>
public static class ErroresDeLineasDeCredito
{
    public const string CodigoInvalido = "Validation.Invalid";
    public const string CodigoModalidadDesconocida = "Lending.CreditLine.ModalityUnknown";
    public const string CodigoViviendaNoDisponible = "Lending.CreditLine.HousingNotAvailable";
    public const string CodigoNoAsociadosNoPermitidos = "Lending.CreditLine.NonAssociatesNotAllowed";
    public const string CodigoVersionCruzada = "Lending.CreditLine.VersionOverlaps";
    public const string CodigoConCreditos = "Lending.CreditLine.HasLoans";
    public const string CodigoUsuraExcedida = ControlDeUsura.CodigoExcedida;
    public const string CodigoNivelDesconocido = "Lending.Attribution.LevelUnknown";

    /// <summary>La entrega desde la que se admiten las líneas de vivienda y UVR (D18).</summary>
    public const string EntregaDeVivienda = "K6b";

    public static Error LineaInexistente() => ErroresDeGobierno.LineaInexistente();

    /// <summary>
    /// Lo que rechazó una entidad de la línea (<c>CreditLineVersion.Validar</c>, <c>CreditLine</c>, <c>CommercialTermEquivalence</c>, el
    /// modelo de puntaje) como error del contrato. <c>Validation.Invalid</c> lleva el campo en <c>data.errors</c>.
    /// </summary>
    public static Error De(ProblemaDeCartera problema)
    {
        ArgumentNullException.ThrowIfNull(problema);
        if (problema.Codigo == CodigoInvalido)
        {
            var campo = problema.Datos is { } d && d.TryGetValue("field", out var f) ? f?.ToString() ?? string.Empty : string.Empty;
            return new ErrorConDatos(CodigoInvalido, problema.Mensaje,
                new { errors = new[] { new ErrorDeCampo(campo, CodigoInvalido, problema.Mensaje) } });
        }
        return ErroresDeGobierno.De(problema);
    }

    /// <summary>Un 400 de un campo (el mismo sobre que el validador).</summary>
    public static Error Invalido(string campo, string mensaje) =>
        new ErrorConDatos(CodigoInvalido, mensaje, new { errors = new[] { new ErrorDeCampo(campo, CodigoInvalido, mensaje) } });

    public static Error ModalidadDesconocida(string modalidad, string? subsegmento, DateOnly fecha, bool retirada = false) => new ErrorConDatos(
        CodigoModalidadDesconocida,
        retirada
            ? $"La modalidad {modalidad} salió del catálogo nacional de modalidades vigente el {Fecha(fecha)}. Elija una modalidad vigente."
            : subsegmento is null
                ? $"La modalidad {modalidad} no está en el catálogo nacional de modalidades vigente el {Fecha(fecha)}. Elija una de la lista."
                : $"La combinación de la modalidad {modalidad} con el sub-segmento {subsegmento} no está en el catálogo nacional vigente el {Fecha(fecha)}. Elija una de la lista.",
        new { modalityCode = modalidad, subsegmentCode = subsegmento, date = Fecha(fecha), errors = new[] { new ErrorDeCampo("modalityCode", CodigoModalidadDesconocida, "Modalidad o sub-segmento fuera del catálogo vigente.") } });

    public static Error ViviendaNoDisponible(string modalidad) => new ErrorConDatos(CodigoViviendaNoDisponible,
        $"Las líneas de vivienda y en UVR ({modalidad}) todavía no se pueden crear: llegan con una entrega posterior de Cartera.",
        new { modalityCode = modalidad, availableFrom = EntregaDeVivienda });

    public static Error NoAsociadosNoPermitidos(string? tipoDeOrganizacion) => new ErrorConDatos(CodigoNoAsociadosNoPermitidos,
        tipoDeOrganizacion is null
            ? "La cooperativa no tiene un perfil regulatorio vigente: mientras no lo registre, sus líneas sólo admiten asociados."
            : "El perfil regulatorio de la cooperativa no le permite prestar a personas que no son asociadas. Desmarque «admite no asociados».",
        new { organizationTypeCode = tipoDeOrganizacion, errors = new[] { new ErrorDeCampo("allowsNonAssociates", CodigoNoAsociadosNoPermitidos, "La cooperativa no puede prestar a no asociados.") } });

    public static Error VersionCruzada(DateOnly existente) => new ErrorConDatos(CodigoVersionCruzada,
        $"La línea ya tiene una versión que rige desde el {Fecha(existente)}: la nueva tiene que empezar ese mismo día o después.",
        new { existingValidFrom = Fecha(existente), errors = new[] { new ErrorDeCampo("validFrom", CodigoVersionCruzada, "Empieza antes de la última versión registrada.") } });

    public static Error ConCreditos() => new ErrorConDatos(CodigoConCreditos,
        "La línea ya tiene solicitudes, créditos, cupos o ventas: no se elimina, se cierra a créditos nuevos.",
        new { alternatives = new[] { "Close" } });

    public static Error NivelDesconocido(string nivel) => new ErrorConDatos(CodigoNivelDesconocido,
        $"El nivel {nivel} no existe en la matriz de atribuciones vigente. Elija uno de sus niveles para las excepciones del puntaje.",
        new { levelCode = nivel });

    public static Error DefectoComercialRepetido(string otra) => Invalido("commercialDefaultFor",
        $"La línea {otra} ya es la de defecto de Comercial para ese tipo de deudor: sólo puede haber una activa por tipo.");

    /// <summary>
    /// La efectiva con cargos supera la usura (api.md §1.2): tasa, usura, modalidad certificada, fecha, el valor nacional usado, la
    /// diferencia y el campo que la produjo (la tasa de la versión, la de una banda o la mora).
    /// </summary>
    public static Error UsuraExcedida(string campo, DateOnly fecha, ResultadoDeUsura r, string modalidad)
    {
        var mensaje = $"La tasa efectiva resultante ({Porcentaje(r.EfectivaConCargos)} EA, con los cargos que computan) supera la usura vigente " +
            $"de {modalidad} el {Fecha(fecha)} ({Porcentaje(r.Usura ?? 0m)} EA) en {Porcentaje(r.Diferencia ?? 0m)}. Ajuste la tasa o los cargos.";
        return new ErrorConDatos(CodigoUsuraExcedida, mensaje, new
        {
            effectiveRate = Tasa(r.EfectivaConCargos),
            effectiveRateWithoutCharges = Tasa(r.EfectivaSinCargos),
            usuryRate = r.Usura,
            modalityKey = r.ClaveDeUsura,
            modalityCode = modalidad,
            date = Fecha(fecha),
            nationalValuePublicId = r.UsuraPublicId,
            difference = Tasa(r.Diferencia ?? 0m),
            errors = new[] { new ErrorDeCampo(campo, CodigoUsuraExcedida, mensaje) },
        });
    }

    /// <summary>La mora fija por encima de la usura del día (plantillas.md §1: «nunca por encima de la usura del día»).</summary>
    public static Error MoraSobreLaUsura(decimal mora, decimal usura, Guid usuraPublicId, string clave, string modalidad, DateOnly fecha)
    {
        var mensaje = $"La tasa de mora ({Porcentaje(mora)} EA) supera la usura vigente de {modalidad} el {Fecha(fecha)} ({Porcentaje(usura)} EA). Bájela o use la mora relativa.";
        return new ErrorConDatos(CodigoUsuraExcedida, mensaje, new
        {
            effectiveRate = mora,
            usuryRate = usura,
            modalityKey = clave,
            modalityCode = modalidad,
            date = Fecha(fecha),
            nationalValuePublicId = usuraPublicId,
            difference = mora - usura,
            errors = new[] { new ErrorDeCampo("defaultRate", CodigoUsuraExcedida, mensaje) },
        });
    }

    /// <summary>Una tasa del contrato: hasta 18 decimales (<c>TasaEquivalente</c>).</summary>
    public static decimal Tasa(decimal t) => EquivalenciaDeTasas.Redondear(t);

    private static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Porcentaje(decimal fraccion) =>
        (fraccion * 100m).ToString("0.##", CultureInfo.GetCultureInfo("es-CO")) + " %";
}

/// <summary>Un error de un campo dentro de <c>data.errors</c> (api.md §1.2). (nuevo)</summary>
public sealed record ErrorDeCampo(string Field, string Code, string Message);
