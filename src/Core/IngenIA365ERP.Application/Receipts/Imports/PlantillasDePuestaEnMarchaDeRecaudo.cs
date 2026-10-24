using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Imports;

namespace IngenIA365ERP.Application.Receipts.Imports;

/// <summary>
/// Las plantillas de puesta en marcha de Recaudo (feature 015, US13, T0287; contracts/archivos-y-plantillas.md §5; FR-157): una hoja «Datos» con los encabezados
/// en la fila 1 y la hoja «Instrucciones» que arma la API con estas mismas columnas. Todas escriben en las tablas reales y <b>ninguna emite hechos</b> (FR-158). Las
/// cifras de referencia (FR-160) tienen la suya. Ninguna nombra al sistema de origen. (nuevo)
/// </summary>
public static class PlantillasDePuestaEnMarchaDeRecaudo
{
    public const string PuntosYCajas = "recaudo.puntos-y-cajas";
    public const string CajerosYAlcances = "recaudo.cajeros-y-alcances";
    public const string MediosPorPunto = "recaudo.medios-por-punto";
    public const string BovedasAlCorte = "recaudo.bovedas-al-corte";
    public const string PartidasAlCorte = "recaudo.partidas-al-corte";
    public const string ChequesEnCanje = "recaudo.cheques-en-canje";
    public const string CifrasDeReferencia = "recaudo.cifras-de-referencia";

    // Columnas (camelCase español, §0 de la plataforma de la 012).
    public const string Oficina = "oficina";
    public const string Punto = "punto";
    public const string NombrePunto = "nombrePunto";
    public const string Caja = "caja";
    public const string NombreCaja = "nombreCaja";
    public const string Base = "base";
    public const string Modo = "modo";
    public const string TopeDeAviso = "topeDeAviso";
    public const string TopeDeBloqueo = "topeDeBloqueo";
    public const string CajaPrincipal = "cajaPrincipal";
    public const string VigenteDesde = "vigenteDesde";
    public const string Documento = "documento";
    public const string Rol = "rol";
    public const string Medio = "medio";
    public const string FechaDeCorte = "fechaDeCorte";
    public const string Denominacion = "denominacion";
    public const string Cantidad = "cantidad";
    public const string SaldoDeclarado = "saldoDeclarado";
    public const string CuentaPropia = "cuentaPropia";
    public const string Fecha = "fecha";
    public const string Valor = "valor";
    public const string Referencia = "referencia";
    public const string Consignante = "consignante";
    public const string DocumentoConsignante = "documentoConsignante";
    public const string Banco = "banco";
    public const string Numero = "numero";
    public const string Cuenta = "cuenta";
    public const string Girador = "girador";
    public const string Pagador = "pagador";
    public const string Estado = "estado";
    public const string MismaPlaza = "mismaPlaza";
    public const string Concepto = "concepto";
    public const string Dimension = "dimension";

    private static DefinicionDePlantilla Una(string clave, string nombre, params ColumnaDePlantilla[] columnas) =>
        new(clave, nombre, ModuloDeAuditoria.Receipts, [new HojaDePlantilla("Datos", columnas)]);

