// Package placetopay es el adaptador de Placetopay WebCheckout (Avalpay) detrás de pasarela.Pasarela (research R11).
// Nada fuera de este paquete conoce su formato. Ninguna URL ni credencial de una cooperativa vive en el código: la URL
// base y el login llegan con el sitio y el secreto sale cifrado de la base.
package placetopay

import (
	"bytes"
	"context"
	"crypto/rand"
	"crypto/sha1" //nolint:gosec // la pasarela todavía firma con SHA-1 los sitios que no activaron SHA-256 (R11)
	"crypto/sha256"
	"crypto/subtle"
	"encoding/base64"
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"net/http"
	"strconv"
	"strings"
	"time"

	"github.com/google/uuid"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/pasarela"
)

// Pasarela habla con Placetopay.
type Pasarela struct {
	http  *http.Client
	ahora func() time.Time
	// nonce genera los bytes crudos del nonce (16); en las pruebas, fijo.
	nonce func() ([]byte, error)
}

// Nueva crea el adaptador. h puede ser nil (cliente con 20 s de espera).
func Nueva(h *http.Client) *Pasarela {
	if h == nil {
		h = &http.Client{Timeout: 20 * time.Second}
	}
	return &Pasarela{http: h, ahora: time.Now, nonce: nonceAleatorio}
}

func nonceAleatorio() ([]byte, error) {
	b := make([]byte, 16)
	_, err := rand.Read(b)
	return b, err
}

var _ pasarela.Pasarela = (*Pasarela)(nil)

// --------------------------------------------------------------------------------------------- autenticación --

// Autenticacion es el bloque auth de cada petición.
type Autenticacion struct {
	Login   string `json:"login"`
	TranKey string `json:"tranKey"`
	Nonce   string `json:"nonce"`
	Seed    string `json:"seed"`
}

// TranKey = Base64(SHA-256(nonceCrudo + seed + secreto)) (R11).
func TranKey(nonceCrudo []byte, seed, secreto string) string {
	h := sha256.New()
	h.Write(nonceCrudo)
	h.Write([]byte(seed))
	h.Write([]byte(secreto))
	return base64.StdEncoding.EncodeToString(h.Sum(nil))
}

func (p *Pasarela) autenticacion(sitio pasarela.Sitio) (Autenticacion, error) {
	crudo, err := p.nonce()
	if err != nil {
		return Autenticacion{}, fmt.Errorf("placetopay: generar el nonce: %w", err)
	}
	// La semilla en ISO 8601 con zona; la pasarela la rechaza si está a más de 5 min de su reloj.
	seed := p.ahora().UTC().Format(time.RFC3339)
	return Autenticacion{
		Login:   sitio.Login,
		TranKey: TranKey(crudo, seed, sitio.Secreto.Revelar()),
		Nonce:   base64.StdEncoding.EncodeToString(crudo),
		Seed:    seed,
	}, nil
}

// ----------------------------------------------------------------------------------------------------- formato --

// Motivo es status.reason: la pasarela lo manda como texto o como número.
type Motivo string

// UnmarshalJSON acepta texto o número.
func (m *Motivo) UnmarshalJSON(b []byte) error {
	if len(b) > 0 && b[0] == '"' {
		var s string
		if err := json.Unmarshal(b, &s); err != nil {
			return err
		}
		*m = Motivo(s)
		return nil
	}
	*m = Motivo(strings.Trim(string(b), " "))
	if *m == "null" {
		*m = ""
	}
	return nil
}

// EstadoPTP es el bloque status.
type EstadoPTP struct {
	Status  string `json:"status"`
	Reason  Motivo `json:"reason"`
	Message string `json:"message"`
	Date    string `json:"date"`
}

type persona struct {
	DocumentType string `json:"documentType"`
	Document     string `json:"document"`
	Name         string `json:"name"`
	Surname      string `json:"surname,omitempty"`
	Email        string `json:"email"`
	Mobile       string `json:"mobile,omitempty"`
}

type impuesto struct {
	Kind   string `json:"kind"`
	Amount int64  `json:"amount"`
	Base   int64  `json:"base,omitempty"`
}

type monto struct {
	Currency string     `json:"currency"`
	Total    int64      `json:"total"`
	Taxes    []impuesto `json:"taxes,omitempty"`
}

type pago struct {
	Reference    string `json:"reference"`
	Description  string `json:"description"`
	Amount       monto  `json:"amount"`
	AllowPartial bool   `json:"allowPartial"`
}

