package identidad

import (
	"crypto/hmac"
	"crypto/rand"
	"crypto/sha256"
	"encoding/base64"
	"fmt"
	"math/big"
	"time"

	"github.com/google/uuid"
)

// Reglas de los códigos de un solo uso (FR-022, T064).
const (
	DigitosDelCodigo     = 6
	VidaDelCodigo        = 5 * time.Minute
	IntentosPorCodigo    = 3
	EnviosPorDesafio     = 5
	VidaDelPermiso       = 10 * time.Minute // tras verificar el código, para terminar de activar o recuperar
	DesafiosPorDocumento = 6                // por hora
	DesafiosPorIP        = 30               // por hora
	VentanaDeDesafios    = time.Hour
	FallosParaBloquear   = 5
	VentanaDeFallos      = 15 * time.Minute
	DuracionDelBloqueo   = 15 * time.Minute
)

// NuevoCodigo genera un código de 6 dígitos con el generador criptográfico.
func NuevoCodigo() (string, error) {
	n, err := rand.Int(rand.Reader, big.NewInt(1_000_000))
	if err != nil {
		return "", err
	}
	return fmt.Sprintf("%06d", n.Int64()), nil
}

// HashDelCodigo: SHA-256 del id del desafío y el código. Con 5 minutos de vida y 3 intentos, no hace falta más; el
// código nunca se guarda en claro.
func HashDelCodigo(desafio uuid.UUID, codigo string) []byte {
	h := sha256.Sum256([]byte(desafio.String() + ":" + codigo))
	return h[:]
}

// CodigoCorrecto compara en tiempo constante.
func CodigoCorrecto(desafio uuid.UUID, codigo string, guardado []byte) bool {
	if len(guardado) == 0 {
		return false
	}
	return hmac.Equal(HashDelCodigo(desafio, codigo), guardado)
}

// NuevoSecreto devuelve 32 bytes aleatorios en base64url (sin relleno) y su SHA-256: el texto va a quien lo usa (token de
// sesión, credencial de dispositivo, permiso de activación) y en la base queda sólo el hash.
func NuevoSecreto() (string, []byte, error) {
	b := make([]byte, 32)
	if _, err := rand.Read(b); err != nil {
		return "", nil, err
	}
	texto := base64.RawURLEncoding.EncodeToString(b)
	return texto, HashDeSecreto(texto), nil
}

// HashDeSecreto es el SHA-256 del texto del secreto.
func HashDeSecreto(texto string) []byte {
	h := sha256.Sum256([]byte(texto))
	return h[:]
}

// HashDeDestino identifica a qué correo o celular se envió un código, sin guardarlo en claro.
func HashDeDestino(destino string) []byte {
	h := sha256.Sum256([]byte("destino:" + destino))
	return h[:]
}

func nuevoID() uuid.UUID {
	id, err := uuid.NewV7()
	if err != nil {
		return uuid.New()
	}
	return id
}
