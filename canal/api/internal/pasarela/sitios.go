package pasarela

import (
	"context"
	"errors"
	"fmt"
	"log/slog"
	"net/http"
	"net/url"
	"slices"
	"strings"
	"time"
	"unicode"
	"unicode/utf8"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"
	"github.com/jackc/pgx/v5/pgxpool"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/auditoria"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/cifrado"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/config"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// Sitios de pasarela (research R10). Las credenciales las digita el administrador en el ERP, el ERP las reenvía
// aquí sin guardarlas y el canal las guarda cifradas con la KEK activa, amarradas al id del sitio. El secreto no sale
// nunca: ni en la respuesta, ni en la auditoría, ni en el log.

// PasarelaPlacetopay es la única pasarela admitida hoy (columna gateway).
const PasarelaPlacetopay = "placetopay"

// MediosDePagoAdmitidos son los medios que un sitio puede ofrecer.
var MediosDePagoAdmitidos = []string{"pse", "visa", "master", "amex", "diners"}

// Estado de las credenciales de un sitio (columna status).
type Estado string

const (
	EstadoActiva       Estado = "active"
	EstadoFallida      Estado = "failed"
	EstadoPorVerificar Estado = "pending_verification"
)

// Secreto es la llave secreta de una pasarela. fmt, slog y encoding/json lo escriben como «[oculto]»: el valor sólo
// sale con Revelar, para cifrarlo o usarlo contra la pasarela.
type Secreto string

const oculto = "[oculto]"

// Format hace que cualquier verbo de fmt (%v, %s, %q, %#v…) escriba «[oculto]».
func (Secreto) Format(f fmt.State, _ rune) { _, _ = f.Write([]byte(oculto)) }

// LogValue oculta el valor en slog.
func (Secreto) LogValue() slog.Value { return slog.StringValue(oculto) }

// MarshalJSON oculta el valor al serializar (la deserialización sí lo lee: así llega del ERP).
func (Secreto) MarshalJSON() ([]byte, error) { return []byte(`"` + oculto + `"`), nil }

// Revelar devuelve el valor.
func (s Secreto) Revelar() string { return string(s) }

func (s Secreto) valido() bool {
	v := string(s)
	return strings.TrimSpace(v) != "" && utf8.RuneCountInString(v) <= 200 && !strings.ContainsFunc(v, unicode.IsControl)
}

// Credenciales de un sitio tal como llegan del ERP.
type Credenciales struct {
	Pasarela     string
	URLBase      string
	Login        string
	Secreto      Secreto
	MediosDePago []string
}

// Verificacion es el resultado de probar unas credenciales contra la pasarela.
type Verificacion struct {
	Estado       Estado
	Motivo       string     // vacío si no hay
	VerificadaEn *time.Time // nil si no se pudo probar
}

// VerificadorDeCredenciales prueba unas credenciales contra la pasarela (R10: crear y consultar una sesión de
// prueba). Un error es una falla inesperada; una pasarela que rechaza las credenciales es Estado = EstadoFallida.
type VerificadorDeCredenciales interface {
	Verificar(ctx context.Context, sitio uuid.UUID, c Credenciales) (Verificacion, error)
}

// VerificadorSimulado acepta todo: es el de la pasarela simulada (desarrollo y pruebas).
type VerificadorSimulado struct{ Ahora func() time.Time }

// Verificar marca las credenciales activas.
func (v VerificadorSimulado) Verificar(context.Context, uuid.UUID, Credenciales) (Verificacion, error) {
	ahora := time.Now
	if v.Ahora != nil {
		ahora = v.Ahora
	}
	t := ahora().UTC()
	return Verificacion{Estado: EstadoActiva, VerificadaEn: &t}, nil
}

// MotivoSinAdaptador explica por qué unas credenciales de Placetopay quedan por verificar.
const MotivoSinAdaptador = "La verificación contra la pasarela se habilita con su adaptador."

// VerificadorPendiente deja las credenciales por verificar: la prueba real contra Placetopay llega con su adaptador
// (tarea T086).
type VerificadorPendiente struct{}

// Verificar no prueba nada todavía.
func (VerificadorPendiente) Verificar(context.Context, uuid.UUID, Credenciales) (Verificacion, error) {
	return Verificacion{Estado: EstadoPorVerificar, Motivo: MotivoSinAdaptador}, nil
}

// VerificadorPara elige el verificador según CANAL_PASARELA.
func VerificadorPara(pasarela string) VerificadorDeCredenciales {
	if pasarela == config.PasarelaSimulada {
		return VerificadorSimulado{}
	}
	return VerificadorPendiente{}
}

// Errores de los sitios.
var (
	ErrSitioDeOtraCooperativa = sobre.Nuevo(http.StatusConflict, "Canal.Sitio.DeOtraCooperativa",
		"El sitio de pasarela pertenece a otra cooperativa.")
)

// Sitios guarda y verifica las credenciales de los sitios de pasarela.
type Sitios struct {
	pool              *pgxpool.Pool
	q                 *gen.Queries
	llavero           *cifrado.Llavero
	verificador       VerificadorDeCredenciales
	auditoria         *auditoria.Registro
	permitirHTTPLocal bool
	logger            *slog.Logger
}

// NuevosSitios crea el servicio. permitirHTTPLocal admite http://localhost y http://127.0.0.1 como URL base (sólo
// con la pasarela simulada).
func NuevosSitios(pool *pgxpool.Pool, llavero *cifrado.Llavero, verificador VerificadorDeCredenciales,
	registro *auditoria.Registro, permitirHTTPLocal bool, logger *slog.Logger) *Sitios {
	if logger == nil {
		logger = slog.Default()
	}
	return &Sitios{
		pool: pool, q: gen.New(pool), llavero: llavero, verificador: verificador, auditoria: registro,
		permitirHTTPLocal: permitirHTTPLocal, logger: logger,
	}
}

// Resultado es lo que vuelve al ERP: nunca el secreto.
type Resultado struct {
	Estado        Estado
	VerificadaEn  *time.Time
	LoginUltimos4 string
	Motivo        *string
}

// Activa dice si las credenciales quedaron listas para cobrar.
func (r Resultado) Activa() bool { return r.Estado == EstadoActiva }

// Acciones de auditoría.
const (
	AccionCredencialesGuardadas  = "Canal.Sitio.CredencialesGuardadas"
	AccionCredencialesRechazadas = "Canal.Sitio.CredencialesRechazadas"
)

// Guardar valida, cifra, verifica y guarda las credenciales del sitio de la cooperativa. Los rechazos son
// *sobre.Error (400 validación, 409 sitio de otra cooperativa); todo intento queda auditado.
func (s *Sitios) Guardar(ctx context.Context, cooperativa, sitio uuid.UUID, c Credenciales) (Resultado, error) {
	normalizadas, errSobre := Validar(c, s.permitirHTTPLocal)
	if errSobre != nil {
		return Resultado{}, s.rechazar(ctx, cooperativa, sitio, errSobre)
	}

	existente, err := s.q.GetGatewaySite(ctx, sitio)
	switch {
	case err == nil && existente.TenantPublicID != cooperativa:
		return Resultado{}, s.rechazar(ctx, cooperativa, sitio, ErrSitioDeOtraCooperativa)
	case err != nil && !errors.Is(err, pgx.ErrNoRows):
		return Resultado{}, fmt.Errorf("pasarela: leer el sitio: %w", err)
	}
	nuevo := errors.Is(err, pgx.ErrNoRows)

	cifradoDelSecreto, version, err := s.llavero.Cifrar([]byte(normalizadas.Secreto.Revelar()), sitio[:])
	if err != nil {
		return Resultado{}, fmt.Errorf("pasarela: cifrar el secreto: %w", err)
	}

	verificacion, err := s.verificador.Verificar(ctx, sitio, normalizadas)
	if err != nil {
		return Resultado{}, fmt.Errorf("pasarela: verificar las credenciales: %w", err)
	}
	var motivo *string
	if verificacion.Motivo != "" {
		motivo = &verificacion.Motivo
	}

	tx, err := s.pool.Begin(ctx)
	if err != nil {
		return Resultado{}, fmt.Errorf("pasarela: abrir la transacción: %w", err)
	}
	defer func() {
		if err := tx.Rollback(context.WithoutCancel(ctx)); err != nil && !errors.Is(err, pgx.ErrTxClosed) {
			s.logger.ErrorContext(ctx, "no se pudo deshacer la transacción del sitio de pasarela", "error", err)
		}
	}()
	guardado, err := s.q.WithTx(tx).UpsertGatewaySite(ctx, gen.UpsertGatewaySiteParams{
		ID:               sitio,
		TenantPublicID:   cooperativa,
		Gateway:          PasarelaPlacetopay,
		BaseUrl:          normalizadas.URLBase,
		Login:            normalizadas.Login,
		SecretCiphertext: cifradoDelSecreto,
		KekVersion:       int32(version),
		PaymentMethods:   normalizadas.MediosDePago,
		Status:           string(verificacion.Estado),
		StatusReason:     motivo,
		CheckedAt:        verificacion.VerificadaEn,
	})
	if errors.Is(err, pgx.ErrNoRows) {
		// Otra cooperativa creó el sitio entre la lectura y la escritura.
		return Resultado{}, s.rechazar(ctx, cooperativa, sitio, ErrSitioDeOtraCooperativa)
	}
	if err != nil {
		return Resultado{}, fmt.Errorf("pasarela: guardar el sitio: %w", err)
	}

	res := Resultado{
		Estado:        Estado(guardado.Status),
		VerificadaEn:  guardado.CheckedAt,
		LoginUltimos4: UltimosCuatro(guardado.Login),
		Motivo:        guardado.StatusReason,
	}
	host := ""
	if u, err := url.Parse(guardado.BaseUrl); err == nil {
		host = u.Host
	}
	if err := s.auditoria.EnTransaccion(tx).Registrar(ctx, AccionCredencialesGuardadas, sitio.String(), map[string]any{
		"tenantPublicId": cooperativa,
		"gateway":        guardado.Gateway,
		"baseUrlHost":    host,
		"loginLast4":     res.LoginUltimos4,
		"paymentMethods": guardado.PaymentMethods,
		"status":         guardado.Status,
		"statusReason":   guardado.StatusReason,
		"kekVersion":     guardado.KekVersion,
		"new":            nuevo,
	}); err != nil {
		return Resultado{}, err
	}
	if err := tx.Commit(ctx); err != nil {
		return Resultado{}, fmt.Errorf("pasarela: confirmar el sitio: %w", err)
	}
	s.logger.InfoContext(ctx, "credenciales de pasarela guardadas", "sitio", sitio, "estado", guardado.Status, "kek_version", version)
	return res, nil
}

// rechazar audita el rechazo y devuelve el error del sobre. Si la auditoría falla, eso es lo que se devuelve.
func (s *Sitios) rechazar(ctx context.Context, cooperativa, sitio uuid.UUID, e *sobre.Error) error {
	if err := s.auditoria.Registrar(ctx, AccionCredencialesRechazadas, sitio.String(), map[string]any{
		"tenantPublicId": cooperativa,
		"code":           e.Codigo,
	}); err != nil {
		return err
	}
	s.logger.WarnContext(ctx, "credenciales de pasarela rechazadas", "sitio", sitio, "codigo", e.Codigo)
	return e
}

// UltimosCuatro devuelve los cuatro últimos caracteres del login (todo, si es más corto).
func UltimosCuatro(login string) string {
	runas := []rune(login)
	if len(runas) <= 4 {
		return login
	}
	return string(runas[len(runas)-4:])
}

// Problema es un error de validación de un campo (va en data.errores del sobre).
type Problema struct {
	Campo   string `json:"campo"`
	Codigo  string `json:"code"`
	Mensaje string `json:"message"`
}

// Validar revisa y normaliza las credenciales. Con varios problemas, el sobre lleva el código y el mensaje del
// primero y la lista completa en data.errores.
func Validar(c Credenciales, permitirHTTPLocal bool) (Credenciales, *sobre.Error) {
	var problemas []Problema
	agregar := func(campo, codigo, mensaje string) {
		problemas = append(problemas, Problema{Campo: campo, Codigo: codigo, Mensaje: mensaje})
	}

	n := Credenciales{Secreto: c.Secreto}
	n.Pasarela = strings.ToLower(strings.TrimSpace(c.Pasarela))
	if n.Pasarela != PasarelaPlacetopay {
		agregar("gateway", "Canal.Sitio.PasarelaNoAdmitida", "La pasarela no está admitida. Admite: Placetopay.")
	}

	n.URLBase = strings.TrimSuffix(strings.TrimSpace(c.URLBase), "/")
	if !urlValida(n.URLBase, permitirHTTPLocal) {
		mensaje := "La URL base de la pasarela debe ser una dirección https completa, sin usuario, consulta ni fragmento."
		if permitirHTTPLocal {
			mensaje += " Con la pasarela simulada también se admite http://localhost o http://127.0.0.1."
		}
		agregar("baseUrl", "Canal.Sitio.UrlInvalida", mensaje)
	}

	n.Login = strings.TrimSpace(c.Login)
	if largo := utf8.RuneCountInString(n.Login); largo < 1 || largo > 100 || strings.ContainsFunc(n.Login, unicode.IsControl) {
		agregar("login", "Canal.Sitio.LoginInvalido", "El login de la pasarela es obligatorio y admite hasta 100 caracteres.")
	}

	if !c.Secreto.valido() {
		agregar("secretKey", "Canal.Sitio.SecretoInvalido", "La llave secreta de la pasarela es obligatoria y admite hasta 200 caracteres.")
	}

	n.MediosDePago = make([]string, 0, len(c.MediosDePago)) // nunca nil: la columna es NOT NULL
	var desconocidos []string
	for _, m := range c.MediosDePago {
		m = strings.ToLower(strings.TrimSpace(m))
		switch {
		case !slices.Contains(MediosDePagoAdmitidos, m):
			desconocidos = append(desconocidos, m)
		case !slices.Contains(n.MediosDePago, m):
			n.MediosDePago = append(n.MediosDePago, m)
		}
	}
	if len(desconocidos) > 0 {
		agregar("paymentMethods", "Canal.Sitio.MedioDePagoDesconocido", fmt.Sprintf(
			"Medios de pago no admitidos: %s. Admite: %s.", strings.Join(desconocidos, ", "), strings.Join(MediosDePagoAdmitidos, ", ")))
	}

	if len(problemas) == 0 {
		return n, nil
	}
	return Credenciales{}, sobre.Nuevo(http.StatusBadRequest, problemas[0].Codigo, problemas[0].Mensaje).
		ConDatos(map[string]any{"errores": problemas})
}

func urlValida(crudo string, permitirHTTPLocal bool) bool {
	if crudo == "" || len(crudo) > 500 {
		return false
	}
	u, err := url.Parse(crudo)
	if err != nil || u.Host == "" || u.User != nil || u.Opaque != "" || u.RawQuery != "" || u.Fragment != "" || u.ForceQuery {
		return false
	}
	switch u.Scheme {
	case "https":
		return true
	case "http":
		host := u.Hostname()
		return permitirHTTPLocal && (strings.EqualFold(host, "localhost") || host == "127.0.0.1")
	}
	return false
}

// ErrSitioNoExiste: no hay credenciales guardadas para el sitio.
var ErrSitioNoExiste = errors.New("pasarela: el sitio no existe")

// Cargar lee el sitio y descifra su secreto para usarlo contra la pasarela. Devuelve también el estado de las
// credenciales: sólo un sitio activo cobra; las notificaciones se verifican con cualquiera.
func (s *Sitios) Cargar(ctx context.Context, id uuid.UUID) (Sitio, Estado, error) {
	g, err := s.q.GetGatewaySite(ctx, id)
	if errors.Is(err, pgx.ErrNoRows) {
		return Sitio{}, "", ErrSitioNoExiste
	}
	if err != nil {
		return Sitio{}, "", fmt.Errorf("pasarela: leer el sitio: %w", err)
	}
	secreto, err := s.llavero.Descifrar(g.SecretCiphertext, int(g.KekVersion), g.ID[:])
	if err != nil {
		return Sitio{}, "", fmt.Errorf("pasarela: descifrar el secreto del sitio %s: %w", g.ID, err)
	}
	return Sitio{
		ID: g.ID, Cooperativa: g.TenantPublicID, URLBase: g.BaseUrl, Login: g.Login, Secreto: Secreto(secreto),
		MediosDePago: g.PaymentMethods,
	}, Estado(g.Status), nil
}
