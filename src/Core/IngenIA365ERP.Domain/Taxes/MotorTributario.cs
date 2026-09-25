using System.Globalization;
using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Domain.Taxes;

/// <summary>
/// El motor tributario del comercio (feature 012, T22, T162; FR-013, FR-044; research R21; data-model §17), puro: recibe
/// la foto del catálogo (<see cref="TaxCatalogSnapshot"/>) y la operación (<see cref="EntradaTributaria"/>) y devuelve
/// los renglones con su explicación. Sin IO y sin valores legales: toda tarifa, base mínima y UVT sale de la foto.
///
/// <list type="bullet">
/// <item><b>Selección de tarifa</b>: candidatas = tarifas vigentes a la fecha de la definición (o del concepto), con
/// <c>AppliesTo</c> compatible con la perspectiva; en ICA/ReteICA, del municipio de la operación con la actividad
/// exacta del vendedor o, si no hay, la fila <c>*</c>; cuyas condiciones no nulas se cumplen con el perfil del sujeto
/// (vendedor) y del agente (comprador). Gana la de mayor <c>Priority</c>; a igual prioridad, la de más condiciones; si
/// aún empatan, el documento no se confirma (<c>Core.TaxRate.Ambiguous</c>, que nombra las dos).</item>
/// <item><b>Impuestos por línea</b> (IVA, INC, por unidad), redondeados por línea: el total es la suma. En ventas son
/// <c>Generated</c>; en compras, descontables o al costo según <see cref="IvaDescontable"/>. El IVA sólo lo cobra un
/// vendedor responsable, y en ventas sólo sobre productos gravados.</item>
/// <item><b>Retenciones por documento</b> y por (impuesto, concepto, municipio), cuando la base es <b>igual o
/// superior</b> al mínimo convertido a pesos (<see cref="ConversionUvt"/>). ReteIVA (<c>PercentOfTax</c>) se calcula
/// sobre el IVA de las líneas y su mínimo se compara con la base de la operación gravada. En compras la cooperativa las
/// practica (<c>WithholdingApplied</c>); en ventas las sufre (<c>WithholdingSuffered</c>). El sujeto exento
/// (<c>WithholdingExempt</c>, <c>IcaWithholdingExempt</c>) lo resuelve el motor, no una condición.</item>
/// <item><b>Notas y devoluciones</b>: la foto del original, con sus tarifas, sin volver a probar el mínimo (E9).</item>
/// </list>
/// </summary>
public static class MotorTributario
{
    public const string CodigoAmbiguo = "Core.TaxRate.Ambiguous";
    public const string CodigoTarifaNoVigente = "Core.TaxRate.NotFound";
    public const string CodigoImpuestoInexistente = "Core.Tax.NotFound";

    /// <summary>La actividad general del municipio (ReteICA con caída a la fila <c>*</c>).</summary>
    public const string ActividadGeneral = "*";

    public static ResultadoTributario Calcular(TaxCatalogSnapshot foto, EntradaTributaria entrada)
    {
        ArgumentNullException.ThrowIfNull(foto);
        ArgumentNullException.ThrowIfNull(entrada);

        var calculo = new Calculo(foto, entrada);
        if (entrada.Original is { Count: > 0 } original)
        {
            calculo.ConLaFotoDelOriginal(original);
        }
        else
        {
            calculo.ImpuestosPorLinea();
            calculo.Retenciones();
        }
        return calculo.Resultado();
    }

    private sealed class Calculo(TaxCatalogSnapshot foto, EntradaTributaria entrada)
    {
        private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

        private readonly List<RenglonTributario> _renglones = [];
        private readonly List<RechazoTributario> _rechazos = [];
        private readonly List<string> _omisiones = [];

        private PerfilTributario Sujeto => entrada.Vendedor;
        private PerfilTributario Agente => entrada.Comprador;
        private bool EnCompras => entrada.Perspectiva == TaxAppliesTo.Purchases;

        public ResultadoTributario Resultado() => new(_renglones, _rechazos, _omisiones);

