using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending.BulkCharges;

namespace IngenIA365ERP.Application.Lending.BulkCharges;

/// <summary>Los errores de los cargos masivos (2026-11-25, feature 026; contracts/api.md). (nuevo)</summary>
public static class ErroresDeCargoMasivo
{
    public const string CodigoInexistente = "Lending.BulkCharge.NotFound";
    public const string CodigoLineaInexistente = "Lending.BulkCharge.LineNotFound";
    public const string CodigoLineaNoHabilitada = "Lending.BulkCharge.LineNotEnabled";
    public const string CodigoFueraDeRango = "Lending.BulkCharge.OutOfRange";
    public const string CodigoPeriodoInvalido = "Lending.BulkCharge.PeriodInvalid";
    public const string CodigoNoBorrador = BulkChargeRun.CodigoNoBorrador;
    public const string CodigoEstadoInvalido = BulkChargeRun.CodigoEstadoInvalido;
    public const string CodigoVistaCambiada = "Lending.BulkCharge.PreviewChanged";
    public const string CodigoMismaPersona = "Lending.BulkCharge.SamePerson";
    public const string CodigoNadaQueCargar = "Lending.BulkCharge.NothingToCharge";
    public const string CodigoTieneMovimientos = "Lending.BulkCharge.HasMovements";
    public const string CodigoPersonaInexistente = "Lending.BulkCharge.CounterpartNotFound";
    public const string CodigoCarteraSinActivar = "Lending.BulkCharge.LendingNotActivated";
    public const string CodigoLineaDeCorridaInexistente = "Lending.BulkCharge.LineItemNotFound";
    public const string CodigoNoEsPlano = "Lending.BulkCharge.NotFileSource";

    public static Error Inexistente() => new(CodigoInexistente, "La corrida de cargos masivos no existe.");

    public static Error LineaInexistente() => new(CodigoLineaInexistente, "La línea de crédito no existe.");

    public static Error LineaNoHabilitada(string codigo, DateOnly fecha) => new ErrorConDatos(CodigoLineaNoHabilitada,
        $"La línea {codigo} no tiene una versión vigente al {fecha:dd/MM/yyyy} que admita cargos masivos. Márquela en la versión de la línea.",
        new { creditLineCode = codigo, date = fecha });

    public static Error FueraDeRango(string mensaje) => new(CodigoFueraDeRango, mensaje);

    public static Error VistaCambiada() => new(CodigoVistaCambiada,
        "La vista previa cambió desde que la revisó (alguien se retiró, ya tiene el cargo o cambió la línea). Revise la vista previa nueva y confirme otra vez.");

    public static Error MismaPersona() => new(CodigoMismaPersona,
        "La cooperativa pide que la corrida la confirme una persona distinta de quien la preparó.");

    public static Error NadaQueCargar() => new(CodigoNadaQueCargar, "La vista previa no tiene a nadie a quien cargar.");

    public static Error TieneMovimientos(string credito) => new ErrorConDatos(CodigoTieneMovimientos,
        $"La obligación {credito} ya tiene pagos, causación u otras operaciones: no se anula. Corríjala con una novedad o una condonación.",
        new { loanNumber = credito });

    public static Error PersonaInexistente() => new(CodigoPersonaInexistente, "La persona de la contrapartida no existe.");

    public static Error CarteraSinActivar() => new(CodigoCarteraSinActivar,
        "Cartera todavía no está activada en esta cooperativa: los cargos masivos estarán disponibles cuando se registre la activación.");

    public static Error LineaDeCorridaInexistente() => new(CodigoLineaDeCorridaInexistente, "El asociado no está en esta corrida.");

    public static Error NoEsPlano() => new(CodigoNoEsPlano, "Esta corrida toma a los asociados de los filtros, no de un plano.");
}
