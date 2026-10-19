using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Collections;

/// <summary>Un aviso sobre una plantilla o un mensaje renderizado: no impide guardar ni enviar. (nuevo)</summary>
public sealed record AvisoDePlantilla(string Codigo, string Mensaje, object? Datos = null);

/// <summary>
/// El largo de un SMS (§4.7): caracteres contados en su alfabeto (GSM-7, donde los de la extensión valen dos, o UCS-2), el límite de un
/// mensaje de un solo segmento (160 o 70) y cuántos segmentos ocupa (153 o 67 por segmento cuando son varios). (nuevo)
/// </summary>
public sealed record LargoDeSms(int Largo, bool Gsm7, int Limite, int Segmentos);

/// <summary>Lo que dice la revisión de una plantilla antes de guardarla: errores (no se guarda) y avisos. (nuevo)</summary>
public sealed record RevisionDePlantilla(IReadOnlyList<Error> Errores, IReadOnlyList<AvisoDePlantilla> Avisos, LargoDeSms? Sms)
{
    public bool EsValida => Errores.Count == 0;
}

/// <summary>Un mensaje renderizado: asunto, cuerpo, el largo si es SMS y los avisos. (nuevo)</summary>
public sealed record MensajeRenderizado(string? Asunto, string Cuerpo, LargoDeSms? Sms, IReadOnlyList<AvisoDePlantilla> Avisos);

/// <summary>
/// Sustituye <c>{{Variable}}</c> en las plantillas de mensaje (feature 013, T0084; T76; contracts/adaptadores-y-archivos.md §4.7). Puro: sin
/// IO ni estado.
/// <list type="bullet">
/// <item>Sólo variables de la <b>lista blanca</b> del propósito —las comunes más las del propósito—, sin expresiones: cualquier
/// <c>{{…}}</c> que no sea una variable permitida es <c>Lending.MessageTemplate.VariableNotAllowed</c> al guardar
/// (<c>data: { variable, allowed[] }</c>).</item>
/// <item>En el correo escapa el HTML de los <b>valores</b> (la plantilla es HTML de la cooperativa; lo que se sustituye no).</item>
/// <item>SMS: avisa el largo (160 en GSM-7, 70 en UCS-2) y el número de segmentos (<c>Lending.MessageTemplate.SmsTooLong</c>, aviso).</item>
/// <item>WhatsApp: sin <c>ProviderTemplateId</c> (la plantilla aprobada por Meta que la cooperativa registró) el canal no se usa
/// (pregunta I15): al guardar es un aviso y al renderizar, <c>Lending.MessageTemplate.ProviderTemplateMissing</c>.</item>
/// </list>
/// Los valores de cobranza se escriben «a la fecha {{FechaDeCorte}}»: el cuerpo se sella al nacer y el envío puede diferirse. (nuevo)
/// </summary>
public static partial class RenderizadorDePlantillasDeMensaje
{
    public const string CodigoVariableNoPermitida = "Lending.MessageTemplate.VariableNotAllowed";
    public const string CodigoSmsLargo = "Lending.MessageTemplate.SmsTooLong";
    public const string CodigoSinPlantillaDelProveedor = "Lending.MessageTemplate.ProviderTemplateMissing";
    public const string CodigoVariableSinValor = "Lending.MessageTemplate.VariableWithoutValue";

    /// <summary>Las variables de todo propósito (§4.7).</summary>
    public static IReadOnlyList<string> VariablesComunes { get; } =
        ["NombreDelTitular", "NombreDeLaCooperativa", "TelefonoDeLaCooperativa", "NumeroDelCredito", "FechaDeCorte"];

