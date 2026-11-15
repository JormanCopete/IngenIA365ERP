package push

import (
	"bytes"
	"context"
	"crypto/aes"
	"crypto/cipher"
	"crypto/ecdh"
	"crypto/ecdsa"
	"crypto/elliptic"
	"crypto/rand"
	"crypto/sha256"
	"encoding/base64"
	"encoding/binary"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"strconv"
	"time"

	"github.com/golang-jwt/jwt/v5"
	"golang.org/x/crypto/hkdf"
)

// Web Push con VAPID: el cuerpo va cifrado con aes128gcm (RFC 8291) para la llave del navegador, y el servidor se identifica con un
// JWT ES256 firmado con la llave VAPID (RFC 8292). La llave pública se publica en GET /api/v1/notifications/webpush-key para que la
// PWA se suscriba con ella.

// TamanoDeRegistro es el rs del encabezado aes128gcm (un solo registro: el aviso es corto).
const TamanoDeRegistro = 4096

// VidaDelAviso es el TTL que se pide al servicio de push.
const VidaDelAviso = 24 * time.Hour

// WebPushVAPID entrega por Web Push.
type WebPushVAPID struct {
	privada *ecdsa.PrivateKey
	publica string
	sujeto  string
	http    *http.Client
	ahora   func() time.Time
}

// NuevoWebPush crea el enviador con la llave privada VAPID (el escalar d de P-256 en base64url, 32 bytes) y el sujeto
// (mailto: o https: de quien opera el canal).
func NuevoWebPush(privadaB64, sujeto string, h *http.Client) (*WebPushVAPID, error) {
	d, err := base64.RawURLEncoding.DecodeString(privadaB64)
	if err != nil || len(d) != 32 {
		return nil, errors.New("push: la llave VAPID debe ser el escalar de P-256 en base64url (32 bytes)")
	}
	llave, err := ecdh.P256().NewPrivateKey(d)
	if err != nil {
		return nil, fmt.Errorf("push: llave VAPID inválida: %w", err)
	}
	publica := llave.PublicKey().Bytes()
	privada, err := ecdsa.ParseRawPrivateKey(elliptic.P256(), d)
	if err != nil {
		return nil, fmt.Errorf("push: llave VAPID inválida: %w", err)
	}
	if h == nil {
		h = &http.Client{Timeout: 15 * time.Second}
	}
	return &WebPushVAPID{privada: privada, publica: base64.RawURLEncoding.EncodeToString(publica), sujeto: sujeto, http: h, ahora: time.Now}, nil
}

// LlavePublica es la llave VAPID pública (punto sin comprimir en base64url) que usa el navegador al suscribirse.
func (w *WebPushVAPID) LlavePublica() string { return w.publica }

func (w *WebPushVAPID) Enviar(ctx context.Context, s Suscripcion, n Notificacion) error {
	cuerpo, err := json.Marshal(n)
	if err != nil {
		return err
	}
	cifrado, err := Cifrar(cuerpo, s.P256dh, s.Auth)
	if err != nil {
		return err
	}
	destino, err := url.Parse(s.Endpoint)
	if err != nil || destino.Scheme != "https" {
		return fmt.Errorf("push: endpoint de Web Push inválido")
	}
	firma, err := jwt.NewWithClaims(jwt.SigningMethodES256, jwt.MapClaims{
		"aud": destino.Scheme + "://" + destino.Host,
		"exp": w.ahora().Add(12 * time.Hour).Unix(),
		"sub": w.sujeto,
	}).SignedString(w.privada)
	if err != nil {
		return fmt.Errorf("push: firmar VAPID: %w", err)
	}
	req, err := http.NewRequestWithContext(ctx, http.MethodPost, s.Endpoint, bytes.NewReader(cifrado))
	if err != nil {
		return err
	}
	req.Header.Set("Content-Encoding", "aes128gcm")
	req.Header.Set("Content-Type", "application/octet-stream")
	req.Header.Set("TTL", strconv.Itoa(int(VidaDelAviso.Seconds())))
	req.Header.Set("Urgency", "normal")
	req.Header.Set("Authorization", "vapid t="+firma+", k="+w.publica)
	resp, err := w.http.Do(req)
	if err != nil {
		return fmt.Errorf("push: Web Push: %w", err)
	}
	defer resp.Body.Close()
	_, _ = io.Copy(io.Discard, io.LimitReader(resp.Body, 4<<10))
	switch {
	case resp.StatusCode == http.StatusNotFound || resp.StatusCode == http.StatusGone:
		return ErrVencida
	case resp.StatusCode >= 300:
		return fmt.Errorf("push: Web Push respondió %d", resp.StatusCode)
	}
	return nil
}