type pedidoDeSesion struct {
	Auth          Autenticacion `json:"auth"`
	Locale        string        `json:"locale"`
	Buyer         *persona      `json:"buyer,omitempty"`
	Payment       pago          `json:"payment"`
	Expiration    string        `json:"expiration"`
	ReturnURL     string        `json:"returnUrl"`
	IPAddress     string        `json:"ipAddress"`
	UserAgent     string        `json:"userAgent"`
	PaymentMethod string        `json:"paymentMethod,omitempty"`
}

type respuestaDeSesion struct {
	Status     EstadoPTP `json:"status"`
	RequestID  int64     `json:"requestId"`
	ProcessURL string    `json:"processUrl"`
}

type pagoPTP struct {
	Status            EstadoPTP `json:"status"`
	InternalReference int64     `json:"internalReference"`
	PaymentMethod     string    `json:"paymentMethod"`
	PaymentMethodName string    `json:"paymentMethodName"`
	IssuerName        string    `json:"issuerName"`
	Authorization     string    `json:"authorization"`
	Reference         string    `json:"reference"`
	Receipt           string    `json:"receipt"`
	Franchise         string    `json:"franchise"`
	Refunded          bool      `json:"refunded"`
	Amount            struct {
		From struct {
			Currency string      `json:"currency"`
			Total    json.Number `json:"total"`
		} `json:"from"`
	} `json:"amount"`
}

type respuestaDeConsulta struct {
	RequestID int64     `json:"requestId"`
	Status    EstadoPTP `json:"status"`
	Request   struct {
		Expiration string `json:"expiration"`
		Payment    struct {
			Reference string `json:"reference"`
		} `json:"payment"`
	} `json:"request"`
	Payment []pagoPTP `json:"payment"`
}

// ------------------------------------------------------------------------------------------------- operaciones --

// CrearSesion crea la sesión de WebCheckout (POST /api/session).
func (p *Pasarela) CrearSesion(ctx context.Context, sitio pasarela.Sitio, s pasarela.SolicitudDeSesion) (pasarela.SesionCreada, error) {
	tipo, err := pasarela.ValidarComprador(s.Comprador)
	if err != nil {
		return pasarela.SesionCreada{}, err
	}
	if !pasarela.ReferenciaValida(s.Referencia) || s.Total <= 0 {
		return pasarela.SesionCreada{}, fmt.Errorf("placetopay: referencia o total inválidos")
	}
	auth, err := p.autenticacion(sitio)
	if err != nil {
		return pasarela.SesionCreada{}, err
	}
	cuerpo := pedidoDeSesion{
		Auth:   auth,
		Locale: "es_CO",
		Buyer: &persona{
			DocumentType: tipo,
			Document:     s.Comprador.NumeroDeDocumento,
			Name:         strings.TrimSpace(s.Comprador.Nombres),
			Surname:      strings.TrimSpace(s.Comprador.Apellidos),
			Email:        s.Comprador.Correo,
			Mobile:       s.Comprador.Celular,
		},
		Payment: pago{
			Reference:   s.Referencia,
			Description: recortar(s.Descripcion, 250),
			Amount:      monto{Currency: "COP", Total: s.Total},
		},
		Expiration:    s.Vence.UTC().Format(time.RFC3339),
		ReturnURL:     s.URLDeRetorno,
		IPAddress:     s.IP,
		UserAgent:     recortar(s.Agente, 255),
		PaymentMethod: strings.Join(medios(s.Medios, sitio.MediosDePago), ","),
	}
	for _, i := range s.Impuestos {
		cuerpo.Payment.Amount.Taxes = append(cuerpo.Payment.Amount.Taxes, impuesto{Kind: i.Tipo, Amount: i.Valor, Base: i.Base})
	}
	var r respuestaDeSesion
	if err := p.enviar(ctx, sitio, "/api/session", cuerpo, &r); err != nil {
		return pasarela.SesionCreada{}, err
	}
	if r.Status.Status != "OK" || r.RequestID == 0 || r.ProcessURL == "" {
		return pasarela.SesionCreada{}, &pasarela.ErrRechazo{Codigo: string(r.Status.Reason), Mensaje: r.Status.Message}
	}
	return pasarela.SesionCreada{RequestID: r.RequestID, ProcessURL: r.ProcessURL}, nil
}

