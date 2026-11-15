package entidades

import (
	"context"
	"errors"
	"fmt"
	"iter"
	"log/slog"
	"net/http"
	"strings"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// Códigos de canal (research R12): tres letras por cooperativa, prefijo de sus referencias de pago. Los reserva el
// canal cuando el ERP enciende el canal de una cooperativa; una vez reservado, el código es suyo para siempre.

// Errores de la reserva.
var (
	ErrCodigosAgotados = sobre.Nuevo(http.StatusConflict, "Canal.Codigo.Agotados",
		"No quedan códigos de canal de tres letras libres.")
	ErrCodigoEnConflicto = sobre.Nuevo(http.StatusConflict, "Canal.Codigo.EnConflicto",
		"El código de canal que la cooperativa ya tiene en el directorio está reservado por otra cooperativa. "+
			"Comuníquese con soporte.")
)

// Codigos reserva los códigos de canal.
type Codigos struct {
	q      *gen.Queries
	logger *slog.Logger
	ahora  func() time.Time
}

// NuevosCodigos crea el reservador sobre la base del canal.
func NuevosCodigos(db gen.DBTX, logger *slog.Logger) *Codigos {
	if logger == nil {
		logger = slog.Default()
	}
	return &Codigos{q: gen.New(db), logger: logger, ahora: time.Now}
}

// Reserva es el código de una cooperativa; Nueva indica si se reservó en esta llamada.
type Reserva struct {
	Codigo string
	Nueva  bool
}

// Reservar devuelve el código de la cooperativa. Si no tiene, reserva el que trae el directorio para ella o, si
// tampoco, la sugerencia normalizada o su primera variante libre (ver Candidatos). Es seguro con llamadas
// concurrentes: la unicidad la garantizan las restricciones de channel_codes y una carrera perdida se relee.
func (c *Codigos) Reservar(ctx context.Context, cooperativa uuid.UUID, sugerido string) (Reserva, error) {
	if codigo, ok, err := c.suyo(ctx, cooperativa); err != nil || ok {
		return Reserva{Codigo: codigo}, err
	}

	// Si el directorio ya la trae con código (encendida antes de existir esta reserva), ése es el suyo.
	directorio, err := c.q.GetTenantByPublicID(ctx, cooperativa)
	switch {
	case err == nil:
		reservado, err := c.intentar(ctx, cooperativa, directorio.ChannelCode)
		if err != nil || reservado {
			return Reserva{Codigo: directorio.ChannelCode, Nueva: reservado}, err
		}
		if codigo, ok, err := c.suyo(ctx, cooperativa); err != nil || ok {
			return Reserva{Codigo: codigo}, err
		}
		c.logger.ErrorContext(ctx, "el código de canal del directorio está reservado por otra cooperativa",
			"cooperativa", cooperativa, "codigo", directorio.ChannelCode)
		return Reserva{}, ErrCodigoEnConflicto
	case !errors.Is(err, pgx.ErrNoRows):
		return Reserva{}, fmt.Errorf("entidades: leer la cooperativa del directorio: %w", err)
	}

	tomados, err := c.q.ListTakenChannelCodes(ctx, cooperativa)
	if err != nil {
		return Reserva{}, fmt.Errorf("entidades: leer los códigos ocupados: %w", err)
	}
	ocupados := make(map[string]bool, len(tomados))
	for _, t := range tomados {
		ocupados[t] = true
	}
	for candidato := range Candidatos(sugerido) {
		if ocupados[candidato] {
			continue
		}
		reservado, err := c.intentar(ctx, cooperativa, candidato)
		if err != nil {
			return Reserva{}, err
		}
		if reservado {
			return Reserva{Codigo: candidato, Nueva: true}, nil
		}
		// Carrera perdida: o la cooperativa ya obtuvo uno en otra petición, o el código lo tomó otra.
		if codigo, ok, err := c.suyo(ctx, cooperativa); err != nil || ok {
			return Reserva{Codigo: codigo}, err
		}
		ocupados[candidato] = true
	}
	return Reserva{}, ErrCodigosAgotados
}

func (c *Codigos) suyo(ctx context.Context, cooperativa uuid.UUID) (string, bool, error) {
	codigo, err := c.q.GetChannelCodeByTenant(ctx, cooperativa)
	if errors.Is(err, pgx.ErrNoRows) {
		return "", false, nil
	}
	if err != nil {
		return "", false, fmt.Errorf("entidades: leer el código de la cooperativa: %w", err)
	}
	return codigo, true, nil
}

func (c *Codigos) intentar(ctx context.Context, cooperativa uuid.UUID, codigo string) (bool, error) {
	filas, err := c.q.ReserveChannelCode(ctx, gen.ReserveChannelCodeParams{
		Code: codigo, TenantPublicID: cooperativa, ReservedAt: c.ahora().UTC(),
	})
	if err != nil {
		return false, fmt.Errorf("entidades: reservar el código %s: %w", codigo, err)
	}
	return filas == 1, nil
}

// NormalizarSugerencia deja sólo letras A-Z en mayúscula (las tildes y la Ñ pierden su marca) y ajusta a tres:
// recorta lo que sobra y completa con X lo que falta.
func NormalizarSugerencia(sugerido string) string {
	sinMarcas := strings.NewReplacer("Á", "A", "É", "E", "Í", "I", "Ó", "O", "Ú", "U", "Ü", "U", "Ñ", "N").
		Replace(strings.ToUpper(sugerido))
	var b strings.Builder
	for _, r := range sinMarcas {
		if r >= 'A' && r <= 'Z' {
			b.WriteRune(r)
			if b.Len() == 3 {
				break
			}
		}
	}
	return b.String() + strings.Repeat("X", 3-b.Len())
}

// Candidatos da, en orden fijo y sin repetir: la sugerencia normalizada; sus dos primeras letras con A..Z; su
// primera letra con AA..ZZ; y AAA..ZZZ.
func Candidatos(sugerido string) iter.Seq[string] {
	base := NormalizarSugerencia(sugerido)
	return func(yield func(string) bool) {
		vistos := map[string]bool{}
		dar := func(codigo string) bool {
			if vistos[codigo] {
				return true
			}
			vistos[codigo] = true
			return yield(codigo)
		}
		if !dar(base) {
			return
		}
		for a := 'A'; a <= 'Z'; a++ {
			if !dar(base[:2] + string(a)) {
				return
			}
		}
		for a := 'A'; a <= 'Z'; a++ {
			for b := 'A'; b <= 'Z'; b++ {
				if !dar(base[:1] + string(a) + string(b)) {
					return
				}
			}
		}
		for a := 'A'; a <= 'Z'; a++ {
			for b := 'A'; b <= 'Z'; b++ {
				for c := 'A'; c <= 'Z'; c++ {
					if !dar(string(a) + string(b) + string(c)) {
						return
					}
				}
			}
		}
	}
}
