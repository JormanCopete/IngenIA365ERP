// Package simulada es una pasarela de mentira para desarrollo y pruebas (T085), como el mock de la pasarela: crea
// sesiones en memoria, deja elegir el resultado (aprobada, pendiente, rechazada, fallida, vencida) y firma
// notificaciones con el secreto del sitio igual que la real. Nunca se usa con CANAL_PASARELA = placetopay.
package simulada

import (
	"context"
	"crypto/sha256"
	"crypto/subtle"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"html/template"
	"net/http"
	"strconv"
	"strings"
	"sync"
	"time"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/pasarela"
)

// Pasarela simulada. Es segura para varias goroutines; el estado vive en memoria del proceso.
type Pasarela struct {
	mu        sync.Mutex
	siguiente int64
	sesiones  map[int64]*sesion
	ahora     func() time.Time
	// HostPublico arma la processUrl (la página de la pasarela simulada la sirve el propio canal).
	HostPublico string
	// Esquema de la processUrl: https salvo en pruebas.
	Esquema string
	// Fallar hace que la próxima operación falle como pasarela caída (pruebas).
	Fallar bool
}

type sesion struct {
	sitio      string
	solicitud  pasarela.SolicitudDeSesion
	estado     pasarela.EstadoDePago
	fecha      time.Time
	aprobacion *pasarela.Aprobacion
	reversada  bool
}

// Nueva crea la pasarela simulada.
func Nueva(hostPublico string) *Pasarela {
	return &Pasarela{siguiente: 1000, sesiones: map[int64]*sesion{}, ahora: time.Now, HostPublico: hostPublico, Esquema: "https"}
}

var _ pasarela.Pasarela = (*Pasarela)(nil)

// ConReloj fija el reloj (pruebas).
func (p *Pasarela) ConReloj(ahora func() time.Time) *Pasarela {
	p.ahora = ahora
	return p
}

// CrearSesion crea una sesión pendiente.
func (p *Pasarela) CrearSesion(_ context.Context, sitio pasarela.Sitio, s pasarela.SolicitudDeSesion) (pasarela.SesionCreada, error) {
	if _, err := pasarela.ValidarComprador(s.Comprador); err != nil {
		return pasarela.SesionCreada{}, err
	}
	p.mu.Lock()
	defer p.mu.Unlock()
	if p.Fallar {
		return pasarela.SesionCreada{}, pasarela.ErrNoDisponible
	}
	p.siguiente++
	id := p.siguiente
	p.sesiones[id] = &sesion{sitio: sitio.ID.String(), solicitud: s, estado: pasarela.EstadoPendiente, fecha: p.ahora().UTC()}
	return pasarela.SesionCreada{RequestID: id, ProcessURL: fmt.Sprintf("%s://%s/simulada/%d", p.Esquema, p.HostPublico, id)}, nil
}

// ConsultarSesion devuelve el estado elegido. Una pendiente cuyo vencimiento pasó queda vencida, como en la real.
func (p *Pasarela) ConsultarSesion(_ context.Context, _ pasarela.Sitio, requestID int64) (pasarela.EstadoDeSesion, error) {
	p.mu.Lock()
	defer p.mu.Unlock()
	if p.Fallar {
		return pasarela.EstadoDeSesion{}, pasarela.ErrNoDisponible
	}
	s, ok := p.sesiones[requestID]
	if !ok {
		return pasarela.EstadoDeSesion{}, &pasarela.ErrRechazo{Codigo: "404", Mensaje: "La sesión no existe."}
	}
	if s.estado == pasarela.EstadoPendiente && p.ahora().After(s.solicitud.Vence) {
		s.estado, s.fecha = pasarela.EstadoVencido, s.solicitud.Vence.UTC()
	}
	e := pasarela.EstadoDeSesion{Estado: s.estado, Crudo: strings.ToUpper(string(s.estado)), Fecha: s.fecha, Aprobacion: s.aprobacion}
	e.Respuesta, _ = json.Marshal(map[string]any{"requestId": requestID, "status": e.Crudo, "simulada": true})
	return e, nil
}

// Reversar marca la sesión reversada si estaba aprobada.
func (p *Pasarela) Reversar(_ context.Context, _ pasarela.Sitio, referenciaInterna int64) (pasarela.ResultadoDeReverso, error) {
	p.mu.Lock()
	defer p.mu.Unlock()
	if p.Fallar {
		return pasarela.ResultadoDeReverso{}, pasarela.ErrNoDisponible
	}
	for _, s := range p.sesiones {
		if s.aprobacion != nil && s.aprobacion.ReferenciaInterna == referenciaInterna && !s.reversada {
			s.reversada = true
			return pasarela.ResultadoDeReverso{Exitoso: true, Estado: "APPROVED", Autorizacion: s.aprobacion.Autorizacion,
				Respuesta: json.RawMessage(`{"status":"APPROVED","simulada":true}`)}, nil
		}
	}
	return pasarela.ResultadoDeReverso{Estado: "REJECTED", Mensaje: "No hay un pago aprobado con esa referencia.",
		Respuesta: json.RawMessage(`{"status":"REJECTED","simulada":true}`)}, nil
}

