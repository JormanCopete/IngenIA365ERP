package identidad

import (
	"encoding/json"
	"errors"
	"fmt"
	"net/http"
	"strings"
	"time"

	"github.com/go-webauthn/webauthn/protocol"
	"github.com/go-webauthn/webauthn/webauthn"
	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// Las passkeys son de la PWA (T068, research R13): RP ID = el host del canal. En la app (HybridWebView) no sirven porque
// el origen es local; allá va la biometría con la credencial de dispositivo.

const vidaDeCeremonia = 5 * time.Minute

var (
	ErrPasskeysApagadas = sobre.Nuevo(http.StatusNotFound, "Canal.Passkey.NoDisponible", "Las llaves de acceso no están disponibles.")
	ErrPasskeyInvalida  = sobre.Nuevo(http.StatusUnauthorized, "Canal.Passkey.Invalida",
		"No pudimos verificar la llave de acceso. Ingresa con tu documento y tu clave.")
	ErrCeremoniaVencida = sobre.Nuevo(http.StatusUnprocessableEntity, "Canal.Passkey.Vencida",
		"Pasó demasiado tiempo. Inténtalo de nuevo.")
)

type passkeys struct {
	w *webauthn.WebAuthn
}

func nuevasPasskeys(rpID string, origenes []string) (*passkeys, error) {
	if len(origenes) == 0 {
		origenes = []string{"https://" + rpID}
	}
	w, err := webauthn.New(&webauthn.Config{
		RPID:          rpID,
		RPDisplayName: "Canal de asociados",
		RPOrigins:     origenes,
		Timeouts: webauthn.TimeoutsConfig{
			Login:        webauthn.TimeoutConfig{Enforce: true, Timeout: vidaDeCeremonia},
			Registration: webauthn.TimeoutConfig{Enforce: true, Timeout: vidaDeCeremonia},
		},
	})
	if err != nil {
		return nil, fmt.Errorf("identidad: configuración de passkeys: %w", err)
	}
	return &passkeys{w: w}, nil
}

// usuarioWebAuthn adapta la cuenta a webauthn.User. El user handle es el id de la cuenta (16 bytes, opaco).
type usuarioWebAuthn struct {
	cuenta       gen.Account
	credenciales []webauthn.Credential
}

func (u usuarioWebAuthn) WebAuthnID() []byte {
	b, _ := u.cuenta.ID.MarshalBinary()
	return b
}
func (u usuarioWebAuthn) WebAuthnName() string {
	return u.cuenta.DocumentType + " " + enmascararDocumento(u.cuenta.DocumentNumber)
}
func (u usuarioWebAuthn) WebAuthnDisplayName() string {
	return strings.TrimSpace(u.cuenta.FirstNames + " " + u.cuenta.LastNames)
}
func (u usuarioWebAuthn) WebAuthnCredentials() []webauthn.Credential { return u.credenciales }

func credencialesDe(ds []gen.Device) []webauthn.Credential {
	var r []webauthn.Credential
	for _, d := range ds {
		var c webauthn.Credential
		if len(d.WebauthnCredential) > 0 && json.Unmarshal(d.WebauthnCredential, &c) == nil {
			r = append(r, c)
		}
	}
	return r
}

// opcionesDeRegistro atiende POST /api/v1/me/passkeys/options (sesión): opciones para crear una llave en este navegador.
func (s *Servicio) opcionesDeRegistro(w http.ResponseWriter, r *http.Request) {
	if s.pasekey == nil {
		sobre.Escribir(w, ErrPasskeysApagadas)
		return
	}
	ctx := r.Context()
	activa, _ := SesionDe(ctx)
	cuenta, err := s.q.GetAccount(ctx, activa.CuentaID)
	if err != nil {
		s.fallo(w, r, "no se pudo leer la cuenta", err)
		return
	}
	ds, err := s.q.ListWebauthnDevices(ctx, cuenta.ID)
	if err != nil {
		s.fallo(w, r, "no se pudieron leer las llaves", err)
		return
	}
	usuario := usuarioWebAuthn{cuenta: cuenta, credenciales: credencialesDe(ds)}
	excluir := make([]protocol.CredentialDescriptor, 0, len(usuario.credenciales))
	for _, c := range usuario.credenciales {
		excluir = append(excluir, c.Descriptor())
	}
	creacion, sesion, err := s.pasekey.w.BeginRegistration(usuario,
		webauthn.WithResidentKeyRequirement(protocol.ResidentKeyRequirementRequired),
		webauthn.WithAuthenticatorSelection(protocol.AuthenticatorSelection{
			ResidentKey: protocol.ResidentKeyRequirementRequired, UserVerification: protocol.VerificationRequired,
		}),
		webauthn.WithExclusions(excluir))
	if err != nil {
		s.fallo(w, r, "no se pudo iniciar el registro de la llave", err)
		return
	}
	id, e := s.guardarCeremonia(r, "register", &cuenta.ID, sesion)
	if e != nil {
		sobre.Escribir(w, e)
		return
	}
	sobre.JSON(w, http.StatusOK, map[string]any{"ceremonyId": id, "publicKey": creacion.Response})
}

// registrarPasskey atiende POST /api/v1/me/passkeys (sesión): guarda la llave creada como un dispositivo.
func (s *Servicio) registrarPasskey(w http.ResponseWriter, r *http.Request) {
	if s.pasekey == nil {
		sobre.Escribir(w, ErrPasskeysApagadas)
		return
	}
	ctx := r.Context()
	activa, _ := SesionDe(ctx)
	var cuerpo struct {
		CeremonyID uuid.UUID       `json:"ceremonyId"`
		Credential json.RawMessage `json:"credential"`
	}
	if e := leer(w, r, &cuerpo); e != nil {
		sobre.Escribir(w, e)
		return
	}
	sesion, cuentaID, e := s.tomarCeremonia(r, cuerpo.CeremonyID, "register")
	if e != nil {
		sobre.Escribir(w, e)
		return
	}
	if cuentaID == nil || *cuentaID != activa.CuentaID {
		sobre.Escribir(w, ErrCeremoniaVencida)
		return
	}
	cuenta, err := s.q.GetAccount(ctx, activa.CuentaID)
	if err != nil {
		s.fallo(w, r, "no se pudo leer la cuenta", err)
		return
	}
	creada, err := protocol.ParseCredentialCreationResponseBytes(cuerpo.Credential)
	if err != nil {
		s.o.Logger.InfoContext(ctx, "respuesta de registro de passkey ilegible", "error", err)
		sobre.Escribir(w, ErrPasskeyInvalida)
		return
	}
	credencial, err := s.pasekey.w.CreateCredential(usuarioWebAuthn{cuenta: cuenta}, sesion, creada)
	if err != nil {
		s.o.Logger.InfoContext(ctx, "registro de passkey rechazado", "error", err)
		sobre.Escribir(w, ErrPasskeyInvalida)
		return
	}
	guardada, err := json.Marshal(credencial)
	if err != nil {
		s.fallo(w, r, "no se pudo serializar la llave", err)
		return
	}
	id := nuevoID()
	etiqueta := "Llave de acceso · " + etiquetaDeDispositivo(Navegador, contexto.Agente(ctx))
	if e := s.enTransaccion(ctx, func(q *gen.Queries, tx pgx.Tx) error {
		if err := q.InsertDevice(ctx, gen.InsertDeviceParams{ID: id, AccountID: cuenta.ID, Kind: string(Navegador), Label: recorte(etiqueta, 120),
			WebauthnID: credencial.ID, WebauthnCredential: guardada, LastSeenAt: s.ahora()}); err != nil {
			return err
		}
		return s.o.Auditoria.EnTransaccion(tx).Registrar(ctx, "Canal.Passkey.Registrada", id.String(), nil)
	}); e != nil {
		escribirError(w, s, r, e)
		return
	}
	sobre.JSON(w, http.StatusCreated, map[string]any{"id": id, "label": etiqueta})
}

// opcionesDeIngreso atiende POST /api/v1/auth/passkey/options: ingreso sin documento (credencial descubrible), así las
// opciones no dicen nada de nadie.
func (s *Servicio) opcionesDeIngreso(w http.ResponseWriter, r *http.Request) {
	if s.pasekey == nil {
		sobre.Escribir(w, ErrPasskeysApagadas)
		return
	}
	asercion, sesion, err := s.pasekey.w.BeginDiscoverableLogin(webauthn.WithUserVerification(protocol.VerificationRequired))
	if err != nil {
		s.fallo(w, r, "no se pudo iniciar el ingreso con llave", err)
		return
	}
	id, e := s.guardarCeremonia(r, "login", nil, sesion)
	if e != nil {
		sobre.Escribir(w, e)
		return
	}
	sobre.JSON(w, http.StatusOK, map[string]any{"ceremonyId": id, "publicKey": asercion.Response})
}

// verificarIngresoConPasskey atiende POST /api/v1/auth/passkey/verify: la llave reemplaza clave y código (FR-021).
func (s *Servicio) verificarIngresoConPasskey(w http.ResponseWriter, r *http.Request) {
	if s.pasekey == nil {
		sobre.Escribir(w, ErrPasskeysApagadas)
		return
	}
	ctx := r.Context()
	var cuerpo struct {
		CeremonyID uuid.UUID       `json:"ceremonyId"`
		Credential json.RawMessage `json:"credential"`
	}
	if e := leer(w, r, &cuerpo); e != nil {
		sobre.Escribir(w, e)
		return
	}
	sesion, _, e := s.tomarCeremonia(r, cuerpo.CeremonyID, "login")
	if e != nil {
		sobre.Escribir(w, e)
		return
	}
	asercion, err := protocol.ParseCredentialRequestResponseBytes(cuerpo.Credential)
	if err != nil {
		sobre.Escribir(w, ErrPasskeyInvalida)
		return
	}
	var dispositivo gen.Device
	var cuenta gen.Account
	buscar := func(rawID, userHandle []byte) (webauthn.User, error) {
		id, err := uuid.FromBytes(userHandle)
		if err != nil {
			return nil, err
		}
		d, err := s.q.DeviceByWebauthnID(ctx, rawID)
		if err != nil {
			return nil, err
		}
		if d.AccountID != id {
			return nil, errors.New("la llave no es de esa cuenta")
		}
		c, err := s.q.GetAccount(ctx, id)
		if err != nil {
			return nil, err
		}
		dispositivo, cuenta = d, c
		return usuarioWebAuthn{cuenta: c, credenciales: credencialesDe([]gen.Device{d})}, nil
	}
	_, credencial, err := s.pasekey.w.ValidatePasskeyLogin(buscar, sesion, asercion)
	if err != nil {
		s.o.Logger.InfoContext(ctx, "ingreso con passkey rechazado", "error", err)
		sobre.Escribir(w, ErrPasskeyInvalida)
		return
	}
	if cuenta.Status != "active" {
		sobre.Escribir(w, ErrCuentaBloqueada)
		return
	}
	guardada, err := json.Marshal(credencial)
	if err != nil {
		s.fallo(w, r, "no se pudo serializar la llave", err)
		return
	}
	cctx := contexto.ConActor(contexto.ConCuenta(ctx, cuenta.ID), contexto.ActorAsociado)
	var resp RespuestaDeSesion
	if e := s.enTransaccion(cctx, func(q *gen.Queries, tx pgx.Tx) error {
		if err := q.UpdateWebauthnCredential(cctx, gen.UpdateWebauthnCredentialParams{ID: dispositivo.ID, WebauthnCredential: guardada, At: s.ahora()}); err != nil {
			return err
		}
		var err error
		resp, err = s.abrirSesion(cctx, q, w, r, cuenta, &dispositivo.ID)
		if err != nil {
			return err
		}
		return s.o.Auditoria.EnTransaccion(tx).Registrar(cctx, "Canal.Ingreso.Exitoso", cuenta.ID.String(),
			map[string]any{"metodo": "passkey", "dispositivo": dispositivo.ID})
	}); e != nil {
		escribirError(w, s, r, e)
		return
	}
	sobre.JSON(w, http.StatusOK, resp)
}

func (s *Servicio) guardarCeremonia(r *http.Request, tipo string, cuenta *uuid.UUID, sesion *webauthn.SessionData) (uuid.UUID, *sobre.Error) {
	datos, err := json.Marshal(sesion)
	if err != nil {
		s.o.Logger.ErrorContext(r.Context(), "no se pudo serializar la ceremonia", "error", err)
		return uuid.Nil, sobre.ErrInesperado
	}
	id := nuevoID()
	if err := s.q.InsertCeremony(r.Context(), gen.InsertCeremonyParams{ID: id, Kind: tipo, AccountID: cuenta, SessionData: datos,
		ExpiresAt: s.ahora().Add(vidaDeCeremonia)}); err != nil {
		s.o.Logger.ErrorContext(r.Context(), "no se pudo guardar la ceremonia", "error", err)
		return uuid.Nil, sobre.ErrInesperado
	}
	return id, nil
}

func (s *Servicio) tomarCeremonia(r *http.Request, id uuid.UUID, tipo string) (webauthn.SessionData, *uuid.UUID, *sobre.Error) {
	c, err := s.q.TakeCeremony(r.Context(), gen.TakeCeremonyParams{ID: id, Kind: tipo, At: s.ahora()})
	if errors.Is(err, pgx.ErrNoRows) {
		return webauthn.SessionData{}, nil, ErrCeremoniaVencida
	}
	if err != nil {
		s.o.Logger.ErrorContext(r.Context(), "no se pudo leer la ceremonia", "error", err)
		return webauthn.SessionData{}, nil, sobre.ErrInesperado
	}
	var sesion webauthn.SessionData
	if err := json.Unmarshal(c.SessionData, &sesion); err != nil {
		s.o.Logger.ErrorContext(r.Context(), "ceremonia ilegible", "error", err)
		return webauthn.SessionData{}, nil, sobre.ErrInesperado
	}
	return sesion, c.AccountID, nil
}