// Cifrar arma el cuerpo aes128gcm de RFC 8291 para la llave del navegador (p256dh, punto sin comprimir) y su secreto (auth),
// ambos en base64url.
func Cifrar(mensaje []byte, p256dhB64, authB64 string) ([]byte, error) {
	uaPublica, err := decodificar(p256dhB64)
	if err != nil {
		return nil, fmt.Errorf("push: p256dh inválida: %w", err)
	}
	secreto, err := decodificar(authB64)
	if err != nil || len(secreto) != 16 {
		return nil, errors.New("push: auth inválida")
	}
	ua, err := ecdh.P256().NewPublicKey(uaPublica)
	if err != nil {
		return nil, fmt.Errorf("push: p256dh no es un punto de P-256: %w", err)
	}
	efimera, err := ecdh.P256().GenerateKey(rand.Reader)
	if err != nil {
		return nil, err
	}
	compartido, err := efimera.ECDH(ua)
	if err != nil {
		return nil, err
	}
	asPublica := efimera.PublicKey().Bytes()
	sal := make([]byte, 16)
	if _, err := rand.Read(sal); err != nil {
		return nil, err
	}

	// IKM = HKDF(auth, ecdh, "WebPush: info\0" || ua_public || as_public, 32).
	info := append(append([]byte("WebPush: info\x00"), uaPublica...), asPublica...)
	ikm, err := expandir(compartido, secreto, info, 32)
	if err != nil {
		return nil, err
	}
	cek, err := expandir(ikm, sal, []byte("Content-Encoding: aes128gcm\x00"), 16)
	if err != nil {
		return nil, err
	}
	nonce, err := expandir(ikm, sal, []byte("Content-Encoding: nonce\x00"), 12)
	if err != nil {
		return nil, err
	}
	bloque, err := aes.NewCipher(cek)
	if err != nil {
		return nil, err
	}
	gcm, err := cipher.NewGCM(bloque)
	if err != nil {
		return nil, err
	}
	// Un solo registro: el mensaje y el delimitador de último registro (0x02).
	texto := append(append([]byte(nil), mensaje...), 0x02)
	if len(texto)+gcm.Overhead() > TamanoDeRegistro {
		return nil, errors.New("push: el aviso es demasiado largo para Web Push")
	}
	cifrado := gcm.Seal(nil, nonce, texto, nil)

	encabezado := make([]byte, 0, 16+4+1+len(asPublica))
	encabezado = append(encabezado, sal...)
	encabezado = binary.BigEndian.AppendUint32(encabezado, TamanoDeRegistro)
	encabezado = append(encabezado, byte(len(asPublica)))
	encabezado = append(encabezado, asPublica...)
	return append(encabezado, cifrado...), nil
}

func expandir(secreto, sal, info []byte, largo int) ([]byte, error) {
	salida := make([]byte, largo)
	if _, err := io.ReadFull(hkdf.New(sha256.New, secreto, sal, info), salida); err != nil {
		return nil, err
	}
	return salida, nil
}

// decodificar acepta base64url con o sin relleno (los navegadores mandan sin relleno).
func decodificar(s string) ([]byte, error) {
	if b, err := base64.RawURLEncoding.DecodeString(s); err == nil {
		return b, nil
	}
	return base64.URLEncoding.DecodeString(s)
}
