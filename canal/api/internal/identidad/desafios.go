package identidad

import (
	"context"
	"crypto/hmac"
	"crypto/sha256"
	"encoding/binary"
	"encoding/json"
	"errors"
	"fmt"
	"net/http"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/mensajeria"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// Propósitos de un desafío (otp_challenges.purpose).
const (
	propositoActivacion   = "activation"
	propositoDispositivo  = "new_device"
	propositoRecuperacion = "recovery"
	propositoContacto     = "contact_change"
	propositoPago         = "payment"
)

// Un desafío abierto se puede usar media hora; cada código vive 5 minutos desde que se envía.
const vidaDelDesafio = 30 * time.Minute

// CanalOfrecido: por dónde se puede recibir el código, con la máscara.
type CanalOfrecido struct {
	Kind   string `json:"kind"` // email | sms
	Masked string `json:"masked"`
}

// RespuestaDeInicio es la respuesta de /activation/start, /auth/recovery/start y de un ingreso que pide código.
type RespuestaDeInicio struct {
	ChallengeID uuid.UUID       `json:"challengeId"`
	Channels    []CanalOfrecido `json:"channels"`
	// FR-019: la entidad validadora no tiene a dónde enviar; la persona debe acercarse a ella.
	NeedsEntityUpdate bool            `json:"needsEntityUpdate,omitempty"`
	EntityName        string          `json:"entityName,omitempty"`
	Support           json.RawMessage `json:"support,omitempty"`
}

// RespuestaDeEnvio: a dónde salió el código y hasta cuándo sirve.
type RespuestaDeEnvio struct {
	SentTo    string    `json:"sentTo"`
	ExpiresAt time.Time `json:"expiresAt"`
}

// limitarDesafios: a lo sumo DesafiosPorDocumento por documento y DesafiosPorIP por IP en la última hora. Cuenta igual
// los documentos que no existen.
func (s *Servicio) limitarDesafios(ctx context.Context, llave []byte) *sobre.Error {
	desde := s.ahora().Add(-VentanaDeDesafios)
	n, err := s.q.CountChallengesByDocument(ctx, gen.CountChallengesByDocumentParams{DocumentKey: llave, Since: desde})
	if err != nil {
		s.o.Logger.ErrorContext(ctx, "no se pudieron contar los desafíos", "error", err)
		return sobre.ErrInesperado
	}
	if n >= DesafiosPorDocumento {
		return ErrDemasiadosCodigos
	}
	if ip := contexto.IP(ctx); ip != "" {
		m, err := s.q.CountChallengesByIP(ctx, gen.CountChallengesByIPParams{Ip: &ip, Since: desde})
		if err != nil {
			s.o.Logger.ErrorContext(ctx, "no se pudieron contar los desafíos", "error", err)
			return sobre.ErrInesperado
		}
		if m >= DesafiosPorIP {
			return ErrDemasiadosCodigos
		}
	}
	return nil
}

// nuevoDesafio inserta el desafío con los datos comunes.
func (s *Servicio) nuevoDesafio(ctx context.Context, q *gen.Queries, p gen.InsertChallengeParams) (uuid.UUID, error) {
	ahora := s.ahora()
	p.ID = nuevoID()
	p.CreatedAt = ahora
	p.ExpiresAt = ahora.Add(VidaDelCodigo)
	p.Ip = recortar(contexto.IP(ctx), 64)
	return p.ID, q.InsertChallenge(ctx, p)
}

// canalesSenuelo inventa, de forma estable para el documento, las máscaras de un documento desconocido: una persona
// real tiene correo casi siempre y celular a veces; el señuelo también.
func (s *Servicio) canalesSenuelo(llave []byte) []CanalOfrecido {
	m := hmac.New(sha256.New, s.o.ClaveDeSenuelos)
	m.Write(llave)
	h := m.Sum(nil)
	letras := "abcdefghijklmnoprstvy"
	dominios := []struct{ inicial, terminacion string }{
		{"g", ".com"}, {"g", ".com"}, {"g", ".com"}, {"h", ".com"}, {"h", ".com"}, {"o", ".com"}, {"y", ".com"}, {"o", ".es"},
	}
	d := dominios[int(h[1])%len(dominios)]
	correo := CanalOfrecido{Kind: "email", Masked: fmt.Sprintf("%c•••@%s•••%s", letras[int(h[0])%len(letras)], d.inicial, d.terminacion)}
	canales := []CanalOfrecido{correo}
	if s.o.Mensajero.Disponible(mensajeria.SMS) && h[2]%10 < 6 {
		canales = append(canales, CanalOfrecido{Kind: "sms", Masked: fmt.Sprintf("•••••• %04d", binary.BigEndian.Uint16(h[3:5])%10000)})
	}
	return canales
}

// canalesDeContacto arma lo ofrecido con las máscaras reales. sms sólo si hay proveedor y la cooperativa lo encendió.
func (s *Servicio) canalesDeContacto(correo, celular *string, smsDeLaCooperativa bool) []CanalOfrecido {
	var canales []CanalOfrecido
	if correo != nil && *correo != "" {
		canales = append(canales, CanalOfrecido{Kind: "email", Masked: enmascararCorreo(*correo)})
	}
	if celular != nil && *celular != "" && smsDeLaCooperativa && s.o.Mensajero.Disponible(mensajeria.SMS) {
		canales = append(canales, CanalOfrecido{Kind: "sms", Masked: enmascararCelular(*celular)})
	}
	return canales
}

// enviarCodigo atiende /…/send: genera y envía el código por el canal elegido. Un señuelo responde igual sin enviar.
func (s *Servicio) enviarCodigo(proposito string) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		inicio := time.Now()
		ctx := r.Context()
		var cuerpo struct {
			ChallengeID uuid.UUID `json:"challengeId"`
			Channel     string    `json:"channel"`
		}
		if e := leer(w, r, &cuerpo); e != nil {
			sobre.Escribir(w, e)
			return
		}
		defer s.igualar(ctx, inicio)
		if proposito == propositoContacto || proposito == propositoPago {
			// El cambio de contacto y el código de pago son de la cuenta de la sesión: nadie más reenvía su código.
			activa, ok := SesionDe(ctx)
			d, err := s.q.GetChallenge(ctx, cuerpo.ChallengeID)
			if !ok || err != nil || d.AccountID == nil || *d.AccountID != activa.CuentaID {
				sobre.Escribir(w, ErrDesafioVencido)
				return
			}
		}
		resp, e := s.despachar(ctx, cuerpo.ChallengeID, proposito, mensajeria.Canal(cuerpo.Channel))
		if e != nil {
			sobre.Escribir(w, e)
			return
		}
		sobre.JSON(w, http.StatusAccepted, resp)
	}
}

