package pasarela

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"regexp"
	"strings"
	"time"
	"unicode"

	"github.com/google/uuid"
)

// T084 (US4; contracts/pasarela-y-app.md): la interfaz común de las pasarelas. Nada fuera de este paquete y de sus
// implementaciones (placetopay, simulada) conoce el formato de una pasarela: los intentos de pago hablan sólo estos tipos.

// Pasarela es una pasarela de pago. Toda llamada recibe el sitio con su secreto ya descifrado.
type Pasarela interface {
	CrearSesion(ctx context.Context, sitio Sitio, s SolicitudDeSesion) (SesionCreada, error)
	ConsultarSesion(ctx context.Context, sitio Sitio, requestID int64) (EstadoDeSesion, error)
	Reversar(ctx context.Context, sitio Sitio, referenciaInterna int64) (ResultadoDeReverso, error)
	// VerificarNotificacion comprueba la firma del cuerpo con el secreto del sitio (ErrFirmaInvalida si no corresponde).
	VerificarNotificacion(sitio Sitio, cuerpo []byte) (Notificacion, error)
}

// Sitio es un sitio de pasarela listo para usar: credenciales descifradas y medios que ofrece.
type Sitio struct {
	ID           uuid.UUID
	Cooperativa  uuid.UUID
	URLBase      string
	Login        string
	Secreto      Secreto
	MediosDePago []string
}

// Comprador son los datos del asociado que la pasarela pide (FR-037). Se validan con ValidarComprador antes de crear
// la sesión.
type Comprador struct {
	TipoDeDocumento   string // catálogo del ERP: CC, CE, TI, NIT, PPT, PEP, PA
	NumeroDeDocumento string
	Nombres           string
	Apellidos         string
	Correo            string
	Celular           string
}

// Impuesto de un concepto (sólo si el concepto lo trae, FR-033).
type Impuesto struct {
	Tipo  string
	Base  int64
	Valor int64
}

// SolicitudDeSesion es lo que el canal pide a la pasarela para un intento. Valores en pesos enteros.
type SolicitudDeSesion struct {
	Referencia   string
	Descripcion  string
	Total        int64
	Impuestos    []Impuesto
	Comprador    Comprador
	IP           string
	Agente       string
	Vence        time.Time
	URLDeRetorno string
	Medios       []string // los del sitio; vacío = los que tenga la pasarela
}

// SesionCreada es la respuesta de crear la sesión.
type SesionCreada struct {
	RequestID  int64
	ProcessURL string
}

// EstadoDePago es el estado normalizado de una sesión (contracts/pasarela-y-app.md, «Normalización de estados»).
type EstadoDePago string

const (
	EstadoPendiente EstadoDePago = "pending"
	EstadoAprobado  EstadoDePago = "approved"
	EstadoRechazado EstadoDePago = "rejected"
	EstadoFallido   EstadoDePago = "failed"
	EstadoVencido   EstadoDePago = "expired"
)

// Final dice si el estado ya no cambia (salvo vencido → aprobado, el caso borde de data-model.md).
func (e EstadoDePago) Final() bool { return e != EstadoPendiente && e != "" }

// Aprobacion es el intento aprobado dentro de la sesión. Nunca lleva datos de tarjeta (FR-042).
type Aprobacion struct {
	ReferenciaInterna int64     `json:"internalReference"`
	Autorizacion      string    `json:"authorization"`
	Recibo            string    `json:"receipt"`
	Franquicia        string    `json:"franchise"`
	Medio             string    `json:"paymentMethodName"`
	Emisor            string    `json:"issuerName"`
	Fecha             time.Time `json:"approvedAt"`
}

// EstadoDeSesion es la respuesta de consultar una sesión.
type EstadoDeSesion struct {
	Estado     EstadoDePago
	Crudo      string // el estado de la pasarela tal como llegó (APPROVED, PENDING…)
	Motivo     string
	Mensaje    string
	Fecha      time.Time
	Aprobacion *Aprobacion
	// Diferencia: la pasarela respondió algo que no se espera (pago parcial); se registra y alerta.
	Diferencia bool
	// Respuesta es la respuesta de la pasarela ya sin datos de tarjeta, para attempt_events (FR-043).
	Respuesta json.RawMessage
}

// ResultadoDeReverso es la respuesta de reversar un pago aprobado.
type ResultadoDeReverso struct {
	Exitoso      bool
	Estado       string
	Mensaje      string
	Autorizacion string
	Respuesta    json.RawMessage
}

