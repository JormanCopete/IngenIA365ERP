"""Extrae el SQL completo de cada migrationBuilder.Sql(...) del Up() de cada migración, en orden.

Uso: python -I sqlcompleto.py <carpeta Application> [tablas,separadas,por,coma]
Imprime, por llamada: archivo, índice, si toca alguna de las tablas dadas, y el texto (sin interpretar).
"""
import glob
import os
import re
import sys


def leer_cadena(t, i):
    """Lee el literal de cadena C# que empieza en t[i] (tras espacios). Devuelve (texto_crudo, fin)."""
    while t[i] in ' \r\n\t':
        i += 1
    pref = ''
    while t[i] in '$@':
        pref += t[i]
        i += 1
    if t.startswith('"""', i):
        n = 0
        while t[i + n] == '"':
            n += 1
        cierre = '"' * n
        fin = t.index(cierre, i + n)
        return pref, t[i + n:fin], fin + n
    if t[i] == '"':
        j = i + 1
        if '@' in pref:
            while True:
                if t[j] == '"' and t[j + 1] == '"':
                    j += 2
                    continue
                if t[j] == '"':
                    break
                j += 1
            return pref, t[i + 1:j].replace('""', '"'), j + 1
        while t[j] != '"':
            j += 2 if t[j] == '\\' else 1
        return pref, t[i + 1:j], j + 1
    # una variable o expresión
    m = re.match(r'[A-Za-z_][A-Za-z0-9_.]*', t[i:])
    return 'expr', m.group(0) if m else t[i:i + 40], i + (len(m.group(0)) if m else 0)


def main():
    carpeta = sys.argv[1]
    tablas = [x for x in (sys.argv[2].split(',') if len(sys.argv) > 2 else []) if x]
    for f in sorted(glob.glob(os.path.join(carpeta, '*.cs'))):
        if f.endswith('.Designer.cs') or 'Snapshot' in f:
            continue
        t = open(f, encoding='utf-8-sig').read()
        down = t.find('protected override void Down(')
        k = 0
        for m in re.finditer(r'migrationBuilder\.Sql\(', t):
            if 0 <= down < m.start():
                continue
            k += 1
            pref, texto, _ = leer_cadena(t, m.end())
            toca = [x for x in tablas if x in texto]
            print(f'#### {os.path.basename(f)} #{k} pref={pref} toca={toca}')
            print(texto.strip())
            print()


main()
