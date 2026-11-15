"""Escribe, por motor, la lista de migraciones que absorbió EsquemaBase (lee el árbol de git de la referencia dada)."""
import os, subprocess, sys
REPO, REF = sys.argv[1], sys.argv[2]
for motor in ('PostgreSql', 'SqlServer'):
    carpeta = f'src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{motor}/Application'
    nombres = subprocess.run(['git', '-C', REPO, 'ls-tree', '--name-only', f'{REF}:{carpeta}'], capture_output=True, check=True).stdout.decode().split()
    ids = sorted(n[:-3] for n in nombres if n.endswith('.cs') and not n.endswith('.Designer.cs') and 'Snapshot' not in n)
    filas = '\n'.join(f'        "{i}",' for i in ids)
    texto = f'''using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.Initialization;

namespace IngenIA365ERP.Persistence.Migrations.{motor}.Application;

/// <summary>
/// Las {len(ids)} migraciones del árbol operativo que <see cref="EsquemaBase"/> absorbió (consolidación del 2026-11-15).
/// Sus archivos salieron del compilado y siguen en git; esta lista es lo que queda de ellas en el código, y la usa
/// <see cref="ConsolidacionDeMigraciones"/> para reconocer una base vieja (las tiene todas: se marca la base) y para
/// sellar una nueva (se le anotan después de aplicar la base). Se generó con
/// <c>tools/scripts/consolidar-migraciones/generar_absorbidas.py</c>; no se edita a mano.
/// </summary>
public sealed class MigracionesAbsorbidasPorEsquemaBase : MigracionesAbsorbidas
{{
    public override Type Contexto => typeof(ApplicationDbContext);

    public override Type MigracionBase => typeof(EsquemaBase);

    public override IReadOnlyList<string> Absorbidas {{ get; }} =
    [
{filas}
    ];
}}
'''
    destino = os.path.join(REPO, *carpeta.split('/'), 'EsquemaBase.Absorbidas.cs')
    open(destino, 'w', encoding='utf-8', newline='\n').write(texto)
    print(motor, len(ids), ids[0], ids[-1])
