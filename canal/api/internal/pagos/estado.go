package pagos

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"net/http"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/pasarela"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// T091 (FR-038, FR-040, FR-043; data-model.md «Transiciones»): created → pending → approved | rejected | failed | expired;
// expired → approved si la pasarela aprueba después. Un estado final no cambia. Cada cambio deja su evento con la
// fuente; repetir la misma noticia no deja nada.

// Fuentes de un evento (attempt_events.source).
const (
	FuenteCreacion = "create"
	FuenteRetorno  = "return"
	FuenteWebhook  = "webhook"
	FuenteSonda    = "probe"
)

// TransicionValida dice si el intento puede pasar de un estado a otro.
func TransicionValida(de, a string) bool {
	switch de {
	case "created":
		return a == "pending" || a == "failed"
	case "pending":
		return a == "approved" || a == "rejected" || a == "failed" || a == "expired"
	case "expired":
		return a == "approved"
	}
	return false
}

// Cambio es lo que se aplica a un intento.
type Cambio struct {
	Estado     pasarela.EstadoDePago
	Crudo      string
	Mensaje    string
	Aprobacion *pasarela.Aprobacion
	Respuesta  json.RawMessage
}

// sesionCreada pasa el intento a pendiente con la sesión de la pasarela.
func (s *Servicio) sesionCreada(ctx context.Context, id uuid.UUID, sesion pasarela.SesionCreada) (gen.PaymentAttempt, error) {
	var intento gen.PaymentAttempt
	err := s.enTransaccion(ctx, func(q *gen.Queries, _ pgx.Tx) error {
		if err := q.SetAttemptSession(ctx, gen.SetAttemptSessionParams{ID: id, GatewayRequestID: &sesion.RequestID, ProcessUrl: &sesion.ProcessURL}); err != nil {
			return err
		}
		raw, _ := json.Marshal(map[string]any{"requestId": sesion.RequestID})
		if err := q.InsertAttemptEvent(ctx, gen.InsertAttemptEventParams{ID: nuevoID(), AttemptID: id, Source: FuenteCreacion,
			GatewayStatus: ptr("CREATED"), Status: "pending", Raw: raw, At: s.ahora()}); err != nil {
			return err
		}
		var err error
		intento, err = q.GetPaymentAttempt(ctx, id)
		return err
	})
	return intento, err
}

// transicion aplica un cambio al intento con cerrojo. Si la transición no es válida (estado repetido, final que no
// cambia), sólo anota la consulta. Devuelve el intento como quedó.
func (s *Servicio) transicion(ctx context.Context, id uuid.UUID, fuente string, c Cambio) (gen.PaymentAttempt, error) {
	var intento gen.PaymentAttempt
	err := s.enTransaccion(ctx, func(q *gen.Queries, tx pgx.Tx) error {
		a, err := q.GetPaymentAttemptForUpdate(ctx, id)
		if err != nil {
			return err
		}
		ahora := s.ahora()
		nuevo := string(c.Estado)
		if !TransicionValida(a.Status, nuevo) {
			if err := q.TouchAttemptChecked(ctx, gen.TouchAttemptCheckedParams{ID: id, CheckedAt: &ahora}); err != nil {
				return err
			}
			intento, err = q.GetPaymentAttempt(ctx, id)
			return err
		}
		var aprobacion []byte
		if c.Aprobacion != nil {
			if aprobacion, err = json.Marshal(c.Aprobacion); err != nil {
				return err
			}
		}
		var final *time.Time
		if nuevo != "pending" {
			final = &ahora
		}
		var mensaje *string
		if c.Mensaje != "" && nuevo != "approved" {
			mensaje = ptr(recortar(c.Mensaje, 500))
		}
		if err := q.SetAttemptStatus(ctx, gen.SetAttemptStatusParams{ID: id, Status: nuevo, Approval: aprobacion, StatusMessage: mensaje,
			FinalAt: final, CheckedAt: &ahora}); err != nil {
			return err
		}
		if final != nil {
			if err := q.ReleasePendingConcepts(ctx, id); err != nil {
				return err
			}
		}
		raw := c.Respuesta
		if len(raw) == 0 {
			raw = json.RawMessage("{}")
		}
		var crudo *string
		if c.Crudo != "" {
			crudo = ptr(recortar(c.Crudo, 40))
		}
		if err := q.InsertAttemptEvent(ctx, gen.InsertAttemptEventParams{ID: nuevoID(), AttemptID: id, Source: fuente,
			GatewayStatus: crudo, Status: nuevo, Raw: raw, At: ahora}); err != nil {
			return err
		}
		if intento, err = q.GetPaymentAttempt(ctx, id); err != nil {
			return err
		}
		if err := s.o.Auditoria.EnTransaccion(tx).Registrar(ctx, "Canal.Pago.EstadoCambiado", id.String(), map[string]any{
			"reference": a.Reference, "from": a.Status, "to": nuevo, "source": fuente,
		}); err != nil {
			return err
		}
		if err := s.encolarAvisoDeTransicion(ctx, tx, intento, nuevo, fuente, c.Mensaje); err != nil {
			return err
		}
		if nuevo == "approved" && s.o.AlAprobar != nil {
			return s.o.AlAprobar(ctx, tx, intento)
		}
		return nil
	})
	return intento, err
}

// VidaSinSesion: un intento que no llegó a tener sesión en la pasarela en este tiempo se da por fallido.
const VidaSinSesion = 5 * time.Minute

