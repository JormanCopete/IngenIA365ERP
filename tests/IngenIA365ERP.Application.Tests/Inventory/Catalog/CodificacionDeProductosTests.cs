using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Catalog;
using IngenIA365ERP.Application.Inventory.Catalog.Products;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Catalog;

/// <summary>
/// La codificación automática del código de los productos (decisión del dueño del 2026-11-12, <see cref="CodificacionDeProductos"/>):
/// grupo = código de la categoría, subgrupo = número de la marca dentro de la categoría (asignado la primera vez), consecutivo =
/// último usado del prefijo + 1 contando los dados de baja. Con el parámetro en «Manual» (el defecto) todo sigue como antes.
/// </summary>
public class CodificacionDeProductosTests
{
    private readonly CatalogoDePrueba _c;
    private readonly ProductCategory _aceites;
    private readonly ProductCategory _baterias;
    private readonly Brand _castrol;
    private readonly Brand _mobil;
    private readonly Brand _shell;
    private readonly Brand _varta;

    public CodificacionDeProductosTests()
    {
        _c = CatalogoDePrueba.CrearAsync().GetAwaiter().GetResult();
        _aceites = new ProductCategory { Code = "01", Name = "Aceites", Level = 1, IsActive = true };
        _baterias = new ProductCategory { Code = "08", Name = "Baterías", Level = 1, IsActive = true };
        _castrol = new Brand { Code = "CASTROL", Name = "Castrol", IsActive = true };
        _mobil = new Brand { Code = "MOBIL", Name = "Mobil", IsActive = true };
        _shell = new Brand { Code = "SHELL", Name = "Shell", IsActive = true };
        _varta = new Brand { Code = "VARTA", Name = "Varta", IsActive = true };
        _c.Db.AddRange(_aceites, _baterias, _castrol, _mobil, _shell, _varta);
        _c.Db.SaveChanges();
    }

    private CodificacionDeProductos Codificacion() => new(_c.Db, new LectorDeParametros(_c.Db), Substitute.For<ICerrojoPorClave>());