    private static readonly IReadOnlyDictionary<DebtorMessagePurpose, IReadOnlyList<string>> PorProposito =
        new Dictionary<DebtorMessagePurpose, IReadOnlyList<string>>
        {
            [DebtorMessagePurpose.Collection] = ["ValorVencido", "DiasDeMora", "CuotasVencidas", "ValorParaPonerseAlDia"],
            [DebtorMessagePurpose.PriorNotice] = ["ValorDeLaObligacion", "ValorVencido", "FechaMasTempranaDeReporte", "Centrales"],
            [DebtorMessagePurpose.CollectionFeeNotice] = ["ValorDelGasto", "Etapa", "FechaDeCausacion"],
            [DebtorMessagePurpose.Informative] =
                ["ValorAplicado", "ModalidadAplicada", "NuevaCuota", "NuevoPlazo", "MotivoDeNoDescuento", "FormaDePagoNueva",
                    // Feature 014, US1 (T067): el aviso de cambio de tasa de una línea a la vista.
                    "NumeroDeCuenta", "NombreDeLaLinea", "TasaAnterior", "TasaNueva", "FechaDelCambio",
                    // US2 (T076): el aviso de la mayoría de edad del titular.
                    "FechaDeMayoriaDeEdad",
                    // US7 (T182): el aviso del incumplimiento del ahorro contractual.
                    "CuotasAtrasadasDelContrato", "FechaDeIncumplimiento", "EfectoDelIncumplimiento",
                    // US14 (T208, T209): los avisos de la inactividad y del traslado a no reclamados.
                    "FechaDeInactividad", "DiasDeInactividad", "ValorNoReclamado", "FechaDelTraslado"],
            [DebtorMessagePurpose.DocumentDelivery] = ["ClaseDeDocumento", "NumeroDelDocumento", "CodigoDeVerificacion"],
        };

    // Alfabeto GSM 03.38: la tabla básica y la extensión (dos septetos cada uno).
    private const string Gsm7Basico =
        "@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞÆæßÉ !\"#¤%&'()*+,-./0123456789:;<=>?¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§¿abcdefghijklmnopqrstuvwxyzäöñüà";
    private const string Gsm7Extension = "^{}\\[~]|€\f";

