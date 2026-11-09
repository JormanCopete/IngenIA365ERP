package identidad

import "net/http"

// Registrar pone las rutas de identidad de /api/v1 (contracts/canal-api.md §A, «Identidad»).
func (s *Servicio) Registrar(m Registrador) {
	publica := func(patron string, h http.HandlerFunc) { m.Manejar(patron, h) }
	conSesion := func(patron string, h http.HandlerFunc) { m.Manejar(patron, s.ConSesion(h)) }

	// Activación (FR-016 a FR-020).
	publica("POST /api/v1/activation/start", s.iniciarActivacion)
	publica("POST /api/v1/activation/send", s.enviarCodigo(propositoActivacion))
	publica("POST /api/v1/activation/verify", s.verificarActivacion)
	publica("POST /api/v1/activation/complete", s.completarActivacion)

	// Ingreso (FR-021, FR-022) y recuperación (FR-023).
	publica("POST /api/v1/auth/login", s.ingresar)
	publica("POST /api/v1/auth/login/send", s.enviarCodigo(propositoDispositivo))
	publica("POST /api/v1/auth/login/verify", s.verificarIngreso)
	publica("POST /api/v1/auth/passkey/options", s.opcionesDeIngreso)
	publica("POST /api/v1/auth/passkey/verify", s.verificarIngresoConPasskey)
	publica("POST /api/v1/auth/recovery/start", s.iniciarRecuperacion)
	publica("POST /api/v1/auth/recovery/send", s.enviarCodigo(propositoRecuperacion))
	publica("POST /api/v1/auth/recovery/verify", s.verificarRecuperacion)
	publica("POST /api/v1/auth/recovery/complete", s.completarRecuperacion)
	publica("POST /api/v1/auth/logout", s.salir)

	// Sesión (FR-025, FR-027). GET /session no cuenta como actividad.
	m.Manejar("GET /api/v1/session", s.conSesion(http.HandlerFunc(s.estadoDeLaSesion), false))
	conSesion("POST /api/v1/session/keepalive", s.seguir)
	conSesion("POST /api/v1/session/tenant", s.elegirCooperativa)

	// Perfil (FR-024, FR-027).
	conSesion("GET /api/v1/me", s.perfil)
	conSesion("POST /api/v1/me/contact", s.pedirCambioDeContacto)
	conSesion("POST /api/v1/me/contact/send", s.enviarCodigo(propositoContacto))
	conSesion("POST /api/v1/me/contact/verify", s.confirmarCambioDeContacto)
	conSesion("POST /api/v1/me/password", s.cambiarClave)
	conSesion("GET /api/v1/me/devices", s.dispositivos)
	conSesion("DELETE /api/v1/me/devices/{id}", s.revocarDispositivo)
	conSesion("DELETE /api/v1/me/sessions/{id}", s.cerrarSesion)
	conSesion("POST /api/v1/me/passkeys/options", s.opcionesDeRegistro)
	conSesion("POST /api/v1/me/passkeys", s.registrarPasskey)

	// Código para confirmar un pago, si la cooperativa lo exige (FR-009).
	m.Manejar("POST /api/v1/payments/otp", s.Acotada(http.HandlerFunc(s.iniciarCodigoDePago)))
	m.Manejar("POST /api/v1/payments/otp/send", s.Acotada(s.enviarCodigo(propositoPago)))
	m.Manejar("POST /api/v1/payments/otp/verify", s.Acotada(http.HandlerFunc(s.verificarCodigoDePago)))
}
