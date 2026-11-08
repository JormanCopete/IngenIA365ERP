package pasarela

import (
	"bytes"
	"encoding/json"
	"fmt"
	"log/slog"
	"strings"
	"testing"
)

const secretoDePrueba = "SecretoDePrueba-9f8e7d6c"

func credencialesValidas() Credenciales {
	return Credenciales{
		Pasarela: "Placetopay", URLBase: "https://checkout.pasarela.test/", Login: "login-de-prueba-1234",
		Secreto: secretoDePrueba, MediosDePago: []string{"PSE", "visa", "visa"},
	}
}

func TestValidarNormaliza(t *testing.T) {
	n, err := Validar(credencialesValidas(), false)
	if err != nil {
		t.Fatalf("credenciales válidas rechazadas: %v", err)
	}
	if n.Pasarela != PasarelaPlacetopay || n.URLBase != "https://checkout.pasarela.test" || n.Login != "login-de-prueba-1234" {
		t.Errorf("normalización inesperada: %+v", n)
	}
	if strings.Join(n.MediosDePago, ",") != "pse,visa" {
		t.Errorf("medios = %v", n.MediosDePago)
	}
	if n.Secreto.Revelar() != secretoDePrueba {
		t.Errorf("el secreto no debía cambiar")
	}
	sinMedios := credencialesValidas()
	sinMedios.MediosDePago = nil
	if n, err := Validar(sinMedios, false); err != nil || n.MediosDePago == nil {
		t.Errorf("sin medios: la lista debía quedar vacía y no nil: %v %v", n.MediosDePago, err)
	}
}

func TestValidarRechaza(t *testing.T) {
	casos := []struct {
		nombre string
		mutar  func(*Credenciales)
		local  bool
		codigo string
	}{
		{"otra pasarela", func(c *Credenciales) { c.Pasarela = "paypal" }, false, "Canal.Sitio.PasarelaNoAdmitida"},
		{"http público", func(c *Credenciales) { c.URLBase = "http://checkout.pasarela.test" }, true, "Canal.Sitio.UrlInvalida"},
		{"http local sin simulada", func(c *Credenciales) { c.URLBase = "http://localhost:9000" }, false, "Canal.Sitio.UrlInvalida"},
		{"con usuario", func(c *Credenciales) { c.URLBase = "https://u:p@checkout.pasarela.test" }, false, "Canal.Sitio.UrlInvalida"},
		{"con consulta", func(c *Credenciales) { c.URLBase = "https://checkout.pasarela.test/?a=1" }, false, "Canal.Sitio.UrlInvalida"},
		{"relativa", func(c *Credenciales) { c.URLBase = "/checkout" }, false, "Canal.Sitio.UrlInvalida"},
		{"login vacío", func(c *Credenciales) { c.Login = "  " }, false, "Canal.Sitio.LoginInvalido"},
		{"login largo", func(c *Credenciales) { c.Login = strings.Repeat("a", 101) }, false, "Canal.Sitio.LoginInvalido"},
		{"secreto vacío", func(c *Credenciales) { c.Secreto = " " }, false, "Canal.Sitio.SecretoInvalido"},
		{"secreto largo", func(c *Credenciales) { c.Secreto = Secreto(strings.Repeat("s", 201)) }, false, "Canal.Sitio.SecretoInvalido"},
		{"medio desconocido", func(c *Credenciales) { c.MediosDePago = []string{"pse", "bitcoin"} }, false, "Canal.Sitio.MedioDePagoDesconocido"},
	}
	for _, c := range casos {
		cred := credencialesValidas()
		c.mutar(&cred)
		_, err := Validar(cred, c.local)
		if err == nil || err.Codigo != c.codigo || err.Estado != 400 {
			t.Errorf("%s: se esperaba 400 %s, llegó %v", c.nombre, c.codigo, err)
		}
	}

	if _, err := Validar(Credenciales{Pasarela: "placetopay", URLBase: "http://127.0.0.1:8081", Login: "l", Secreto: "s"}, true); err != nil {
		t.Errorf("con la simulada se admite http://127.0.0.1: %v", err)
	}

	// Varios problemas: el primero manda y la lista completa va en data.errores.
	_, err := Validar(Credenciales{Pasarela: "placetopay", URLBase: "https://x.test"}, false)
	if err == nil || err.Codigo != "Canal.Sitio.LoginInvalido" {
		t.Fatalf("se esperaba el error del login primero, llegó %v", err)
	}
	if problemas := err.Datos.(map[string]any)["errores"].([]Problema); len(problemas) != 2 {
		t.Errorf("se esperaban 2 problemas, hay %+v", problemas)
	}
}

func TestElSecretoNoSeEscapa(t *testing.T) {
	c := credencialesValidas()
	var log bytes.Buffer
	slog.New(slog.NewJSONHandler(&log, nil)).Info("prueba", "credenciales", c, "secreto", c.Secreto)
	json1, _ := json.Marshal(c)
	salidas := []string{
		fmt.Sprintf("%v %+v %#v %s %q", c, c, c, c.Secreto, c.Secreto),
		log.String(),
		string(json1),
	}
	for _, s := range salidas {
		if strings.Contains(s, secretoDePrueba) {
			t.Fatalf("el secreto se escapó: %s", s)
		}
	}
	var leido struct {
		S Secreto `json:"s"`
	}
	if err := json.Unmarshal([]byte(`{"s":"`+secretoDePrueba+`"}`), &leido); err != nil || leido.S.Revelar() != secretoDePrueba {
		t.Fatalf("el secreto debía poder leerse del JSON del ERP: %v", err)
	}
}

func TestUltimosCuatro(t *testing.T) {
	for entrada, esperado := range map[string]string{"login-1234": "1234", "abc": "abc", "ñandú": "andú"} {
		if got := UltimosCuatro(entrada); got != esperado {
			t.Errorf("UltimosCuatro(%q) = %q", entrada, got)
		}
	}
}
