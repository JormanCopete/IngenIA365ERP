// Package mensajeria: envío de correos, SMS y notificaciones push.
//
// Las pantallas y los flujos piden «envía este mensaje por este canal»; quién lo entrega depende de la configuración:
// correo por SMTP; SMS por un proveedor que contrata la plataforma (FR-011a) y que todavía no existe, así que el SMS
// responde ErrNoDisponible y el flujo ofrece el correo. Los textos van en español y nombran a la cooperativa, nunca
// a otra marca.
package mensajeria

import (
	"context"
	"crypto/tls"
	"errors"
	"fmt"
	"log/slog"
	"mime"
	"net"
	"net/mail"
	"net/smtp"
	"strconv"
	"strings"
	"sync"
	"time"
)

// Canal por el que sale un mensaje.
type Canal string

const (
	Correo Canal = "email"
	SMS    Canal = "sms"
)

// ErrNoDisponible: el canal no tiene proveedor configurado.
var ErrNoDisponible = errors.New("mensajeria: el canal no está disponible")

// Mensaje es lo que se entrega. Asunto sólo aplica al correo.
type Mensaje struct {
	Canal   Canal
	Destino string
	Asunto  string
	Texto   string
}

// Mensajero entrega un mensaje.
type Mensajero interface {
	Enviar(ctx context.Context, m Mensaje) error
}

// Compuesto enruta por canal. Un canal sin mensajero responde ErrNoDisponible.
type Compuesto struct {
	PorCorreo Mensajero
	PorSMS    Mensajero
}

func (c Compuesto) Enviar(ctx context.Context, m Mensaje) error {
	var destino Mensajero
	switch m.Canal {
	case Correo:
		destino = c.PorCorreo
	case SMS:
		destino = c.PorSMS
	}
	if destino == nil {
		return ErrNoDisponible
	}
	return destino.Enviar(ctx, m)
}

// Disponible dice si el canal tiene quién lo entregue.
func (c Compuesto) Disponible(canal Canal) bool {
	switch canal {
	case Correo:
		return c.PorCorreo != nil
	case SMS:
		return c.PorSMS != nil
	}
	return false
}

// ---------------------------------------------------------------------------------------------------------- SMTP --

// ConfigSMTP es el relevo de correo.
type ConfigSMTP struct {
	Host      string
	Puerto    int
	Usuario   string
	Clave     string
	Remitente string // «Canal de asociados <no-responder@ejemplo.test>»
}

// SMTPMensajero entrega correos por un relevo SMTP con STARTTLS (o TLS directo en el 465).
type SMTPMensajero struct {
	cfg     ConfigSMTP
	timeout time.Duration
}

func NuevoSMTP(cfg ConfigSMTP) *SMTPMensajero {
	return &SMTPMensajero{cfg: cfg, timeout: 20 * time.Second}
}

