package identidad

import (
	"context"
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

var (
	ErrIngresoInvalido  = sobre.Nuevo(http.StatusUnauthorized, "Canal.Ingreso.Invalido", "El documento o la clave no son correctos.")
	ErrIngresoBloqueado = sobre.Nuevo(http.StatusTooManyRequests, "Canal.Ingreso.Bloqueado",
		"Hubo demasiados intentos fallidos. Espera unos minutos e inténtalo de nuevo, o recupera tu clave.")
	ErrCuentaBloqueada = sobre.Nuevo(http.StatusForbidden, "Canal.Cuenta.Bloqueada",
		"Tu cuenta está bloqueada. Comunícate con tu cooperativa.")
)

// ingresar atiende POST /api/v1/auth/login (FR-021, FR-022). La respuesta y el tiempo son los mismos exista o no la
// cuenta; el bloqueo por fallos se cuenta por documento, también para los que no existen.
func (s *Servicio) ingresar(w http.ResponseWriter, r *http.Request) {
	inicio := time.Now()
	ctx := r.Context()
	var cuerpo struct {
		DocumentType     string `json:"documentType"`
		DocumentNumber   string `json:"documentNumber"`
		Password         string `json:"password"`
		DeviceCredential string `json:"deviceCredential"`
	}
	if e := leer(w, r, &cuerpo); e != nil {
		sobre.Escribir(w, e)
		return
	}
	defer s.igualar(ctx, inicio)

	doc, valido := NormalizarDocumento(cuerpo.DocumentType, cuerpo.DocumentNumber)
	llave := LlaveDeTexto(cuerpo.DocumentType, cuerpo.DocumentNumber)
	if valido {
		llave = doc.Llave()
	}
	ahora := s.ahora()
	if freno, err := s.q.GetThrottle(ctx, llave); err == nil && freno.LockedUntil != nil && ahora.Before(*freno.LockedUntil) {
		_ = CompararClave("", cuerpo.Password)
		sobre.Escribir(w, ErrIngresoBloqueado.ConDatos(map[string]any{"hasta": freno.LockedUntil}))
		return
	} else if err != nil && !errors.Is(err, pgx.ErrNoRows) {
		s.fallo(w, r, "no se pudo leer el freno de ingresos", err)
		return
	}

	var cuenta gen.Account
	existe := false
	if valido {
		c, err := s.q.GetAccountByDocument(ctx, gen.GetAccountByDocumentParams{DocumentType: doc.Tipo, DocumentNumber: doc.Numero})
		switch {
		case err == nil:
			cuenta, existe = c, true
		case !errors.Is(err, pgx.ErrNoRows):
			s.fallo(w, r, "no se pudo leer la cuenta", err)
			return
		}
	}
	hash := ""
	if existe {
		hash = cuenta.PasswordHash
	}
	if len(cuerpo.Password) > LargoMaximoDeClave*4 || !CompararClave(hash, cuerpo.Password) {
		s.registrarFallo(ctx, llave, existe, cuenta)
		sobre.Escribir(w, ErrIngresoInvalido)
		return
	}
	if err := s.q.ClearThrottle(ctx, llave); err != nil {
		s.o.Logger.WarnContext(ctx, "no se pudo limpiar el freno de ingresos", "error", err)
	}
	ctx = contexto.ConActor(contexto.ConCuenta(ctx, cuenta.ID), contexto.ActorAsociado)
	if cuenta.Status != "active" {
		sobre.Escribir(w, ErrCuentaBloqueada)
		return
	}

	// Dispositivo de confianza: sin código (FR-021).
	if credencial := credencialDeDispositivoDe(r, cuerpo.DeviceCredential); credencial != "" && len(credencial) < 200 {
		d, err := s.q.DeviceByCredential(ctx, HashDeSecreto(credencial))
		if err == nil && d.AccountID == cuenta.ID {
			var resp RespuestaDeSesion
			if e := s.enTransaccion(ctx, func(q *gen.Queries, tx pgx.Tx) error {
				if err := q.TouchDevice(ctx, gen.TouchDeviceParams{ID: d.ID, At: s.ahora()}); err != nil {
					return err
				}
				var err error
				resp, err = s.abrirSesion(ctx, q, w, r, cuenta, &d.ID)
				if err != nil {
					return err
				}
				return s.o.Auditoria.EnTransaccion(tx).Registrar(ctx, "Canal.Ingreso.Exitoso", cuenta.ID.String(),
					map[string]any{"metodo": "clave", "dispositivo": d.ID})
			}); e != nil {
				escribirError(w, s, r, e)
				return
			}
			sobre.JSON(w, http.StatusOK, resp)
			return
		}
	}

	// Dispositivo nuevo: se pide un código a los datos de la cuenta.
	id, err := s.nuevoDesafio(ctx, s.q, gen.InsertChallengeParams{Purpose: propositoDispositivo, AccountID: &cuenta.ID, DocumentKey: llave})
	if err != nil {
		s.fallo(w, r, "no se pudo crear el desafío de dispositivo nuevo", err)
		return
	}
	_, sms := s.cooperativaDeLaCuenta(ctx, &cuenta.ID)
	sobre.JSON(w, http.StatusOK, RespuestaDeInicio{ChallengeID: id, Channels: s.canalesDeContacto(cuenta.Email, cuenta.Mobile, sms)})
}

// registrarFallo suma el fallo del documento y, al quinto, bloquea 15 minutos y avisa a la cuenta si existe.
func (s *Servicio) registrarFallo(ctx context.Context, llave []byte, existe bool, cuenta gen.Account) {
	ahora := s.ahora()
	freno, err := s.q.RecordLoginFailure(ctx, gen.RecordLoginFailureParams{DocumentKey: llave, At: ahora, WindowStart: ahora.Add(-VentanaDeFallos)})
	if err != nil {
		s.o.Logger.ErrorContext(ctx, "no se pudo registrar el fallo de ingreso", "error", err)
		return
	}
	if freno.Failures < FallosParaBloquear {
		return
	}
	if err := s.q.LockThrottle(ctx, gen.LockThrottleParams{DocumentKey: llave, LockedUntil: ahora.Add(DuracionDelBloqueo)}); err != nil {
		s.o.Logger.ErrorContext(ctx, "no se pudo bloquear el ingreso", "error", err)
		return
	}
	if existe {
		auditado := contexto.ConActor(contexto.ConCuenta(ctx, cuenta.ID), contexto.ActorSistema)
		s.auditar(auditado, "Canal.Ingreso.Bloqueado", cuenta.ID.String(), map[string]any{"minutos": int(DuracionDelBloqueo / time.Minute)})
		if cuenta.Email != nil {
			s.avisar(ctx, mensajeria.AvisoDeBloqueo(*cuenta.Email, int(DuracionDelBloqueo/time.Minute)))
		}
	}
}

// verificarIngreso atiende POST /api/v1/auth/login/verify: el código del dispositivo nuevo abre la sesión y, si la
// persona lo pide, deja el dispositivo como de confianza.
func (s *Servicio) verificarIngreso(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	var cuerpo struct {
		ChallengeID uuid.UUID `json:"challengeId"`
		Code        string    `json:"code"`
		TrustDevice bool      `json:"trustDevice"`
	}
	if e := leer(w, r, &cuerpo); e != nil {
		sobre.Escribir(w, e)
		return
	}
	credencial, hashCredencial, err := NuevoSecreto()
	if err != nil {
		s.fallo(w, r, "no se pudo generar la credencial de dispositivo", err)
		return
	}
	if !cuerpo.TrustDevice {
		credencial, hashCredencial = "", nil
	}
	var resp RespuestaDeSesion
	e := s.verificarCodigo(ctx, cuerpo.ChallengeID, strings.TrimSpace(cuerpo.Code), propositoDispositivo, nil,
		func(q *gen.Queries, tx pgx.Tx, d gen.OtpChallenge) error {
			if d.AccountID == nil {
				return ErrDesafioVencido
			}
			cuenta, err := q.GetAccount(ctx, *d.AccountID)
			if err != nil {
				return err
			}
			if cuenta.Status != "active" {
				return ErrCuentaBloqueada
			}
			cctx := contexto.ConActor(contexto.ConCuenta(ctx, cuenta.ID), contexto.ActorAsociado)
			plataforma := PlataformaDe(r)
			dispositivo := nuevoID()
			if err := q.InsertDevice(cctx, gen.InsertDeviceParams{ID: dispositivo, AccountID: cuenta.ID, Kind: string(plataforma),
				Label: etiquetaDeDispositivo(plataforma, contexto.Agente(ctx)), CredentialHash: hashCredencial, LastSeenAt: s.ahora()}); err != nil {
				return err
			}
			resp, err = s.abrirSesion(cctx, q, w, r, cuenta, &dispositivo)
			if err != nil {
				return err
			}
			return s.o.Auditoria.EnTransaccion(tx).Registrar(cctx, "Canal.Ingreso.Exitoso", cuenta.ID.String(),
				map[string]any{"metodo": "clave+codigo", "confiable": cuerpo.TrustDevice})
		})
	if e != nil {
		sobre.Escribir(w, e)
		return
	}
	s.entregarCredencialDeDispositivo(w, r, &resp, credencial)
	sobre.JSON(w, http.StatusOK, resp)
}

// salir atiende POST /api/v1/auth/logout. Sin sesión también responde 204: salir siempre funciona.
func (s *Servicio) salir(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	if activa, e := s.autenticar(ctx, r, false); e == nil {
		motivo := "logout"
		if _, err := s.q.EndSession(ctx, gen.EndSessionParams{ID: activa.ID, At: s.ahora(), EndReason: &motivo}); err != nil {
			s.fallo(w, r, "no se pudo cerrar la sesión", err)
			return
		}
		s.auditar(contexto.ConActor(contexto.ConCuenta(ctx, activa.CuentaID), contexto.ActorAsociado), "Canal.Sesion.Cerrada", activa.ID.String(), nil)
	}
	if !PlataformaDe(r).esApp() {
		s.borrarCookieDeSesion(w)
	}
	w.WriteHeader(http.StatusNoContent)
}