    private void Parametro(string clave, string valor)
    {
        _c.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeInventario.Modulo, Key = clave, ScopeKind = ParameterScopeKind.None, ScopeId = 0, Value = valor, ValidFrom = CatalogoDePrueba.Hoy.AddDays(-1), Reason = "prueba",
        });
        _c.Db.SaveChanges();
    }

    private void Automatica() => Parametro(ParametrosDeInventario.ProductosCodificacion, ParametrosDeInventario.CodificacionAutomatica);

    private CreateProductCommand Alta(ProductCategory categoria, Brand? marca, string codigo = "", string nombre = "Aceite") =>
        _c.Alta(codigo: codigo, nombre: nombre, marca: marca?.PublicId) with { CategoryPublicId = categoria.PublicId };

    private Task<Result<ProductDto>> CrearAsync(CreateProductCommand comando) =>
        new CreateProductCommandHandler(_c.Db, _c.Reloj, Codificacion()).Handle(comando, default);

    private async Task<string> CodigoAsync(ProductCategory categoria, Brand? marca)
    {
        var r = await CrearAsync(Alta(categoria, marca));
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value.Code;
    }

    /// <summary>Un producto que ya existe con su código (cargado por plantilla, por ejemplo al recodificar), sin pasar por la codificación.</summary>
    private void Existente(string codigo, ProductCategory categoria, Brand? marca)
    {
        _c.Db.Products.Add(new Product
        {
            Code = codigo, Name = codigo, CategoryId = categoria.Id, BrandId = marca?.Id, BaseUnitId = _c.Unidad("UND").Id,
            AccountingGroupId = _c.GrupoAbarrotes.Id, SearchText = codigo,
        });
        _c.Db.SaveChanges();
    }

    [Fact]
    public async Task Con_la_codificacion_manual_el_codigo_es_obligatorio()
    {
        var r = await CrearAsync(Alta(_aceites, _castrol));

        r.Error.Code.Should().Be("Inventory.Product.CodeRequired");
    }

    [Fact]
    public async Task Con_la_codificacion_manual_el_codigo_es_el_digitado()
    {
        var r = await CrearAsync(Alta(_aceites, _castrol, codigo: "abc-1"));

        r.Value.Code.Should().Be("ABC-1");
        (await _c.ContarAsync<ProductCodeSubgroup>()).Should().Be(0);
    }

    [Fact]
    public async Task El_codigo_es_categoria_mas_numero_de_la_marca_mas_consecutivo()
    {
        Automatica();

        (await CodigoAsync(_aceites, _castrol)).Should().Be("0101001");
        (await CodigoAsync(_aceites, _castrol)).Should().Be("0101002");
        (await CodigoAsync(_aceites, _mobil)).Should().Be("0102001");
        (await CodigoAsync(_aceites, _castrol)).Should().Be("0101003");
    }

    [Fact]
    public async Task La_numeracion_de_las_marcas_empieza_de_nuevo_en_cada_categoria()
    {
        Automatica();

        (await CodigoAsync(_aceites, _castrol)).Should().Be("0101001");
        (await CodigoAsync(_baterias, _varta)).Should().Be("0801001");
        (await CodigoAsync(_baterias, _castrol)).Should().Be("0802001");

        var numeros = await _c.Db.ProductCodeSubgroups.AsNoTracking()
            .Select(s => new { s.CategoryId, s.BrandId, s.Segment }).ToListAsync();
        numeros.Should().BeEquivalentTo(new[]
        {
            new { CategoryId = _aceites.Id, BrandId = _castrol.Id, Segment = "01" },
            new { CategoryId = _baterias.Id, BrandId = _varta.Id, Segment = "01" },
            new { CategoryId = _baterias.Id, BrandId = _castrol.Id, Segment = "02" },
        });
    }

    [Fact]
    public async Task Un_codigo_de_un_producto_dado_de_baja_no_se_reutiliza()
    {
        Automatica();
        await CodigoAsync(_aceites, _castrol);
        var segundo = await CodigoAsync(_aceites, _castrol);
        var producto = _c.Db.Products.Single(p => p.Code == segundo);
        producto.IsDeleted = true;
        await _c.Db.SaveChangesAsync();

        (await CodigoAsync(_aceites, _castrol)).Should().Be("0101003", "el consecutivo es el último usado más uno, no la cantidad de vivos más uno");
    }

    [Fact]
    public async Task La_marca_adopta_el_numero_que_ya_usan_sus_productos_y_sigue_su_consecutivo()
    {
        Automatica();
        Existente("0101012", _aceites, _castrol);
        Existente("0102021", _aceites, _mobil);
        Existente("0105001", _aceites, null);

        (await CodigoAsync(_aceites, _castrol)).Should().Be("0101013");
        (await CodigoAsync(_aceites, _mobil)).Should().Be("0102022");
        (await CodigoAsync(_aceites, _shell)).Should().Be("0106001", "el siguiente al mayor número usado en la categoría, aunque lo use un producto sin marca");
    }

    [Fact]
    public async Task Un_codigo_digitado_con_la_codificacion_automatica_se_rechaza()
    {
        Automatica();

        var r = await CrearAsync(Alta(_aceites, _castrol, codigo: "0101999"));

        r.Error.Code.Should().Be("Inventory.Product.CodeIsAutomatic");
    }

    [Fact]
    public async Task Sin_marca_no_se_puede_armar_el_codigo()
    {
        Automatica();

        var r = await CrearAsync(Alta(_aceites, null));

        r.Error.Code.Should().Be("Inventory.Product.CodeNeedsBrand");
        (await _c.ContarAsync<Product>()).Should().Be(0);
    }

    [Fact]
    public async Task Los_digitos_son_parametros_y_el_prefijo_lleno_se_rechaza()
    {
        Automatica();
        Parametro(ParametrosDeInventario.ProductosDigitosDelSubgrupo, "1");
        Parametro(ParametrosDeInventario.ProductosDigitosDelConsecutivo, "1");
        for (var i = 1; i <= 9; i++) (await CodigoAsync(_aceites, _castrol)).Should().Be($"011{i}");

        var r = await CrearAsync(Alta(_aceites, _castrol));

        r.Error.Code.Should().Be("Inventory.Product.CodeSequenceFull");
    }

    [Fact]
    public async Task La_propuesta_no_escribe_y_avisa_que_la_marca_recibira_numero()
    {
        Automatica();
        Existente("0101012", _aceites, _castrol);
        var consulta = new GetProductCodeProposalQueryHandler(_c.Db, Codificacion(), _c.Reloj);

        var propia = (await consulta.Handle(new GetProductCodeProposalQuery(_aceites.PublicId, _castrol.PublicId), default)).Value;
        var nueva = (await consulta.Handle(new GetProductCodeProposalQuery(_aceites.PublicId, _shell.PublicId), default)).Value;
        var sinMarca = (await consulta.Handle(new GetProductCodeProposalQuery(_aceites.PublicId, null), default)).Value;

        propia.Code.Should().Be("0101013");
        nueva.Should().BeEquivalentTo(new { Mode = "Automatica", Code = "0102001", Segment = "02", SegmentIsNew = true });
        nueva.Message.Should().Contain("Shell");
        sinMarca.Code.Should().BeNull();
        sinMarca.Message.Should().Contain("marca");
        (await _c.ContarAsync<ProductCodeSubgroup>()).Should().Be(0);
    }

    [Fact]
    public async Task Con_la_codificacion_manual_la_propuesta_solo_dice_el_modo()
    {
        var r = await new GetProductCodeProposalQueryHandler(_c.Db, Codificacion(), _c.Reloj)
            .Handle(new GetProductCodeProposalQuery(_aceites.PublicId, _castrol.PublicId), default);

        r.Value.Should().BeEquivalentTo(new { Mode = "Manual", Code = (string?)null, SegmentIsNew = false });
    }

    [Fact]
    public async Task Cambiar_la_marca_despues_no_cambia_el_codigo_ni_el_numero_de_la_pareja()
    {
        Automatica();
        var r = await CrearAsync(Alta(_aceites, _castrol));
        var producto = _c.Producto(r.Value.PublicId);
        producto.BrandId = _mobil.Id;
        await _c.Db.SaveChangesAsync();

        producto.Code.Should().Be("0101001");
        (await CodigoAsync(_aceites, _castrol)).Should().Be("0101002");
        (await CodigoAsync(_aceites, _mobil)).Should().Be("0102001");
    }
}
