package identidad

import (
	"bytes"
	"strings"
	"testing"

	"github.com/google/uuid"
)

func TestNormalizarDocumento(t *testing.T) {
	casos := []struct {
		tipo, numero string
		ok           bool
		esperado     Documento
	}{
		{"cc", "1.010.101.010", true, Documento{"CC", "1010101010"}},
		{" CE ", "987 654", true, Documento{"CE", "987654"}},
		{"PA", "ab-123", true, Documento{"PA", "AB123"}},
		{"CC", "0012345", true, Documento{"CC", "0012345"}},
		{"CC", "12AB", false, Documento{}},
		{"CC", "12", false, Documento{}},
		{"CC", "12345678901", false, Documento{}},
		{"NIT", "900123456", false, Documento{}},
		{"RC", "1234567", false, Documento{}},
		{"XX", "123456", false, Documento{}},
		{"TI", "1234/5", false, Documento{}},
	}
	for _, c := range casos {
		d, ok := NormalizarDocumento(c.tipo, c.numero)
		if ok != c.ok || d != c.esperado {
			t.Errorf("NormalizarDocumento(%q, %q) = %+v, %v; se esperaba %+v, %v", c.tipo, c.numero, d, ok, c.esperado, c.ok)
		}
	}
	a, _ := NormalizarDocumento("CC", "1.010")
	b, _ := NormalizarDocumento("cc", "1010")
	if !bytes.Equal(a.Llave(), b.Llave()) {
		t.Error("el mismo documento escrito distinto debe dar la misma llave")
	}
	if bytes.Equal(a.Llave(), LlaveDeTexto("CC", "1010")) {
		t.Error("la llave de un texto sin normalizar no debe chocar con la de un documento válido")
	}
}

func TestValidarClave(t *testing.T) {
	doc := Documento{Tipo: "CC", Numero: "1010101010"}
	rechazadas := map[string]string{
		"corta1":                 "al menos 8",
		"solotexto":              "letras y números",
		"1234567890":             "letras y números",
		"password1":              "más usadas",
		"Colombia123":            "más usadas",
		"x1010101010y":           "documento",
		strings.Repeat("a1", 40): "demasiado larga",
	}
	for clave, motivo := range rechazadas {
		err := ValidarClave(clave, doc)
		if err == nil || !strings.Contains(err.Error(), motivo) {
			t.Errorf("ValidarClave(%q) = %v; se esperaba un rechazo por «%s»", clave, err, motivo)
		}
	}
	for _, clave := range []string{"Cafetal2026x", "río-verde-77", "Mañana de sol 4"} {
		if err := ValidarClave(clave, doc); err != nil {
			t.Errorf("ValidarClave(%q) = %v; debía aceptarse", clave, err)
		}
	}
}

func TestCodigosYSecretos(t *testing.T) {
	id := uuid.New()
	codigo, err := NuevoCodigo()
	if err != nil || len(codigo) != DigitosDelCodigo {
		t.Fatalf("código %q, %v", codigo, err)
	}
	h := HashDelCodigo(id, codigo)
	if !CodigoCorrecto(id, codigo, h) || CodigoCorrecto(uuid.New(), codigo, h) || CodigoCorrecto(id, "000000x", h) || CodigoCorrecto(id, codigo, nil) {
		t.Error("la comparación del código no se comporta como debe")
	}
	texto, hash, err := NuevoSecreto()
	if err != nil || len(texto) < 40 || !bytes.Equal(hash, HashDeSecreto(texto)) {
		t.Errorf("secreto %q, %v", texto, err)
	}
}

func TestMascaras(t *testing.T) {
	if m := enmascararCorreo("ana.prueba@correo.com"); m != "a•••@c•••.com" {
		t.Errorf("correo: %q", m)
	}
	if m := enmascararCorreo("x@dominio.com.co"); m != "x•••@d•••.co" {
		t.Errorf("correo con dos puntos: %q", m)
	}
	if m := enmascararCelular("3001234567"); m != "•••••• 4567" {
		t.Errorf("celular: %q", m)
	}
	if m := enmascararDocumento("1010101010"); m != "••••••1010" {
		t.Errorf("documento: %q", m)
	}
}

func TestEtiquetaDeDispositivo(t *testing.T) {
	ua := "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0 Safari/537.36"
	if e := etiquetaDeDispositivo(Navegador, ua); e != "Chrome en Windows" {
		t.Errorf("etiqueta = %q", e)
	}
	if e := etiquetaDeDispositivo(Android, ua); e != "App en Android" {
		t.Errorf("etiqueta de la app = %q", e)
	}
}

func TestNormalizarContacto(t *testing.T) {
	if v, _, ok := normalizarContacto("mobile", "+57 311 222 3344"); !ok || v != "3112223344" {
		t.Errorf("celular: %q %v", v, ok)
	}
	if _, _, ok := normalizarContacto("mobile", "6025550000"); ok {
		t.Error("un fijo no es celular")
	}
	if v, _, ok := normalizarContacto("email", "Ana@Correo.com"); !ok || v != "ana@correo.com" {
		t.Errorf("correo: %q %v", v, ok)
	}
	if _, _, ok := normalizarContacto("email", "Ana <ana@correo.com>"); ok {
		t.Error("un correo con nombre no se admite")
	}
}