    public static IReadOnlyDictionary<string, DefinicionDePlantilla> Todas { get; } = new Dictionary<string, DefinicionDePlantilla>(StringComparer.OrdinalIgnoreCase)
    {
        [PuntosYCajas] = Una(PuntosYCajas, "Puntos de recaudo y cajas",
            new(Oficina, TipoDeValor.Sucursal, Obligatoria: true, Reglas: "oficina existente (código o nombre)", Ejemplo: "001"),
            new(Punto, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Reglas: "código del punto; varias filas del mismo punto, una por caja", Ejemplo: "PTO01"),
            new(NombrePunto, TipoDeValor.Texto, Obligatoria: true, Largo: 150, Ejemplo: "Oficina principal"),
            new(Caja, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Reglas: "código único de la caja", Ejemplo: "CAJA01"),
            new(NombreCaja, TipoDeValor.Texto, Obligatoria: true, Largo: 150, Ejemplo: "Caja 1"),
            new(Base, TipoDeValor.Monto, Reglas: "base diaria o fondo fijo; vacío = 0", Ejemplo: "500000"),
            new(Modo, TipoDeValor.Enumeracion, Reglas: "DailyBase (base diaria) o FixedFund (fondo fijo); vacío = el parámetro Caja.BaseModo", Ejemplo: "DailyBase"),
            new(TopeDeAviso, TipoDeValor.Monto, Reglas: "efectivo en caja que avisa"),
            new(TopeDeBloqueo, TipoDeValor.Monto, Reglas: "efectivo en caja que bloquea; mayor o igual que el de aviso"),
            new(CajaPrincipal, TipoDeValor.SiNo, Reglas: "la caja principal de la oficina"),
            new(VigenteDesde, TipoDeValor.Fecha, Reglas: "vacío = hoy")),
        [CajerosYAlcances] = Una(CajerosYAlcances, "Cajeros y alcances",
            new(Documento, TipoDeValor.Persona, Obligatoria: true, Largo: 20, Reglas: "documento de la persona del usuario (tiene que tener usuario)", Ejemplo: "1098765432"),
            new(Rol, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "Cajero (exige caja), Supervisor o Analista (exigen punto)", Ejemplo: "Cajero"),
            new(Caja, TipoDeValor.Codigo, Largo: 10, Reglas: "la caja del cajero"),
            new(Punto, TipoDeValor.Codigo, Largo: 10, Reglas: "el punto del supervisor o del analista; con caja, debe ser el de la caja"),
            new(VigenteDesde, TipoDeValor.Fecha, Reglas: "vacío = hoy")),
        [MediosPorPunto] = Una(MediosPorPunto, "Medios de pago por punto",
            new(Punto, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Ejemplo: "PTO01"),
            new(Medio, TipoDeValor.Codigo, Obligatoria: true, Largo: 20, Reglas: "medio de pago activo de una clase admitida en esta entrega (efectivo, transferencia, consignación, cheque)", Ejemplo: "EFE"),
            new(VigenteDesde, TipoDeValor.Fecha, Reglas: "vacío = hoy")),
        [BovedasAlCorte] = Una(BovedasAlCorte, "Bóvedas al corte",
            new(Oficina, TipoDeValor.Sucursal, Obligatoria: true, Reglas: "oficina con bóveda configurada"),
            new(FechaDeCorte, TipoDeValor.Fecha, Obligatoria: true, Reglas: "la misma en todas las filas de la oficina"),
            new(Denominacion, TipoDeValor.Monto, Obligatoria: true, Reglas: "valor de un billete o moneda vigente", Ejemplo: "50000"),
            new(Cantidad, TipoDeValor.Entero, Obligatoria: true, Reglas: "mayor que cero", Ejemplo: "120"),
            new(SaldoDeclarado, TipoDeValor.Monto, Obligatoria: true, Reglas: "el saldo de la bóveda al corte; la suma de denominación × cantidad de la oficina tiene que darlo")),
        [PartidasAlCorte] = Una(PartidasAlCorte, "Partidas por identificar al corte",
            new(CuentaPropia, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Reglas: "código de una cuenta bancaria propia de recaudo"),
            new(Oficina, TipoDeValor.Sucursal, Obligatoria: true),
            new(Fecha, TipoDeValor.Fecha, Obligatoria: true, Reglas: "fecha del abono; sin fecha la fila se rechaza"),
            new(Valor, TipoDeValor.Monto, Obligatoria: true, Reglas: "mayor que cero"),
            new(Referencia, TipoDeValor.Texto, Largo: 100),
            new(Consignante, TipoDeValor.Texto, Largo: 200),
            new(DocumentoConsignante, TipoDeValor.Texto, Largo: 20)),
        [ChequesEnCanje] = Una(ChequesEnCanje, "Cheques recibidos en canje",
            new(Banco, TipoDeValor.Banco, Obligatoria: true, Reglas: "banco librado"),
            new(Numero, TipoDeValor.Texto, Obligatoria: true, Largo: 20),
            new(Cuenta, TipoDeValor.Texto, Obligatoria: true, Largo: 30),
            new(Girador, TipoDeValor.Texto, Largo: 200),
            new(Pagador, TipoDeValor.Persona, Obligatoria: true, Largo: 20, Reglas: "documento de quien entregó el cheque"),
            new(Valor, TipoDeValor.Monto, Obligatoria: true),
            new(Fecha, TipoDeValor.Fecha, Obligatoria: true, Reglas: "fecha de emisión del cheque"),
            new(Estado, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "InCustody, Deposited o InClearing"),
            new(Oficina, TipoDeValor.Sucursal, Obligatoria: true),
            new(MismaPlaza, TipoDeValor.SiNo, Reglas: "vacío = sí")),
        [CifrasDeReferencia] = Una(CifrasDeReferencia, "Cifras de referencia de Recaudo",
            new(FechaDeCorte, TipoDeValor.Fecha, Obligatoria: true),
            new(Concepto, TipoDeValor.Codigo, Obligatoria: true, Largo: 40, Reglas: "Boveda, PartidasPorIdentificar, ChequesEnCanje o SaldoAFavorDePagadores (o un concepto propio para el paralelo)"),
            new(Dimension, TipoDeValor.Codigo, Obligatoria: true, Largo: 40, Reglas: "el código de la oficina"),
            new(Valor, TipoDeValor.Monto, Obligatoria: true)),
    };

    public static DefinicionDePlantilla? Por(string codigo) => Todas.GetValueOrDefault(codigo);
}
