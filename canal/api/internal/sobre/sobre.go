// Package sobre: el sobre de error del canal, {code, message, data}, igual en forma al del ERP.
//
// Los códigos llevan espacio de nombres («Canal.Servicio.TokenInvalido») y los mensajes van en español
// (constitución, «Sistemas satélite», regla 6).
package sobre

import (
	"encoding/json"
	"log/slog"
	"net/http"
)

// Error es un error que se le puede mostrar a quien llamó: estado HTTP, código y mensaje.
type Error struct {
	Estado  int    `json:"-"`
	Codigo  string `json:"code"`
	Mensaje string `json:"message"`
	Datos   any    `json:"data,omitempty"`
}

func (e *Error) Error() string { return e.Codigo + ": " + e.Mensaje }

// Nuevo arma un error del sobre.
func Nuevo(estado int, codigo, mensaje string) *Error {
	return &Error{Estado: estado, Codigo: codigo, Mensaje: mensaje}
}

// ConDatos devuelve una copia con datos adicionales.
func (e *Error) ConDatos(datos any) *Error {
	copia := *e
	copia.Datos = datos
	return &copia
}

// Errores de uso común en todo el servicio.
var (
	ErrInesperado = Nuevo(http.StatusInternalServerError, "Canal.Error.Inesperado",
		"Ocurrió un error inesperado. Intente de nuevo; si persiste, comuníquese con su cooperativa.")
	ErrRutaInexistente  = Nuevo(http.StatusNotFound, "Canal.Ruta.NoExiste", "Recurso no encontrado.")
	ErrMetodoNoAdmitido = Nuevo(http.StatusMethodNotAllowed, "Canal.Ruta.MetodoNoAdmitido",
		"El método no está admitido en esta ruta.")
	ErrDemasiadasSolicitudes = Nuevo(http.StatusTooManyRequests, "Canal.Solicitudes.Demasiadas",
		"Demasiadas solicitudes. Espere un momento e intente de nuevo.")
)

// Escribir responde el error con su estado y el sobre en JSON.
func Escribir(w http.ResponseWriter, e *Error) {
	JSON(w, e.Estado, e)
}

// JSON responde un valor cualquiera en JSON con el estado indicado.
func JSON(w http.ResponseWriter, estado int, valor any) {
	cuerpo, err := json.Marshal(valor)
	if err != nil {
		// Un valor que no se puede serializar es un defecto del programa: se registra y se responde el genérico.
		slog.Error("no se pudo serializar la respuesta", "error", err)
		estado = http.StatusInternalServerError
		cuerpo, _ = json.Marshal(ErrInesperado)
	}
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.Header().Set("Cache-Control", "no-store")
	w.WriteHeader(estado)
	if _, err := w.Write(cuerpo); err != nil {
		slog.Debug("el cliente cerró la conexión antes de recibir la respuesta", "error", err)
	}
}