// Fijar pone el resultado de una sesión (la página de la simulada o la prueba). Aprobada genera los datos de la
// aprobación como lo haría la pasarela; vencida y rechazada, no.
func (p *Pasarela) Fijar(requestID int64, estado pasarela.EstadoDePago) error {
	p.mu.Lock()
	defer p.mu.Unlock()
	s, ok := p.sesiones[requestID]
	if !ok {
		return fmt.Errorf("simulada: la sesión %d no existe", requestID)
	}
	s.estado, s.fecha = estado, p.ahora().UTC()
	if estado == pasarela.EstadoAprobado {
		s.aprobacion = &pasarela.Aprobacion{
			ReferenciaInterna: requestID * 10,
			Autorizacion:      fmt.Sprintf("%06d", requestID%1_000_000),
			Recibo:            strconv.FormatInt(requestID*7, 10),
			Franquicia:        "PS_PSE",
			Medio:             "PSE",
			Emisor:            "Banco simulado",
			Fecha:             s.fecha,
		}
	}
	return nil
}

// Solicitud devuelve lo que el canal pidió para una sesión (pruebas).
func (p *Pasarela) Solicitud(requestID int64) (pasarela.SolicitudDeSesion, bool) {
	p.mu.Lock()
	defer p.mu.Unlock()
	s, ok := p.sesiones[requestID]
	if !ok {
		return pasarela.SolicitudDeSesion{}, false
	}
	return s.solicitud, true
}

// Sesiones cuenta las sesiones creadas (pruebas).
func (p *Pasarela) Sesiones() int {
	p.mu.Lock()
	defer p.mu.Unlock()
	return len(p.sesiones)
}

// ------------------------------------------------------------------------------------------------ notificación --

type notificacion struct {
	Status struct {
		Status  string `json:"status"`
		Reason  string `json:"reason"`
		Message string `json:"message"`
		Date    string `json:"date"`
	} `json:"status"`
	RequestID int64  `json:"requestId"`
	Reference string `json:"reference"`
	Signature string `json:"signature"`
}

func firma(requestID int64, estado, fecha, secreto string) string {
	s := sha256.Sum256([]byte(strconv.FormatInt(requestID, 10) + estado + fecha + secreto))
	return "sha256:" + hex.EncodeToString(s[:])
}

// Notificacion arma el cuerpo firmado que mandaría la pasarela para la sesión, con su estado actual.
func (p *Pasarela) Notificacion(sitio pasarela.Sitio, requestID int64) ([]byte, error) {
	p.mu.Lock()
	defer p.mu.Unlock()
	s, ok := p.sesiones[requestID]
	if !ok {
		return nil, fmt.Errorf("simulada: la sesión %d no existe", requestID)
	}
	var n notificacion
	n.RequestID, n.Reference = requestID, s.solicitud.Referencia
	n.Status.Status = strings.ToUpper(string(s.estado))
	n.Status.Date = s.fecha.Format(time.RFC3339)
	n.Signature = firma(requestID, n.Status.Status, n.Status.Date, sitio.Secreto.Revelar())
	return json.Marshal(n)
}

// VerificarNotificacion comprueba la firma SHA-256 en tiempo constante.
func (p *Pasarela) VerificarNotificacion(sitio pasarela.Sitio, cuerpo []byte) (pasarela.Notificacion, error) {
	var n notificacion
	if err := json.Unmarshal(cuerpo, &n); err != nil || n.RequestID == 0 {
		return pasarela.Notificacion{}, pasarela.ErrFirmaInvalida
	}
	esperada := firma(n.RequestID, n.Status.Status, n.Status.Date, sitio.Secreto.Revelar())
	if subtle.ConstantTimeCompare([]byte(n.Signature), []byte(esperada)) != 1 {
		return pasarela.Notificacion{}, pasarela.ErrFirmaInvalida
	}
	return pasarela.Notificacion{RequestID: n.RequestID, Referencia: n.Reference, Estado: n.Status.Status, Fecha: n.Status.Date}, nil
}

// --------------------------------------------------------------------------------------- página de la simulada --

var pagina = template.Must(template.New("simulada").Parse(`<!doctype html>
<html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Pasarela simulada</title>
<style>body{font-family:system-ui,sans-serif;max-width:32rem;margin:2rem auto;padding:0 1rem}button{display:block;width:100%;margin:.5rem 0;padding:.75rem;font-size:1rem}</style>
</head><body>
<h1>Pasarela simulada</h1>
<p>Sólo para desarrollo y pruebas: no se cobra nada.</p>
<p>Referencia <strong>{{.Referencia}}</strong> · {{.Total}} COP</p>
<form method="post">
<button name="estado" value="approved">Aprobar</button>
<button name="estado" value="pending">Dejar pendiente</button>
<button name="estado" value="rejected">Rechazar</button>
<button name="estado" value="failed">Fallar</button>
<button name="estado" value="cancel">Cancelar y volver</button>
</form>
</body></html>`))

// Pagina sirve GET y POST /simulada/{requestId}: muestra los botones, fija el estado y vuelve a la URL de retorno.
func (p *Pasarela) Pagina(w http.ResponseWriter, r *http.Request) {
	id, err := strconv.ParseInt(r.PathValue("requestId"), 10, 64)
	solicitud, ok := p.Solicitud(id)
	if err != nil || !ok {
		http.NotFound(w, r)
		return
	}
	if r.Method == http.MethodGet {
		w.Header().Set("Content-Type", "text/html; charset=utf-8")
		_ = pagina.Execute(w, map[string]any{"Referencia": solicitud.Referencia, "Total": solicitud.Total})
		return
	}
	switch estado := pasarela.EstadoDePago(r.FormValue("estado")); estado {
	case pasarela.EstadoAprobado, pasarela.EstadoPendiente, pasarela.EstadoRechazado, pasarela.EstadoFallido:
		_ = p.Fijar(id, estado)
	case "cancel":
		_ = p.Fijar(id, pasarela.EstadoRechazado)
	default:
		http.Error(w, "estado no admitido", http.StatusBadRequest)
		return
	}
	http.Redirect(w, r, solicitud.URLDeRetorno, http.StatusSeeOther)
}
