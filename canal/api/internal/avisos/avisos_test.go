package avisos

import (
	"context"
	"encoding/json"
	"fmt"
	"strings"
	"sync"
	"testing"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"
	"github.com/riverqueue/river"
	"github.com/riverqueue/river/rivertype"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/auditoria"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/dbtest"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/erp"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/mensajeria"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/mensajeria/push"
)

type cola struct {
	mu   sync.Mutex
	args []river.JobArgs
}

func (c *cola) InsertTx(_ context.Context, _ pgx.Tx, a river.JobArgs, _ *river.InsertOpts) (*rivertype.JobInsertResult, error) {
	return c.Insert(context.Background(), a, nil)
}

func (c *cola) Insert(_ context.Context, a river.JobArgs, _ *river.InsertOpts) (*rivertype.JobInsertResult, error) {
	c.mu.Lock()
	defer c.mu.Unlock()
	c.args = append(c.args, a)
	return &rivertype.JobInsertResult{}, nil
}

type cuotasFalsas struct{ cuotas []erp.CuotaProxima }

func (c cuotasFalsas) CuotasProximas(context.Context, uuid.UUID, int) ([]erp.CuotaProxima, error) {
	return c.cuotas, nil
}

func TestAvisos(t *testing.T) {
	pool := dbtest.Migrada(t)
	ctx := context.Background()
	q := gen.New(pool)
	ahora := time.Now().UTC().Truncate(time.Second)
	coop, err := q.UpsertTenant(ctx, gen.UpsertTenantParams{ID: uuid.Must(uuid.NewV7()), TenantPublicID: uuid.New(), ChannelCode: "AVS", DisplayName: "Cooperativa Avisos",
		Brand: json.RawMessage(`{}`), Support: json.RawMessage(`{}`), InactivityMinutes: 5, DueReminderDays: 3, LastSyncAt: ahora})
	if err != nil {
		t.Fatal(err)
	}
	persona := uuid.New()
	if _, err := q.UpsertEnablement(ctx, gen.UpsertEnablementParams{ID: uuid.New(), TenantID: coop.ID, PersonPublicID: persona, DocumentType: "CC",
		DocumentNumber: "31000001", Status: "enabled", EnabledAt: ahora, ChangeSequence: 1}); err != nil {
		t.Fatal(err)
	}
	cuenta := uuid.Must(uuid.NewV7())
	correo := "ana@ejemplo.test"
	if err := q.InsertAccount(ctx, gen.InsertAccountParams{ID: cuenta, DocumentType: "CC", DocumentNumber: "31000001", FirstNames: "Ana", Email: &correo,
		PasswordHash: strings.Repeat("x", 60), ActivatedAt: ahora, ValidatingTenantID: coop.ID}); err != nil {
		t.Fatal(err)
	}
	correos := &mensajeria.Memoria{}
	pushes := &push.Memoria{Vencidas: map[string]bool{"token-viejo": true}}
	s := Nuevo(Opciones{Q: q, Mensajero: mensajeria.Compuesto{PorCorreo: correos}, Push: push.Compuesto{PorFCM: pushes}, Auditoria: auditoria.Nuevo(pool)})
	if _, err := s.RegistrarSuscripcion(ctx, cuenta, push.FCM, "token-nuevo", nil, nil); err != nil {
		t.Fatal(err)
	}
	if _, err := s.RegistrarSuscripcion(ctx, cuenta, push.FCM, "token-viejo", nil, nil); err != nil {
		t.Fatal(err)
	}

	t.Run("por defecto todo encendido y la seguridad bloqueada", func(t *testing.T) {
		ps, err := s.Preferencias(ctx, cuenta)
		if err != nil || len(ps) != len(Configurables)+1 || !ps[len(ps)-1].Bloqueada || ps[len(ps)-1].Clase != Seguridad {
			t.Fatalf("preferencias: %+v %v", ps, err)
		}
		if err := s.GuardarPreferencias(ctx, cuenta, []Preferencia{{Clase: Seguridad}}); err != ErrClaseNoConfigurable {
			t.Fatalf("la seguridad no se apaga: %v", err)
		}
	})

	t.Run("respeta las preferencias y revoca la suscripción vencida", func(t *testing.T) {
		if err := s.GuardarPreferencias(ctx, cuenta, []Preferencia{{Clase: PagoAplicado, Correo: false, Push: true}}); err != nil {
			t.Fatal(err)
		}
		if err := s.Enviar(ctx, ArgsEnviarAviso{Cuenta: cuenta, Clase: PagoAplicado, Titulo: "Tu pago quedó registrado", Cuerpo: "x"}); err != nil {
			t.Fatal(err)
		}
		if len(correos.Enviados()) != 0 || len(pushes.Enviadas()) != 1 || pushes.Enviadas()[0].Suscripcion.Endpoint != "token-nuevo" {
			t.Fatalf("correos %d, push %+v", len(correos.Enviados()), pushes.Enviadas())
		}
		subs, _ := q.ListPushSubscriptions(ctx, cuenta)
		if len(subs) != 1 || subs[0].Endpoint != "token-nuevo" {
			t.Fatalf("la vencida debe quedar revocada: %+v", subs)
		}
		// Seguridad: siempre por correo, aunque se haya apagado todo lo demás.
		if err := s.Enviar(ctx, ArgsEnviarAviso{Cuenta: cuenta, Clase: Seguridad, Titulo: "Nuevo ingreso", Cuerpo: "y"}); err != nil {
			t.Fatal(err)
		}
		if m, ok := correos.Ultimo(correo); !ok || m.Asunto != "Nuevo ingreso" {
			t.Fatalf("aviso de seguridad: %+v", m)
		}
	})

	t.Run("recordatorio de cuota una vez por cuota y día", func(t *testing.T) {
		c := &cola{}
		tr := &TrabajadorDeRecordatorios{Q: q, Encolador: c, Publico: "https://asociados.ejemplo.test", ERP: cuotasFalsas{cuotas: []erp.CuotaProxima{
			{Persona: persona, Clave: "LOAN:x", Descripcion: "Crédito CR-1", Vence: "2026-12-05", Valor: 332500},
			{Persona: uuid.New(), Clave: "LOAN:y", Descripcion: "Crédito CR-2", Vence: "2026-12-05", Valor: 1000},
		}}}
		n, err := tr.Recordar(ctx)
		if err != nil || n != 1 {
			t.Fatalf("encolados %d: %v", n, err)
		}
		a := c.args[0].(ArgsEnviarAviso)
		if a.Cuenta != cuenta || a.Clase != CuotaProxima || !strings.Contains(a.Cuerpo, "$ 332.500") || !strings.Contains(a.Cuerpo, "5 de diciembre") ||
			a.Llave != fmt.Sprintf("cuota:%s:LOAN:x:2026-12-05", coop.TenantPublicID) {
			t.Fatalf("aviso: %+v", a)
		}
	})
}
