package httpapi

import (
	"context"
	"encoding/json"
	"errors"
	"io"
	"log/slog"
	"net/http"
	"strings"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"
	"github.com/riverqueue/river"
	"github.com/riverqueue/river/rivertype"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/auditoria"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/entidades"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/erp"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/pasarela"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// Rutas de /internal/v1 por cooperativa que llama el ERP (contracts/canal-api.md §C). El verificador del token ya
// comprobó que la cooperativa de la ruta es la del token; aquí se vuelve a mirar (defensa en profundidad), se marca
// el actor erp_staff y se audita cada llamada.

// Encolador inserta trabajos de River (lo cumple *river.Client[pgx.Tx]).
type Encolador interface {
	Insert(ctx context.Context, args river.JobArgs, opts *river.InsertOpts) (*rivertype.JobInsertResult, error)
}

// ReservadorDeCodigos lo cumple *entidades.Codigos.
type ReservadorDeCodigos interface {
	Reservar(ctx context.Context, cooperativa uuid.UUID, sugerido string) (entidades.Reserva, error)
}

// GuardadorDeSitios lo cumple *pasarela.Sitios.
type GuardadorDeSitios interface {
	Guardar(ctx context.Context, cooperativa, sitio uuid.UUID, c pasarela.Credenciales) (pasarela.Resultado, error)
}

// LectorDeCooperativas lo cumple *gen.Queries.
type LectorDeCooperativas interface {
	GetTenantByPublicID(ctx context.Context, tenantPublicID uuid.UUID) (gen.Tenant, error)
}

// RutasInternasDeCooperativas reúne las dependencias de estas rutas.
type RutasInternasDeCooperativas struct {
	Encolador    Encolador
	Codigos      ReservadorDeCodigos
	Sitios       GuardadorDeSitios
	Cooperativas LectorDeCooperativas
	Auditoria    *auditoria.Registro
	Logger       *slog.Logger
}

// Acciones de auditoría de estas rutas (las de credenciales las registra pasarela.Sitios en su transacción).
const (
	AccionArrastreSolicitado = "Canal.Habilitaciones.ArrastreSolicitado"
	AccionCodigoReservado    = "Canal.Codigo.Reservado"
	AccionCodigoRechazado    = "Canal.Codigo.Rechazado"
)

// Errores de la superficie interna.
var (
	ErrIdentificadorInvalido = sobre.Nuevo(http.StatusBadRequest, "Canal.Ruta.IdentificadorInvalido",
		"El identificador de la ruta no es un UUID válido.")
	ErrCuerpoInvalido = sobre.Nuevo(http.StatusBadRequest, "Canal.Solicitud.CuerpoInvalido",
		"El cuerpo de la solicitud no es un JSON válido o excede el tamaño admitido.")
)

const maximoCuerpoInterno = 64 << 10

// RegistrarInternasDeCooperativas registra las rutas en el servidor.
func (s *Servidor) RegistrarInternasDeCooperativas(r RutasInternasDeCooperativas) {
	if r.Logger == nil {
		r.Logger = s.opciones.Logger
	}
	s.ManejarInterno("POST /internal/v1/tenants/{tenant}/sync", r.envolver(r.sincronizar))
	s.ManejarInterno("POST /internal/v1/tenants/{tenant}/channel-code", r.envolver(r.reservarCodigo))
	s.ManejarInterno("PUT /internal/v1/tenants/{tenant}/sites/{site}/credentials", r.envolver(r.guardarCredenciales))
}

type manejadorDeCooperativa func(w http.ResponseWriter, r *http.Request, cooperativa uuid.UUID)

// envolver resuelve la cooperativa de la ruta, comprueba que sea la del token, marca el actor y, si la cooperativa
// ya está en el directorio, la pone en el contexto (para el log y la columna tenant_id de la auditoría).
func (rc RutasInternasDeCooperativas) envolver(h manejadorDeCooperativa) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		cooperativa, err := uuid.Parse(r.PathValue("tenant"))
		if err != nil {
			sobre.Escribir(w, ErrIdentificadorInvalido)
			return
		}
		sujeto, ok := erp.SujetoDe(r.Context())
		if deToken, err := uuid.Parse(sujeto); !ok || err != nil || deToken != cooperativa {
			sobre.Escribir(w, erp.ErrCooperativaDistinta)
			return
		}
		ctx := contexto.ConActor(r.Context(), contexto.ActorFuncionarioERP)
		fila, err := rc.Cooperativas.GetTenantByPublicID(ctx, cooperativa)
		switch {
		case err == nil:
			ctx = contexto.ConCooperativa(ctx, contexto.Cooperativa{ID: fila.ID, PublicID: fila.TenantPublicID})
		case !errors.Is(err, pgx.ErrNoRows):
			rc.Logger.ErrorContext(ctx, "no se pudo leer la cooperativa del directorio", "cooperativa", cooperativa, "error", err)
			sobre.Escribir(w, sobre.ErrInesperado)
			return
		}
		h(w, r.WithContext(ctx), cooperativa)
	})
}

