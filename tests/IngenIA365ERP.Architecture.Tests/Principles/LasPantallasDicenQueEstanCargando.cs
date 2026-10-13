using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Toda pantalla que espera datos lo dice: la grilla o el formulario que está cargando o
/// guardando va dentro de <c>&lt;IndicadorDeCarga&gt;</c> (docs/manual/indicador-de-carga.md).
/// Hasta el 2026-09-13 una grilla vacía y una grilla cargando se veían igual y «Guardar»
/// admitía el doble clic; se corrigió en Core y Nómina y esta prueba impide que una pantalla
/// nueva o modificada de esos módulos vuelva atrás. Cuando otro módulo se migre, se agrega
/// su carpeta a <see cref="ModulosMigrados"/> y la prueba lo cubre desde ese momento.
///
/// <para>
/// Es una prueba sobre el fuente porque el repositorio no tiene pruebas de navegador: un
/// spinner que falta no rompe ninguna prueba de unidad, sólo la experiencia de la persona
/// que mira una tabla en blanco sin saber si es que no hay datos o es que todavía no
/// llegaron.
/// </para>
/// </summary>
public class LasPantallasDicenQueEstanCargando
{
    /// <summary>Carpetas bajo <c>Shared/Pages</c> ya migradas al patrón. Se amplía módulo a módulo.</summary>
    /// Feature 012 (T118, decisiones-transversales §2.11): «Inventario» entra con sus primeras pantallas (informes y tipos
    /// de documento); «Ventas» entra con Canales (US1, T239); «Compras» y «Pos» los agrega la historia que crea la primera
    /// pantalla de cada carpeta.
    /// US9 (T358): «Compras» entra con sus pantallas de I1 (recepciones, compra directa, facturas, notas y devoluciones).
    /// Feature 013, fase 2 (T0038, T0095): «Administracion» y «Compliance» entran con las seis pantallas de la plataforma de Cartera
    /// (valores nacionales, perfil regulatorio, canales externos, participantes, listas restrictivas, solicitudes del titular).
    /// Feature 013, fase 3 (T0188): «AhorrosYAportes» entra con las 18 pantallas conservadas de la cartera heredada.
    /// Feature 013, fase 4 (T0324): «Cartera» entra con las pantallas de gobierno, configuración y mensajes al deudor.
    private static readonly string[] ModulosMigrados =
        ["Maestros", "Nomina", "Asociados", "Contabilidad", "Inventario", "Ventas", "Compras", "Pos", "Administracion", "Compliance",
            "AhorrosYAportes", "Cartera"];

    /// <summary>Feature 013 (T0038): las pantallas de la fase 2 llevan su indicador aunque no usen <c>SfGrid</c>.</summary>
    private static readonly string[] PantallasDeLaPlataformaDeCartera =
    [
        "Administracion/ValoresNacionales.razor",
        "Administracion/PerfilRegulatorio.razor",
        "Administracion/CanalesExternos.razor",
        "Administracion/ParticipantesDeIntegracion.razor",
        "Compliance/ListasRestrictivas.razor",
        "Compliance/SolicitudesDelTitular.razor",
        // Feature 013, US2 (T0414): las pantallas de la originación que cargan datos (el simulador lleva su indicador en el componente).
        "Cartera/Solicitudes.razor",
        "Cartera/SolicitudNueva.razor",
        "Cartera/SolicitudDetalle.razor",
        "Cartera/Aprobaciones.razor",
        "Cartera/Cupos.razor",
        "Cartera/Vinculados.razor",
        // US5 (T0526, T0563): la caja de prueba con el recaudador y la tesorería de prueba.
        "Cartera/DoblesDePrueba.razor",
        // US7 (T0611): la integración contable de Cartera, también las que no usan SfGrid.
        "Cartera/ConciliacionContable.razor",
        "Cartera/BandejaDeMensajes.razor",
        "Contabilidad/Cartera/Matriz.razor",
        "Contabilidad/Cartera/TiposDeComprobanteDeCartera.razor",
        "Contabilidad/Cartera/Completitud.razor",
        "Contabilidad/Cartera/Lotes.razor",
        "Contabilidad/Cartera/Lote.razor",
        "Contabilidad/IndicadoresRegulatorios.razor",
        "Contabilidad/Periodos.razor",
    ];

