// Package identidad: cuentas del canal, activación, ingreso, sesiones, dispositivos, passkeys y códigos de un solo uso.
//
// La cuenta es única por tipo y número de documento en toda la plataforma (FR-015) y no tiene nada que ver con los
// usuarios del ERP. Activar, ingresar y recuperar la clave responden igual exista o no la persona (FR-018): por eso los
// documentos desconocidos dejan desafíos «señuelo» con máscaras inventadas y estables, y todas esas respuestas tardan
// lo mismo.
package identidad

import (
	"crypto/sha256"
	"strings"
	"unicode"
)

// Tipos de documento con los que se entra: los del catálogo fijo del ERP que sirven para entrar (TiposDeDocumento,
// longitudes D-17). Letras sólo en el pasaporte. RC y NIT existen en el espejo pero no abren cuenta.
var tiposDeDocumento = map[string]struct {
	minimo, maximo int
	letras         bool
}{
	"CC":  {3, 10, false},
	"CE":  {3, 10, false},
	"PPT": {3, 15, false},
	"PEP": {3, 15, false},
	"TI":  {3, 11, false},
	"PA":  {3, 16, true},
}

// Documento es un tipo y número ya normalizados.
type Documento struct {
	Tipo   string
	Numero string
}

// NormalizarDocumento quita puntos, espacios y guiones, pasa a mayúsculas y valida contra el catálogo. ok = false si
// el tipo no existe o el número no cumple su largo; el error que se muestra es siempre el mismo.
func NormalizarDocumento(tipo, numero string) (Documento, bool) {
	t := strings.ToUpper(strings.TrimSpace(tipo))
	regla, existe := tiposDeDocumento[t]
	if !existe {
		return Documento{}, false
	}
	var b strings.Builder
	for _, r := range strings.ToUpper(numero) {
		switch {
		case r == '.' || r == '-' || unicode.IsSpace(r):
			continue
		case r >= '0' && r <= '9':
			b.WriteRune(r)
		case r >= 'A' && r <= 'Z' && regla.letras:
			b.WriteRune(r)
		default:
			return Documento{}, false
		}
	}
	n := b.String()
	if len(n) < regla.minimo || len(n) > regla.maximo {
		return Documento{}, false
	}
	// Igual que el ERP (UsuarioDeDocumento.NormalizarNumero): los ceros a la izquierda se conservan.
	return Documento{Tipo: t, Numero: n}, true
}

// Llave es el SHA-256 de «tipo:número»: identifica al documento en los límites y desafíos sin guardarlo en claro.
func (d Documento) Llave() []byte {
	h := sha256.Sum256([]byte(d.Tipo + ":" + d.Numero))
	return h[:]
}

// LlaveDeTexto es la llave de una entrada que no pasó la normalización: los límites la cuentan igual.
func LlaveDeTexto(tipo, numero string) []byte {
	h := sha256.Sum256([]byte("?" + strings.ToUpper(strings.TrimSpace(tipo)) + ":" + strings.ToUpper(strings.TrimSpace(numero))))
	return h[:]
}