// Notificacion es lo que la pasarela avisa por el webhook, con la firma ya verificada.
type Notificacion struct {
	RequestID  int64
	Referencia string
	Estado     string
	Fecha      string
}

// Errores comunes.
var (
	// ErrFirmaInvalida: la notificación no está firmada con el secreto del sitio (FR-039).
	ErrFirmaInvalida = errors.New("pasarela: la firma de la notificación no corresponde")
	// ErrNoDisponible: la pasarela no respondió o respondió con error de servidor; se puede reintentar.
	ErrNoDisponible = errors.New("pasarela: no disponible")
)

// ErrRechazo es un rechazo de la pasarela a una petición (credenciales, datos inválidos). No se reintenta.
type ErrRechazo struct {
	Codigo  string
	Mensaje string
}

func (e *ErrRechazo) Error() string {
	return fmt.Sprintf("pasarela: rechazo %s: %s", e.Codigo, e.Mensaje)
}

// ----------------------------------------------------------------------------------------- validación (FR-037) --

// Expresiones de la pasarela por tipo de documento (contracts/pasarela-y-app.md). PPT, PEP y PA del ERP van como PPN
// (research R11, por confirmar con el analista); RC no paga.
var documentos = map[string]*regexp.Regexp{
	"CC":  regexp.MustCompile(`^[1-9][0-9]{3,9}$`),
	"CE":  regexp.MustCompile(`^([a-zA-Z]{1,5})?[1-9][0-9]{3,7}$`),
	"TI":  regexp.MustCompile(`^[1-9][0-9]{4,11}$`),
	"NIT": regexp.MustCompile(`^[1-9]\d{6,9}$`),
	"PPN": regexp.MustCompile(`^[a-zA-Z0-9_]{4,16}$`),
}

var (
	correoValido     = regexp.MustCompile(`^[^@\s]{1,64}@[^@\s]{1,189}\.[^@\s.]{2,63}$`)
	celularValido    = regexp.MustCompile(`^[0-9]{7,15}$`)
	referenciaValida = regexp.MustCompile(`^[A-Z0-9]{1,32}$`)
)

// TipoDeDocumentoDePasarela traduce el tipo del ERP al de la pasarela; vacío si no puede pagar.
func TipoDeDocumentoDePasarela(tipo string) string {
	switch tipo {
	case "CC", "CE", "TI", "NIT":
		return tipo
	case "PPT", "PEP", "PA":
		return "PPN"
	}
	return ""
}

// ErrCompradorInvalido dice qué dato del asociado no sirve para la pasarela.
type ErrCompradorInvalido struct{ Campo, Mensaje string }

func (e *ErrCompradorInvalido) Error() string { return "pasarela: comprador inválido: " + e.Campo }

// ValidarComprador aplica las reglas de la pasarela al comprador (FR-037) y devuelve el tipo de documento de la
// pasarela. El primer problema gana.
func ValidarComprador(c Comprador) (string, error) {
	tipo := TipoDeDocumentoDePasarela(c.TipoDeDocumento)
	if tipo == "" {
		return "", &ErrCompradorInvalido{"documentType", "Con este tipo de documento no se puede pagar en línea."}
	}
	if !documentos[tipo].MatchString(c.NumeroDeDocumento) {
		return "", &ErrCompradorInvalido{"documentNumber", "El número de documento no tiene el formato que admite la pasarela."}
	}
	if !nombreValido(c.Nombres) {
		return "", &ErrCompradorInvalido{"names", "Los nombres sólo pueden tener letras y espacios."}
	}
	if strings.TrimSpace(c.Apellidos) != "" && !nombreValido(c.Apellidos) {
		return "", &ErrCompradorInvalido{"surnames", "Los apellidos sólo pueden tener letras y espacios."}
	}
	if !correoValido.MatchString(c.Correo) {
		return "", &ErrCompradorInvalido{"email", "Necesitas un correo verificado para pagar en línea."}
	}
	if c.Celular != "" && !celularValido.MatchString(c.Celular) {
		return "", &ErrCompradorInvalido{"mobile", "El celular sólo puede tener dígitos."}
	}
	return tipo, nil
}

// nombreValido: letras (con tildes y Ñ), espacios y nada más.
func nombreValido(s string) bool {
	s = strings.TrimSpace(s)
	if s == "" || len([]rune(s)) > 60 {
		return false
	}
	for _, r := range s {
		if !unicode.IsLetter(r) && r != ' ' {
			return false
		}
	}
	return true
}

// ReferenciaValida: alfanumérica en mayúscula, hasta 32 caracteres (FR-035).
func ReferenciaValida(ref string) bool { return referenciaValida.MatchString(ref) }
