package erp

import (
	"context"
	"crypto/ecdsa"
	"log/slog"
	"net/http"
	"strings"
	"time"

	"github.com/golang-jwt/jwt/v5"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// AlmacenDeJti marca los jti ya usados. Registrar devuelve false si el jti ya estaba (token repetido).
type AlmacenDeJti interface {
	Registrar(ctx context.Context, jti string, vence time.Time) (bool, error)
}

// AlmacenDeJtiEnBase guarda los jti en service_token_jti, compartida por las réplicas (R2).
type AlmacenDeJtiEnBase struct{ q *gen.Queries }

// NuevoAlmacenDeJtiEnBase crea el almacén sobre la base del canal.
func NuevoAlmacenDeJtiEnBase(q *gen.Queries) *AlmacenDeJtiEnBase { return &AlmacenDeJtiEnBase{q: q} }

// Registrar inserta el jti; 0 filas = ya se había usado.
func (a *AlmacenDeJtiEnBase) Registrar(ctx context.Context, jti string, vence time.Time) (bool, error) {
	filas, err := a.q.RegisterServiceJti(ctx, gen.RegisterServiceJtiParams{Jti: jti, ExpiresAt: vence})
	if err != nil {
		return false, err
	}
	return filas == 1, nil
}

// Errores del verificador (sobre del canal).
var (
	ErrTokenAusente = sobre.Nuevo(http.StatusUnauthorized, "Canal.Servicio.TokenAusente",
		"Falta el token de servicio del ERP.")
	ErrTokenInvalido = sobre.Nuevo(http.StatusUnauthorized, "Canal.Servicio.TokenInvalido",
		"El token de servicio del ERP no es válido.")
	ErrTokenRepetido = sobre.Nuevo(http.StatusUnauthorized, "Canal.Servicio.TokenRepetido",
		"El token de servicio del ERP ya se usó.")
	ErrCooperativaDistinta = sobre.Nuevo(http.StatusForbidden, "Canal.Servicio.CooperativaDistinta",
		"El token de servicio del ERP no es de esta cooperativa.")
)

// Verificador valida los tokens que el ERP firma hacia el canal (iss ingenia365-erp, aud ingenia365-canal, ES256,
// a lo sumo 60 s, jti de un solo uso) y deja la cooperativa del sub en el contexto.
type Verificador struct {
	llave   *ecdsa.PublicKey
	almacen AlmacenDeJti
	logger  *slog.Logger
	ahora   func() time.Time
}

// NuevoVerificador crea el verificador.
func NuevoVerificador(llave *ecdsa.PublicKey, almacen AlmacenDeJti, logger *slog.Logger) *Verificador {
	if logger == nil {
		logger = slog.Default()
	}
	return &Verificador{llave: llave, almacen: almacen, logger: logger, ahora: time.Now}
}

type claveSujeto struct{}

// SujetoDe devuelve el sub del token del ERP verificado en la petición.
func SujetoDe(ctx context.Context) (string, bool) {
	v, ok := ctx.Value(claveSujeto{}).(string)
	return v, ok
}

// Middleware protege /internal/v1. Si la ruta lleva /tenants/{id}/, el id debe coincidir con el sub del token.
func (v *Verificador) Middleware(siguiente http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		sujeto, errSobre := v.verificar(r)
		if errSobre != nil {
			sobre.Escribir(w, errSobre)
			return
		}
		if deLaRuta, ok := cooperativaDeLaRuta(r.URL.Path); ok && !strings.EqualFold(deLaRuta, sujeto) {
			sobre.Escribir(w, ErrCooperativaDistinta)
			return
		}
		siguiente.ServeHTTP(w, r.WithContext(context.WithValue(r.Context(), claveSujeto{}, sujeto)))
	})
}

func (v *Verificador) verificar(r *http.Request) (string, *sobre.Error) {
	cabecera := r.Header.Get("Authorization")
	crudo, ok := strings.CutPrefix(cabecera, "Bearer ")
	if !ok || strings.TrimSpace(crudo) == "" {
		return "", ErrTokenAusente
	}

	reclamos := &jwt.RegisteredClaims{}
	_, err := jwt.ParseWithClaims(crudo, reclamos, func(*jwt.Token) (any, error) { return v.llave, nil },
		jwt.WithValidMethods([]string{jwt.SigningMethodES256.Alg()}),
		jwt.WithIssuer(EmisorDelERP),
		jwt.WithAudience(AudienciaDelCanal),
		jwt.WithExpirationRequired(),
		jwt.WithIssuedAt(),
		jwt.WithLeeway(30*time.Second),
		jwt.WithTimeFunc(v.ahora),
	)
	if err != nil {
		v.logger.WarnContext(r.Context(), "token del ERP rechazado", "motivo", err)
		return "", ErrTokenInvalido
	}
	if reclamos.IssuedAt == nil || reclamos.ExpiresAt.Sub(reclamos.IssuedAt.Time) > VidaDelToken+5*time.Second {
		v.logger.WarnContext(r.Context(), "token del ERP con vida mayor a 60 s")
		return "", ErrTokenInvalido
	}
	if reclamos.ID == "" || len(reclamos.ID) > 128 || reclamos.Subject == "" {
		return "", ErrTokenInvalido
	}

	nuevo, err := v.almacen.Registrar(r.Context(), reclamos.ID, reclamos.ExpiresAt.Add(time.Minute))
	if err != nil {
		v.logger.ErrorContext(r.Context(), "no se pudo registrar el jti del ERP", "error", err)
		return "", sobre.ErrInesperado
	}
	if !nuevo {
		v.logger.WarnContext(r.Context(), "token del ERP repetido", "jti", reclamos.ID)
		return "", ErrTokenRepetido
	}
	return reclamos.Subject, nil
}

// cooperativaDeLaRuta extrae {id} de /internal/v1/tenants/{id}/….
func cooperativaDeLaRuta(ruta string) (string, bool) {
	const prefijo = "/internal/v1/tenants/"
	resto, ok := strings.CutPrefix(ruta, prefijo)
	if !ok || resto == "" {
		return "", false
	}
	if i := strings.IndexByte(resto, '/'); i >= 0 {
		resto = resto[:i]
	}
	return resto, resto != ""
}