    [Fact]
    public void Las_pantallas_de_la_plataforma_de_Cartera_dicen_que_estan_cargando()
    {
        var pages = Path.Combine(RepoPath.FindRepoRoot(), "src", "Presentation", "IngenIA365ERP.Shared", "Pages");
        foreach (var relativa in PantallasDeLaPlataformaDeCartera)
        {
            var archivo = Path.Combine(pages, relativa.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(archivo), $"No existe {relativa}.");
            var texto = File.ReadAllText(archivo);
            Assert.Contains("<IndicadorDeCarga", texto, StringComparison.Ordinal);
            Assert.Contains("EstadoDeCarga", texto, StringComparison.Ordinal);
            Assert.Contains("<PermissionGate", texto, StringComparison.Ordinal);
            Assert.DoesNotContain("<style", texto, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotMatch(@"#[0-9a-fA-F]{3,6}\b|rgb\(", texto);
            Assert.DoesNotContain("SOLIDO", texto, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Feature 013 (T0188): las 18 pantallas mudadas a <c>AhorrosYAportes</c> cargan o guardan todas, tengan o no grilla; las
    /// dos liquidaciones esperan dentro de <c>ProcessExecutor</c>, que lleva el indicador enlazado a su estado.
    /// </summary>
    [Fact]
    public void Las_18_pantallas_de_ahorros_y_aportes_dicen_que_estan_cargando()
    {
        var raiz = RepoPath.FindRepoRoot();
        var carpeta = Path.Combine(raiz, "src", "Presentation", "IngenIA365ERP.Shared", "Pages", "AhorrosYAportes");
        var pantallas = Directory.EnumerateFiles(carpeta, "*.razor", SearchOption.AllDirectories).ToList();
        Assert.Equal(18, pantallas.Count);

        var ejecutor = File.ReadAllText(Path.Combine(raiz, "src", "Presentation", "IngenIA365ERP.Shared", "Components", "Shared", "ProcessExecutor.razor"));
        Assert.Contains("<IndicadorDeCarga", ejecutor, StringComparison.Ordinal);

        var sinIndicador = pantallas
            .Where(f =>
            {
                var texto = File.ReadAllText(f);
                return !(texto.Contains("<IndicadorDeCarga", StringComparison.Ordinal) && texto.Contains("EstadoDeCarga", StringComparison.Ordinal))
                    && !texto.Contains("<ProcessExecutor", StringComparison.Ordinal);
            })
            .Select(f => Path.GetRelativePath(raiz, f))
            .ToList();
        Assert.True(sinIndicador.Count == 0, "Pantallas de ahorros y aportes sin indicador de carga:\n  " + string.Join("\n  ", sinIndicador));
    }

    private static IEnumerable<string> PantallasMigradas()
    {
        var pages = Path.Combine(RepoPath.FindRepoRoot(), "src", "Presentation", "IngenIA365ERP.Shared", "Pages");
        foreach (var modulo in ModulosMigrados)
        {
            var carpeta = Path.Combine(pages, modulo);
            Assert.True(Directory.Exists(carpeta), $"No existe {carpeta}: si el módulo se movió, actualizá ModulosMigrados.");
            foreach (var f in Directory.EnumerateFiles(carpeta, "*.razor", SearchOption.AllDirectories))
                yield return f;
        }
    }

    [Fact]
    public void Toda_pantalla_con_grilla_o_dialogo_de_edicion_lleva_IndicadorDeCarga()
    {
        var sinIndicador = new List<string>();
        var revisadas = 0;
        foreach (var archivo in PantallasMigradas())
        {
            var texto = File.ReadAllText(archivo);
            var esperaDatos = texto.Contains("<SfGrid", StringComparison.Ordinal)
                           || texto.Contains("<DialogTemplates>", StringComparison.Ordinal);
            if (!esperaDatos) continue;
            revisadas++;
            if (!texto.Contains("<IndicadorDeCarga", StringComparison.Ordinal))
                sinIndicador.Add(Path.GetRelativePath(RepoPath.FindRepoRoot(), archivo));
        }

        Assert.True(revisadas > 0, "No se encontró ninguna pantalla con grilla o diálogo en los módulos migrados: revisar ModulosMigrados.");
        Assert.True(sinIndicador.Count == 0,
            "Pantallas con grilla o diálogo de edición sin <IndicadorDeCarga> (ver docs/manual/indicador-de-carga.md):\n  " +
            string.Join("\n  ", sinIndicador));
    }

    [Fact]
    public void Ninguna_pantalla_migrada_deja_la_lista_vacia_en_silencio()
    {
        // El compañero del indicador: cuando la carga falla, la zona se destapa Y aparece el
        // aviso. Un `catch { _items = []; }` deja una tabla vacía sin explicación (Principio IX).
        var silenciosas = PantallasMigradas()
            .Where(f => File.ReadAllText(f).Contains("catch { _items = ", StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(RepoPath.FindRepoRoot(), f))
            .ToList();

        Assert.True(silenciosas.Count == 0,
            "Pantallas que vacían la lista sin avisar cuando la carga falla:\n  " + string.Join("\n  ", silenciosas));
    }
}
