// Package contexto: datos de la petición que viajan en el context.Context (correlación, IP, agente, cuenta,
// cooperativa, intento y actor). Lo leen el log, la auditoría y el cliente del ERP.
package contexto

import (
	"context"

	"github.com/google/uuid"
)

type clave int

const (
	claveCorrelacion clave = iota
	claveIP
	claveAgente
	claveCuenta
	claveCooperativa
	claveIntento
	claveActor
	clavePersona
)

// Actor es quién origina una acción auditada (columna actor de audit_log).
type Actor string

const (
	ActorAsociado       Actor = "member"
	ActorFuncionarioERP Actor = "erp_staff"
	ActorSistema        Actor = "system"
)

// Cooperativa identifica a la cooperativa de la petición: el Id propio del canal y el PublicId del ERP.
type Cooperativa struct {
	ID       uuid.UUID
	PublicID uuid.UUID
}

func ConCorrelacion(ctx context.Context, id string) context.Context {
	return context.WithValue(ctx, claveCorrelacion, id)
}

func Correlacion(ctx context.Context) string {
	v, _ := ctx.Value(claveCorrelacion).(string)
	return v
}

func ConIP(ctx context.Context, ip string) context.Context {
	return context.WithValue(ctx, claveIP, ip)
}

func IP(ctx context.Context) string {
	v, _ := ctx.Value(claveIP).(string)
	return v
}

func ConAgente(ctx context.Context, agente string) context.Context {
	return context.WithValue(ctx, claveAgente, agente)
}

func Agente(ctx context.Context) string {
	v, _ := ctx.Value(claveAgente).(string)
	return v
}

func ConCuenta(ctx context.Context, cuenta uuid.UUID) context.Context {
	return context.WithValue(ctx, claveCuenta, cuenta)
}

func Cuenta(ctx context.Context) (uuid.UUID, bool) {
	v, ok := ctx.Value(claveCuenta).(uuid.UUID)
	return v, ok
}

func ConCooperativa(ctx context.Context, c Cooperativa) context.Context {
	return context.WithValue(ctx, claveCooperativa, c)
}

func CooperativaDe(ctx context.Context) (Cooperativa, bool) {
	v, ok := ctx.Value(claveCooperativa).(Cooperativa)
	return v, ok
}

func ConIntento(ctx context.Context, intento uuid.UUID) context.Context {
	return context.WithValue(ctx, claveIntento, intento)
}

func Intento(ctx context.Context) (uuid.UUID, bool) {
	v, ok := ctx.Value(claveIntento).(uuid.UUID)
	return v, ok
}

// ConPersona: el PublicId de la persona en la cooperativa elegida (lo pone el acotamiento de la sesión).
func ConPersona(ctx context.Context, persona uuid.UUID) context.Context {
	return context.WithValue(ctx, clavePersona, persona)
}

func Persona(ctx context.Context) (uuid.UUID, bool) {
	v, ok := ctx.Value(clavePersona).(uuid.UUID)
	return v, ok
}

func ConActor(ctx context.Context, a Actor) context.Context {
	return context.WithValue(ctx, claveActor, a)
}

// ActorDe devuelve el actor de la petición; sin uno explícito, el sistema.
func ActorDe(ctx context.Context) Actor {
	if v, ok := ctx.Value(claveActor).(Actor); ok && v != "" {
		return v
	}
	return ActorSistema
}