// sincronizar encola el arrastre de habilitaciones de la cooperativa. El cuerpo no se lee (el ERP manda {}).
func (rc RutasInternasDeCooperativas) sincronizar(w http.ResponseWriter, r *http.Request, cooperativa uuid.UUID) {
	ctx := r.Context()
	resultado, err := rc.Encolador.Insert(ctx, entidades.ArgsArrastrarCooperativa{TenantPublicID: cooperativa}, nil)
	if err != nil {
		rc.Logger.ErrorContext(ctx, "no se pudo encolar el arrastre de habilitaciones", "cooperativa", cooperativa, "error", err)
		sobre.Escribir(w, sobre.ErrInesperado)
		return
	}
	datos := map[string]any{}
	if resultado != nil && resultado.Job != nil {
		datos["jobId"] = resultado.Job.ID
	}
	if err := rc.Auditoria.Registrar(ctx, AccionArrastreSolicitado, cooperativa.String(), datos); err != nil {
		rc.Logger.ErrorContext(ctx, "no se pudo auditar la solicitud de arrastre", "error", err)
		sobre.Escribir(w, sobre.ErrInesperado)
		return
	}
	sobre.JSON(w, http.StatusAccepted, map[string]bool{"enqueued": true})
}

// reservarCodigo devuelve el código de canal de la cooperativa, reservándolo si no tiene.
func (rc RutasInternasDeCooperativas) reservarCodigo(w http.ResponseWriter, r *http.Request, cooperativa uuid.UUID) {
	ctx := r.Context()
	var cuerpo struct {
		Suggested string `json:"suggested"`
	}
	if errSobre := leerJSON(w, r, &cuerpo); errSobre != nil {
		sobre.Escribir(w, errSobre)
		return
	}
	reserva, err := rc.Codigos.Reservar(ctx, cooperativa, cuerpo.Suggested)
	var errSobre *sobre.Error
	if errors.As(err, &errSobre) {
		if err := rc.Auditoria.Registrar(ctx, AccionCodigoRechazado, cooperativa.String(), map[string]any{
			"suggested": entidades.NormalizarSugerencia(cuerpo.Suggested), "code": errSobre.Codigo,
		}); err != nil {
			rc.Logger.ErrorContext(ctx, "no se pudo auditar el rechazo del código de canal", "error", err)
		}
		sobre.Escribir(w, errSobre)
		return
	}
	if err != nil {
		rc.Logger.ErrorContext(ctx, "no se pudo reservar el código de canal", "cooperativa", cooperativa, "error", err)
		sobre.Escribir(w, sobre.ErrInesperado)
		return
	}
	if err := rc.Auditoria.Registrar(ctx, AccionCodigoReservado, cooperativa.String(), map[string]any{
		"channelCode": reserva.Codigo, "new": reserva.Nueva, "suggested": entidades.NormalizarSugerencia(cuerpo.Suggested),
	}); err != nil {
		rc.Logger.ErrorContext(ctx, "no se pudo auditar la reserva del código de canal", "error", err)
		sobre.Escribir(w, sobre.ErrInesperado)
		return
	}
	if reserva.Nueva {
		rc.Logger.InfoContext(ctx, "código de canal reservado", "cooperativa", cooperativa, "codigo", reserva.Codigo)
	}
	sobre.JSON(w, http.StatusOK, map[string]string{"code": reserva.Codigo})
}