// despachar genera el código del desafío y lo envía por el canal. Un señuelo guarda un código imposible y no envía nada.
func (s *Servicio) despachar(ctx context.Context, id uuid.UUID, proposito string, canal mensajeria.Canal) (RespuestaDeEnvio, *sobre.Error) {
	if canal != mensajeria.Correo && canal != mensajeria.SMS {
		return RespuestaDeEnvio{}, ErrPeticionInvalida
	}
	d, err := s.q.GetChallenge(ctx, id)
	if errors.Is(err, pgx.ErrNoRows) || (err == nil && d.Purpose != proposito) {
		return RespuestaDeEnvio{}, ErrDesafioVencido
	}
	if err != nil {
		s.o.Logger.ErrorContext(ctx, "no se pudo leer el desafío", "error", err)
		return RespuestaDeEnvio{}, sobre.ErrInesperado
	}
	ahora := s.ahora()
	if d.ConsumedAt != nil || ahora.After(d.CreatedAt.Add(vidaDelDesafio)) || d.Attempts >= IntentosPorCodigo {
		return RespuestaDeEnvio{}, ErrDesafioVencido
	}
	if d.Sends >= EnviosPorDesafio {
		return RespuestaDeEnvio{}, ErrEnviosAgotados
	}
	if canal == mensajeria.SMS && !s.o.Mensajero.Disponible(mensajeria.SMS) {
		return RespuestaDeEnvio{}, ErrCanalNoDisponible
	}

	vence := ahora.Add(VidaDelCodigo)
	if d.Decoy {
		_, hash, err := NuevoSecreto()
		if err == nil {
			err = s.q.SetChallengeCode(ctx, gen.SetChallengeCodeParams{ID: d.ID, Channel: ptr(string(canal)), CodeHash: hash, ExpiresAt: vence})
		}
		if err != nil {
			s.o.Logger.ErrorContext(ctx, "no se pudo preparar el desafío", "error", err)
			return RespuestaDeEnvio{}, sobre.ErrInesperado
		}
		return RespuestaDeEnvio{SentTo: mascaraSenuelo(s.canalesSenuelo(d.DocumentKey), canal), ExpiresAt: vence}, nil
	}

	destino, cooperativa, sms, e := s.destinoDelCodigo(ctx, d, canal)
	if e != nil {
		return RespuestaDeEnvio{}, e
	}
	if canal == mensajeria.SMS && !sms {
		return RespuestaDeEnvio{}, ErrCanalNoDisponible
	}
	codigo, err := NuevoCodigo()
	if err != nil {
		s.o.Logger.ErrorContext(ctx, "no se pudo generar el código", "error", err)
		return RespuestaDeEnvio{}, sobre.ErrInesperado
	}
	if err := s.q.SetChallengeCode(ctx, gen.SetChallengeCodeParams{
		ID: d.ID, Channel: ptr(string(canal)), DestinationHash: HashDeDestino(destino), CodeHash: HashDelCodigo(d.ID, codigo), ExpiresAt: vence,
	}); err != nil {
		s.o.Logger.ErrorContext(ctx, "no se pudo guardar el código", "error", err)
		return RespuestaDeEnvio{}, sobre.ErrInesperado
	}
	mensaje := mensajeria.MensajeDeCodigo(canal, destino, cooperativa, mensajeria.Proposito(proposito), codigo, int(VidaDelCodigo/time.Minute))
	if err := s.o.Mensajero.Enviar(ctx, mensaje); err != nil {
		if errors.Is(err, mensajeria.ErrNoDisponible) {
			return RespuestaDeEnvio{}, ErrCanalNoDisponible
		}
		s.o.Logger.ErrorContext(ctx, "no se pudo enviar el código", "canal", string(canal), "proposito", proposito, "error", err)
		return RespuestaDeEnvio{}, ErrEnvioFallido
	}
	s.auditar(ctx, "Canal.Codigo.Enviado", d.ID.String(), map[string]any{"proposito": proposito, "canal": string(canal)})
	mascara := enmascararCorreo(destino)
	if canal == mensajeria.SMS {
		mascara = enmascararCelular(destino)
	}
	return RespuestaDeEnvio{SentTo: mascara, ExpiresAt: vence}, nil
}

