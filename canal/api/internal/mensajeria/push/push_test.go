package push

import (
	"context"
	"crypto/aes"
	"crypto/cipher"
	"crypto/ecdh"
	"crypto/rand"
	"crypto/rsa"
	"crypto/x509"
	"encoding/base64"
	"encoding/binary"
	"encoding/json"
	"encoding/pem"
	"errors"
	"io"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"github.com/google/uuid"
)

// descifrar hace lo que hace el navegador con un cuerpo aes128gcm (RFC 8291 §3.4 y RFC 8188).
func descifrar(t *testing.T, cuerpo []byte, ua *ecdh.PrivateKey, auth []byte) []byte {
	t.Helper()
	sal, rs, idlen := cuerpo[:16], binary.BigEndian.Uint32(cuerpo[16:20]), int(cuerpo[20])
	if rs != TamanoDeRegistro || idlen != 65 {
		t.Fatalf("encabezado: rs=%d idlen=%d", rs, idlen)
	}
	asPublica := cuerpo[21 : 21+idlen]
	as, err := ecdh.P256().NewPublicKey(asPublica)
	if err != nil {
		t.Fatal(err)
	}
	compartido, err := ua.ECDH(as)
	if err != nil {
		t.Fatal(err)
	}
	info := append(append([]byte("WebPush: info\x00"), ua.PublicKey().Bytes()...), asPublica...)
	ikm, _ := expandir(compartido, auth, info, 32)
	cek, _ := expandir(ikm, sal, []byte("Content-Encoding: aes128gcm\x00"), 16)
	nonce, _ := expandir(ikm, sal, []byte("Content-Encoding: nonce\x00"), 12)
	bloque, _ := aes.NewCipher(cek)
	gcm, _ := cipher.NewGCM(bloque)
	claro, err := gcm.Open(nil, nonce, cuerpo[21+idlen:], nil)
	if err != nil {
		t.Fatalf("descifrar: %v", err)
	}
	if claro[len(claro)-1] != 0x02 {
		t.Fatalf("sin delimitador de último registro")
	}
	return claro[:len(claro)-1]
}

func llavesDelNavegador(t *testing.T) (*ecdh.PrivateKey, []byte, string, string) {
	t.Helper()
	ua, err := ecdh.P256().GenerateKey(rand.Reader)
	if err != nil {
		t.Fatal(err)
	}
	auth := make([]byte, 16)
	_, _ = rand.Read(auth)
	return ua, auth, base64.RawURLEncoding.EncodeToString(ua.PublicKey().Bytes()), base64.RawURLEncoding.EncodeToString(auth)
}

func TestElCifradoDeWebPushLoDescifraElNavegador(t *testing.T) {
	ua, auth, p256dh, authB64 := llavesDelNavegador(t)
	mensaje := []byte(`{"title":"Tu cuota vence pronto","body":"Crédito CR-1: $ 332.500"}`)
	cuerpo, err := Cifrar(mensaje, p256dh, authB64)
	if err != nil {
		t.Fatal(err)
	}
	if got := descifrar(t, cuerpo, ua, auth); string(got) != string(mensaje) {
		t.Fatalf("descifrado: %q", got)
	}
	if _, err := Cifrar(mensaje, p256dh, "corto"); err == nil {
		t.Fatal("un auth que no es de 16 bytes debe rechazarse")
	}
}

func vapidDePrueba(t *testing.T) string {
	t.Helper()
	k, err := ecdh.P256().GenerateKey(rand.Reader)
	if err != nil {
		t.Fatal(err)
	}
	return base64.RawURLEncoding.EncodeToString(k.Bytes())
}

