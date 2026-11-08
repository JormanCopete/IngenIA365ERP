package auditoria_test

import (
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"log/slog"
	"strings"
	"testing"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5/pgxpool"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/auditoria"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/dbtest"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
)

// Un solo contenedor para las pruebas que escriben en audit_log.
func TestRegistro(t *testing.T) {
	pool := dbtest.Migrada(t)
	t.Run("toma los datos del contexto", func(t *testing.T) { registrarTomaLosDatosDelContexto(t, pool) })
	t.Run("rechaza secretos", func(t *testing.T) { registrarRechazaSecretos(t, pool) })
}

func registrarTomaLosDatosDelContexto(t *testing.T, pool *pgxpool.Pool) {
	ctx := context.Background()
	registro := auditoria.Nuevo(pool)

	cuenta := uuid.New()
	ctx = contexto.ConActor(ctx, contexto.ActorAsociado)
	ctx = contexto.ConCuenta(ctx, cuenta)
	ctx = contexto.ConIP(ctx, "203.0.113.7")
	ctx = contexto.ConAgente(ctx, "Navegador de prueba")
	ctx = contexto.ConCorrelacion(ctx, "corr-1")

	if err := registro.Registrar(ctx, "Canal.Prueba.Hecha", "sujeto-1", map[string]any{"monto": 1000}); err != nil {
		t.Fatal(err)
	}
	filas, err := gen.New(pool).ListAuditEntriesByAction(ctx, gen.ListAuditEntriesByActionParams{Action: "Canal.Prueba.Hecha", MaxRows: 10})
	if err != nil {
		t.Fatal(err)
	}
	if len(filas) != 1 {
		t.Fatalf("filas = %d", len(filas))
	}
	f := filas[0]
	if f.Actor != "member" || f.AccountID == nil || *f.AccountID != cuenta || f.Subject != "sujeto-1" {
		t.Errorf("fila inesperada: %+v", f)
	}
	if f.Ip == nil || *f.Ip != "203.0.113.7" || f.UserAgent == nil || f.CorrelationID == nil || *f.CorrelationID != "corr-1" {
		t.Errorf("faltan IP, agente o correlación: %+v", f)
	}
	var datos map[string]any
	if err := json.Unmarshal(f.Data, &datos); err != nil || datos["monto"] != float64(1000) {
		t.Errorf("datos = %s (%v)", f.Data, err)
	}
}

func registrarRechazaSecretos(t *testing.T, pool *pgxpool.Pool) {
	registro := auditoria.Nuevo(pool)
	for _, datos := range []any{
		map[string]any{"secretKey": "x"},
		map[string]any{"sitio": map[string]any{"tranKey": "x"}},
		[]any{map[string]any{"Contraseña": "x"}},
		struct {
			Token string `json:"token"`
		}{"x"},
	} {
		if err := registro.Registrar(context.Background(), "Canal.Prueba.Secreto", "s", datos); !errors.Is(err, auditoria.ErrDatosConSecretos) {
			t.Errorf("con %#v: %v", datos, err)
		}
	}
	if err := registro.Registrar(context.Background(), "Canal.Prueba.Secreto", "s",
		map[string]any{"authorization": "123", "code": "Canal.X"}); err != nil {
		t.Errorf("autorización y código de error sí se auditan: %v", err)
	}
}

func TestLoggerAgregaLosDatosDeLaPeticion(t *testing.T) {
	var salida bytes.Buffer
	logger := auditoria.NuevoLogger(&salida, slog.LevelInfo)
	coop := contexto.Cooperativa{ID: uuid.New(), PublicID: uuid.New()}
	ctx := contexto.ConCooperativa(contexto.ConCorrelacion(context.Background(), "corr-9"), coop)
	logger.InfoContext(ctx, "algo pasó")

	var linea map[string]any
	if err := json.Unmarshal(salida.Bytes(), &linea); err != nil {
		t.Fatalf("no es JSON: %s", salida.String())
	}
	if linea["correlacion"] != "corr-9" || linea["tenant"] != coop.PublicID.String() {
		t.Errorf("línea sin correlación o cooperativa: %s", salida.String())
	}
	if strings.Contains(salida.String(), "cuenta") {
		t.Errorf("sin cuenta en el contexto no debía salir el campo: %s", salida.String())
	}
}