// destinoDelCodigo resuelve el correo o celular real del desafío, el nombre de la cooperativa que firma el mensaje y si
// esa cooperativa encendió el SMS.
func (s *Servicio) destinoDelCodigo(ctx context.Context, d gen.OtpChallenge, canal mensajeria.Canal) (string, string, bool, *sobre.Error) {
	switch d.Purpose {
	case propositoActivacion:
		if d.TenantID == nil || d.PersonPublicID == nil {
			return "", "", false, ErrDesafioVencido
		}
		coop, err := s.q.GetTenantByID(ctx, *d.TenantID)
		if err != nil {
			s.o.Logger.ErrorContext(ctx, "no se pudo leer la cooperativa del desafío", "error", err)
			return "", "", false, sobre.ErrInesperado
		}
		contacto, err := s.o.ERP.ContactoDeActivacion(ctx, coop.TenantPublicID, *d.PersonPublicID)
		if err != nil {
			s.o.Logger.WarnContext(ctx, "el ERP no dio el contacto de activación", "error", err)
			return "", "", false, ErrERPNoDisponible
		}
		valor := contacto.Entrega.Correo
		if canal == mensajeria.SMS {
			valor = contacto.Entrega.Celular
		}
		if valor == nil || *valor == "" {
			return "", "", false, ErrCanalNoDisponible
		}
		return *valor, coop.DisplayName, coop.SmsEnabled, nil
	case propositoContacto:
		if d.ContactValue == nil || d.ContactField == nil || (canal == mensajeria.SMS) != (*d.ContactField == "mobile") {
			return "", "", false, ErrCanalNoDisponible
		}
		nombre, sms := s.cooperativaDeLaCuenta(ctx, d.AccountID)
		return *d.ContactValue, nombre, sms || canal == mensajeria.Correo, nil
	default:
		if d.AccountID == nil {
			return "", "", false, ErrDesafioVencido
		}
		cuenta, err := s.q.GetAccount(ctx, *d.AccountID)
		if err != nil {
			s.o.Logger.ErrorContext(ctx, "no se pudo leer la cuenta del desafío", "error", err)
			return "", "", false, sobre.ErrInesperado
		}
		valor := cuenta.Email
		if canal == mensajeria.SMS {
			valor = cuenta.Mobile
		}
		if valor == nil || *valor == "" {
			return "", "", false, ErrCanalNoDisponible
		}
		nombre, sms := s.cooperativaDeLaCuenta(ctx, d.AccountID)
		return *valor, nombre, sms, nil
	}
}

// cooperativaDeLaCuenta: el nombre de la entidad validadora de la cuenta (firma los mensajes) y si tiene SMS.
func (s *Servicio) cooperativaDeLaCuenta(ctx context.Context, cuenta *uuid.UUID) (string, bool) {
	if cuenta == nil {
		return "tu cooperativa", false
	}
	c, err := s.q.GetAccount(ctx, *cuenta)
	if err != nil {
		return "tu cooperativa", false
	}
	coop, err := s.q.GetTenantByID(ctx, c.ValidatingTenantID)
	if err != nil {
		return "tu cooperativa", false
	}
	return coop.DisplayName, coop.SmsEnabled
}

