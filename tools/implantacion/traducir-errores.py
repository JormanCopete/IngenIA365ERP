# -*- coding: utf-8 -*-
"""Kit de implantación (feature 016, D10). Ver README.md. Uso: python traducir-errores.py --help"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from kit.comandos import traducir_errores  # noqa: E402

if __name__ == "__main__":
    sys.exit(traducir_errores())