// ConsultarSesion consulta la sesión (POST /api/session/{requestId}). El estado de la sesión decide; del arreglo de
// pagos se toma el aprobado.
func (p *Pasarela) ConsultarSesion(ctx context.Context, sitio pasarela.Sitio, requestID int64) (pasarela.EstadoDeSesion, error) {
	auth, err := p.autenticacion(sitio)
	if err != nil {
		return pasarela.EstadoDeSesion{}, err
	}
	var r respuestaDeConsulta
	if err := p.enviar(ctx, sitio, "/api/session/"+strconv.FormatInt(requestID, 10), map[string]any{"auth": auth}, &r); err != nil {
		return pasarela.EstadoDeSesion{}, err
	}
	return Normalizar(r, p.ahora()), nil
}

// Normalizar traduce la respuesta de la consulta al estado del canal (contracts/pasarela-y-app.md). Exportada para
// las pruebas con respuestas grabadas.
func Normalizar(r respuestaDeConsulta, ahora time.Time) pasarela.EstadoDeSesion {
	e := pasarela.EstadoDeSesion{
		Crudo:   r.Status.Status,
		Motivo:  string(r.Status.Reason),
		Mensaje: r.Status.Message,
	}
	if t, err := time.Parse(time.RFC3339, r.Status.Date); err == nil {
		e.Fecha = t
	}
	switch r.Status.Status {
	case "PENDING":
		e.Estado = pasarela.EstadoPendiente
	case "APPROVED":
		e.Estado = pasarela.EstadoAprobado
	case "REJECTED":
		e.Estado = pasarela.EstadoRechazado
		// Vencida: la pasarela marca la sesión rechazada con motivo EX, o la rechaza sin ningún intento de pago después
		// de su vencimiento (por verificar en el ambiente de pruebas, R11).
		if r.Status.Reason == "EX" || (len(r.Payment) == 0 && vencida(r.Request.Expiration, ahora)) {
			e.Estado = pasarela.EstadoVencido
		}
	case "APPROVED_PARTIAL", "PARTIAL_EXPIRED":
		// No se espera (allowPartial = false): queda pendiente para que una persona lo mire.
		e.Estado = pasarela.EstadoPendiente
		e.Diferencia = true
	default:
		e.Estado = pasarela.EstadoFallido
	}
	for _, pg := range r.Payment {
		if pg.Status.Status != "APPROVED" {
			continue
		}
		a := &pasarela.Aprobacion{
			ReferenciaInterna: pg.InternalReference,
			Autorizacion:      pg.Authorization,
			Recibo:            pg.Receipt,
			Franquicia:        pg.Franchise,
			Medio:             pg.PaymentMethodName,
			Emisor:            pg.IssuerName,
		}
		if t, err := time.Parse(time.RFC3339, pg.Status.Date); err == nil {
			a.Fecha = t
		}
		e.Aprobacion = a
		break
	}
	e.Respuesta = resumen(r)
	return e
}

func vencida(expiracion string, ahora time.Time) bool {
	t, err := time.Parse(time.RFC3339, expiracion)
	return err == nil && ahora.After(t)
}

// resumen arma la respuesta que se guarda en attempt_events: sólo estados y datos del pago, sin el bloque del
// comprador, sin processorFields ni instrumento (FR-042: nada de tarjeta, ni siquiera tokenizado).
func resumen(r respuestaDeConsulta) json.RawMessage {
	type pagoResumido struct {
		Status            EstadoPTP `json:"status"`
		InternalReference int64     `json:"internalReference"`
		PaymentMethod     string    `json:"paymentMethod"`
		PaymentMethodName string    `json:"paymentMethodName"`
		IssuerName        string    `json:"issuerName"`
		Authorization     string    `json:"authorization"`
		Receipt           string    `json:"receipt"`
		Franchise         string    `json:"franchise"`
		Refunded          bool      `json:"refunded"`
	}
	pagos := make([]pagoResumido, 0, len(r.Payment))
	for _, pg := range r.Payment {
		pagos = append(pagos, pagoResumido{pg.Status, pg.InternalReference, pg.PaymentMethod, pg.PaymentMethodName,
			pg.IssuerName, pg.Authorization, pg.Receipt, pg.Franchise, pg.Refunded})
	}
	b, _ := json.Marshal(map[string]any{
		"requestId": r.RequestID,
		"status":    r.Status,
		"reference": r.Request.Payment.Reference,
		"payment":   pagos,
	})
	return b
}