// solicitudDeCredenciales es el cuerpo de PUT …/sites/{site}/credentials.
type solicitudDeCredenciales struct {
	Gateway        string           `json:"gateway"`
	BaseURL        string           `json:"baseUrl"`
	Login          string           `json:"login"`
	SecretKey      pasarela.Secreto `json:"secretKey"`
	PaymentMethods []string         `json:"paymentMethods"`
}

// respuestaDeCredenciales es lo que vuelve al ERP (VerificacionDeCredencialesDto). Nunca el secreto.
type respuestaDeCredenciales struct {
	Activa       bool    `json:"activa"`
	VerificadaEn *string `json:"verificadaEn"`
	LoginLast4   string  `json:"loginLast4"`
	Motivo       *string `json:"motivo"`
}

// guardarCredenciales cifra, verifica y guarda las credenciales del sitio (R10).
func (rc RutasInternasDeCooperativas) guardarCredenciales(w http.ResponseWriter, r *http.Request, cooperativa uuid.UUID) {
	ctx := r.Context()
	sitio, err := uuid.Parse(r.PathValue("site"))
	if err != nil {
		sobre.Escribir(w, ErrIdentificadorInvalido)
		return
	}
	var cuerpo solicitudDeCredenciales
	if errSobre := leerJSON(w, r, &cuerpo); errSobre != nil {
		sobre.Escribir(w, errSobre)
		return
	}
	resultado, err := rc.Sitios.Guardar(ctx, cooperativa, sitio, pasarela.Credenciales{
		Pasarela:     cuerpo.Gateway,
		URLBase:      cuerpo.BaseURL,
		Login:        cuerpo.Login,
		Secreto:      cuerpo.SecretKey,
		MediosDePago: cuerpo.PaymentMethods,
	})
	var errSobre *sobre.Error
	if errors.As(err, &errSobre) {
		sobre.Escribir(w, errSobre)
		return
	}
	if err != nil {
		rc.Logger.ErrorContext(ctx, "no se pudieron guardar las credenciales del sitio", "sitio", sitio, "error", err)
		sobre.Escribir(w, sobre.ErrInesperado)
		return
	}
	respuesta := respuestaDeCredenciales{
		Activa:     resultado.Activa(),
		LoginLast4: resultado.LoginUltimos4,
		Motivo:     resultado.Motivo,
	}
	if resultado.VerificadaEn != nil {
		v := resultado.VerificadaEn.UTC().Format(time.RFC3339)
		respuesta.VerificadaEn = &v
	}
	sobre.JSON(w, http.StatusOK, respuesta)
}

// leerJSON decodifica el cuerpo (hasta 64 KiB). El error nunca repite el contenido: puede llevar un secreto.
func leerJSON(w http.ResponseWriter, r *http.Request, destino any) *sobre.Error {
	if tipo := r.Header.Get("Content-Type"); tipo != "" && !strings.HasPrefix(strings.ToLower(tipo), "application/json") {
		return ErrCuerpoInvalido
	}
	decodificador := json.NewDecoder(http.MaxBytesReader(w, r.Body, maximoCuerpoInterno))
	if err := decodificador.Decode(destino); err != nil {
		return ErrCuerpoInvalido
	}
	if _, err := decodificador.Token(); !errors.Is(err, io.EOF) {
		return ErrCuerpoInvalido // basura después del objeto
	}
	return nil
}
