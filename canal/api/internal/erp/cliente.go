package erp

import (
	"bytes"
	"context"
	"crypto/ecdsa"
	"crypto/rand"
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"strings"
	"time"

	"github.com/golang-jwt/jwt/v5"
	"github.com/google/uuid"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
)

// Emisor, audiencia y vida del token de servicio canal → ERP (research R2; contracts/erp-api-de-servicio.md).
const (
	EmisorDelCanal     = "ingenia365-canal"
	AudienciaDelERP    = "ingenia365-erp"
	EmisorDelERP       = "ingenia365-erp"
	AudienciaDelCanal  = "ingenia365-canal"
	SujetoDePlataforma = "platform"
	VidaDelToken       = 60 * time.Second

	// Cabeceras con el contexto del asociado que el ERP usa para su auditoría (Principio X del ERP).
	CabeceraIP     = "X-Canal-Ip"
	CabeceraAgente = "X-Canal-UserAgent"
	CabeceraCuenta = "X-Canal-Account"

	prefijoServicio = "/api/canal/v1"
)

// ErrorDelERP es una respuesta de error del ERP con su sobre {code, message, data}.
type ErrorDelERP struct {
	Estado  int
	Codigo  string
	Mensaje string
	Datos   json.RawMessage
}

func (e *ErrorDelERP) Error() string {
	return fmt.Sprintf("el ERP respondió %d %s: %s", e.Estado, e.Codigo, e.Mensaje)
}

// ErrERPNoDisponible es un fallo de red o un 5xx sin sobre: el ERP de la cooperativa no respondió.
var ErrERPNoDisponible = errors.New("erp: el ERP no está disponible")

// Cliente llama a la API de servicio del ERP firmando un token nuevo por petición.
type Cliente struct {
	base  *url.URL
	llave *ecdsa.PrivateKey
	kid   string
	http  *http.Client
	ahora func() time.Time
}

// NuevoCliente crea el cliente. base es la URL interna del ERP (sin /api/canal/v1).
func NuevoCliente(base *url.URL, llave *ecdsa.PrivateKey, kid string, h *http.Client) *Cliente {
	if h == nil {
		h = &http.Client{Timeout: 15 * time.Second}
	}
	return &Cliente{base: base, llave: llave, kid: kid, http: h, ahora: time.Now}
}

// Cooperativa es una fila del directorio de cooperativas del ERP (GET /api/canal/v1/tenants).
type Cooperativa struct {
	TenantPublicID    uuid.UUID          `json:"tenantPublicId"`
	ChannelCode       *string            `json:"channelCode"`
	DisplayName       string             `json:"displayName"`
	Brand             MarcaDeCooperativa `json:"brand"`
	Support           SoporteCooperativa `json:"support"`
	InactivityMinutes int                `json:"inactivityMinutes"`
	SmsEnabled        bool               `json:"smsEnabled"`
	// PaymentSessionMinutes es el vencimiento de la sesión en la pasarela (FR-008); RequireOtpToPay, si se pide código
	// al confirmar un pago (FR-009).
	PaymentSessionMinutes int  `json:"paymentSessionMinutes"`
	RequireOtpToPay       bool `json:"requireOtpToPay"`
	// DueReminderDays: días de anticipación del aviso de cuota próxima (FR-012; 0 = sin aviso).
	DueReminderDays int `json:"dueReminderDays"`
}

// MarcaDeCooperativa es el logo y los colores de la cooperativa.
type MarcaDeCooperativa struct {
	LogoURL   *string `json:"logoUrl"`
	Primary   *string `json:"primary"`
	Secondary *string `json:"secondary"`
}

// SoporteCooperativa es el contacto de soporte de la cooperativa.
type SoporteCooperativa struct {
	Phone *string `json:"phone"`
	Email *string `json:"email"`
	Hours *string `json:"hours"`
}

// ListarCooperativas trae las cooperativas con el canal encendido.
func (c *Cliente) ListarCooperativas(ctx context.Context) ([]Cooperativa, error) {
	var cooperativas []Cooperativa
	if err := c.Hacer(ctx, http.MethodGet, SujetoDePlataforma, "/tenants", nil, &cooperativas); err != nil {
		return nil, err
	}
	return cooperativas, nil
}

// Hacer envía una petición a la API de servicio. sujeto es el PublicId de la cooperativa o SujetoDePlataforma;
// ruta va relativa a /api/canal/v1. Si destino no es nil, decodifica allí la respuesta.
func (c *Cliente) Hacer(ctx context.Context, metodo, sujeto, ruta string, cuerpo, destino any) error {
	cuerpoRespuesta, _, err := c.ejecutar(ctx, metodo, sujeto, ruta, cuerpo, "application/json")
	if err != nil {
		return err
	}
	if destino == nil || len(bytes.TrimSpace(cuerpoRespuesta)) == 0 {
		return nil
	}
	if err := json.Unmarshal(cuerpoRespuesta, destino); err != nil {
		return fmt.Errorf("erp: decodificar la respuesta de %s: %w", ruta, err)
	}
	return nil
}

