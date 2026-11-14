// Package config: configuración del canal por variables de entorno, validada al arrancar.
//
// Si falta algo obligatorio o un valor no sirve, el proceso no arranca y el error lista TODOS los problemas de una
// vez. Los secretos (cadena de conexión, clave SMTP, llaves) nunca salen en el log: ver LogValue.
package config

import (
	"crypto/ecdsa"
	"crypto/elliptic"
	"crypto/x509"
	"encoding/base64"
	"encoding/pem"
	"errors"
	"fmt"
	"log/slog"
	"net"
	"net/url"
	"os"
	"regexp"
	"slices"
	"strconv"
	"strings"
	"time"
)

// Pasarelas admitidas en CANAL_PASARELA.
const (
	PasarelaSimulada   = "simulada"
	PasarelaPlacetopay = "placetopay"
)

// TamanoKEK es el largo exacto de cada KEK (AES-256).
const TamanoKEK = 32

// Config es la configuración ya validada.
type Config struct {
	Direccion   string
	BaseURL     string // cadena de conexión de PostgreSQL; lleva la clave: nunca se registra
	HostPublico string
	ERPURL      *url.URL

	LlavePrivada    *ecdsa.PrivateKey // firma los tokens canal → ERP
	LlaveKID        string
	ERPLlavePublica *ecdsa.PublicKey // verifica los tokens ERP → canal

	KEKs      map[int][]byte // por versión; nunca se registran
	KEKActiva int

	// OrigenesExtra admitidos para las passkeys además de https://HostPublico (p. ej. http://localhost:5173 en desarrollo).
	OrigenesExtra []string

	SMTP *SMTP // nil si no se configuró
	// Push (US10, research R15): Web Push con VAPID para la PWA y FCM HTTP v1 para la app. Sin configurar, los avisos salen sólo por
	// correo. Nunca se registran.
	VAPIDPrivada string // escalar de P-256 en base64url
	VAPIDSujeto  string // mailto: o https: de quien opera el canal
	FCMCuenta    []byte // JSON de la cuenta de servicio de Firebase
	// App de tiendas (US11, T138): la identidad firmada de la app en este ambiente, para assetlinks.json y apple-app-site-association.
	AndroidPaquete string
	AndroidHuellas []string
	IOSAppID       string
	Pasarela       string
	NivelLog       slog.Level

	Frecuencias Frecuencias
	Limite      Limite
}

// SMTP es el relevo de correo. Opcional mientras no haya envíos (C1).
type SMTP struct {
	Host      string
	Puerto    int
	Usuario   string
	Clave     string // nunca se registra
	Remitente string
}

// Frecuencias de los trabajos periódicos.
type Frecuencias struct {
	Cooperativas   time.Duration // arrastre del directorio (sync_tenants)
	Habilitaciones time.Duration // arrastre de habilitaciones (R8)
	Pendientes     time.Duration // consulta de intentos pendientes (R11)
	PurgaDeTokens  time.Duration // purga de jti vencidos
}

// Limite por IP de las peticiones públicas (cubeta de fichas).
type Limite struct {
	PorMinuto int
	Rafaga    int
}

// Fuente abstrae el entorno para poder probar la carga sin tocar el del proceso.
type Fuente struct {
	Buscar  func(string) (string, bool)
	Entorno func() []string
	Leer    func(string) ([]byte, error)
}

// Cargar lee la configuración del entorno del proceso.
func Cargar() (*Config, error) {
	return CargarDe(Fuente{Buscar: os.LookupEnv, Entorno: os.Environ, Leer: os.ReadFile})
}

// ErrorDeConfiguracion junta todos los problemas encontrados.
type ErrorDeConfiguracion struct {
	Faltan    []string
	Invalidas []string
}

func (e *ErrorDeConfiguracion) Error() string {
	var partes []string
	if len(e.Faltan) > 0 {
		partes = append(partes, "faltan "+strings.Join(e.Faltan, ", "))
	}
	if len(e.Invalidas) > 0 {
		partes = append(partes, strings.Join(e.Invalidas, "; "))
	}
	return "configuración del canal incompleta o inválida: " + strings.Join(partes, "; ")
}

type cargador struct {
	f   Fuente
	err ErrorDeConfiguracion
}

func (c *cargador) valor(nombre string) string {
	v, _ := c.f.Buscar(nombre)
	return strings.TrimSpace(v)
}

func (c *cargador) obligatoria(nombre string) string {
	v := c.valor(nombre)
	if v == "" {
		c.err.Faltan = append(c.err.Faltan, nombre)
	}
	return v
}

func (c *cargador) invalida(nombre, formato string, args ...any) {
	c.err.Invalidas = append(c.err.Invalidas, nombre+": "+fmt.Sprintf(formato, args...))
}

