package auditoria

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"strings"
	"time"
	"unicode"
	"unicode/utf8"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
)

// ErrDatosConSecretos: los datos de auditoría traen un campo con nombre de secreto. Es un defecto del llamador.
var ErrDatosConSecretos = errors.New("auditoria: los datos traen un campo que parece un secreto")

// Registro anexa entradas a audit_log. No tiene operación de modificar ni de borrar, y la base las rechaza.
type Registro struct {
	q     *gen.Queries
	ahora func() time.Time
}

// Nuevo crea el registro sobre el pool (o cualquier gen.DBTX).
func Nuevo(db gen.DBTX) *Registro {
	return &Registro{q: gen.New(db), ahora: time.Now}
}

// EnTransaccion devuelve un registro que escribe dentro de tx: la auditoría queda con el cambio o no queda.
func (r *Registro) EnTransaccion(tx pgx.Tx) *Registro {
	return &Registro{q: r.q.WithTx(tx), ahora: r.ahora}
}

// Registrar anexa una entrada. accion lleva espacio de nombres («Canal.Cuenta.Activada»), sujeto es el
// identificador opaco de lo afectado y datos se guarda como JSON (sin secretos ni datos de tarjeta: un campo con
// nombre de secreto se rechaza). Actor, cuenta, cooperativa, IP, agente y correlación salen del contexto.
func (r *Registro) Registrar(ctx context.Context, accion, sujeto string, datos any) error {
	if strings.TrimSpace(accion) == "" {
		return errors.New("auditoria: la acción es obligatoria")
	}
	cuerpo, err := serializar(datos)
	if err != nil {
		return err
	}
	id, err := uuid.NewV7()
	if err != nil {
		return fmt.Errorf("auditoria: id: %w", err)
	}

	p := gen.InsertAuditEntryParams{
		ID:            id,
		At:            r.ahora(),
		Actor:         string(contexto.ActorDe(ctx)),
		Action:        accion,
		Subject:       sujeto,
		Ip:            textoONulo(contexto.IP(ctx), 64),
		UserAgent:     textoONulo(contexto.Agente(ctx), 512),
		CorrelationID: textoONulo(contexto.Correlacion(ctx), 64),
		Data:          cuerpo,
	}
	if cuenta, ok := contexto.Cuenta(ctx); ok {
		p.AccountID = &cuenta
	}
	if coop, ok := contexto.CooperativaDe(ctx); ok && coop.ID != uuid.Nil {
		p.TenantID = &coop.ID
	}
	if err := r.q.InsertAuditEntry(ctx, p); err != nil {
		return fmt.Errorf("auditoria: anexar %s: %w", accion, err)
	}
	return nil
}

func serializar(datos any) (json.RawMessage, error) {
	if datos == nil {
		return json.RawMessage(`{}`), nil
	}
	cuerpo, err := json.Marshal(datos)
	if err != nil {
		return nil, fmt.Errorf("auditoria: serializar los datos: %w", err)
	}
	var arbol any
	if err := json.Unmarshal(cuerpo, &arbol); err != nil {
		return nil, fmt.Errorf("auditoria: releer los datos: %w", err)
	}
	if campo, ok := buscarSecreto(arbol); ok {
		return nil, fmt.Errorf("%w: «%s»", ErrDatosConSecretos, campo)
	}
	return cuerpo, nil
}

// camposProhibidos son nombres (normalizados: minúsculas, sin separadores ni tildes) que nunca entran a la
// auditoría. «code» y «authorization» no están: son el código de un error y el número de autorización de la
// pasarela, que sí se auditan; el código de un solo uso se llama «otp».
var camposProhibidos = map[string]bool{
	"password": true, "contrasena": true, "clave": true, "secret": true, "secreto": true, "secretkey": true,
	"trankey": true, "token": true, "accesstoken": true, "refreshtoken": true, "activationtoken": true,
	"otp": true, "cvv": true, "cvc": true, "pan": true, "cardnumber": true, "numerotarjeta": true,
}

func buscarSecreto(v any) (string, bool) {
	switch x := v.(type) {
	case map[string]any:
		for k, hijo := range x {
			if camposProhibidos[normalizar(k)] {
				return k, true
			}
			if campo, ok := buscarSecreto(hijo); ok {
				return k + "." + campo, true
			}
		}
	case []any:
		for _, hijo := range x {
			if campo, ok := buscarSecreto(hijo); ok {
				return campo, true
			}
		}
	}
	return "", false
}

func normalizar(s string) string {
	var b strings.Builder
	for _, r := range strings.ToLower(s) {
		if unicode.IsLetter(r) || unicode.IsDigit(r) {
			b.WriteRune(r)
		}
	}
	return strings.NewReplacer("ñ", "n", "á", "a", "é", "e", "í", "i", "ó", "o", "ú", "u").Replace(b.String())
}

// textoONulo devuelve nil para el texto vacío y recorta a maximo bytes sin partir un carácter UTF-8.
func textoONulo(s string, maximo int) *string {
	if s == "" {
		return nil
	}
	s = strings.ToValidUTF8(s, "")
	if len(s) > maximo {
		corte := maximo
		for corte > 0 && !utf8.RuneStart(s[corte]) {
			corte--
		}
		s = s[:corte]
	}
	return &s
}
