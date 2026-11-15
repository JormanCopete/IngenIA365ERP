"""Inserta en el Up() de cada *_EsquemaBase.cs generado por EF las llamadas a sus complementos.

Uso: python -I insertar_llamadas.py <raíz del repositorio>

Al principio del Up() (PostgreSQL): CrearExtensiones, antes de los índices GIN con gin_trgm_ops.
Al final del Up() (los dos motores): RestituirValoresPorDefectoHeredados y SembrarLoQueSembrabanLasAbsorbidas.
Es idempotente: si las llamadas ya están, no hace nada. Conserva BOM y saltos de línea del archivo.
"""
import glob
import os
import sys

REPO = sys.argv[1]

for motor in ('PostgreSql', 'SqlServer'):
    patron = os.path.join(REPO, 'src', 'Infrastructure', f'IngenIA365ERP.Persistence.Migrations.{motor}',
                          'Application', '*_EsquemaBase.cs')
    archivos = glob.glob(patron)
    assert len(archivos) == 1, (motor, archivos)
    f = archivos[0]
    raw = open(f, 'rb').read()
    bom = raw.startswith(b'\xef\xbb\xbf')
    t = raw.decode('utf-8-sig')
    nl = '\r\n' if '\r\n' in t else '\n'
    t = t.replace('\r\n', '\n')
    if 'SembrarLoQueSembrabanLasAbsorbidas(migrationBuilder);' in t:
        print('ya estaban', os.path.basename(f))
        continue

    inicio = 'protected override void Up(MigrationBuilder migrationBuilder)\n        {\n'
    assert t.count(inicio) == 1
    antes = '            // Consolidación (2026-11-15): lo que el modelo no declara va en EsquemaBase.Complementos.cs.\n'
    antes += '            CrearExtensiones(migrationBuilder);\n\n' if motor == 'PostgreSql' else '\n'
    t = t.replace(inicio, inicio + antes)

    fin = '\n        }\n\n        /// <inheritdoc />\n        protected override void Down('
    assert t.count(fin) == 1
    t = t.replace(fin, '\n\n            RestituirValoresPorDefectoHeredados(migrationBuilder);'
                       '\n            SembrarLoQueSembrabanLasAbsorbidas(migrationBuilder);' + fin)

    open(f, 'wb').write((b'\xef\xbb\xbf' if bom else b'') + t.replace('\n', nl).encode('utf-8'))
    print('insertadas en', os.path.basename(f))