// Descargar pide un archivo (un PDF) y devuelve sus bytes y su tipo. Los errores son los de Hacer.
func (c *Cliente) Descargar(ctx context.Context, sujeto, ruta string) ([]byte, string, error) {
	return c.ejecutar(ctx, http.MethodGet, sujeto, ruta, nil, "*/*")
}

// ejecutar envía la petición firmada y devuelve el cuerpo de una respuesta 2xx con su tipo; lo demás es error.
func (c *Cliente) ejecutar(ctx context.Context, metodo, sujeto, ruta string, cuerpo any, aceptar string) ([]byte, string, error) {
	token, err := c.firmar(sujeto)
	if err != nil {
		return nil, "", fmt.Errorf("erp: firmar el token de servicio: %w", err)
	}

	var lector io.Reader
	if cuerpo != nil {
		datos, err := json.Marshal(cuerpo)
		if err != nil {
			return nil, "", fmt.Errorf("erp: serializar el cuerpo: %w", err)
		}
		lector = bytes.NewReader(datos)
	}

	destinoURL := c.base.JoinPath(prefijoServicio, ruta)
	if i := strings.IndexByte(ruta, '?'); i >= 0 {
		destinoURL = c.base.JoinPath(prefijoServicio, ruta[:i])
		destinoURL.RawQuery = ruta[i+1:]
	}

	peticion, err := http.NewRequestWithContext(ctx, metodo, destinoURL.String(), lector)
	if err != nil {
		return nil, "", fmt.Errorf("erp: armar la petición: %w", err)
	}
	peticion.Header.Set("Authorization", "Bearer "+token)
	peticion.Header.Set("Accept", aceptar)
	if cuerpo != nil {
		peticion.Header.Set("Content-Type", "application/json")
	}
	if v := contexto.Correlacion(ctx); v != "" {
		peticion.Header.Set("X-Correlation-Id", v)
	}
	if v := contexto.IP(ctx); v != "" {
		peticion.Header.Set(CabeceraIP, v)
	}
	if v := contexto.Agente(ctx); v != "" {
		peticion.Header.Set(CabeceraAgente, v)
	}
	if cuenta, ok := contexto.Cuenta(ctx); ok {
		peticion.Header.Set(CabeceraCuenta, cuenta.String())
	}

	respuesta, err := c.http.Do(peticion)
	if err != nil {
		return nil, "", fmt.Errorf("%w: %v", ErrERPNoDisponible, err)
	}
	defer respuesta.Body.Close()

	cuerpoRespuesta, err := io.ReadAll(io.LimitReader(respuesta.Body, 8<<20))
	if err != nil {
		return nil, "", fmt.Errorf("%w: leer la respuesta: %v", ErrERPNoDisponible, err)
	}

	if respuesta.StatusCode >= 200 && respuesta.StatusCode < 300 {
		return cuerpoRespuesta, respuesta.Header.Get("Content-Type"), nil
	}

	var sobre struct {
		Code    string          `json:"code"`
		Message string          `json:"message"`
		Data    json.RawMessage `json:"data"`
	}
	if json.Unmarshal(cuerpoRespuesta, &sobre) == nil && sobre.Code != "" {
		return nil, "", &ErrorDelERP{Estado: respuesta.StatusCode, Codigo: sobre.Code, Mensaje: sobre.Message, Datos: sobre.Data}
	}
	if respuesta.StatusCode >= 500 {
		return nil, "", fmt.Errorf("%w: respondió %d sin sobre", ErrERPNoDisponible, respuesta.StatusCode)
	}
	return nil, "", &ErrorDelERP{Estado: respuesta.StatusCode, Codigo: "Canal.ERP.Respuesta", Mensaje: http.StatusText(respuesta.StatusCode)}
}

// firmar acuña un token ES256 de 60 s con jti aleatorio.
func (c *Cliente) firmar(sujeto string) (string, error) {
	jti := make([]byte, 16)
	if _, err := rand.Read(jti); err != nil {
		return "", err
	}
	ahora := c.ahora().UTC()
	reclamos := jwt.RegisteredClaims{
		Issuer:    EmisorDelCanal,
		Audience:  jwt.ClaimStrings{AudienciaDelERP},
		Subject:   sujeto,
		IssuedAt:  jwt.NewNumericDate(ahora),
		NotBefore: jwt.NewNumericDate(ahora),
		ExpiresAt: jwt.NewNumericDate(ahora.Add(VidaDelToken)),
		ID:        hex.EncodeToString(jti),
	}
	token := jwt.NewWithClaims(jwt.SigningMethodES256, reclamos)
	token.Header["kid"] = c.kid
	return token.SignedString(c.llave)
}