func (c *cargador) duracion(nombre string, porDefecto time.Duration) time.Duration {
	v := c.valor(nombre)
	if v == "" {
		return porDefecto
	}
	d, err := time.ParseDuration(v)
	if err != nil || d <= 0 {
		c.invalida(nombre, "«%s» no es una duración positiva (ejemplos: 60s, 5m, 1h)", v)
		return porDefecto
	}
	return d
}

func (c *cargador) entero(nombre string, porDefecto, minimo, maximo int) int {
	v := c.valor(nombre)
	if v == "" {
		return porDefecto
	}
	n, err := strconv.Atoi(v)
	if err != nil || n < minimo || n > maximo {
		c.invalida(nombre, "«%s» debe ser un entero entre %d y %d", v, minimo, maximo)
		return porDefecto
	}
	return n
}

// archivo lee el archivo que nombra la variable; registra el problema si no se puede.
func (c *cargador) archivo(nombre string) []byte {
	ruta := c.obligatoria(nombre)
	if ruta == "" {
		return nil
	}
	contenido, err := c.f.Leer(ruta)
	if err != nil {
		c.invalida(nombre, "no se pudo leer el archivo: %v", err)
		return nil
	}
	return contenido
}

var patronHuella = regexp.MustCompile(`^([0-9A-F]{2}:){31}[0-9A-F]{2}$`)

var patronKEK = regexp.MustCompile(`^CANAL_KEK_V([1-9][0-9]{0,3})_FILE$`)

// CargarDe lee y valida la configuración desde una fuente.
func CargarDe(f Fuente) (*Config, error) {
	c := &cargador{f: f}
	cfg := &Config{}

	cfg.Direccion = c.valor("CANAL_DIRECCION")
	if cfg.Direccion == "" {
		cfg.Direccion = ":8080"
	} else if _, _, err := net.SplitHostPort(cfg.Direccion); err != nil {
		c.invalida("CANAL_DIRECCION", "«%s» no es una dirección host:puerto", cfg.Direccion)
	}

	cfg.BaseURL = c.obligatoria("CANAL_BASE_URL")
	if cfg.BaseURL != "" {
		if u, err := url.Parse(cfg.BaseURL); err != nil || (u.Scheme != "postgres" && u.Scheme != "postgresql") {
			// No se repite el valor: lleva la clave.
			c.invalida("CANAL_BASE_URL", "debe ser una URL postgres://usuario:clave@host:puerto/base")
		}
	}

	cfg.HostPublico = c.obligatoria("CANAL_HOST_PUBLICO")
	if cfg.HostPublico != "" && strings.ContainsAny(cfg.HostPublico, "/ ") {
		c.invalida("CANAL_HOST_PUBLICO", "«%s» debe ser sólo el host (sin esquema ni ruta), p. ej. asociados.ejemplo.test", cfg.HostPublico)
	}

	for _, o := range strings.Split(c.valor("CANAL_ORIGENES_EXTRA"), ",") {
		o = strings.TrimSpace(o)
		if o == "" {
			continue
		}
		if u, err := url.Parse(o); err != nil || (u.Scheme != "http" && u.Scheme != "https") || u.Host == "" || u.Path != "" {
			c.invalida("CANAL_ORIGENES_EXTRA", "«%s» debe ser un origen http(s)://host[:puerto] sin ruta", o)
			continue
		}
		cfg.OrigenesExtra = append(cfg.OrigenesExtra, o)
	}

	if v := c.obligatoria("CANAL_ERP_URL"); v != "" {
		u, err := url.Parse(v)
		if err != nil || (u.Scheme != "http" && u.Scheme != "https") || u.Host == "" {
			c.invalida("CANAL_ERP_URL", "«%s» debe ser una URL absoluta http(s)", v)
		} else {
			cfg.ERPURL = u
		}
	}

	if pemPrivada := c.archivo("CANAL_LLAVE_PRIVADA_FILE"); pemPrivada != nil {
		llave, err := LeerLlavePrivadaES256(pemPrivada)
		if err != nil {
			c.invalida("CANAL_LLAVE_PRIVADA_FILE", "%v", err)
		}
		cfg.LlavePrivada = llave
	}
	cfg.LlaveKID = c.obligatoria("CANAL_LLAVE_KID")

	if pemPublica := c.archivo("CANAL_ERP_LLAVE_PUBLICA_FILE"); pemPublica != nil {
		llave, err := LeerLlavePublicaES256(pemPublica)
		if err != nil {
			c.invalida("CANAL_ERP_LLAVE_PUBLICA_FILE", "%v", err)
		}
		cfg.ERPLlavePublica = llave
	}

	c.cargarKEKs(cfg)
	c.cargarSMTP(cfg)
	cfg.VAPIDPrivada = c.valor("CANAL_VAPID_PRIVADA")
	if cfg.VAPIDPrivada != "" {
		cfg.VAPIDSujeto = c.obligatoria("CANAL_VAPID_SUJETO")
	}
	cfg.AndroidPaquete = c.valor("CANAL_ANDROID_PAQUETE")
	if cfg.AndroidPaquete == "" {
		cfg.AndroidPaquete = "com.ingenia365.canal"
	}
	for _, h := range strings.Split(c.valor("CANAL_ANDROID_HUELLAS"), ",") {
		h = strings.ToUpper(strings.TrimSpace(h))
		if h == "" {
			continue
		}
		if !patronHuella.MatchString(h) {
			c.invalida("CANAL_ANDROID_HUELLAS", "cada huella es el SHA-256 del certificado en AA:BB:… (32 pares)")
			continue
		}
		cfg.AndroidHuellas = append(cfg.AndroidHuellas, h)
	}
	cfg.IOSAppID = c.valor("CANAL_IOS_APP_ID")
	if c.valor("CANAL_FCM_CUENTA_DE_SERVICIO") != "" {
		cfg.FCMCuenta = c.archivo("CANAL_FCM_CUENTA_DE_SERVICIO")
	}

	cfg.Pasarela = strings.ToLower(c.valor("CANAL_PASARELA"))
	if cfg.Pasarela == "" {
		cfg.Pasarela = PasarelaSimulada
	} else if cfg.Pasarela != PasarelaSimulada && cfg.Pasarela != PasarelaPlacetopay {
		c.invalida("CANAL_PASARELA", "«%s» no es una pasarela admitida (simulada | placetopay)", cfg.Pasarela)
	}

	if v := c.valor("CANAL_NIVEL_LOG"); v != "" {
		if err := cfg.NivelLog.UnmarshalText([]byte(v)); err != nil {
			c.invalida("CANAL_NIVEL_LOG", "«%s» no es un nivel (debug | info | warn | error)", v)
		}
	}

	cfg.Frecuencias = Frecuencias{
		Cooperativas:   c.duracion("CANAL_FRECUENCIA_COOPERATIVAS", 5*time.Minute),
		Habilitaciones: c.duracion("CANAL_FRECUENCIA_HABILITACIONES", 60*time.Second),
		Pendientes:     c.duracion("CANAL_FRECUENCIA_PENDIENTES", 15*time.Minute),
		PurgaDeTokens:  c.duracion("CANAL_FRECUENCIA_PURGA_TOKENS", 10*time.Minute),
	}
	cfg.Limite = Limite{
		PorMinuto: c.entero("CANAL_LIMITE_IP_POR_MINUTO", 300, 1, 100000),
		Rafaga:    c.entero("CANAL_LIMITE_IP_RAFAGA", 60, 1, 100000),
	}

	if len(c.err.Faltan) > 0 || len(c.err.Invalidas) > 0 {
		return nil, &c.err
	}
	return cfg, nil
}

