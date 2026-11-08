package cifrado

import (
	"bytes"
	"encoding/hex"
	"errors"
	"testing"
)

func llave(b byte) []byte { return bytes.Repeat([]byte{b}, 32) }

func TestIdaYVuelta(t *testing.T) {
	l, err := NuevoLlavero(map[int][]byte{1: llave(1)}, 1)
	if err != nil {
		t.Fatal(err)
	}
	secreto := []byte("secreto-de-prueba")
	cifrado, version, err := l.Cifrar(secreto, []byte("sitio-1"))
	if err != nil {
		t.Fatal(err)
	}
	if version != 1 {
		t.Errorf("versión = %d", version)
	}
	if bytes.Contains(cifrado, secreto) {
		t.Error("el texto cifrado contiene el texto plano")
	}
	plano, err := l.Descifrar(cifrado, version, []byte("sitio-1"))
	if err != nil {
		t.Fatal(err)
	}
	if !bytes.Equal(plano, secreto) {
		t.Errorf("descifrado = %q", plano)
	}
}

func TestDosCifradosDelMismoTextoDifieren(t *testing.T) {
	l, _ := NuevoLlavero(map[int][]byte{1: llave(1)}, 1)
	a, _, _ := l.Cifrar([]byte("igual"), nil)
	b, _, _ := l.Cifrar([]byte("igual"), nil)
	if bytes.Equal(a, b) {
		t.Error("el nonce no es aleatorio: dos cifrados idénticos")
	}
}

func TestVersionEquivocada(t *testing.T) {
	l, _ := NuevoLlavero(map[int][]byte{1: llave(1), 2: llave(2)}, 2)
	cifrado, version, _ := l.Cifrar([]byte("x"), nil)
	if version != 2 {
		t.Fatalf("debía cifrar con la activa (2), usó %d", version)
	}
	if _, err := l.Descifrar(cifrado, 1, nil); !errors.Is(err, ErrNoSePudoDescifrar) {
		t.Errorf("con otra versión cargada: %v", err)
	}
	if _, err := l.Descifrar(cifrado, 3, nil); !errors.Is(err, ErrVersionDesconocida) {
		t.Errorf("con una versión que no existe: %v", err)
	}
}

func TestRotacionLeeLoViejo(t *testing.T) {
	viejo, _ := NuevoLlavero(map[int][]byte{1: llave(1)}, 1)
	cifrado, version, _ := viejo.Cifrar([]byte("antes de rotar"), nil)
	nuevo, _ := NuevoLlavero(map[int][]byte{1: llave(1), 2: llave(2)}, 2)
	plano, err := nuevo.Descifrar(cifrado, version, nil)
	if err != nil || string(plano) != "antes de rotar" {
		t.Errorf("tras rotar no se lee lo cifrado con v1: %q, %v", plano, err)
	}
}

func TestAlteradoODatosAsociadosDistintos(t *testing.T) {
	l, _ := NuevoLlavero(map[int][]byte{1: llave(1)}, 1)
	cifrado, v, _ := l.Cifrar([]byte("monto=100"), []byte("sitio-1"))

	for i := range cifrado {
		alterado := bytes.Clone(cifrado)
		alterado[i] ^= 0x01
		if _, err := l.Descifrar(alterado, v, []byte("sitio-1")); !errors.Is(err, ErrNoSePudoDescifrar) {
			t.Fatalf("el byte %d alterado no se detectó: %v", i, err)
		}
	}
	if _, err := l.Descifrar(cifrado, v, []byte("sitio-2")); !errors.Is(err, ErrNoSePudoDescifrar) {
		t.Errorf("con datos asociados de otro sitio: %v", err)
	}
	if _, err := l.Descifrar(cifrado[:10], v, []byte("sitio-1")); !errors.Is(err, ErrNoSePudoDescifrar) {
		t.Errorf("truncado: %v", err)
	}
}

// Vector conocido: caso de prueba 16 de McGrew y Viega, «The Galois/Counter Mode of Operation (GCM)» (AES-256,
// 60 bytes de texto y 20 de datos asociados). Comprueba que el formato nonce || cifrado || etiqueta es el estándar.
func TestVectorConocidoGCM(t *testing.T) {
	k := decodificar(t, "feffe9928665731c6d6a8f9467308308feffe9928665731c6d6a8f9467308308")
	iv := decodificar(t, "cafebabefacedbaddecaf888")
	p := decodificar(t, "d9313225f88406e5a55909c5aff5269a86a7a9531534f7da2e4c303d8a318a72"+
		"1c3c0c95956809532fcf0e2449a6b525b16aedf5aa0de657ba637b39")
	a := decodificar(t, "feedfacedeadbeeffeedfacedeadbeefabaddad2")
	c := decodificar(t, "522dc1f099567d07f47f37a32a84427d643a8cdcbfe5c0c97598a2bd2555d1aa"+
		"8cb08e48590dbb3da7b08b1056828838c5f61e6393ba7a0abcc9f662")
	etiqueta := decodificar(t, "76fc6ece0f4e1768cddf8853bb2d551b")

	l, err := NuevoLlavero(map[int][]byte{7: k}, 7)
	if err != nil {
		t.Fatal(err)
	}
	texto := append(append(bytes.Clone(iv), c...), etiqueta...)
	plano, err := l.Descifrar(texto, 7, a)
	if err != nil {
		t.Fatalf("no descifró el vector conocido: %v", err)
	}
	if !bytes.Equal(plano, p) {
		t.Errorf("texto plano distinto del vector")
	}
}

func TestLlaveDeLargoIncorrecto(t *testing.T) {
	if _, err := NuevoLlavero(map[int][]byte{1: []byte("corta")}, 1); err == nil {
		t.Error("debía rechazar una KEK de largo distinto de 32")
	}
	if _, err := NuevoLlavero(map[int][]byte{1: llave(1)}, 2); err == nil {
		t.Error("debía rechazar una versión activa que no está cargada")
	}
}

func decodificar(t *testing.T, s string) []byte {
	t.Helper()
	b, err := hex.DecodeString(s)
	if err != nil {
		t.Fatal(err)
	}
	return b
}