func (s *SMTPMensajero) Enviar(ctx context.Context, m Mensaje) error {
	if m.Canal != Correo {
		return ErrNoDisponible
	}
	remitente, err := mail.ParseAddress(s.cfg.Remitente)
	if err != nil {
		return fmt.Errorf("mensajeria: remitente inválido: %w", err)
	}
	destino, err := mail.ParseAddress(m.Destino)
	if err != nil {
		return fmt.Errorf("mensajeria: destino inválido: %w", err)
	}
	cuerpo := armarCorreo(remitente, destino, m.Asunto, m.Texto)

	direccion := net.JoinHostPort(s.cfg.Host, strconv.Itoa(s.cfg.Puerto))
	marcador := &net.Dialer{Timeout: s.timeout}
	limite, cancelar := context.WithTimeout(ctx, s.timeout)
	defer cancelar()

	var conexion net.Conn
	tlsCfg := &tls.Config{ServerName: s.cfg.Host, MinVersion: tls.VersionTLS12}
	if s.cfg.Puerto == 465 {
		conexion, err = (&tls.Dialer{NetDialer: marcador, Config: tlsCfg}).DialContext(limite, "tcp", direccion)
	} else {
		conexion, err = marcador.DialContext(limite, "tcp", direccion)
	}
	if err != nil {
		return fmt.Errorf("mensajeria: conectar al relevo: %w", err)
	}
	if plazo, ok := limite.Deadline(); ok {
		_ = conexion.SetDeadline(plazo)
	}
	cliente, err := smtp.NewClient(conexion, s.cfg.Host)
	if err != nil {
		_ = conexion.Close()
		return fmt.Errorf("mensajeria: saludo del relevo: %w", err)
	}
	defer cliente.Close()
	if s.cfg.Puerto != 465 {
		if ok, _ := cliente.Extension("STARTTLS"); !ok {
			return errors.New("mensajeria: el relevo no ofrece STARTTLS; no se envía en claro")
		}
		if err := cliente.StartTLS(tlsCfg); err != nil {
			return fmt.Errorf("mensajeria: STARTTLS: %w", err)
		}
	}
	if s.cfg.Usuario != "" {
		if err := cliente.Auth(smtp.PlainAuth("", s.cfg.Usuario, s.cfg.Clave, s.cfg.Host)); err != nil {
			return fmt.Errorf("mensajeria: autenticación en el relevo: %w", err)
		}
	}
	if err := cliente.Mail(remitente.Address); err != nil {
		return fmt.Errorf("mensajeria: MAIL FROM: %w", err)
	}
	if err := cliente.Rcpt(destino.Address); err != nil {
		return fmt.Errorf("mensajeria: RCPT TO: %w", err)
	}
	w, err := cliente.Data()
	if err != nil {
		return fmt.Errorf("mensajeria: DATA: %w", err)
	}
	if _, err := w.Write(cuerpo); err != nil {
		_ = w.Close()
		return fmt.Errorf("mensajeria: escribir el correo: %w", err)
	}
	if err := w.Close(); err != nil {
		return fmt.Errorf("mensajeria: cerrar el correo: %w", err)
	}
	return cliente.Quit()
}

func armarCorreo(de, para *mail.Address, asunto, texto string) []byte {
	var b strings.Builder
	b.WriteString("From: " + de.String() + "\r\n")
	b.WriteString("To: " + para.String() + "\r\n")
	b.WriteString("Subject: " + mime.QEncoding.Encode("utf-8", asunto) + "\r\n")
	b.WriteString("Date: " + time.Now().Format(time.RFC1123Z) + "\r\n")
	b.WriteString("MIME-Version: 1.0\r\n")
	b.WriteString("Content-Type: text/plain; charset=utf-8\r\n")
	b.WriteString("Content-Transfer-Encoding: 8bit\r\n")
	b.WriteString("Auto-Submitted: auto-generated\r\n\r\n")
	b.WriteString(strings.ReplaceAll(texto, "\n", "\r\n"))
	b.WriteString("\r\n")
	return []byte(b.String())
}

// ------------------------------------------------------------------------------------------------------- consola --

// Consola escribe el mensaje en el log en vez de enviarlo. Sólo fuera de producción (pasarela simulada y sin SMTP):
// así se puede activar una cuenta en local leyendo el código del log.
type Consola struct {
	Logger *slog.Logger
}

func (c Consola) Enviar(ctx context.Context, m Mensaje) error {
	c.Logger.WarnContext(ctx, "mensaje no enviado (modo consola, sólo desarrollo)",
		"canal", string(m.Canal), "destino", m.Destino, "asunto", m.Asunto, "texto", m.Texto)
	return nil
}

// ------------------------------------------------------------------------------------------------------- memoria --

// Memoria guarda los mensajes para las pruebas.
type Memoria struct {
	mu        sync.Mutex
	mensajes  []Mensaje
	FallarCon error
}

func (m *Memoria) Enviar(_ context.Context, msg Mensaje) error {
	m.mu.Lock()
	defer m.mu.Unlock()
	if m.FallarCon != nil {
		return m.FallarCon
	}
	m.mensajes = append(m.mensajes, msg)
	return nil
}

// Enviados devuelve una copia de lo enviado.
func (m *Memoria) Enviados() []Mensaje {
	m.mu.Lock()
	defer m.mu.Unlock()
	return append([]Mensaje(nil), m.mensajes...)
}

// Ultimo devuelve el último mensaje a destino, o false.
func (m *Memoria) Ultimo(destino string) (Mensaje, bool) {
	m.mu.Lock()
	defer m.mu.Unlock()
	for i := len(m.mensajes) - 1; i >= 0; i-- {
		if m.mensajes[i].Destino == destino {
			return m.mensajes[i], true
		}
	}
	return Mensaje{}, false
}