// Actualizar consulta la pasarela y aplica lo que diga. Un intento final no se consulta, salvo vencido (puede
// aprobarse después) cuando la fuente no es la sonda. Si la pasarela no responde, el intento queda como estaba.
func (s *Servicio) Actualizar(ctx context.Context, id uuid.UUID, fuente string) (gen.PaymentAttempt, error) {
	a, err := s.q.GetPaymentAttempt(ctx, id)
	if err != nil {
		return a, err
	}
	switch {
	case a.Status == "created":
		if s.ahora().Sub(a.CreatedAt) > VidaSinSesion {
			return s.transicion(ctx, id, fuente, Cambio{Estado: pasarela.EstadoFallido, Mensaje: "La sesión de pago no se pudo crear."})
		}
		return a, nil
	case a.Status == "expired" && fuente != FuenteSonda && a.GatewayRequestID != nil:
	case a.Status != "pending" || a.GatewayRequestID == nil:
		return a, nil
	}
	sitio, _, err := s.o.Sitios.Cargar(ctx, a.SiteID)
	if err != nil {
		return a, fmt.Errorf("pagos: cargar el sitio del intento: %w", err)
	}
	e, err := s.o.Pasarela.ConsultarSesion(ctx, sitio, *a.GatewayRequestID)
	if err != nil {
		ahora := s.ahora()
		_ = s.q.TouchAttemptChecked(ctx, gen.TouchAttemptCheckedParams{ID: id, CheckedAt: &ahora})
		return a, fmt.Errorf("pagos: consultar la pasarela: %w", err)
	}
	if e.Diferencia {
		s.o.Logger.ErrorContext(ctx, "la pasarela respondió un estado que no se espera; revisar el intento",
			"intento", id, "referencia", a.Reference, "estado", e.Crudo)
		s.auditar(ctx, "Canal.Pago.Diferencia", id.String(), map[string]any{"reference": a.Reference, "gatewayStatus": e.Crudo})
	}
	if e.Estado == pasarela.EstadoAprobado && e.Aprobacion == nil {
		// Aprobada sin un intento aprobado en la lista: no se marca hasta tener los datos.
		s.o.Logger.ErrorContext(ctx, "sesión aprobada sin pago aprobado en la respuesta", "intento", id)
		return a, nil
	}
	return s.transicion(ctx, id, fuente, Cambio{Estado: e.Estado, Crudo: e.Crudo, Mensaje: e.Mensaje, Aprobacion: e.Aprobacion, Respuesta: e.Respuesta})
}

// consultar atiende GET /api/v1/payments/{id}. Si el intento está abierto, o vencido y el asociado acaba de volver
// (?return=1), pregunta primero a la pasarela (FR-038): nunca se confía en los datos de la URL de retorno.
func (s *Servicio) consultar(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	coop, _, cuenta, ok := s.alcance(w, r)
	if !ok {
		return
	}
	id, err := uuid.Parse(r.PathValue("id"))
	if err != nil {
		sobre.Escribir(w, ErrNoExiste)
		return
	}
	a, err := s.q.GetPaymentAttempt(ctx, id)
	if errors.Is(err, pgx.ErrNoRows) || (err == nil && (a.AccountID != cuenta || a.TenantID != coop.ID)) {
		sobre.Escribir(w, ErrNoExiste)
		return
	}
	if err != nil {
		s.fallo(w, r, "no se pudo leer el intento", err)
		return
	}
	retorno := r.URL.Query().Get("return") == "1"
	if a.Status == "pending" || a.Status == "created" || (retorno && a.Status == "expired") {
		fuente := FuenteSonda
		if retorno {
			fuente = FuenteRetorno
		}
		if actual, err := s.Actualizar(ctx, id, fuente); err != nil {
			s.o.Logger.WarnContext(ctx, "no se pudo consultar el intento en la pasarela", "intento", id, "error", err)
		} else {
			a = actual
		}
	}
	sobre.JSON(w, http.StatusOK, s.respuesta(ctx, a))
}

// Pendiente es un concepto con intento abierto (para el aviso de FR-041 en la lista de conceptos).
type Pendiente struct {
	Clave      string               `json:"conceptKey"`
	Intento    uuid.UUID            `json:"attemptId"`
	Referencia string               `json:"reference"`
	Total      int64                `json:"total"`
	Estado     string               `json:"status"`
	Aprobacion *pasarela.Aprobacion `json:"approval"`
	Creado     time.Time            `json:"createdAt"`
}

// pendientes atiende GET /api/v1/payments/pending: los conceptos con un intento abierto.
func (s *Servicio) pendientes(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	coop, _, cuenta, ok := s.alcance(w, r)
	if !ok {
		return
	}
	filas, err := s.q.OpenConceptKeys(ctx, gen.OpenConceptKeysParams{AccountID: cuenta, TenantID: coop.ID})
	if err != nil {
		s.fallo(w, r, "no se pudieron leer los pendientes", err)
		return
	}
	lista := make([]Pendiente, 0, len(filas))
	for _, f := range filas {
		p := Pendiente{Clave: f.ConceptKey, Intento: f.AttemptID, Referencia: f.Reference, Total: f.Total, Estado: f.Status, Creado: f.CreatedAt}
		if len(f.Approval) > 0 {
			var ap pasarela.Aprobacion
			if json.Unmarshal(f.Approval, &ap) == nil {
				p.Aprobacion = &ap
			}
		}
		lista = append(lista, p)
	}
	soporte := json.RawMessage("{}")
	if t, err := s.q.GetTenantByID(ctx, coop.ID); err == nil && len(t.Support) > 0 {
		soporte = t.Support
	}
	sobre.JSON(w, http.StatusOK, map[string]any{"items": lista, "support": soporte})
}
