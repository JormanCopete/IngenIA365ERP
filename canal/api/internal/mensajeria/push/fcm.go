package push

import (
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"strings"
	"sync"
	"time"

	"github.com/golang-jwt/jwt/v5"
)

// FCM HTTP v1 para la app de tiendas (Android e iOS por Firebase): el canal se identifica con la cuenta de servicio del proyecto
// (OAuth 2.0 con un JWT RS256 firmado con su llave, alcance firebase.messaging) y manda el aviso al token del teléfono. La cuenta de
// servicio llega por configuración del ambiente y nunca va al repositorio.

const alcanceFCM = "https://www.googleapis.com/auth/firebase.messaging"

// CuentaDeServicio es lo que se usa del JSON de la cuenta de servicio de Google.
type CuentaDeServicio struct {
	ProyectoID   string `json:"project_id"`
	Correo       string `json:"client_email"`
	LlavePrivada string `json:"private_key"`
	URLDeToken   string `json:"token_uri"`
}

// FCMv1 entrega por FCM HTTP v1.
type FCMv1 struct {
	cuenta CuentaDeServicio
	firma  any
	http   *http.Client
	base   string
	ahora  func() time.Time

	mu    sync.Mutex
	token string
	vence time.Time
}

// NuevoFCM crea el enviador con el JSON de la cuenta de servicio.
func NuevoFCM(jsonDeCuenta []byte, h *http.Client) (*FCMv1, error) {
	var c CuentaDeServicio
	if err := json.Unmarshal(jsonDeCuenta, &c); err != nil || c.ProyectoID == "" || c.Correo == "" || c.LlavePrivada == "" {
		return nil, errors.New("push: la cuenta de servicio de FCM no tiene project_id, client_email y private_key")
	}
	if c.URLDeToken == "" {
		c.URLDeToken = "https://oauth2.googleapis.com/token"
	}
	llave, err := jwt.ParseRSAPrivateKeyFromPEM([]byte(c.LlavePrivada))
	if err != nil {
		return nil, fmt.Errorf("push: llave de la cuenta de servicio de FCM: %w", err)
	}
	if h == nil {
		h = &http.Client{Timeout: 15 * time.Second}
	}
	return &FCMv1{cuenta: c, firma: llave, http: h, base: "https://fcm.googleapis.com", ahora: time.Now}, nil
}

func (f *FCMv1) Enviar(ctx context.Context, s Suscripcion, n Notificacion) error {
	token, err := f.acceso(ctx)
	if err != nil {
		return err
	}
	cuerpo, _ := json.Marshal(map[string]any{"message": map[string]any{
		"token":        s.Endpoint,
		"notification": map[string]string{"title": n.Titulo, "body": n.Cuerpo},
		"data":         map[string]string{"url": n.URL, "kind": n.Tipo},
	}})
	req, err := http.NewRequestWithContext(ctx, http.MethodPost, f.base+"/v1/projects/"+url.PathEscape(f.cuenta.ProyectoID)+"/messages:send", bytes.NewReader(cuerpo))
	if err != nil {
		return err
	}
	req.Header.Set("Authorization", "Bearer "+token)
	req.Header.Set("Content-Type", "application/json")
	resp, err := f.http.Do(req)
	if err != nil {
		return fmt.Errorf("push: FCM: %w", err)
	}
	defer resp.Body.Close()
	respuesta, _ := io.ReadAll(io.LimitReader(resp.Body, 16<<10))
	switch {
	case resp.StatusCode == http.StatusNotFound || strings.Contains(string(respuesta), "UNREGISTERED"):
		return ErrVencida
	case resp.StatusCode >= 300:
		return fmt.Errorf("push: FCM respondió %d", resp.StatusCode)
	}
	return nil
}

// acceso devuelve el token OAuth vigente o pide uno nuevo con el JWT de la cuenta de servicio.
func (f *FCMv1) acceso(ctx context.Context) (string, error) {
	f.mu.Lock()
	defer f.mu.Unlock()
	ahora := f.ahora()
	if f.token != "" && ahora.Before(f.vence.Add(-time.Minute)) {
		return f.token, nil
	}
	afirmacion, err := jwt.NewWithClaims(jwt.SigningMethodRS256, jwt.MapClaims{
		"iss": f.cuenta.Correo, "scope": alcanceFCM, "aud": f.cuenta.URLDeToken, "iat": ahora.Unix(), "exp": ahora.Add(time.Hour).Unix(),
	}).SignedString(f.firma)
	if err != nil {
		return "", fmt.Errorf("push: firmar la afirmación de FCM: %w", err)
	}
	forma := url.Values{"grant_type": {"urn:ietf:params:oauth:grant-type:jwt-bearer"}, "assertion": {afirmacion}}
	req, err := http.NewRequestWithContext(ctx, http.MethodPost, f.cuenta.URLDeToken, strings.NewReader(forma.Encode()))
	if err != nil {
		return "", err
	}
	req.Header.Set("Content-Type", "application/x-www-form-urlencoded")
	resp, err := f.http.Do(req)
	if err != nil {
		return "", fmt.Errorf("push: token de FCM: %w", err)
	}
	defer resp.Body.Close()
	var r struct {
		Token  string `json:"access_token"`
		Expira int    `json:"expires_in"`
	}
	if resp.StatusCode != http.StatusOK || json.NewDecoder(io.LimitReader(resp.Body, 16<<10)).Decode(&r) != nil || r.Token == "" {
		return "", fmt.Errorf("push: Google no dio el token de FCM (%d)", resp.StatusCode)
	}
	f.token, f.vence = r.Token, ahora.Add(time.Duration(r.Expira)*time.Second)
	return f.token, nil
}
