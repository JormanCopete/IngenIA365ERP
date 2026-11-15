package identidad

import (
	"context"
	"errors"
	"net/http"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// Código para confirmar un pago (FR-009, T090): la cooperativa puede exigirlo. El asociado lo pide, lo recibe por
// correo (o SMS si la cooperativa lo encendió), lo verifica y manda el id del desafío con el pago; el pago lo toma una
// sola vez dentro de VidaDelCodigoDePago.

// VidaDelCodigoDePago es cuánto sirve un código ya verificado para confirmar el pago.
const VidaDelCodigoDePago = 10 * time.Minute

// ErrCodigoDePagoRequerido: la cooperativa exige código y no llegó uno verificado y sin usar.
var ErrCodigoDePagoRequerido = sobre.Nuevo(http.StatusForbidden, "Canal.Pago.CodigoRequerido",
	"Para confirmar el pago necesitas el código que te enviamos.").ConDatos(map[string]bool{"otpRequired": true})

// iniciarCodigoDePago atiende POST /api/v1/payments/otp: crea el desafío de la cuenta y dice por dónde puede llegar.
func (s *Servicio) iniciarCodigoDePago(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	activa, _ := SesionDe(ctx)
	cuenta, err := s.q.GetAccount(ctx, activa.CuentaID)
	if err != nil {
		s.fallo(w, r, "no se pudo leer la cuenta", err)
		return
	}
	llave := Documento{Tipo: cuenta.DocumentType, Numero: cuenta.DocumentNumber}.Llave()
	if e := s.limitarDesafios(ctx, llave); e != nil {
		sobre.Escribir(w, e)
		return
	}
	sms := false
	if c, ok := contexto.CooperativaDe(ctx); ok {
		if coop, err := s.q.GetTenantByID(ctx, c.ID); err == nil {
			sms = coop.SmsEnabled
		}
	}
	canales := s.canalesDeContacto(cuenta.Email, cuenta.Mobile, sms)
	if len(canales) == 0 {
		sobre.Escribir(w, ErrCanalNoDisponible)
		return
	}
	id, err := s.nuevoDesafio(ctx, s.q, gen.InsertChallengeParams{Purpose: propositoPago, AccountID: &cuenta.ID, DocumentKey: llave})
	if err != nil {
		s.fallo(w, r, "no se pudo crear el desafío de pago", err)
		return
	}
	sobre.JSON(w, http.StatusOK, RespuestaDeInicio{ChallengeID: id, Channels: canales})
}

// verificarCodigoDePago atiende POST /api/v1/payments/otp/verify. El desafío queda consumido; el pago lo toma.
func (s *Servicio) verificarCodigoDePago(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	activa, _ := SesionDe(ctx)
	var cuerpo struct {
		ChallengeID uuid.UUID `json:"challengeId"`
		Code        string    `json:"code"`
	}
	if e := leer(w, r, &cuerpo); e != nil {
		sobre.Escribir(w, e)
		return
	}
	if d, err := s.q.GetChallenge(ctx, cuerpo.ChallengeID); err != nil || d.AccountID == nil || *d.AccountID != activa.CuentaID {
		sobre.Escribir(w, ErrDesafioVencido)
		return
	}
	if e := s.verificarCodigo(ctx, cuerpo.ChallengeID, cuerpo.Code, propositoPago, nil,
		func(*gen.Queries, pgx.Tx, gen.OtpChallenge) error { return nil }); e != nil {
		sobre.Escribir(w, e)
		return
	}
	sobre.JSON(w, http.StatusOK, map[string]any{"challengeId": cuerpo.ChallengeID, "verified": true})
}

// TomarCodigoDePago marca usado, dentro de la transacción del pago, el código verificado de la cuenta. Un código de
// otra cuenta, sin verificar, vencido o ya usado da ErrCodigoDePagoRequerido.
func (s *Servicio) TomarCodigoDePago(ctx context.Context, q *gen.Queries, cuenta, desafio uuid.UUID) error {
	d, err := q.GetChallengeForUpdate(ctx, desafio)
	if errors.Is(err, pgx.ErrNoRows) {
		return ErrCodigoDePagoRequerido
	}
	if err != nil {
		return err
	}
	if d.Purpose != propositoPago || d.AccountID == nil || *d.AccountID != cuenta || d.ConsumedAt == nil || d.GrantUsedAt != nil ||
		s.ahora().After(d.ConsumedAt.Add(VidaDelCodigoDePago)) {
		return ErrCodigoDePagoRequerido
	}
	return q.UseGrant(ctx, gen.UseGrantParams{ID: d.ID, At: s.ahora()})
}