func (c *cargador) cargarKEKs(cfg *Config) {
	cfg.KEKs = map[int][]byte{}
	for _, par := range c.f.Entorno() {
		nombre, _, _ := strings.Cut(par, "=")
		m := patronKEK.FindStringSubmatch(nombre)
		if m == nil {
			continue
		}
		version, _ := strconv.Atoi(m[1])
		contenido := c.archivo(nombre)
		if contenido == nil {
			continue
		}
		llave, err := base64.StdEncoding.DecodeString(strings.TrimSpace(string(contenido)))
		if err != nil {
			c.invalida(nombre, "el archivo no está en base64")
			continue
		}
		if len(llave) != TamanoKEK {
			c.invalida(nombre, "la KEK debe tener %d bytes y tiene %d", TamanoKEK, len(llave))
			continue
		}
		cfg.KEKs[version] = llave
	}
	if len(cfg.KEKs) == 0 {
		c.err.Faltan = append(c.err.Faltan, "CANAL_KEK_V1_FILE (al menos una CANAL_KEK_V{n}_FILE)")
		return
	}

	activa := c.valor("CANAL_KEK_ACTIVA")
	if activa == "" {
		if len(cfg.KEKs) > 1 {
			// Con varias versiones, cuál cifra es una decisión explícita (rotación), no un valor por defecto.
			c.err.Faltan = append(c.err.Faltan, "CANAL_KEK_ACTIVA (hay varias versiones de KEK)")
			return
		}
		for v := range cfg.KEKs {
			cfg.KEKActiva = v
		}
		return
	}
	v, err := strconv.Atoi(activa)
	if err != nil {
		c.invalida("CANAL_KEK_ACTIVA", "«%s» no es un número de versión", activa)
		return
	}
	if _, ok := cfg.KEKs[v]; !ok {
		c.invalida("CANAL_KEK_ACTIVA", "no hay CANAL_KEK_V%d_FILE", v)
		return
	}
	cfg.KEKActiva = v
}