// Reversar reversa un pago aprobado (POST /api/transaction, action = reverse). Sólo antes del corte diario; eso lo
// decide quien llama.
func (p *Pasarela) Reversar(ctx context.Context, sitio pasarela.Sitio, referenciaInterna int64) (pasarela.ResultadoDeReverso, error) {
	auth, err := p.autenticacion(sitio)
	if err != nil {
		return pasarela.ResultadoDeReverso{}, err
	}
	var r struct {
		Status        EstadoPTP `json:"status"`
		Authorization string    `json:"authorization"`
	}
	cuerpo := map[string]any{"auth": auth, "internalReference": referenciaInterna, "action": "reverse"}
	if err := p.enviar(ctx, sitio, "/api/transaction", cuerpo, &r); err != nil {
		return pasarela.ResultadoDeReverso{}, err
	}
	resp, _ := json.Marshal(map[string]any{"status": r.Status, "authorization": r.Authorization})
	return pasarela.ResultadoDeReverso{
		Exitoso:      r.Status.Status == "APPROVED",
		Estado:       r.Status.Status,
		Mensaje:      r.Status.Message,
		Autorizacion: r.Authorization,
		Respuesta:    resp,
	}, nil
}

// ------------------------------------------------------------------------------------------------ notificación --

type notificacionPTP struct {
	Status    EstadoPTP `json:"status"`
	RequestID int64     `json:"requestId"`
	Reference string    `json:"reference"`
	Signature string    `json:"signature"`
}

// Firma calcula la firma de una notificación: SHA-256 con prefijo «sha256:» o, sin prefijo, SHA-1 (R11).
func Firma(requestID int64, estado, fecha, secreto string, conSHA256 bool) string {
	texto := strconv.FormatInt(requestID, 10) + estado + fecha + secreto
	if conSHA256 {
		s := sha256.Sum256([]byte(texto))
		return "sha256:" + hex.EncodeToString(s[:])
	}
	s := sha1.Sum([]byte(texto)) //nolint:gosec // ver el import
	return hex.EncodeToString(s[:])
}

// VerificarNotificacion comprueba la firma con el secreto del sitio, en tiempo constante.
func (p *Pasarela) VerificarNotificacion(sitio pasarela.Sitio, cuerpo []byte) (pasarela.Notificacion, error) {
	var n notificacionPTP
	if err := json.Unmarshal(cuerpo, &n); err != nil || n.RequestID == 0 || n.Signature == "" {
		return pasarela.Notificacion{}, pasarela.ErrFirmaInvalida
	}
	recibida := strings.ToLower(strings.TrimSpace(n.Signature))
	esperada := Firma(n.RequestID, n.Status.Status, n.Status.Date, sitio.Secreto.Revelar(), strings.HasPrefix(recibida, "sha256:"))
	if subtle.ConstantTimeCompare([]byte(recibida), []byte(esperada)) != 1 {
		return pasarela.Notificacion{}, pasarela.ErrFirmaInvalida
	}
	return pasarela.Notificacion{RequestID: n.RequestID, Referencia: n.Reference, Estado: n.Status.Status, Fecha: n.Status.Date}, nil
}

// ------------------------------------------------------------------------------------------------------- HTTP --

// enviar hace el POST JSON. Red caída o 5xx → pasarela.ErrNoDisponible; 4xx con status → *pasarela.ErrRechazo.
func (p *Pasarela) enviar(ctx context.Context, sitio pasarela.Sitio, ruta string, cuerpo, destino any) error {
	b, err := json.Marshal(cuerpo)
	if err != nil {
		return fmt.Errorf("placetopay: serializar: %w", err)
	}
	req, err := http.NewRequestWithContext(ctx, http.MethodPost, strings.TrimSuffix(sitio.URLBase, "/")+ruta, bytes.NewReader(b))
	if err != nil {
		return fmt.Errorf("placetopay: armar la petición: %w", err)
	}
	req.Header.Set("Content-Type", "application/json")
	req.Header.Set("Accept", "application/json")
	resp, err := p.http.Do(req)
	if err != nil {
		return fmt.Errorf("%w: %v", pasarela.ErrNoDisponible, err)
	}
	defer resp.Body.Close()
	datos, err := io.ReadAll(io.LimitReader(resp.Body, 1<<20))
	if err != nil {
		return fmt.Errorf("%w: leer la respuesta: %v", pasarela.ErrNoDisponible, err)
	}
	if resp.StatusCode >= 500 {
		return fmt.Errorf("%w: HTTP %d", pasarela.ErrNoDisponible, resp.StatusCode)
	}
	if resp.StatusCode >= 400 {
		var r struct {
			Status EstadoPTP `json:"status"`
		}
		_ = json.Unmarshal(datos, &r)
		codigo := string(r.Status.Reason)
		if codigo == "" {
			codigo = strconv.Itoa(resp.StatusCode)
		}
		return &pasarela.ErrRechazo{Codigo: codigo, Mensaje: r.Status.Message}
	}
	if err := json.Unmarshal(datos, destino); err != nil {
		return fmt.Errorf("%w: respuesta ilegible: %v", pasarela.ErrNoDisponible, err)
	}
	return nil
}