    [GeneratedRegex(@"\{\{(?<dentro>.*?)\}\}", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex Marcador();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex NombreDeVariable();

    /// <summary>La lista blanca de <paramref name="proposito"/>: las comunes más las suyas.</summary>
    public static IReadOnlyList<string> VariablesPermitidas(DebtorMessagePurpose proposito) =>
        VariablesComunes.Concat(PorProposito.TryGetValue(proposito, out var propias) ? propias : []).ToList();

    /// <summary>Lo que hay entre <c>{{</c> y <c>}}</c> en <paramref name="texto"/>, en orden y sin repetir.</summary>
    public static IReadOnlyList<string> Marcadores(string? texto) =>
        string.IsNullOrEmpty(texto)
            ? []
            : Marcador().Matches(texto).Select(m => m.Groups["dentro"].Value.Trim()).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>
    /// Revisa una plantilla antes de guardarla: cada marcador del asunto y del cuerpo tiene que ser una variable de la lista blanca; avisa el
    /// largo del SMS y el WhatsApp sin plantilla del proveedor.
    /// </summary>
    public static RevisionDePlantilla Revisar(DebtorMessagePurpose proposito, MessageChannel canal, string? asunto, string cuerpo,
        string? providerTemplateId)
    {
        var permitidas = VariablesPermitidas(proposito);
        var errores = Marcadores(asunto).Concat(Marcadores(cuerpo)).Distinct(StringComparer.Ordinal)
            .Where(v => !EsPermitida(v, permitidas))
            .Select(v => (Error)new ErrorConDatos(CodigoVariableNoPermitida,
                $"«{{{{{v}}}}}» no es una variable que pueda usar un mensaje de este propósito. Use una de: {string.Join(", ", permitidas)}.",
                new { variable = v, allowed = permitidas }))
            .ToList();

        var avisos = new List<AvisoDePlantilla>();
        LargoDeSms? sms = null;
        if (canal == MessageChannel.Sms)
        {
            sms = MedirSms(cuerpo);
            if (sms.Segmentos > 1) avisos.Add(AvisoDeSmsLargo(sms));
        }
        if (canal == MessageChannel.WhatsApp && string.IsNullOrWhiteSpace(providerTemplateId))
            avisos.Add(new AvisoDePlantilla(CodigoSinPlantillaDelProveedor,
                "Sin el identificador de la plantilla aprobada ante el proveedor, los mensajes no salen por WhatsApp."));
        return new RevisionDePlantilla(errores, avisos, sms);
    }

    /// <summary>
    /// Renderiza una plantilla con <paramref name="valores"/>. Una variable fuera de la lista blanca es error (la plantilla no debió
    /// guardarse); una permitida sin valor queda vacía con aviso; WhatsApp sin plantilla del proveedor no se usa.
    /// </summary>
    public static Result<MensajeRenderizado> Renderizar(DebtorMessagePurpose proposito, MessageChannel canal, string? asunto, string cuerpo,
        string? providerTemplateId, IReadOnlyDictionary<string, string?> valores)
    {
        ArgumentNullException.ThrowIfNull(valores);
        var revision = Revisar(proposito, canal, asunto, cuerpo, providerTemplateId);
        if (!revision.EsValida) return Result.Failure<MensajeRenderizado>(revision.Errores[0]);
        if (canal == MessageChannel.WhatsApp && string.IsNullOrWhiteSpace(providerTemplateId))
            return Result.Failure<MensajeRenderizado>(CodigoSinPlantillaDelProveedor,
                "La plantilla de WhatsApp no tiene el identificador de la plantilla aprobada ante el proveedor: el mensaje no sale por WhatsApp.");

        var avisos = new List<AvisoDePlantilla>();
        var sinValor = new HashSet<string>(StringComparer.Ordinal);
        string Sustituir(string texto, bool escapar) => Marcador().Replace(texto, m =>
        {
            var nombre = m.Groups["dentro"].Value.Trim();
            if (!valores.TryGetValue(nombre, out var valor) || valor is null)
            {
                sinValor.Add(nombre);
                return string.Empty;
            }
            return escapar ? WebUtility.HtmlEncode(valor) : valor;
        });

        var asuntoFinal = asunto is null ? null : Sustituir(asunto, escapar: false);
        var cuerpoFinal = Sustituir(cuerpo, escapar: canal == MessageChannel.Email);
        avisos.AddRange(sinValor.Select(v => new AvisoDePlantilla(CodigoVariableSinValor, $"La variable «{v}» no tiene valor: quedó vacía.",
            new { variable = v })));

        LargoDeSms? sms = null;
        if (canal == MessageChannel.Sms)
        {
            sms = MedirSms(cuerpoFinal);
            if (sms.Segmentos > 1) avisos.Add(AvisoDeSmsLargo(sms));
        }
        return Result.Success(new MensajeRenderizado(asuntoFinal, cuerpoFinal, sms, avisos));
    }

    /// <summary>El largo de <paramref name="texto"/> como SMS: GSM-7 si todos sus caracteres caben, UCS-2 si no.</summary>
    public static LargoDeSms MedirSms(string? texto)
    {
        texto ??= string.Empty;
        var gsm7 = true;
        var septetos = 0;
        foreach (var c in texto)
        {
            if (Gsm7Basico.Contains(c, StringComparison.Ordinal)) septetos++;
            else if (Gsm7Extension.Contains(c, StringComparison.Ordinal)) septetos += 2;
            else { gsm7 = false; break; }
        }

        if (gsm7)
            return new LargoDeSms(septetos, true, 160, septetos <= 160 ? 1 : (int)Math.Ceiling(septetos / 153m));
        var unidades = texto.Length; // UTF-16: una unidad por carácter del BMP, dos fuera de él (UCS-2 los cuenta así).
        return new LargoDeSms(unidades, false, 70, unidades <= 70 ? 1 : (int)Math.Ceiling(unidades / 67m));
    }

    /// <summary>La lista blanca de <paramref name="proposito"/> como JSON (la columna <c>Variables</c>).</summary>
    public static string VariablesComoJson(DebtorMessagePurpose proposito)
    {
        var sb = new StringBuilder("[");
        var primera = true;
        foreach (var v in VariablesPermitidas(proposito))
        {
            if (!primera) sb.Append(',');
            sb.Append('"').Append(v).Append('"');
            primera = false;
        }
        return sb.Append(']').ToString();
    }

    private static bool EsPermitida(string marcador, IReadOnlyList<string> permitidas) =>
        NombreDeVariable().IsMatch(marcador) && permitidas.Contains(marcador, StringComparer.Ordinal);

    private static AvisoDePlantilla AvisoDeSmsLargo(LargoDeSms sms) =>
        new(CodigoSmsLargo,
            $"El SMS tiene {sms.Largo} caracteres ({(sms.Gsm7 ? "GSM-7" : "UCS-2: lleva caracteres fuera del alfabeto básico, como tildes")}) y " +
            $"pasa los {sms.Limite} de un mensaje: sale en {sms.Segmentos} partes.",
            new { length = sms.Largo, limit = sms.Limite, segments = sms.Segmentos });
}
