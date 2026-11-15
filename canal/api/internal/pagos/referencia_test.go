package pagos

import (
	"testing"
	"time"
)

func TestReferencia(t *testing.T) {
	fecha := time.Date(2026, 12, 5, 23, 0, 0, 0, time.FixedZone("COT", -5*3600))
	ref, err := Referencia("ABC", fecha, 1)
	if err != nil {
		t.Fatal(err)
	}
	if len(ref) != LargoDeReferencia || ref[:17] != "ABC26120500000001" {
		t.Fatalf("referencia %s", ref)
	}
	if !ReferenciaValida(ref) {
		t.Fatalf("la referencia recién hecha no valida: %s", ref)
	}

	ultima, err := Referencia("ABC", fecha, MaximaSecuencia)
	if err != nil || ultima[9:17] != "ZZZZZZZZ" || !ReferenciaValida(ultima) {
		t.Fatalf("máxima: %s %v", ultima, err)
	}
	if _, err := Referencia("ABC", fecha, MaximaSecuencia+1); err == nil {
		t.Fatal("una secuencia que no cabe debe fallar")
	}
	for _, malo := range []string{"AB", "abc", "A1C"} {
		if _, err := Referencia(malo, fecha, 1); err == nil {
			t.Fatalf("código %q aceptado", malo)
		}
	}
}

func TestElDigitoDetectaUnCaracterCambiadoYLaTransposicion(t *testing.T) {
	ref, _ := Referencia("COO", time.Date(2026, 11, 9, 0, 0, 0, 0, time.UTC), 123456789)
	for i := 9; i < 17; i++ {
		for _, c := range []byte(alfabeto36) {
			if c == ref[i] {
				continue
			}
			mal := []byte(ref)
			mal[i] = c
			if ReferenciaValida(string(mal)) {
				t.Fatalf("cambiar la posición %d por %c no se detectó", i, c)
			}
		}
	}
	// Una transposición de vecinos distintos.
	for i := 9; i < 16; i++ {
		if ref[i] == ref[i+1] {
			continue
		}
		mal := []byte(ref)
		mal[i], mal[i+1] = mal[i+1], mal[i]
		if ReferenciaValida(string(mal)) {
			t.Logf("la transposición en %d no se detecta (Luhn admite algunas)", i)
		}
	}
	if ReferenciaValida("COO26110900000001") || ReferenciaValida("coo2611090000000AX") {
		t.Fatal("largo o minúsculas aceptados")
	}
}

func TestReferenciasDistintasPorSecuencia(t *testing.T) {
	fecha := time.Date(2026, 11, 9, 0, 0, 0, 0, time.UTC)
	vistas := map[string]bool{}
	for s := int64(0); s < 5000; s++ {
		r, _ := Referencia("ABC", fecha, s)
		if vistas[r] {
			t.Fatalf("referencia repetida %s", r)
		}
		vistas[r] = true
	}
}
