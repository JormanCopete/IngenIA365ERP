package auditoria

import (
	"context"
	"io"
	"log/slog"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
)

// NuevoLogger crea el logger JSON del servicio. Cada línea registrada con un contexto (slog.InfoContext…) lleva
// además tenant, cuenta, intento y correlacion cuando están en el contexto.
func NuevoLogger(w io.Writer, nivel slog.Leveler) *slog.Logger {
	return slog.New(ManejadorConContexto(slog.NewJSONHandler(w, &slog.HandlerOptions{Level: nivel})))
}

// ManejadorConContexto envuelve un slog.Handler para agregarle los datos de la petición.
func ManejadorConContexto(interno slog.Handler) slog.Handler {
	return &manejador{interno: interno}
}

type manejador struct {
	interno slog.Handler
}

func (m *manejador) Enabled(ctx context.Context, nivel slog.Level) bool {
	return m.interno.Enabled(ctx, nivel)
}

func (m *manejador) Handle(ctx context.Context, r slog.Record) error {
	if ctx != nil {
		if c := contexto.Correlacion(ctx); c != "" {
			r.AddAttrs(slog.String("correlacion", c))
		}
		if coop, ok := contexto.CooperativaDe(ctx); ok {
			r.AddAttrs(slog.String("tenant", coop.PublicID.String()))
		}
		if cuenta, ok := contexto.Cuenta(ctx); ok {
			r.AddAttrs(slog.String("cuenta", cuenta.String()))
		}
		if intento, ok := contexto.Intento(ctx); ok {
			r.AddAttrs(slog.String("intento", intento.String()))
		}
	}
	return m.interno.Handle(ctx, r)
}

func (m *manejador) WithAttrs(attrs []slog.Attr) slog.Handler {
	return &manejador{interno: m.interno.WithAttrs(attrs)}
}

// WithGroup: los datos del contexto quedan dentro del grupo abierto; el servicio no usa grupos en sus logs.
func (m *manejador) WithGroup(nombre string) slog.Handler {
	return &manejador{interno: m.interno.WithGroup(nombre)}
}
