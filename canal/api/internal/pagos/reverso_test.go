package pagos

import (
	"context"
	"errors"
	"net/http"
	"testing"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5/pgxpool"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/pasarela"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

func TestDentroDelCorte(t *testing.T) {
	cot := horaDeColombia
	corte := 20 * time.Hour
	aprobado := time.Date(2026, 12, 5, 10, 0, 0, 0, cot)
	casos := []struct {
		ahora time.Time
		dice  bool
	}{
		{time.Date(2026, 12, 5, 19, 59, 0, 0, cot), true},
		{time.Date(2026, 12, 5, 20, 0, 0, 0, cot), false},
		{time.Date(2026, 12, 6, 9, 0, 0, 0, cot), false},
		// 23:30 en Colombia ya es el día siguiente en UTC: cuenta la fecha de Colombia.
		{time.Date(2026, 12, 6, 4, 30, 0, 0, time.UTC), false},
		{time.Date(2026, 12, 5, 23, 0, 0, 0, time.UTC), true},
	}
	for _, c := range casos {
		if got := DentroDelCorte(aprobado, c.ahora, corte); got != c.dice {
			t.Errorf("aprobado %v, ahora %v: %v, se esperaba %v", aprobado, c.ahora, got, c.dice)
		}
	}
	if _, e := (PedidoDeReverso{SolicitadoPor: uuid.New(), Motivo: "x", Corte: "25:00", Modo: ReversoPorLaPasarela}).Validar(); e == nil {
		t.Fatal("un corte inválido debe rechazarse")
	}
	if d, e := (PedidoDeReverso{SolicitadoPor: uuid.New(), Motivo: "x", Modo: ReversoPorTesoreria}).Validar(); e != nil || d != 20*time.Hour {
		t.Fatalf("corte por defecto: %v %v", d, e)
	}
}

func esError(t *testing.T, err error, codigo string) {
	t.Helper()
	var e *sobre.Error
	if !errors.As(err, &e) || e.Codigo != codigo {
		t.Fatalf("se esperaba %s: %v", codigo, err)
	}
}

func reverso(t *testing.T, pool *pgxpool.Pool) {
	b := nuevoBanco(t, pool)
	ctx := context.Background()
	b.reloj.mu.Lock()
	b.reloj.t = time.Date(2026, 12, 5, 15, 0, 0, 0, horaDeColombia).UTC()
	b.reloj.mu.Unlock()
	pedido := PedidoDeReverso{SolicitadoPor: uuid.New(), Motivo: "Pago doble", Corte: "20:00", Modo: ReversoPorLaPasarela}

	aprobar := func(llave string) (string, string) {
		r := b.pagar(llave, item(claveAporte, "Due", 50000))
		id := r.texto("attemptId")
		if err := b.sim.Fijar(b.requestID(id), pasarela.EstadoAprobado); err != nil {
			t.Fatal(err)
		}
		_ = b.pedir(http.MethodGet, "/api/v1/payments/"+id+"?return=1", "", nil)
		return id, r.texto("reference")
	}

	// Pendiente: no se reversa. Otra cooperativa: no existe. Pedido incompleto: inválido.
	pendiente := b.pagar("llave-0009", item(claveCredito, "Due", 332500))
	_, err := b.s.Reversar(ctx, b.tenant.ID, pendiente.texto("reference"), pedido)
	esError(t, err, "Canal.Reverso.NoAprobado")

	id, ref := aprobar("llave-0001")
	otra := nuevoBanco(t, pool)
	_, err = b.s.Reversar(ctx, otra.tenant.ID, ref, pedido)
	esError(t, err, "Canal.Pago.NoExiste")
	_, err = b.s.Reversar(ctx, b.tenant.ID, ref, PedidoDeReverso{Modo: ReversoPorLaPasarela})
	esError(t, err, "Canal.Reverso.Invalido")

	// Pasarela caída: no se marca nada.
	b.sim.Fallar = true
	_, err = b.s.Reversar(ctx, b.tenant.ID, ref, pedido)
	esError(t, err, "Canal.Pago.PasarelaNoDisponible")
	b.sim.Fallar = false

	// Antes del corte: la pasarela lo reversa y el intento queda marcado, con su evento.
	r, err := b.s.Reversar(ctx, b.tenant.ID, ref, pedido)
	if err != nil || !r.Reversado || r.Modo == nil || *r.Modo != ReversoPorLaPasarela || r.ReversadoEn == nil {
		t.Fatalf("reverso: %+v %v", r, err)
	}
	var eventos int
	for _, e := range b.eventos(id) {
		if e.Source == FuenteReverso {
			eventos++
		}
	}
	if eventos != 1 {
		t.Fatalf("eventos de reverso: %d", eventos)
	}
	// Repetirlo responde lo mismo sin volver a la pasarela (la simulada respondería «no hay pago aprobado»).
	if r2, err := b.s.Reversar(ctx, b.tenant.ID, ref, pedido); err != nil || !r2.Reversado || *r2.Modo != ReversoPorLaPasarela {
		t.Fatalf("repetido: %+v %v", r2, err)
	}
	// El asociado lo ve reversado (FR-053) y el ERP también.
	g := b.pedir(http.MethodGet, "/api/v1/payments/"+id, "", nil)
	if g.texto("reversalKind") != ReversoPorLaPasarela || g.texto("reversedAt") == "" {
		t.Fatalf("lo que ve el asociado: %v", g.cuerpo)
	}
	if is, _ := b.s.BuscarIntentos(ctx, b.tenant.ID, "", "", ref); len(is) != 1 || is[0].Reversado == nil || *is[0].TipoReverso != ReversoPorLaPasarela {
		t.Fatalf("lo que ve el ERP: %+v", is)
	}
	// Un aviso que no alcanzó a salir ya no se envía: aplicaría un pago devuelto.
	antes := len(b.aviso.avisos)
	if err := b.s.Avisar(ctx, uuid.MustParse(id)); err != nil || len(b.aviso.avisos) != antes {
		t.Fatalf("aviso tras el reverso: %v, %d avisos", err, len(b.aviso.avisos)-antes)
	}

	// Después del corte: 409 FueraDeCorte y, por Tesorería, sólo se marca.
	id2, ref2 := aprobar("llave-0002")
	b.reloj.avanzar(5*time.Hour + 30*time.Minute) // 20:30 en Colombia
	_, err = b.s.Reversar(ctx, b.tenant.ID, ref2, pedido)
	esError(t, err, "Canal.Reverso.FueraDeCorte")
	tesoreria := pedido
	tesoreria.Modo = ReversoPorTesoreria
	r3, err := b.s.Reversar(ctx, b.tenant.ID, ref2, tesoreria)
	if err != nil || !r3.Reversado || *r3.Modo != ReversoPorTesoreria {
		t.Fatalf("por Tesorería: %+v %v", r3, err)
	}
	if g := b.pedir(http.MethodGet, "/api/v1/payments/"+id2, "", nil); g.texto("reversalKind") != ReversoPorTesoreria {
		t.Fatalf("devuelto por Tesorería: %v", g.cuerpo)
	}
}
