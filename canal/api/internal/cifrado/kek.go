package cifrado

import (
	"crypto/aes"
	"crypto/cipher"
	"crypto/rand"
	"errors"
	"fmt"
)

// Formato del texto cifrado: nonce (12 bytes, aleatorio) || cifrado AES-256-GCM || etiqueta (16 bytes).
// La versión de la KEK no va dentro: se guarda al lado (columna kek_version), para rotar sin reescribir el formato.

var (
	// ErrVersionDesconocida: el texto se cifró con una versión de KEK que este proceso no tiene cargada.
	ErrVersionDesconocida = errors.New("cifrado: versión de KEK desconocida")
	// ErrNoSePudoDescifrar: llave equivocada, datos asociados distintos o texto alterado. No se distingue a propósito.
	ErrNoSePudoDescifrar = errors.New("cifrado: no se pudo descifrar (llave, datos asociados o texto alterado)")
)

// Llavero guarda las KEK por versión y cifra siempre con la activa.
type Llavero struct {
	aeads  map[int]cipher.AEAD
	activa int
}

// NuevoLlavero recibe las KEK de 32 bytes por versión y la versión activa.
func NuevoLlavero(llaves map[int][]byte, activa int) (*Llavero, error) {
	l := &Llavero{aeads: make(map[int]cipher.AEAD, len(llaves)), activa: activa}
	for version, llave := range llaves {
		if len(llave) != 32 {
			return nil, fmt.Errorf("cifrado: la KEK v%d debe tener 32 bytes, tiene %d", version, len(llave))
		}
		bloque, err := aes.NewCipher(llave)
		if err != nil {
			return nil, fmt.Errorf("cifrado: KEK v%d: %w", version, err)
		}
		aead, err := cipher.NewGCM(bloque)
		if err != nil {
			return nil, fmt.Errorf("cifrado: KEK v%d: %w", version, err)
		}
		l.aeads[version] = aead
	}
	if _, ok := l.aeads[activa]; !ok {
		return nil, fmt.Errorf("cifrado: la KEK activa v%d no está cargada", activa)
	}
	return l, nil
}

// VersionActiva es la versión con la que cifra este llavero.
func (l *Llavero) VersionActiva() int { return l.activa }

// Cifrar cifra con la KEK activa y devuelve el texto cifrado y la versión usada. datosAsociados (puede ser nil)
// amarra el cifrado a su dueño —p. ej. el id del sitio de pasarela—: descifrarlo con otros datos falla, así un
// secreto copiado a otra fila no sirve.
func (l *Llavero) Cifrar(textoPlano, datosAsociados []byte) ([]byte, int, error) {
	aead := l.aeads[l.activa]
	nonce := make([]byte, aead.NonceSize(), aead.NonceSize()+len(textoPlano)+aead.Overhead())
	if _, err := rand.Read(nonce); err != nil {
		return nil, 0, fmt.Errorf("cifrado: nonce: %w", err)
	}
	return aead.Seal(nonce, nonce, textoPlano, datosAsociados), l.activa, nil
}

// Descifrar descifra un texto cifrado con la versión indicada y los mismos datos asociados.
func (l *Llavero) Descifrar(textoCifrado []byte, version int, datosAsociados []byte) ([]byte, error) {
	aead, ok := l.aeads[version]
	if !ok {
		return nil, fmt.Errorf("%w: v%d", ErrVersionDesconocida, version)
	}
	if len(textoCifrado) < aead.NonceSize()+aead.Overhead() {
		return nil, ErrNoSePudoDescifrar
	}
	nonce, resto := textoCifrado[:aead.NonceSize()], textoCifrado[aead.NonceSize():]
	plano, err := aead.Open(nil, nonce, resto, datosAsociados)
	if err != nil {
		return nil, ErrNoSePudoDescifrar
	}
	return plano, nil
}
