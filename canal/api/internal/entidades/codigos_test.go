package entidades

import (
	"context"
	"encoding/json"
	"errors"
	"sync"
	"testing"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5/pgxpool"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/dbtest"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
)

func TestNormalizarSugerencia(t *testing.T) {
	casos := map[string]string{
		"cfp":        "CFP",
		"Coop Ñandú": "COO",
		"Ébano":      "EBA",
		"ñ-1":        "NXX",
		"a1":         "AXX",
		"":           "XXX",
		"12 3":       "XXX",
	}
	for entrada, esperado := range casos {
		if got := NormalizarSugerencia(entrada); got != esperado {
			t.Errorf("NormalizarSugerencia(%q) = %q, se esperaba %q", entrada, got, esperado)
		}
	}
}

func TestCandidatosCubrenTodoSinRepetir(t *testing.T) {
	var lista []string
	vistos := map[string]bool{}
	for c := range Candidatos("cfp") {
		if vistos[c] {
			t.Fatalf("candidato repetido: %s", c)
		}
		if !codigoValido.MatchString(c) {
			t.Fatalf("candidato inválido: %s", c)
		}
		vistos[c] = true
		lista = append(lista, c)
	}
	if len(lista) != 26*26*26 {
		t.Fatalf("candidatos = %d, se esperaban todos los %d", len(lista), 26*26*26)
	}
	// Orden: la sugerencia, sus dos letras con A..Z (sin repetir CFP), su primera letra con AA..ZZ, y el resto.
	for i, esperado := range map[int]string{0: "CFP", 1: "CFA", 2: "CFB", 25: "CFZ", 26: "CAA", 27: "CAB"} {
		if lista[i] != esperado {
			t.Errorf("candidato %d = %s, se esperaba %s", i, lista[i], esperado)
		}
	}
}

func TestReservarCodigos(t *testing.T) {
	pool := dbtest.Migrada(t)
	t.Run("sugerencia, código existente y variante", func(t *testing.T) { reservarSugerenciaYVariante(t, pool) })
	t.Run("respeta el código del directorio", func(t *testing.T) { reservarRespetaDirectorio(t, pool) })
	t.Run("concurrencia", func(t *testing.T) { reservarConcurrente(t, pool) })
}

func reservarSugerenciaYVariante(t *testing.T, pool *pgxpool.Pool) {
	ctx := context.Background()
	codigos := NuevosCodigos(pool, dbtest.Silencioso())
	a, b, c := uuid.New(), uuid.New(), uuid.New()

	r, err := codigos.Reservar(ctx, a, "cfp")
	if err != nil || r.Codigo != "CFP" || !r.Nueva {
		t.Fatalf("primera reserva: %+v %v", r, err)
	}
	// La misma cooperativa recibe el suyo, sugiera lo que sugiera.
	r, err = codigos.Reservar(ctx, a, "OTRO")
	if err != nil || r.Codigo != "CFP" || r.Nueva {
		t.Fatalf("la cooperativa ya tenía código: %+v %v", r, err)
	}
	// Otra cooperativa con la misma sugerencia recibe la primera variante libre.
	r, err = codigos.Reservar(ctx, b, "CFP")
	if err != nil || r.Codigo != "CFA" || !r.Nueva {
		t.Fatalf("colisión: %+v %v", r, err)
	}
	r, err = codigos.Reservar(ctx, c, "Cfp")
	if err != nil || r.Codigo != "CFB" {
		t.Fatalf("segunda colisión: %+v %v", r, err)
	}
}

func reservarRespetaDirectorio(t *testing.T, pool *pgxpool.Pool) {
	ctx := context.Background()
	q := gen.New(pool)
	codigos := NuevosCodigos(pool, dbtest.Silencioso())

	// Una cooperativa que ya está en el directorio con «MNO» (encendida antes de existir la reserva).
	enDirectorio := uuid.New()
	if _, err := q.UpsertTenant(ctx, gen.UpsertTenantParams{
		ID: uuid.Must(uuid.NewV7()), TenantPublicID: enDirectorio, ChannelCode: "MNO", DisplayName: "Antigua",
		Brand: json.RawMessage(`{}`), Support: json.RawMessage(`{}`), InactivityMinutes: 5, LastSyncAt: time.Now(),
	}); err != nil {
		t.Fatal(err)
	}
	// Otra no puede quedarse con «MNO» aunque nadie lo haya reservado en channel_codes.
	r, err := codigos.Reservar(ctx, uuid.New(), "MNO")
	if err != nil || r.Codigo != "MNA" {
		t.Fatalf("«MNO» está en el directorio: %+v %v", r, err)
	}
	// La del directorio recibe el suyo, no la sugerencia.
	r, err = codigos.Reservar(ctx, enDirectorio, "XYZ")
	if err != nil || r.Codigo != "MNO" || !r.Nueva {
		t.Fatalf("debía reservar el código del directorio: %+v %v", r, err)
	}

	// Si el código del directorio ya lo reservó otra (datos inconsistentes), no se inventa otro.
	enConflicto := uuid.New()
	if _, err := q.UpsertTenant(ctx, gen.UpsertTenantParams{
		ID: uuid.Must(uuid.NewV7()), TenantPublicID: enConflicto, ChannelCode: "RST", DisplayName: "En conflicto",
		Brand: json.RawMessage(`{}`), Support: json.RawMessage(`{}`), InactivityMinutes: 5, LastSyncAt: time.Now(),
	}); err != nil {
		t.Fatal(err)
	}
	if _, err := q.ReserveChannelCode(ctx, gen.ReserveChannelCodeParams{Code: "RST", TenantPublicID: uuid.New(), ReservedAt: time.Now()}); err != nil {
		t.Fatal(err)
	}
	if _, err := codigos.Reservar(ctx, enConflicto, "RST"); !errors.Is(err, ErrCodigoEnConflicto) {
		t.Fatalf("se esperaba Canal.Codigo.EnConflicto, llegó %v", err)
	}
}

func reservarConcurrente(t *testing.T, pool *pgxpool.Pool) {
	ctx := context.Background()
	codigos := NuevosCodigos(pool, dbtest.Silencioso())

	// Veinte cooperativas piden el mismo código a la vez: todas reciben uno, todos distintos.
	const n = 20
	resultados := make([]string, n)
	errores := make([]error, n)
	var wg sync.WaitGroup
	for i := range n {
		wg.Go(func() {
			r, err := codigos.Reservar(ctx, uuid.New(), "WWW")
			resultados[i], errores[i] = r.Codigo, err
		})
	}
	wg.Wait()
	vistos := map[string]bool{}
	for i, c := range resultados {
		if errores[i] != nil {
			t.Fatalf("reserva %d: %v", i, errores[i])
		}
		if vistos[c] {
			t.Fatalf("código repetido entre cooperativas: %s (%v)", c, resultados)
		}
		vistos[c] = true
	}
	if !vistos["WWW"] {
		t.Fatalf("una de ellas debía quedarse con la sugerencia: %v", resultados)
	}

	// La misma cooperativa pide a la vez varias veces: un solo código.
	misma := uuid.New()
	for i := range 10 {
		wg.Go(func() {
			r, err := codigos.Reservar(ctx, misma, "VVV")
			resultados[i], errores[i] = r.Codigo, err
		})
	}
	wg.Wait()
	for i := range 10 {
		if errores[i] != nil || resultados[i] != resultados[0] {
			t.Fatalf("la misma cooperativa recibió códigos distintos: %v %v", resultados[:10], errores[:10])
		}
	}
}