        private decimal Redondear(decimal valor) => Math.Round(valor, foto.DecimalesDeMonto, MidpointRounding.AwayFromZero);

        private static bool Compatible(TaxAppliesTo vinculo, TaxAppliesTo perspectiva) =>
            vinculo == TaxAppliesTo.Both || vinculo == perspectiva;

        private static string Pesos(decimal valor) => valor.ToString("N2", Co);

        private static string Porcentaje(decimal fraccion) => (fraccion * 100).ToString("0.####", CultureInfo.InvariantCulture) + " %";

        // ----------------------------------------------------------------------------------- impuestos por línea --

        public void ImpuestosPorLinea()
        {
            foreach (var linea in entrada.Lineas.OrderBy(l => l.Numero))
            {
                foreach (var vinculo in linea.Impuestos)
                {
                    var impuesto = foto.Impuesto(vinculo.TaxDefinitionId);
                    if (impuesto is null || !impuesto.IsActive)
                    {
                        _rechazos.Add(new RechazoTributario(CodigoImpuestoInexistente,
                            $"La línea {linea.Numero} cita un impuesto que no existe o está inactivo (Id {vinculo.TaxDefinitionId})."));
                        continue;
                    }
                    if (impuesto.IsWithholding || impuesto.Kind == TaxKind.Ica)
                    {
                        _omisiones.Add($"Línea {linea.Numero}, {impuesto.Code}: las retenciones y el ICA no se vinculan al producto.");
                        continue;
                    }
                    if (!Compatible(vinculo.AppliesTo, entrada.Perspectiva))
                    {
                        _omisiones.Add($"Línea {linea.Numero}, {impuesto.Code}: el producto no lo lleva en {(EnCompras ? "compras" : "ventas")}.");
                        continue;
                    }
                    if (impuesto.Kind == TaxKind.Iva)
                    {
                        if (!EnCompras && linea.TratamientoIvaVenta != VatSaleTreatment.Taxed)
                        {
                            _omisiones.Add($"Línea {linea.Numero}, {impuesto.Code}: el producto se vende {(linea.TratamientoIvaVenta == VatSaleTreatment.Exempt ? "exento" : "excluido")}.");
                            continue;
                        }
                        if (!Sujeto.IsVatResponsible)
                        {
                            _omisiones.Add($"Línea {linea.Numero}, {impuesto.Code}: el vendedor no es responsable de IVA.");
                            continue;
                        }
                    }

                    var candidatas = foto.Tarifas.Where(t => t.TaxDefinitionId == impuesto.Id
                            && t.VigenteEn(entrada.Fecha)
                            && t.AplicaA(entrada.Perspectiva)
                            && (vinculo.TaxRateCode is null || string.Equals(t.Code, vinculo.TaxRateCode, StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                    var elegida = Elegir(candidatas, $"la línea {linea.Numero} ({impuesto.Code})");
                    if (elegida is null)
                    {
                        if (vinculo.TaxRateCode is not null && !_rechazos.Any(r => r.Codigo == CodigoAmbiguo))
                            _rechazos.Add(new RechazoTributario(CodigoTarifaNoVigente,
                                $"La tarifa {vinculo.TaxRateCode} de {impuesto.Code} no está vigente al {entrada.Fecha:dd/MM/yyyy} para {(EnCompras ? "compras" : "ventas")} (línea {linea.Numero})."));
                        continue;
                    }

                    AgregarImpuestoDeLinea(linea, vinculo, impuesto, elegida);
                }
            }
        }

        private void AgregarImpuestoDeLinea(LineaTributaria linea, ImpuestoDeLinea vinculo, ImpuestoEnFoto impuesto, TarifaEnFoto tarifa)
        {
            var explicacion = new ExplicacionTributaria()
                .Nota("Fecha", entrada.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Nota("Tarifa", $"{tarifa.Code} vigente desde {tarifa.ValidFrom:yyyy-MM-dd} ({tarifa.LegalSource})");

            decimal baseGravable = linea.Base, valor;
            decimal? unidades = null;
            switch (impuesto.CalculationForm)
            {
                case TaxCalculationForm.AmountPerUnit:
                    var porUnidad = tarifa.AmountPerUnit ?? 0;
                    var factor = vinculo.TaxableUnitsPerBaseUnit ?? 1;
                    unidades = linea.UnidadesBase * factor;
                    valor = Redondear(unidades.Value * porUnidad);
                    explicacion.Paso("Unidades base", linea.UnidadesBase)
                        .Paso("Unidades gravables por unidad base", factor)
                        .Paso("Unidades gravables", unidades.Value)
                        .Paso("Valor por unidad", porUnidad)
                        .Paso("Valor", valor);
                    explicacion.Resumen = $"{unidades.Value.ToString("0.####", CultureInfo.InvariantCulture)} unidades × {Pesos(porUnidad)} = {Pesos(valor)}";
                    break;

                case TaxCalculationForm.PercentOfTax:
                    baseGravable = _renglones.Where(r => r.Linea == linea.Numero && r.TaxDefinitionId == impuesto.TaxedOnDefinitionId).Sum(r => r.Amount);
                    valor = Redondear(baseGravable * (tarifa.Rate ?? 0));
                    explicacion.Paso("Base (impuesto de la línea)", baseGravable).Paso("Tarifa", tarifa.Rate ?? 0).Paso("Valor", valor);
                    explicacion.Resumen = $"{Pesos(baseGravable)} × {Porcentaje(tarifa.Rate ?? 0)} = {Pesos(valor)}";
                    break;

                default:
                    valor = Redondear(linea.Base * (tarifa.Rate ?? 0));
                    explicacion.Paso("Base", linea.Base).Paso("Tarifa", tarifa.Rate ?? 0).Paso("Valor", valor);
                    explicacion.Resumen = $"{Pesos(linea.Base)} × {Porcentaje(tarifa.Rate ?? 0)} = {Pesos(valor)}";
                    break;
            }

            TaxTreatment tratamiento;
            if (EnCompras)
            {
                tratamiento = IvaDescontable.Determinar(impuesto.Kind, Agente.IsVatResponsible, entrada.TipoIvaNoDescontable, linea.TratamientoIvaVenta);
                explicacion.Nota("Tratamiento", IvaDescontable.Explicar(impuesto.Kind, Agente.IsVatResponsible, entrada.TipoIvaNoDescontable, linea.TratamientoIvaVenta));
            }
            else
            {
                tratamiento = TaxTreatment.Generated;
                explicacion.Nota("Tratamiento", "Impuesto generado en la venta.");
            }

            _renglones.Add(new RenglonTributario(linea.Numero, impuesto.Id, tarifa.Id, tarifa.Code, impuesto.Kind, tratamiento,
                null, tarifa.MunicipalityDaneCode, tarifa.Rate, tarifa.AmountPerUnit, unidades, baseGravable, valor, impuesto.DianTaxCode, explicacion));
        }

        // --------------------------------------------------------------------------------------- retenciones --

        public void Retenciones()
        {
            foreach (var impuesto in foto.Impuestos.Where(i => i.IsWithholding && i.IsActive).OrderBy(i => i.Id))
            {
                var tarifas = foto.Tarifas
                    .Where(t => t.TaxDefinitionId == impuesto.Id && t.VigenteEn(entrada.Fecha) && t.AplicaA(entrada.Perspectiva))
                    .ToList();
                if (tarifas.Count == 0) continue;

                if (impuesto.CalculationForm == TaxCalculationForm.PercentOfTax)
                    RetencionSobreImpuesto(impuesto, tarifas);
                else if (impuesto.Kind == TaxKind.ReteIca)
                    ReteIca(impuesto, tarifas);
                else
                    RetencionPorConcepto(impuesto, tarifas);
            }
        }

        private void RetencionSobreImpuesto(ImpuestoEnFoto impuesto, List<TarifaEnFoto> tarifas)
        {
            if (Sujeto.WithholdingExempt) { _omisiones.Add($"{impuesto.Code}: el vendedor no está sujeto a retención."); return; }
            var retiene = impuesto.Kind == TaxKind.ReteIva ? Agente.RetieneIva : Agente.EsAgenteDeRetencion;
            if (!retiene) { _omisiones.Add($"{impuesto.Code}: el comprador no es agente de esta retención."); return; }

            var gravados = _renglones.Where(r => !r.EsRetencion && r.TaxDefinitionId == impuesto.TaxedOnDefinitionId && r.Amount != 0).ToList();
            if (gravados.Count == 0) return;
            var baseImpuesto = gravados.Sum(r => r.Amount);
            var lineas = gravados.Select(r => r.Linea).ToHashSet();
            var baseOperacion = entrada.Lineas.Where(l => lineas.Contains(l.Numero)).Sum(l => l.Base);
            Retener(impuesto, null, null, tarifas, baseImpuesto, baseOperacion);
        }

        private void RetencionPorConcepto(ImpuestoEnFoto impuesto, List<TarifaEnFoto> tarifas)
        {
            if (Sujeto.WithholdingExempt) { _omisiones.Add($"{impuesto.Code}: el vendedor no está sujeto a retención."); return; }
            if (!Agente.EsAgenteDeRetencion) { _omisiones.Add($"{impuesto.Code}: el comprador no es agente de retención."); return; }

            foreach (var grupo in entrada.Lineas.Where(l => l.ConceptoDeRetencionId is not null).GroupBy(l => l.ConceptoDeRetencionId!.Value).OrderBy(g => g.Key))
            {
                var candidatas = tarifas.Where(t => t.WithholdingConceptId == grupo.Key).ToList();
                if (candidatas.Count == 0) continue;
                var baseOperacion = grupo.Sum(l => l.Base);
                Retener(impuesto, grupo.Key, null, candidatas, baseOperacion, baseOperacion);
            }
        }

        private void ReteIca(ImpuestoEnFoto impuesto, List<TarifaEnFoto> tarifas)
        {
            if (Sujeto.IcaWithholdingExempt) { _omisiones.Add($"{impuesto.Code}: el vendedor no está sujeto a retención de ICA."); return; }
            if (!Agente.EsAgenteDeRetencion) { _omisiones.Add($"{impuesto.Code}: el comprador no es agente de retención."); return; }
            if (entrada.MunicipioDane is not { Length: > 0 } municipio) { _omisiones.Add($"{impuesto.Code}: la operación no tiene municipio."); return; }

            var delMunicipio = tarifas.Where(t => string.Equals(t.MunicipalityDaneCode, municipio, StringComparison.Ordinal)).ToList();
            var exactas = delMunicipio.Where(t => Sujeto.CiiuCode is { Length: > 0 } && string.Equals(t.ActivityCode, Sujeto.CiiuCode, StringComparison.Ordinal)).ToList();
            var porActividad = exactas.Count > 0
                ? exactas
                : delMunicipio.Where(t => t.ActivityCode is null || t.ActivityCode == ActividadGeneral).ToList();
            if (porActividad.Count == 0) { _omisiones.Add($"{impuesto.Code}: no hay tarifa para el municipio {municipio}."); return; }

            var conceptosConTarifa = porActividad.Where(t => t.WithholdingConceptId is not null).Select(t => t.WithholdingConceptId!.Value).ToHashSet();
            var grupos = entrada.Lineas
                .GroupBy(l => l.ConceptoDeRetencionId is { } c && conceptosConTarifa.Contains(c) ? c : (int?)null)
                .OrderBy(g => g.Key ?? 0);
            foreach (var grupo in grupos)
            {
                var candidatas = porActividad.Where(t => t.WithholdingConceptId == grupo.Key).ToList();
                if (candidatas.Count == 0) continue;
                var baseOperacion = grupo.Sum(l => l.Base);
                Retener(impuesto, grupo.Key, municipio, candidatas, baseOperacion, baseOperacion,
                    exactas.Count > 0 ? $"actividad {Sujeto.CiiuCode}" : $"tarifa general del municipio (fila {ActividadGeneral})");
            }
        }

        private void Retener(ImpuestoEnFoto impuesto, int? concepto, string? municipio, List<TarifaEnFoto> candidatas,
            decimal baseDeCalculo, decimal baseDeOperacion, string? actividad = null)
        {
            var elegida = Elegir(candidatas, $"{impuesto.Code}{(concepto is { } c ? $" (concepto {foto.Concepto(c)?.Code ?? c.ToString(CultureInfo.InvariantCulture)})" : string.Empty)}");
            if (elegida is null)
            {
                _omisiones.Add($"{impuesto.Code}: ninguna tarifa cumple las condiciones de las partes.");
                return;
            }
            if (elegida.Rate is not { } tarifa) return;

            var explicacion = new ExplicacionTributaria()
                .Nota("Fecha", entrada.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Nota("Tarifa", $"{elegida.Code} vigente desde {elegida.ValidFrom:yyyy-MM-dd} ({elegida.LegalSource})")
                .Nota("Condición que decidió", Condicion(elegida));
            if (actividad is not null) explicacion.Nota("Actividad", actividad);

            decimal minimo = 0;
            if (elegida.MinimumBaseUvt is { } enUvt)
            {
                minimo = ConversionUvt.APesos(foto.Uvt, enUvt, foto.RedondeoUvt);
                explicacion.Paso("UVT", foto.Uvt).Paso("Base mínima en UVT", enUvt).Nota("Redondeo", foto.RedondeoUvt.ToString());
            }
            else if (elegida.MinimumBasePesos is { } enPesos)
            {
                minimo = enPesos;
            }
            explicacion.Paso("Base mínima en pesos", minimo).Paso("Base de la operación", baseDeOperacion);

            if (!ConversionUvt.Procede(baseDeOperacion, minimo))
            {
                _omisiones.Add($"{impuesto.Code} {elegida.Code}: la base {Pesos(baseDeOperacion)} es menor que el mínimo {Pesos(minimo)}.");
                return;
            }
            explicacion.Nota("Procede", "La base es igual o superior al mínimo.");

            var valor = Redondear(baseDeCalculo * tarifa);
            explicacion.Paso("Base", baseDeCalculo).Paso("Tarifa", tarifa).Paso("Valor", valor);
            explicacion.Resumen = $"{Pesos(baseDeCalculo)} × {Porcentaje(tarifa)} = {Pesos(valor)}";

            _renglones.Add(new RenglonTributario(null, impuesto.Id, elegida.Id, elegida.Code, impuesto.Kind, TratamientoDeRetencion,
                concepto, municipio, tarifa, null, null, baseDeCalculo, valor, impuesto.DianTaxCode, explicacion));
        }

        private TaxTreatment TratamientoDeRetencion => EnCompras ? TaxTreatment.WithholdingApplied : TaxTreatment.WithholdingSuffered;

        // --------------------------------------------------------------------------------------- selección --

        /// <summary>
        /// La tarifa que gana entre las candidatas cuyas condiciones se cumplen: mayor prioridad y, a igual prioridad, más
        /// condiciones; un empate después de eso es <see cref="CodigoAmbiguo"/>, nombrando las dos.
        /// </summary>
        private TarifaEnFoto? Elegir(List<TarifaEnFoto> candidatas, string donde)
        {
            var cumplen = candidatas.Where(t => t.Condiciones.SeCumplen(Sujeto, Agente)).ToList();
            if (cumplen.Count == 0) return null;

            var prioridad = cumplen.Max(t => t.Priority);
            var primeras = cumplen.Where(t => t.Priority == prioridad).ToList();
            var especificidad = primeras.Max(t => t.Condiciones.Cantidad);
            var ganadoras = primeras.Where(t => t.Condiciones.Cantidad == especificidad).OrderBy(t => t.Code, StringComparer.Ordinal).ToList();
            if (ganadoras.Count > 1)
            {
                _rechazos.Add(new RechazoTributario(CodigoAmbiguo,
                    $"En {donde} empatan las tarifas {ganadoras[0].Code} y {ganadoras[1].Code}: misma prioridad ({prioridad}) y el mismo número de condiciones. Ajuste la prioridad de una de ellas."));
                return null;
            }
            return ganadoras[0];
        }

        private static string Condicion(TarifaEnFoto tarifa)
        {
            var condiciones = tarifa.Condiciones.Lista();
            return condiciones.Count == 0
                ? $"sin condiciones (prioridad {tarifa.Priority})"
                : $"{string.Join(", ", condiciones)} (prioridad {tarifa.Priority})";
        }

        // --------------------------------------------------------------------------- notas y devoluciones --

        public void ConLaFotoDelOriginal(IReadOnlyList<RenglonTributario> original)
        {
            foreach (var linea in entrada.Lineas.OrderBy(l => l.Numero))
            {
                var numeroOriginal = linea.LineaOriginal ?? linea.Numero;
                foreach (var r in original.Where(r => r.Linea == numeroOriginal))
                {
                    var explicacion = new ExplicacionTributaria()
                        .Nota("Foto del original", $"{r.TaxRateCode} de la línea {numeroOriginal} del documento original");
                    decimal valor;
                    decimal? unidades = null;
                    if (r.AmountPerUnit is { } porUnidad)
                    {
                        var factor = linea.Impuestos.FirstOrDefault(i => i.TaxDefinitionId == r.TaxDefinitionId)?.TaxableUnitsPerBaseUnit ?? 1;
                        unidades = linea.UnidadesBase * factor;
                        valor = Redondear(unidades.Value * porUnidad);
                        explicacion.Paso("Unidades gravables", unidades.Value).Paso("Valor por unidad", porUnidad).Paso("Valor", valor);
                        explicacion.Resumen = $"{unidades.Value.ToString("0.####", CultureInfo.InvariantCulture)} unidades × {Pesos(porUnidad)} = {Pesos(valor)}";
                    }
                    else
                    {
                        var tarifa = r.Rate ?? 0;
                        valor = Redondear(linea.Base * tarifa);
                        explicacion.Paso("Base", linea.Base).Paso("Tarifa", tarifa).Paso("Valor", valor);
                        explicacion.Resumen = $"{Pesos(linea.Base)} × {Porcentaje(tarifa)} = {Pesos(valor)}";
                    }
                    _renglones.Add(r with
                    {
                        Linea = linea.Numero,
                        TaxableUnits = unidades,
                        Base = linea.Base,
                        Amount = valor,
                        Explicacion = explicacion,
                    });
                }
            }

            foreach (var r in original.Where(r => r.Linea is null))
            {
                var impuesto = foto.Impuesto(r.TaxDefinitionId);
                decimal baseDeCalculo;
                if (impuesto?.CalculationForm == TaxCalculationForm.PercentOfTax)
                {
                    baseDeCalculo = _renglones.Where(n => !n.EsRetencion && n.TaxDefinitionId == impuesto.TaxedOnDefinitionId).Sum(n => n.Amount);
                }
                else
                {
                    baseDeCalculo = entrada.Lineas
                        .Where(l => r.WithholdingConceptId is null || l.ConceptoDeRetencionId == r.WithholdingConceptId)
                        .Sum(l => l.Base);
                }
                if (baseDeCalculo == 0) continue;

                var tarifa = r.Rate ?? 0;
                var valor = Redondear(baseDeCalculo * tarifa);
                var explicacion = new ExplicacionTributaria()
                    .Nota("Foto del original", $"{r.TaxRateCode} del documento original")
                    .Nota("Base mínima", "Sin volver a probar la base mínima: la nota usa la foto del original.")
                    .Paso("Base", baseDeCalculo).Paso("Tarifa", tarifa).Paso("Valor", valor);
                explicacion.Resumen = $"{Pesos(baseDeCalculo)} × {Porcentaje(tarifa)} = {Pesos(valor)}";
                _renglones.Add(r with { Base = baseDeCalculo, Amount = valor, Explicacion = explicacion });
            }
        }
    }
}