func TestWebPushFirmaConVAPIDYRevocaLaVencida(t *testing.T) {
	ua, auth, p256dh, authB64 := llavesDelNavegador(t)
	var recibido []byte
	var cabeceras http.Header
	estado := http.StatusCreated
	srv := httptest.NewTLSServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		recibido, _ = io.ReadAll(r.Body)
		cabeceras = r.Header.Clone()
		w.WriteHeader(estado)
	}))
	defer srv.Close()
	w, err := NuevoWebPush(vapidDePrueba(t), "mailto:soporte@ejemplo.test", srv.Client())
	if err != nil {
		t.Fatal(err)
	}
	s := Suscripcion{ID: uuid.New(), Tipo: WebPush, Endpoint: srv.URL + "/push/abc", P256dh: p256dh, Auth: authB64}
	if err := w.Enviar(context.Background(), s, Notificacion{Titulo: "Hola", Cuerpo: "Prueba", URL: "/cooperativa", Tipo: "due_reminder"}); err != nil {
		t.Fatal(err)
	}
	if cabeceras.Get("Content-Encoding") != "aes128gcm" || !strings.HasPrefix(cabeceras.Get("Authorization"), "vapid t=") ||
		!strings.Contains(cabeceras.Get("Authorization"), ", k="+w.LlavePublica()) || cabeceras.Get("TTL") == "" {
		t.Fatalf("cabeceras: %v", cabeceras)
	}
	var n Notificacion
	if err := json.Unmarshal(descifrar(t, recibido, ua, auth), &n); err != nil || n.Titulo != "Hola" || n.URL != "/cooperativa" {
		t.Fatalf("notificación: %+v %v", n, err)
	}
	estado = http.StatusGone
	if err := w.Enviar(context.Background(), s, Notificacion{Titulo: "x"}); !errors.Is(err, ErrVencida) {
		t.Fatalf("410 debe ser ErrVencida: %v", err)
	}
	if _, err := NuevoWebPush("no-es-una-llave", "mailto:x@y.z", nil); err == nil {
		t.Fatal("una llave VAPID inválida debe rechazarse")
	}
}

func TestFCMPideElTokenUnaVezYEnviaAlTelefono(t *testing.T) {
	llave, err := rsa.GenerateKey(rand.Reader, 2048)
	if err != nil {
		t.Fatal(err)
	}
	pemLlave := pem.EncodeToMemory(&pem.Block{Type: "RSA PRIVATE KEY", Bytes: x509.MarshalPKCS1PrivateKey(llave)})
	tokens, envios := 0, 0
	var ultimo map[string]any
	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		switch {
		case r.URL.Path == "/token":
			tokens++
			_ = r.ParseForm()
			if r.Form.Get("grant_type") != "urn:ietf:params:oauth:grant-type:jwt-bearer" || r.Form.Get("assertion") == "" {
				w.WriteHeader(http.StatusBadRequest)
				return
			}
			_, _ = w.Write([]byte(`{"access_token":"tok-1","expires_in":3600}`))
		case strings.HasSuffix(r.URL.Path, "/messages:send"):
			envios++
			if r.Header.Get("Authorization") != "Bearer tok-1" {
				w.WriteHeader(http.StatusUnauthorized)
				return
			}
			_ = json.NewDecoder(r.Body).Decode(&ultimo)
			if strings.Contains(r.URL.Path, "vencido") {
				w.WriteHeader(http.StatusNotFound)
				return
			}
			_, _ = w.Write([]byte(`{"name":"projects/p/messages/1"}`))
		}
	}))
	defer srv.Close()
	cuenta, _ := json.Marshal(map[string]string{"project_id": "p", "client_email": "canal@p.iam.gserviceaccount.com", "private_key": string(pemLlave),
		"token_uri": srv.URL + "/token"})
	f, err := NuevoFCM(cuenta, srv.Client())
	if err != nil {
		t.Fatal(err)
	}
	f.base = srv.URL
	s := Suscripcion{ID: uuid.New(), Tipo: FCM, Endpoint: "token-del-telefono"}
	for range 2 {
		if err := f.Enviar(context.Background(), s, Notificacion{Titulo: "Hola", Cuerpo: "Prueba", URL: "/cooperativa", Tipo: "payment_applied"}); err != nil {
			t.Fatal(err)
		}
	}
	if tokens != 1 || envios != 2 {
		t.Fatalf("tokens %d, envíos %d", tokens, envios)
	}
	mensaje := ultimo["message"].(map[string]any)
	if mensaje["token"] != "token-del-telefono" || mensaje["notification"].(map[string]any)["title"] != "Hola" {
		t.Fatalf("mensaje: %v", ultimo)
	}
	f.cuenta.ProyectoID = "vencido"
	if err := f.Enviar(context.Background(), s, Notificacion{Titulo: "x"}); !errors.Is(err, ErrVencida) {
		t.Fatalf("404 debe ser ErrVencida: %v", err)
	}
}
