# -*- coding: utf-8 -*-
"""Los errores y avisos de la lectura de un plano (contracts/plantillas.md §1.4 de la feature 016)."""
from dataclasses import dataclass

LINE_LENGTH = "Import.Profile.LineLength"
FIELD_OUT_OF_LINE = "Import.Profile.FieldOutOfLine"
MISSING_COLUMN = "Import.Profile.MissingColumn"
INVALID_DATE = "Import.Profile.InvalidDate"
INVALID_NUMBER = "Import.Profile.InvalidNumber"
INVALID_BOOLEAN = "Import.Profile.InvalidBoolean"
ENCODING_SUSPECT = "Import.Profile.EncodingSuspect"
EQUIVALENCE_MISSING = "Import.Equivalence.Missing"
EQUIVALENCE_ONE_TO_MANY = "Import.Equivalence.OneToMany"
EQUIVALENCE_NOT_MIGRATED = "Import.Equivalence.NotMigrated"


class PerfilInvalido(Exception):
    """El perfil no se puede usar: se informa antes de leer una sola línea."""


@dataclass(frozen=True)
class ErrorDeLinea:
    """Un error o aviso con la línea del plano, la columna destino y de dónde salió el valor."""

    linea: int
    columna: str
    origen: str
    codigo: str
    mensaje: str
    aviso: bool = False

    def como_fila(self):
        return [self.linea, self.columna, self.origen, self.codigo, "aviso" if self.aviso else "error", self.mensaje]


ENCABEZADO_CSV = ["linea", "columna", "origen", "codigo", "gravedad", "mensaje"]
