# -*- coding: utf-8 -*-
"""Kit de implantación (feature 016, D10). Ver README.md. Uso: python equivalencias.py --help"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from kit.comandos import equivalencias_desde_contadora  # noqa: E402

if __name__ == "__main__":
    sys.exit(equivalencias_desde_contadora())
