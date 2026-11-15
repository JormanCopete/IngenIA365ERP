package identidad

import (
	"errors"
	"net/http"
	"strings"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/mensajeria"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// iniciarRecuperacion atiende POST /api/v1/auth/recovery/start (FR-023): el código va a los datos de la cuenta del
// canal. Igual que la activación, responde lo mismo exista o no la cuenta (FR-018).
func (s *Servicio) iniciarRecuperacion(w http.ResponseWriter, r *http.Request) {
	inicio := time.Now()
	ctx := r.Context()
	var cuerpo struct {
		DocumentType   string `json:"documentType"`
		DocumentNumber string `json:"documentNumber"`
	}
	if e := leer(w, r, &cuerpo); e != nil {
		sobre.Escribir(w, e)
		return
	}
	defer s.igualar(ctx, inicio)

	doc, valido := NormalizarDocumento(cuerpo.DocumentType, cuerpo.DocumentNumber)
	if !valido {
		sobre.Escribir(w, ErrDocumentoInvalido)
		return
	}
	llave := doc.Llave()
	if e := s.limitarDesafios(ctx, llave); e != nil {
		sobre.Escribir(w, e)
		return
	}
	param := gen.InsertChallengeParams{Purpose: propositoRecuperacion, DocumentKey: llave, Decoy: true}
	var canales []CanalOfrecido
	cuenta, err := s.q.GetAccountByDocument(ctx, gen.GetAccountByDocumentParams{DocumentType: doc.Tipo, DocumentNumber: doc.Numero})
	switch {
	case err == nil && cuenta.Status == "active":
		_, sms := s.cooperativaDeLaCuenta(ctx, &cuenta.ID)
		canales = s.canalesDeContacto(cuenta.Email, cuenta.Mobile, sms)
		if len(canales) > 0 {
			param.Decoy = false
			param.AccountID = &cuenta.ID
		}
	case err != nil && !errors.Is(err, pgx.ErrNoRows):
		s.fallo(w, r, "no se pudo leer la cuenta", err)
		return
	}
	if param.Decoy {
		canales = s.canalesSenuelo(llave)
	}
	id, err := s.nuevoDesafio(ctx, s.q, param)
	if err != nil {
		s.fallo(w, r, "no se pudo crear el desafío de recuperación", err)
		return
	}
	sobre.JSON(w, http.StatusOK, RespuestaDeInicio{ChallengeID: id, Channels: canales})
}

// verificarRecuperacion atiende POST /api/v1/auth/recovery/verify: el código correcto da el permiso para cambiar la clave.
func (s *Servicio) verificarRecuperacion(w http.ResponseWriter, r *http.Request) {
	inicio := time.Now()
	ctx := r.Context()
	var cuerpo struct {
		ChallengeID uuid.UUID `json:"challengeId"`
		Code        string    `json:"code"`
	}
	if e := leer(w, r, &cuerpo); e != nil {
		sobre.Escribir(w, e)
		return
	}
	defer s.igualar(ctx, inicio)
	permiso, hash, err := NuevoSecreto()
	if err != nil {
		s.fallo(w, r, "no se pudo generar el permiso", err)
		return
	}
	if e := s.verificarCodigo(ctx, cuerpo.ChallengeID, strings.TrimSpace(cuerpo.Code), propositoRecuperacion, hash,
		func(*gen.Queries, pgx.Tx, gen.OtpChallenge) error { return nil }); e != nil {
		sobre.Escribir(w, e)
		return
	}
	sobre.JSON(w, http.StatusOK, map[string]string{"recoveryToken": permiso})
}

// completarRecuperacion atiende POST /api/v1/auth/recovery/complete: cambia la clave, cierra todas las sesiones, quita
// el bloqueo por fallos y avisa. No abre sesión: la persona ingresa con la clave nueva.
func (s *Servicio) completarRecuperacion(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	var cuerpo struct {
		RecoveryToken string `json:"recoveryToken"`
		Password      string `json:"password"`
	}
	if e := leer(w, r, &cuerpo); e != nil {
		sobre.Escribir(w, e)
		return
	}
	var cuenta gen.Account
	if e := s.enTransaccion(ctx, func(q *gen.Queries, _ pgx.Tx) error {
		d, e := s.tomarPermiso(ctx, q, cuerpo.RecoveryToken, propositoRecuperacion)
		if e != nil {
			return e
		}
		c, err := q.GetAccount(ctx, *d.AccountID)
		if err != nil {
			return err
		}
		cuenta = c
		return nil
	}); e != nil {
		escribirError(w, s, r, e)
		return
	}
	doc := Documento{Tipo: cuenta.DocumentType, Numero: cuenta.DocumentNumber}
	if err := ValidarClave(cuerpo.Password, doc); err != nil {
		sobre.Escribir(w, sobre.Nuevo(http.StatusUnprocessableEntity, "Canal.Clave.Debil", err.Error()))
		return
	}
	hash, err := HashDeClave(cuerpo.Password)
	if err != nil {
		s.fallo(w, r, "no se pudo calcular el hash de la clave", err)
		return
	}
	cctx := contexto.ConActor(contexto.ConCuenta(ctx, cuenta.ID), contexto.ActorAsociado)
	if e := s.enTransaccion(cctx, func(q *gen.Queries, tx pgx.Tx) error {
		d, e := s.tomarPermiso(cctx, q, cuerpo.RecoveryToken, propositoRecuperacion)
		if e != nil {
			return e
		}
		ahora := s.ahora()
		if err := q.UseGrant(cctx, gen.UseGrantParams{ID: d.ID, At: ahora}); err != nil {
			return err
		}
		if err := q.UpdateAccountPassword(cctx, gen.UpdateAccountPasswordParams{ID: cuenta.ID, PasswordHash: hash, ChangedAt: ahora}); err != nil {
			return err
		}
		motivo := "password_changed"
		if _, err := q.EndOtherSessions(cctx, gen.EndOtherSessionsParams{AccountID: cuenta.ID, At: ahora, EndReason: &motivo, KeepID: uuid.Nil}); err != nil {
			return err
		}
		if err := q.ClearThrottle(cctx, doc.Llave()); err != nil {
			return err
		}
		return s.o.Auditoria.EnTransaccion(tx).Registrar(cctx, "Canal.Clave.Recuperada", cuenta.ID.String(), map[string]any{"canal": d.Channel})
	}); e != nil {
		escribirError(w, s, r, e)
		return
	}
	if cuenta.Email != nil {
		s.avisar(cctx, mensajeria.AvisoDeClaveCambiada(*cuenta.Email))
	}
	w.WriteHeader(http.StatusNoContent)
}
