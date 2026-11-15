"""Genera, por motor, el parcial de EsquemaBase con el SQL a mano que las migraciones absorbidas
ejecutaban y que deja algo en una base NUEVA: la extensión pg_trgm y las filas fijas.

Lee las migraciones históricas de git (origin/develop), no del árbol de trabajo.
Uso: python -I generar_datos.py <repo> <ref>
"""
import os
import re
import subprocess
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sqlcompleto import leer_cadena  # noqa: E402

REPO, REF = sys.argv[1], sys.argv[2]

# (migración lógica, número de llamada Sql en el Up) en el orden en que corrían.
SELECCION = [
    ('NominaNovedadesYLiquidacion', 1),
    ('PlataformaParaInventario', 1),
    ('PlataformaParaCartera', 2),
    ('PlataformaParaCartera', 3),
    ('PlataformaParaCartera', 4),
    ('CarteraNucleo', 1),
    ('CarteraLibranza', 1),
    ('CaptacionesAportes', 2),
    ('CaptacionesAportes', 3),
    ('RecaudoNucleo', 1),
    ('RecaudoNucleo', 2),
    ('TesoreriaNucleo', 1),
    ('RecaudoCompleto', 1),
    ('RecaudoCanales', 1),
    ('TesoreriaAmpliada', 1),
    ('TesoreriaAmpliada', 2),
]
# Valores por defecto que el camino viejo dejó en columnas agregadas con defaultValue y que el modelo no
# declara (tabla, columna, expresión PostgreSQL, expresión SQL Server). Salen de comparar los catálogos de una base
# creada por las absorbidas con una creada por la base (2026-11-15): son las únicas diferencias de esquema.
POR_DEFECTO_HEREDADOS = [
    ('ACC_FixedAssets', 'OpeningAccumulated', '0.0', '(0.0)'),
    ('ACC_FixedAssets', 'OpeningElapsedMonths', '0', '(0)'),
    ('COR_NotificationTemplates', 'ValidFrom', "'-infinity'::date", "'0001-01-01'"),
    ('COR_People', 'Username', "''::character varying", "N''"),
    ('LND_DepositLineVersions', 'GmfOnDeceasedAndUnclaimed', 'true', 'CONVERT([bit],(1))'),
    ('PAY_Employees', 'EmployeeClass', '0', '(0)'),
    ('PAY_Employees', 'PayrollPlanId', '0', '(0)'),
    ('PAY_LegalParameters', 'RangeIsMarginal', 'false', 'CONVERT([bit],(0))'),
    ('PAY_PayPeriods', 'PayrollPlanId', '0', '(0)'),
    ('PAY_PayrollRunEmployees', 'BasesJson', "''::text", "N''"),
    ('PAY_PayrollRunEmployees', 'NotesJson', "''::text", "N''"),
    ('PAY_PensionProviders', 'IsAccai', 'false', 'CONVERT([bit],(0))'),
    ('PAY_RecurringNovelties', 'ApplyOn', '0', '(0)'),
    ('PAY_WithholdingParameters', 'PayrollPlanId', '0', '(0)'),
    ('RCD_ReceivedChecks', 'IsOpeningLoad', 'false', 'CONVERT([bit],(0))'),
    ('TES_CashRemittances', 'IsOpeningLoad', 'false', 'CONVERT([bit],(0))'),
]

# Constantes C# interpoladas en el SQL original.
CONSTANTES = {'MarcaDeOrigen': 'migracion:NominaNovedadesYLiquidacion'}


def git(*a):
    return subprocess.run(['git', '-C', REPO, *a], capture_output=True, check=True).stdout.decode('utf-8-sig')


def llamadas_up(texto):
    down = texto.find('protected override void Down(')
    for m in re.finditer(r'migrationBuilder\.Sql\(', texto):
        if 0 <= down < m.start():
            continue
        yield leer_cadena(texto, m.end())


def sql_de(motor, logica, n):
    carpeta = f'src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{motor}/Application'
    archivos = [l for l in git('ls-tree', '--name-only', f'{REF}:{carpeta}').splitlines()
                if l.endswith(f'_{logica}.cs')]
    assert len(archivos) == 1, (motor, logica, archivos)
    texto = git('show', f'{REF}:{carpeta}/{archivos[0]}')
    pref, sql, _ = list(llamadas_up(texto))[n - 1]
    if '$' in pref:
        sql = re.sub(r'\{([A-Za-z_]+)\}', lambda m: CONSTANTES[m.group(1)], sql)
    assert pref != 'expr', (motor, logica, n)
    return archivos[0][:-3], dedent(sql)


