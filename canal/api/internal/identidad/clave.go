package identidad

import (
	"bufio"
	_ "embed"
	"errors"
	"strings"
	"sync"
	"unicode"
	"unicode/utf8"

	"golang.org/x/crypto/bcrypt"
)

// CostoBcrypt de las claves (research R13).
const CostoBcrypt = 12

// Política de clave del canal (FR-020, R13).
const (
	LargoMinimoDeClave = 8
	// bcrypt sólo mira los primeros 72 bytes: una clave más larga no es más segura y confunde.
	LargoMaximoDeClave = 72
)

//go:embed claves_comunes.txt
var clavesComunesTexto string

var (
	clavesComunes      map[string]struct{}
	cargarClavesUnaVez sync.Once
)

func comunes() map[string]struct{} {
	cargarClavesUnaVez.Do(func() {
		clavesComunes = make(map[string]struct{}, 2200)
		s := bufio.NewScanner(strings.NewReader(clavesComunesTexto))
		for s.Scan() {
			linea := strings.TrimSpace(s.Text())
			if linea == "" || strings.HasPrefix(linea, "#") {
				continue
			}
			clavesComunes[strings.ToLower(linea)] = struct{}{}
		}
	})
	return clavesComunes
}

// ErrClaveDebil explica por qué la clave no cumple; el mensaje va a la persona.
type ErrClaveDebil struct{ Motivo string }

func (e *ErrClaveDebil) Error() string { return e.Motivo }

// ValidarClave aplica la política: 8 a 72 bytes, letras y números, que no sea de las comunes ni contenga el número de
// documento.
func ValidarClave(clave string, doc Documento) error {
	if !utf8.ValidString(clave) {
		return &ErrClaveDebil{"La clave tiene caracteres no válidos."}
	}
	if utf8.RuneCountInString(clave) < LargoMinimoDeClave {
		return &ErrClaveDebil{"La clave debe tener al menos 8 caracteres."}
	}
	if len(clave) > LargoMaximoDeClave {
		return &ErrClaveDebil{"La clave es demasiado larga: usa como máximo 72 caracteres."}
	}
	var letras, numeros bool
	for _, r := range clave {
		switch {
		case unicode.IsLetter(r):
			letras = true
		case unicode.IsDigit(r):
			numeros = true
		}
	}
	if !letras || !numeros {
		return &ErrClaveDebil{"La clave debe combinar letras y números."}
	}
	minuscula := strings.ToLower(clave)
	if _, comun := comunes()[minuscula]; comun {
		return &ErrClaveDebil{"Esa clave es de las más usadas y fáciles de adivinar. Elige otra."}
	}
	if doc.Numero != "" && strings.Contains(strings.ToUpper(clave), doc.Numero) {
		return &ErrClaveDebil{"La clave no puede contener tu número de documento."}
	}
	return nil
}

// HashDeClave calcula el bcrypt de una clave ya validada.
func HashDeClave(clave string) (string, error) {
	h, err := bcrypt.GenerateFromPassword([]byte(clave), CostoBcrypt)
	if err != nil {
		return "", err
	}
	return string(h), nil
}

// hashSenuelo se compara cuando no hay cuenta, para que el ingreso tarde lo mismo exista o no (FR-018).
var (
	hashSenuelo       []byte
	hashSenueloUnaVez sync.Once
)

// CompararClave dice si la clave corresponde al hash. Con hash vacío compara contra un señuelo y responde false: el
// tiempo es el de un bcrypt de verdad.
func CompararClave(hash, clave string) bool {
	if hash == "" {
		hashSenueloUnaVez.Do(func() {
			hashSenuelo, _ = bcrypt.GenerateFromPassword([]byte("señuelo-sin-cuenta-0"), CostoBcrypt)
		})
		_ = bcrypt.CompareHashAndPassword(hashSenuelo, []byte(clave))
		return false
	}
	err := bcrypt.CompareHashAndPassword([]byte(hash), []byte(clave))
	if err != nil && !errors.Is(err, bcrypt.ErrMismatchedHashAndPassword) {
		return false
	}
	return err == nil
}