// medios: los pedidos que ofrece el sitio; sin pedidos, los del sitio.
func medios(pedidos, delSitio []string) []string {
	if len(pedidos) == 0 {
		return delSitio
	}
	var r []string
	for _, m := range pedidos {
		for _, s := range delSitio {
			if m == s {
				r = append(r, m)
			}
		}
	}
	return r
}

func recortar(s string, n int) string {
	r := []rune(s)
	if len(r) <= n {
		return s
	}
	return string(r[:n])
}

// ------------------------------------------------------------------------------- verificación de credenciales --

// Verificador prueba unas credenciales creando y consultando una sesión de prueba (R10). La sesión queda sin pagar y
// vence sola.
type Verificador struct {
	Pasarela    *Pasarela
	HostPublico string
}

// Verificar crea y consulta una sesión mínima. Si la pasarela rechaza las credenciales, queda fallida con el motivo;
// si no responde, es un error (el ERP puede reintentar).
func (v Verificador) Verificar(ctx context.Context, sitio uuid.UUID, c pasarela.Credenciales) (pasarela.Verificacion, error) {
	s := pasarela.Sitio{ID: sitio, URLBase: c.URLBase, Login: c.Login, Secreto: c.Secreto, MediosDePago: c.MediosDePago}
	auth, err := v.Pasarela.autenticacion(s)
	if err != nil {
		return pasarela.Verificacion{}, err
	}
	ahora := v.Pasarela.ahora().UTC()
	ref := "VERIF" + strings.ToUpper(strings.ReplaceAll(uuid.NewString(), "-", ""))[:13]
	cuerpo := pedidoDeSesion{
		Auth: auth, Locale: "es_CO",
		Payment:    pago{Reference: ref, Description: "Verificación de credenciales del canal", Amount: monto{Currency: "COP", Total: 1000}},
		Expiration: ahora.Add(10 * time.Minute).Format(time.RFC3339),
		ReturnURL:  "https://" + v.HostPublico + "/",
		IPAddress:  "127.0.0.1",
		UserAgent:  "IngenIA365 canal (verificación)",
	}
	var creada respuestaDeSesion
	err = v.Pasarela.enviar(ctx, s, "/api/session", cuerpo, &creada)
	var rechazo *pasarela.ErrRechazo
	switch {
	case errors.As(err, &rechazo):
		return pasarela.Verificacion{Estado: pasarela.EstadoFallida, Motivo: motivoDeRechazo(rechazo)}, nil
	case err != nil:
		return pasarela.Verificacion{}, err
	case creada.Status.Status != "OK" || creada.RequestID == 0:
		return pasarela.Verificacion{Estado: pasarela.EstadoFallida, Motivo: motivoDeRechazo(&pasarela.ErrRechazo{
			Codigo: string(creada.Status.Reason), Mensaje: creada.Status.Message})}, nil
	}
	if _, err := v.Pasarela.ConsultarSesion(ctx, s, creada.RequestID); err != nil {
		if errors.As(err, &rechazo) {
			return pasarela.Verificacion{Estado: pasarela.EstadoFallida, Motivo: motivoDeRechazo(rechazo)}, nil
		}
		return pasarela.Verificacion{}, err
	}
	t := v.Pasarela.ahora().UTC()
	return pasarela.Verificacion{Estado: pasarela.EstadoActiva, VerificadaEn: &t}, nil
}

func motivoDeRechazo(r *pasarela.ErrRechazo) string {
	m := "La pasarela rechazó las credenciales"
	if r.Mensaje != "" {
		m += ": " + r.Mensaje
	}
	if r.Codigo != "" {
		m += " (" + r.Codigo + ")"
	}
	return recortar(m+".", 500)
}
