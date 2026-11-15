package pagos

import (
	"context"
	"net/http"
	"testing"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5/pgxpool"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/avisos"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/erp"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/pasarela"
)

func avisosEncolados(b *banco) []avisos.ArgsEnviarAviso {
	var salida []avisos.ArgsEnviarAviso
	for _, j := range b.cola.tomar() {
		if a, ok := j.(avisos.ArgsEnviarAviso); ok {
			salida = append(salida, a)
		}
	}
	return salida
}

// T133 (FR-056): el cambio de estado encola su aviso en la misma transacción; el aviso al ERP aplicado avisa que la cooperativa
// lo registró.
func avisosDelPago(t *testing.T, pool *pgxpool.Pool) {
	b := nuevoBanco(t, pool)
	ctx := context.Background()

	rechazado := b.pagar("llave-0001", item(claveAporte, "Due", 50000))
	_ = b.sim.Fijar(b.requestID(rechazado.texto("attemptId")), pasarela.EstadoRechazado)
	b.cola.tomar()
	_ = b.pedir(http.MethodGet, "/api/v1/payments/"+rechazado.texto("attemptId")+"?return=1", "", nil)
	if a := avisosEncolados(b); len(a) != 1 || a[0].Clase != avisos.PagoRechazado || a[0].Cuenta != b.cuenta {
		t.Fatalf("rechazado: %+v", a)
	}

	// Un pendiente que resolvió la sonda (el asociado ya no miraba): «quedó aprobado».
	pendiente := b.pagar("llave-0002", item(claveAporte, "Due", 50000))
	id := uuid.MustParse(pendiente.texto("attemptId"))
	_ = b.sim.Fijar(b.requestID(id.String()), pasarela.EstadoAprobado)
	b.cola.tomar()
	if _, err := b.s.Actualizar(ctx, id, FuenteSonda); err != nil {
		t.Fatal(err)
	}
	if a := avisosEncolados(b); len(a) != 1 || a[0].Clase != avisos.PendienteResuelto {
		t.Fatalf("pendiente resuelto: %+v", a)
	}

	// El ERP lo aplicó: «quedó registrado».
	recibo := uuid.New()
	b.aviso.resultado = erp.ResultadoDelAviso{Resultado: "Applied", Recibo: &recibo}
	if err := b.s.Avisar(ctx, id); err != nil {
		t.Fatal(err)
	}
	if a := avisosEncolados(b); len(a) != 1 || a[0].Clase != avisos.PagoAplicado || a[0].Llave != "intento:"+id.String()+":erp" {
		t.Fatalf("aplicado: %+v", a)
	}
}