func (c *cargador) cargarSMTP(cfg *Config) {
	host := c.valor("CANAL_SMTP_HOST")
	if host == "" {
		return
	}
	cfg.SMTP = &SMTP{
		Host:      host,
		Puerto:    c.entero("CANAL_SMTP_PUERTO", 587, 1, 65535),
		Usuario:   c.valor("CANAL_SMTP_USUARIO"),
		Clave:     c.valor("CANAL_SMTP_CLAVE"),
		Remitente: c.obligatoria("CANAL_SMTP_REMITENTE"),
	}
	if (cfg.SMTP.Usuario == "") != (cfg.SMTP.Clave == "") {
		c.invalida("CANAL_SMTP_USUARIO/CANAL_SMTP_CLAVE", "van las dos o ninguna")
	}
}

// LeerLlavePrivadaES256 lee una llave privada P-256 en PEM (SEC 1 «EC PRIVATE KEY» o PKCS #8 «PRIVATE KEY»).
func LeerLlavePrivadaES256(contenido []byte) (*ecdsa.PrivateKey, error) {
	bloque, _ := pem.Decode(contenido)
	if bloque == nil {
		return nil, errors.New("el archivo no contiene un bloque PEM")
	}
	var llave *ecdsa.PrivateKey
	switch bloque.Type {
	case "EC PRIVATE KEY":
		k, err := x509.ParseECPrivateKey(bloque.Bytes)
		if err != nil {
			return nil, fmt.Errorf("llave EC ilegible: %w", err)
		}
		llave = k
	case "PRIVATE KEY":
		k, err := x509.ParsePKCS8PrivateKey(bloque.Bytes)
		if err != nil {
			return nil, fmt.Errorf("llave PKCS #8 ilegible: %w", err)
		}
		ec, ok := k.(*ecdsa.PrivateKey)
		if !ok {
			return nil, errors.New("la llave no es ECDSA")
		}
		llave = ec
	default:
		return nil, fmt.Errorf("bloque PEM «%s» no admitido (se espera EC PRIVATE KEY o PRIVATE KEY)", bloque.Type)
	}
	if llave.Curve != elliptic.P256() {
		return nil, errors.New("la llave no es de la curva P-256 (ES256)")
	}
	return llave, nil
}

// LeerLlavePublicaES256 lee una llave pública P-256 en PEM («PUBLIC KEY»).
func LeerLlavePublicaES256(contenido []byte) (*ecdsa.PublicKey, error) {
	bloque, _ := pem.Decode(contenido)
	if bloque == nil {
		return nil, errors.New("el archivo no contiene un bloque PEM")
	}
	if bloque.Type != "PUBLIC KEY" {
		return nil, fmt.Errorf("bloque PEM «%s» no admitido (se espera PUBLIC KEY)", bloque.Type)
	}
	k, err := x509.ParsePKIXPublicKey(bloque.Bytes)
	if err != nil {
		return nil, fmt.Errorf("llave pública ilegible: %w", err)
	}
	ec, ok := k.(*ecdsa.PublicKey)
	if !ok || ec.Curve != elliptic.P256() {
		return nil, errors.New("la llave pública no es ECDSA P-256 (ES256)")
	}
	return ec, nil
}

// LogValue registra sólo lo que no es secreto: nunca la cadena de conexión, la clave SMTP ni las llaves.
func (c *Config) LogValue() slog.Value {
	versiones := make([]int, 0, len(c.KEKs))
	for v := range c.KEKs {
		versiones = append(versiones, v)
	}
	slices.Sort(versiones)
	atributos := []slog.Attr{
		slog.String("direccion", c.Direccion),
		slog.String("host_publico", c.HostPublico),
		slog.String("erp_url", c.ERPURL.Redacted()),
		slog.String("llave_kid", c.LlaveKID),
		slog.Any("kek_versiones", versiones),
		slog.Int("kek_activa", c.KEKActiva),
		slog.String("pasarela", c.Pasarela),
		slog.Bool("smtp", c.SMTP != nil),
		slog.Bool("web_push", c.VAPIDPrivada != ""),
		slog.Bool("fcm", len(c.FCMCuenta) > 0),
		slog.Duration("frecuencia_cooperativas", c.Frecuencias.Cooperativas),
		slog.Duration("frecuencia_habilitaciones", c.Frecuencias.Habilitaciones),
		slog.Duration("frecuencia_pendientes", c.Frecuencias.Pendientes),
		slog.Int("limite_ip_por_minuto", c.Limite.PorMinuto),
		slog.Int("limite_ip_rafaga", c.Limite.Rafaga),
	}
	return slog.GroupValue(atributos...)
}
