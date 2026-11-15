package entidades

import (
	"context"
	"errors"
	"testing"

	"github.com/google/uuid"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/dbtest"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/erp"
)

type fuenteFija struct {
	cooperativas []erp.Cooperativa
	err          error
}

func (f *fuenteFija) ListarCooperativas(context.Context) ([]erp.Cooperativa, error) {
	return f.cooperativas, f.err
}

func cadena(s string) *string { return &s }

func TestDirectorio(t *testing.T) {
	pool := dbtest.Migrada(t)
	q := gen.New(pool)
	ctx := context.Background()

	a, b := uuid.New(), uuid.New()
	fuente := &fuenteFija{cooperativas: []erp.Cooperativa{
		{TenantPublicID: a, ChannelCode: cadena("CFP"), DisplayName: "Cooperativa A", InactivityMinutes: 5},
		{TenantPublicID: b, ChannelCode: cadena("CAB"), DisplayName: "Cooperativa B", InactivityMinutes: 10, SmsEnabled: true},
		{TenantPublicID: uuid.New(), ChannelCode: nil, DisplayName: "Sin código"},
	}}
	directorio := NuevoDirectorio(q, fuente, dbtest.Silencioso())

	res, err := directorio.Sincronizar(ctx)
	if err != nil {
		t.Fatalf("primera sincronización: %v", err)
	}
	if res.Vigentes != 2 || res.Omitidas != 1 || res.Inhabilitadas != 0 {
		t.Fatalf("resultado inesperado: %+v", res)
	}

	// Repetir no duplica; B deja de estar listada y queda inhabilitada, sin borrarse.
	fuente.cooperativas = fuente.cooperativas[:1]
	res, err = directorio.Sincronizar(ctx)
	if err != nil {
		t.Fatalf("segunda sincronización: %v", err)
	}
	if res.Vigentes != 1 || res.Inhabilitadas != 1 {
		t.Fatalf("resultado inesperado: %+v", res)
	}
	filas, err := q.ListTenants(ctx)
	if err != nil {
		t.Fatalf("listar: %v", err)
	}
	if len(filas) != 2 {
		t.Fatalf("se esperaban 2 filas (ninguna borrada), hay %d", len(filas))
	}
	for _, f := range filas {
		if f.TenantPublicID == b && f.IsEnabled {
			t.Fatalf("B debía quedar inhabilitada")
		}
		if f.TenantPublicID == a && !f.IsEnabled {
			t.Fatalf("A debía seguir habilitada")
		}
	}

	// Una caída del ERP no apaga a nadie.
	fuente.err = errors.New("ERP caído")
	if _, err := directorio.Sincronizar(ctx); err == nil {
		t.Fatalf("se esperaba el error del ERP")
	}
	vigente, err := q.GetTenantByPublicID(ctx, a)
	if err != nil || !vigente.IsEnabled {
		t.Fatalf("A no debía apagarse por una caída del ERP: %v %+v", err, vigente)
	}
}
