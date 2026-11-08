package config

import (
	"bytes"
	"crypto/ecdsa"
	"crypto/elliptic"
	"crypto/rand"
	"crypto/x509"
	"encoding/base64"
	"encoding/pem"
	"errors"
	"io/fs"
	"log/slog"
	"strings"
	"testing"
	"time"
)

type entornoDePrueba struct {
	vars     map[string]string
	archivos map[string][]byte
}

func (e entornoDePrueba) fuente() Fuente {
	return Fuente{
		Buscar: func(n string) (string, bool) { v, ok := e.vars[n]; return v, ok },
		Entorno: func() []string {
			var r []string
			for k, v := range e.vars {
				r = append(r, k+"="+v)
			}
			return r
		},
		Leer: func(ruta string) ([]byte, error) {
			if c, ok := e.archivos[ruta]; ok {
				return c, nil
			}
			return nil, fs.ErrNotExist
		},
	}
}

func pemPrivada(t *testing.T) ([]byte, []byte) {
	t.Helper()
	llave, err := ecdsa.GenerateKey(elliptic.P256(), rand.Reader)
	if err != nil {
		t.Fatal(err)
	}
	der, err := x509.MarshalECPrivateKey(llave)
	if err != nil {
		t.Fatal(err)
	}
	pub, err := x509.MarshalPKIXPublicKey(&llave.PublicKey)
	if err != nil {
		t.Fatal(err)
	}
	return pem.EncodeToMemory(&pem.Block{Type: "EC PRIVATE KEY", Bytes: der}),
		pem.EncodeToMemory(&pem.Block{Type: "PUBLIC KEY", Bytes: pub})
}

func entornoValido(t *testing.T) entornoDePrueba {
	privada, publica := pemPrivada(t)
	kek := base64.StdEncoding.EncodeToString(bytes.Repeat([]byte{7}, 32))
	return entornoDePrueba{
		vars: map[string]string{
			"CANAL_BASE_URL":               "postgres://canal:clave-de-prueba@localhost:5434/ingenia365_canal",
			"CANAL_HOST_PUBLICO":           "asociados.ejemplo.test",
			"CANAL_ERP_URL":                "http://erp.ejemplo.test",
			"CANAL_LLAVE_PRIVADA_FILE":     "/llaves/canal-privada.pem",
			"CANAL_LLAVE_KID":              "dev1",
			"CANAL_ERP_LLAVE_PUBLICA_FILE": "/llaves/erp-publica.pem",
			"CANAL_KEK_V1_FILE":            "/llaves/kek-v1.b64",
		},
		archivos: map[string][]byte{
			"/llaves/canal-privada.pem": privada,
			"/llaves/erp-publica.pem":   publica,
			"/llaves/kek-v1.b64":        []byte(kek + "\n"),
		},
	}
}

func TestCargaValidaConValoresPorDefecto(t *testing.T) {
	cfg, err := CargarDe(entornoValido(t).fuente())
	if err != nil {
		t.Fatalf("no debía fallar: %v", err)
	}
	if cfg.Direccion != ":8080" || cfg.Pasarela != PasarelaSimulada || cfg.KEKActiva != 1 {
		t.Errorf("valores por defecto inesperados: %+v", cfg)
	}
	if cfg.Frecuencias.Cooperativas != 5*time.Minute {
		t.Errorf("frecuencia de cooperativas = %v", cfg.Frecuencias.Cooperativas)
	}
	if cfg.SMTP != nil {
		t.Error("sin CANAL_SMTP_HOST no debía haber SMTP")
	}
}

func TestFaltantesSeListanTodosDeUnaVez(t *testing.T) {
	_, err := CargarDe(entornoDePrueba{vars: map[string]string{}}.fuente())
	var e *ErrorDeConfiguracion
	if !errors.As(err, &e) {
		t.Fatalf("se esperaba ErrorDeConfiguracion, llegó %v", err)
	}
	for _, nombre := range []string{"CANAL_BASE_URL", "CANAL_HOST_PUBLICO", "CANAL_ERP_URL", "CANAL_LLAVE_PRIVADA_FILE",
		"CANAL_LLAVE_KID", "CANAL_ERP_LLAVE_PUBLICA_FILE", "CANAL_KEK_V1_FILE"} {
		if !strings.Contains(err.Error(), nombre) {
			t.Errorf("el error no nombra %s: %v", nombre, err)
		}
	}
}

func TestValoresInvalidos(t *testing.T) {
	e := entornoValido(t)
	e.vars["CANAL_PASARELA"] = "otra"
	e.vars["CANAL_FRECUENCIA_COOPERATIVAS"] = "pronto"
	e.archivos["/llaves/kek-v1.b64"] = []byte(base64.StdEncoding.EncodeToString([]byte("corta")))
	_, err := CargarDe(e.fuente())
	if err == nil {
		t.Fatal("debía fallar")
	}
	for _, nombre := range []string{"CANAL_PASARELA", "CANAL_FRECUENCIA_COOPERATIVAS", "CANAL_KEK_V1_FILE"} {
		if !strings.Contains(err.Error(), nombre) {
			t.Errorf("el error no nombra %s: %v", nombre, err)
		}
	}
}

func TestVariasKEKExigenLaActiva(t *testing.T) {
	e := entornoValido(t)
	e.vars["CANAL_KEK_V2_FILE"] = "/llaves/kek-v2.b64"
	e.archivos["/llaves/kek-v2.b64"] = []byte(base64.StdEncoding.EncodeToString(bytes.Repeat([]byte{9}, 32)))
	if _, err := CargarDe(e.fuente()); err == nil || !strings.Contains(err.Error(), "CANAL_KEK_ACTIVA") {
		t.Fatalf("debía exigir CANAL_KEK_ACTIVA: %v", err)
	}
	e.vars["CANAL_KEK_ACTIVA"] = "2"
	cfg, err := CargarDe(e.fuente())
	if err != nil {
		t.Fatal(err)
	}
	if cfg.KEKActiva != 2 || len(cfg.KEKs) != 2 {
		t.Errorf("KEK activa %d, versiones %d", cfg.KEKActiva, len(cfg.KEKs))
	}
}

func TestElLogNoLlevaSecretos(t *testing.T) {
	e := entornoValido(t)
	e.vars["CANAL_SMTP_HOST"] = "smtp.ejemplo.test"
	e.vars["CANAL_SMTP_REMITENTE"] = "canal@ejemplo.test"
	e.vars["CANAL_SMTP_USUARIO"] = "usuario"
	e.vars["CANAL_SMTP_CLAVE"] = "clave-smtp-de-prueba"
	cfg, err := CargarDe(e.fuente())
	if err != nil {
		t.Fatal(err)
	}
	var salida bytes.Buffer
	slog.New(slog.NewJSONHandler(&salida, nil)).Info("configuración", "config", cfg)
	for _, secreto := range []string{"clave-de-prueba", "clave-smtp-de-prueba", "PRIVATE KEY"} {
		if strings.Contains(salida.String(), secreto) {
			t.Errorf("el log contiene un secreto (%s): %s", secreto, salida.String())
		}
	}
}