// verificarCodigo comprueba el código en una transacción. Si acierta, consume el desafío y corre alAcertar en la misma
// transacción; si falla, guarda el intento fallido (se confirma aunque la respuesta sea un error).
func (s *Servicio) verificarCodigo(ctx context.Context, id uuid.UUID, codigo, proposito string, permiso []byte,
	alAcertar func(q *gen.Queries, tx pgx.Tx, d gen.OtpChallenge) error) *sobre.Error {
	var resultado *sobre.Error
	err := s.enTransaccion(ctx, func(q *gen.Queries, tx pgx.Tx) error {
		d, err := q.GetChallengeForUpdate(ctx, id)
		if errors.Is(err, pgx.ErrNoRows) || (err == nil && d.Purpose != proposito) {
			resultado = ErrDesafioVencido
			return nil
		}
		if err != nil {
			return err
		}
		ahora := s.ahora()
		switch {
		case d.ConsumedAt != nil, d.CodeHash == nil, ahora.After(d.ExpiresAt):
			resultado = ErrDesafioVencido
			return nil
		case d.Attempts >= IntentosPorCodigo:
			resultado = ErrCodigoAgotado
			return nil
		}
		if d.Decoy || !CodigoCorrecto(d.ID, codigo, d.CodeHash) {
			if err := q.FailChallengeAttempt(ctx, d.ID); err != nil {
				return err
			}
			restantes := IntentosPorCodigo - int(d.Attempts) - 1
			if restantes <= 0 {
				resultado = ErrCodigoAgotado
			} else {
				resultado = ErrCodigoIncorrecto.ConDatos(map[string]int{"intentosRestantes": restantes})
			}
			return nil
		}
		consumo := gen.ConsumeChallengeParams{ID: d.ID, At: ahora}
		if permiso != nil {
			consumo.GrantHash = permiso
			consumo.GrantExpiresAt = ptr(ahora.Add(VidaDelPermiso))
		}
		if err := q.ConsumeChallenge(ctx, consumo); err != nil {
			return err
		}
		return alAcertar(q, tx, d)
	})
	if err != nil {
		var e *sobre.Error
		if errors.As(err, &e) {
			return e
		}
		s.o.Logger.ErrorContext(ctx, "no se pudo verificar el código", "error", err)
		return sobre.ErrInesperado
	}
	return resultado
}

// tomarPermiso bloquea el desafío del permiso (activación o recuperación) y lo marca usado.
func (s *Servicio) tomarPermiso(ctx context.Context, q *gen.Queries, permiso, proposito string) (gen.OtpChallenge, *sobre.Error) {
	if permiso == "" || len(permiso) > 200 {
		return gen.OtpChallenge{}, ErrPermisoVencido
	}
	d, err := q.ChallengeByGrant(ctx, HashDeSecreto(permiso))
	if errors.Is(err, pgx.ErrNoRows) {
		return gen.OtpChallenge{}, ErrPermisoVencido
	}
	if err != nil {
		s.o.Logger.ErrorContext(ctx, "no se pudo leer el permiso", "error", err)
		return gen.OtpChallenge{}, sobre.ErrInesperado
	}
	if d.Purpose != proposito || d.GrantUsedAt != nil || d.GrantExpiresAt == nil || s.ahora().After(*d.GrantExpiresAt) {
		return gen.OtpChallenge{}, ErrPermisoVencido
	}
	return d, nil
}

func mascaraSenuelo(canales []CanalOfrecido, canal mensajeria.Canal) string {
	for _, c := range canales {
		if c.Kind == string(canal) {
			return c.Masked
		}
	}
	if canal == mensajeria.SMS {
		return "•••••• ••••"
	}
	return "•••@•••"
}

// enmascararCorreo: «j•••@g•••.com», igual que el ERP.
func enmascararCorreo(correo string) string {
	arroba := -1
	for i := range len(correo) {
		if correo[i] == '@' {
			arroba = i
		}
	}
	if arroba <= 0 || arroba == len(correo)-1 {
		return "•••"
	}
	dominio := correo[arroba+1:]
	terminacion := ""
	for i := len(dominio) - 1; i > 0; i-- {
		if dominio[i] == '.' {
			terminacion = dominio[i:]
			break
		}
	}
	return string(correo[0]) + "•••@" + string(dominio[0]) + "•••" + terminacion
}

// enmascararCelular: «•••••• 4321».
func enmascararCelular(celular string) string {
	if len(celular) < 4 {
		return "••••••"
	}
	return "•••••• " + celular[len(celular)-4:]
}

func ptr[T any](v T) *T { return &v }