def dedent(s):
    lineas = s.replace('\r\n', '\n').strip('\n').split('\n')
    sangria = min((len(l) - len(l.lstrip()) for l in lineas if l.strip()), default=0)
    return '\n'.join(l[sangria:].rstrip() for l in lineas).strip()


def literal(sql, sangria):
    comillas = '"""' if '"""' not in sql else '""""'
    pad = ' ' * sangria
    cuerpo = '\n'.join((pad + l) if l else '' for l in sql.split('\n'))
    return f'{comillas}\n{cuerpo}\n{pad}{comillas}'


def generar(motor):
    ns = f'IngenIA365ERP.Persistence.Migrations.{motor}.Application'
    bloques = []
    for logica, n in SELECCION:
        origen, sql = sql_de(motor, logica, n)
        bloques.append(f'        // {origen}, llamada {n} de su Up().\n'
                       f'        migrationBuilder.Sql({literal(sql, 12)});\n')
    extension = ''
    if motor == 'PostgreSql':
        extension = ('    /// <summary>\n'
                     '    /// La extensión que las búsquedas por trigramas necesitan antes de crear sus índices GIN\n'
                     '    /// (<c>gin_trgm_ops</c>). La creaba a mano <c>InventarioComercialNucleo</c>: el modelo no la declara.\n'
                     '    /// </summary>\n'
                     '    private static void CrearExtensiones(MigrationBuilder migrationBuilder)\n'
                     '    {\n'
                     '        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");\n'
                     '    }\n\n')
    sentencias = []
    for tabla, columna, pg, ss in POR_DEFECTO_HEREDADOS:
        if motor == 'PostgreSql':
            sentencias.append(f'ALTER TABLE dbo."{tabla}" ALTER COLUMN "{columna}" SET DEFAULT {pg};')
        else:
            sentencias.append(f'ALTER TABLE [dbo].[{tabla}] ADD DEFAULT {ss} FOR [{columna}];')
    por_defecto = ('    /// <summary>\n'
                   '    /// Los 16 valores por defecto que el camino viejo dejó en columnas agregadas con <c>defaultValue</c> y que el\n'
                   '    /// modelo no declara. EF siempre manda esas columnas, pero un INSERT escrito a mano que las omita funciona en\n'
                   '    /// las bases existentes; sin esto fallaría en una cooperativa nueva. Salen de comparar catálogos.\n'
                   '    /// </summary>\n'
                   '    private static void RestituirValoresPorDefectoHeredados(MigrationBuilder migrationBuilder)\n'
                   '    {\n'
                   f'        migrationBuilder.Sql({literal(chr(10).join(sentencias), 12)});\n'
                   '    }\n\n')
    return f'''using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace {ns};

/// <summary>
/// Lo que las migraciones absorbidas por <see cref="EsquemaBase"/> dejaban en una base NUEVA y que el modelo no
/// conoce, copiado tal cual y en el mismo orden (consolidación de migraciones, 2026-11-15). Se generó con
/// <c>tools/scripts/consolidar-migraciones/generar_datos.py</c> desde git; no se edita a mano.
///
/// <para>Quedó fuera, a propósito, todo lo que sobre una base vacía no hace nada: los rellenos de columnas nuevas
/// (<c>UPDATE</c> sobre filas que en una base nueva no existen), las copias desde tablas que nacen vacías y las guardas
/// de las migraciones destructivas. La prueba de equivalencia compara las filas de una base creada por el camino viejo
/// con las de una creada por éste.</para>
/// </summary>
public partial class EsquemaBase
{{
{extension}{por_defecto}    /// <summary>Las filas fijas que sembraban las migraciones absorbidas (12 tablas, 72 filas).</summary>
    private static void SembrarLoQueSembrabanLasAbsorbidas(MigrationBuilder migrationBuilder)
    {{
{chr(10).join(bloques)}    }}
}}
'''


for motor in ('PostgreSql', 'SqlServer'):
    destino = os.path.join(REPO, 'src', 'Infrastructure', f'IngenIA365ERP.Persistence.Migrations.{motor}',
                           'Application', 'EsquemaBase.Complementos.cs')
    with open(destino, 'w', encoding='utf-8', newline='\n') as f:
        f.write(generar(motor))
    print('escrito', destino)
