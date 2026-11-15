package identidad

import (
	"context"
	"crypto/hmac"
	"crypto/sha256"
	"encoding/json"
	"errors"
	"io"
	"log/slog"
	"net/http"
	"strings"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"
	"github.com/jackc/pgx/v5/pgxpool"
	"github.com/riverqueue/river"
	"github.com/riverqueue/river/rivertype"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/auditoria"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/erp"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/mensajeria"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// ERP es lo que la identidad le pide al ERP (lo cumple *erp.Cliente).
type ERP interface {
	ContactoDeActivacion(ctx context.Context, cooperativa, persona uuid.UUID) (erp.ContactoDeActivacion, error)
	TextosLegales(ctx context.Context, cooperativa uuid.UUID) ([]erp.TextoLegal, error)
}

// Mensajero entrega los códigos y avisos (lo cumple mensajeria.Compuesto).
type Mensajero interface {
	Enviar(ctx context.Context, m mensajeria.Mensaje) error
	Disponible(canal mensajeria.Canal) bool
}

// Encolador inserta trabajos de River en la transacción del cambio (lo cumple *river.Client[pgx.Tx]).
type Encolador interface {
	InsertTx(ctx context.Context, tx pgx.Tx, args river.JobArgs, opts *river.InsertOpts) (*rivertype.JobInsertResult, error)
}

// Registrador recibe las rutas públicas (lo cumple *httpapi.Servidor).
type Registrador interface {
	Manejar(patron string, h http.Handler)
}

// Opciones de la identidad.
type Opciones struct {
	Pool      *pgxpool.Pool
	ERP       ERP
	Mensajero Mensajero
	Auditoria *auditoria.Registro
	Encolador Encolador
	Logger    *slog.Logger
	// ClaveDeSenuelos firma las máscaras inventadas de los documentos desconocidos: estables para un mismo documento y
	// imposibles de distinguir de las reales sin la clave. Se deriva de la KEK activa.
	ClaveDeSenuelos []byte
	// CookiesSeguras pone Secure a las cookies (siempre, salvo en desarrollo local por http).
	CookiesSeguras bool
	// TiempoMinimo de las respuestas que no deben delatar si existe la persona (FR-018). 0 = 900 ms.
	TiempoMinimo time.Duration
	// Passkeys: RP ID (el host del canal) y orígenes admitidos. Vacío = sin passkeys.
	RPID     string
	Origenes []string
	Ahora    func() time.Time
}

// Servicio atiende la identidad del canal.
type Servicio struct {
	o       Opciones
	q       *gen.Queries
	pasekey *passkeys
}

// Nuevo crea el servicio. Falla si falta una dependencia.
func Nuevo(o Opciones) (*Servicio, error) {
	if o.Pool == nil || o.ERP == nil || o.Mensajero == nil || o.Auditoria == nil || o.Encolador == nil {
		return nil, errors.New("identidad: faltan dependencias")
	}
	if len(o.ClaveDeSenuelos) < 16 {
		return nil, errors.New("identidad: la clave de señuelos debe tener al menos 16 bytes")
	}
	if o.Logger == nil {
		o.Logger = slog.Default()
	}
	if o.TiempoMinimo == 0 {
		o.TiempoMinimo = 900 * time.Millisecond
	}
	if o.Ahora == nil {
		o.Ahora = time.Now
	}
	s := &Servicio{o: o, q: gen.New(o.Pool)}
	if o.RPID != "" {
		p, err := nuevasPasskeys(o.RPID, o.Origenes)
		if err != nil {
			return nil, err
		}
		s.pasekey = p
	}
	return s, nil
}

// ClaveDeSenuelosDe deriva la clave de señuelos de una KEK.
func ClaveDeSenuelosDe(kek []byte) []byte {
	m := hmac.New(sha256.New, kek)
	m.Write([]byte("canal/identidad/senuelos/v1"))
	return m.Sum(nil)
}

func (s *Servicio) ahora() time.Time { return s.o.Ahora().UTC() }

// igualar espera hasta que pase el tiempo mínimo desde inicio: así las respuestas de un documento que existe y de uno
// que no tardan lo mismo (FR-018). Si el contexto se cancela, deja de esperar.
func (s *Servicio) igualar(ctx context.Context, inicio time.Time) {
	resta := s.o.TiempoMinimo - time.Since(inicio)
	if resta <= 0 {
		return
	}
	t := time.NewTimer(resta)
	defer t.Stop()
	select {
	case <-t.C:
	case <-ctx.Done():
	}
}

// enTransaccion corre fn en una transacción.
func (s *Servicio) enTransaccion(ctx context.Context, fn func(q *gen.Queries, tx pgx.Tx) error) error {
	tx, err := s.o.Pool.Begin(ctx)
	if err != nil {
		return err
	}
	defer func() { _ = tx.Rollback(ctx) }()
	if err := fn(s.q.WithTx(tx), tx); err != nil {
		return err
	}
	return tx.Commit(ctx)
}

// --------------------------------------------------------------------------------------------- errores públicos --

var (
	ErrPeticionInvalida = sobre.Nuevo(http.StatusBadRequest, "Canal.Peticion.Invalida",
		"La información enviada no es válida. Revísala e inténtalo de nuevo.")
	ErrDocumentoInvalido = sobre.Nuevo(http.StatusUnprocessableEntity, "Canal.Documento.Invalido",
		"Revisa el tipo y el número de documento.")
	ErrDemasiadosCodigos = sobre.Nuevo(http.StatusTooManyRequests, "Canal.Codigo.Demasiados",
		"Pediste demasiados códigos. Espera un rato e inténtalo de nuevo.")
	ErrDesafioVencido = sobre.Nuevo(http.StatusUnprocessableEntity, "Canal.Codigo.Vencido",
		"El código venció o ya se usó. Pide uno nuevo.")
	ErrCodigoIncorrecto = sobre.Nuevo(http.StatusUnprocessableEntity, "Canal.Codigo.Incorrecto",
		"El código no es correcto.")
	ErrCodigoAgotado = sobre.Nuevo(http.StatusUnprocessableEntity, "Canal.Codigo.Agotado",
		"Agotaste los intentos con este código. Empieza de nuevo para recibir otro.")
	ErrEnviosAgotados = sobre.Nuevo(http.StatusTooManyRequests, "Canal.Codigo.EnviosAgotados",
		"Ya te enviamos varios códigos. Empieza de nuevo en unos minutos.")
	ErrCanalNoDisponible = sobre.Nuevo(http.StatusUnprocessableEntity, "Canal.Codigo.CanalNoDisponible",
		"Ese medio no está disponible ahora. Elige el correo.")
	ErrERPNoDisponible = sobre.Nuevo(http.StatusServiceUnavailable, "Canal.ERP.NoDisponible",
		"Tu cooperativa no responde en este momento. Inténtalo de nuevo en unos minutos.")
	ErrEnvioFallido = sobre.Nuevo(http.StatusServiceUnavailable, "Canal.Codigo.EnvioFallido",
		"No pudimos enviar el código. Inténtalo de nuevo en unos minutos.")
	ErrPermisoVencido = sobre.Nuevo(http.StatusUnprocessableEntity, "Canal.Permiso.Vencido",
		"Pasó demasiado tiempo. Empieza de nuevo.")
	ErrSesionRequerida = sobre.Nuevo(http.StatusUnauthorized, "Canal.Sesion.Requerida",
		"Tu sesión terminó. Ingresa de nuevo para continuar.")
)

// --------------------------------------------------------------------------------------------- JSON de entrada --

const maximoCuerpo = 64 << 10

// leer decodifica el cuerpo JSON. Exige Content-Type JSON: un formulario de otro sitio no puede mandarlo (con las
// cookies SameSite=Strict, es la segunda defensa contra CSRF).
func leer(w http.ResponseWriter, r *http.Request, destino any) *sobre.Error {
	if !strings.HasPrefix(strings.ToLower(r.Header.Get("Content-Type")), "application/json") {
		return ErrPeticionInvalida
	}
	d := json.NewDecoder(http.MaxBytesReader(w, r.Body, maximoCuerpo))
	if err := d.Decode(destino); err != nil {
		return ErrPeticionInvalida
	}
	if _, err := d.Token(); !errors.Is(err, io.EOF) {
		return ErrPeticionInvalida
	}
	return nil
}

// fallo registra un error interno y responde el genérico.
func (s *Servicio) fallo(w http.ResponseWriter, r *http.Request, que string, err error) {
	s.o.Logger.ErrorContext(r.Context(), que, "error", err)
	sobre.Escribir(w, sobre.ErrInesperado)
}

// erpFallo traduce un error del ERP.
func (s *Servicio) erpFallo(w http.ResponseWriter, r *http.Request, que string, err error) {
	s.o.Logger.WarnContext(r.Context(), que, "error", err)
	sobre.Escribir(w, ErrERPNoDisponible)
}

// auditar deja la entrada; si falla, queda en el log y no corta el flujo.
func (s *Servicio) auditar(ctx context.Context, accion, sujeto string, datos any) {
	if err := s.o.Auditoria.Registrar(ctx, accion, sujeto, datos); err != nil {
		s.o.Logger.ErrorContext(ctx, "no se pudo auditar", "accion", accion, "error", err)
	}
}

// avisar envía un aviso sin esperar a que llegue: un correo de aviso no debe frenar ni tumbar el flujo.
func (s *Servicio) avisar(ctx context.Context, m mensajeria.Mensaje) {
	ctx = context.WithoutCancel(ctx)
	go func() {
		limite, cancelar := context.WithTimeout(ctx, 30*time.Second)
		defer cancelar()
		if err := s.o.Mensajero.Enviar(limite, m); err != nil && !errors.Is(err, mensajeria.ErrNoDisponible) {
			s.o.Logger.WarnContext(ctx, "no se pudo enviar un aviso", "canal", string(m.Canal), "error", err)
		}
	}()
}
