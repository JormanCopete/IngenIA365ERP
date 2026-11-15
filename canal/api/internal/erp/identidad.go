package erp

import (
	"context"
	"fmt"
	"net/http"
	"time"

	"github.com/google/uuid"
)

// ContactoDeActivacion es la respuesta de GET /tenants/{t}/people/{p}/activation-contact (research R9). Las máscaras
// pueden ir a la PWA; Entrega no sale nunca del servicio.
type ContactoDeActivacion struct {
	Nombres   string                   `json:"firstNames"`
	Apellidos string                   `json:"lastNames"`
	Correo    ContactoEnmascarado      `json:"email"`
	Celular   ContactoEnmascarado      `json:"mobile"`
	Permisos  ConsentimientoDeContacto `json:"contactConsent"`
	Entrega   EntregaDeContacto        `json:"delivery"`
}

// ContactoEnmascarado: la máscara y si hay un dato válido.
type ContactoEnmascarado struct {
	Mascara    *string `json:"masked"`
	Disponible bool    `json:"available"`
}

// ConsentimientoDeContacto: la última decisión del titular por canal (Habeas Data); nil = nunca decidió.
type ConsentimientoDeContacto struct {
	Correo *bool `json:"email"`
	SMS    *bool `json:"sms"`
}

// EntregaDeContacto: los valores completos, sólo para enviar el código.
type EntregaDeContacto struct {
	Correo  *string `json:"email"`
	Celular *string `json:"mobile"`
}

// TextoLegal es la versión vigente de un texto de la cooperativa (GET /tenants/{t}/legal-texts).
type TextoLegal struct {
	Tipo      string    `json:"kind"`
	Version   int       `json:"version"`
	Contenido string    `json:"content"`
	Publicado time.Time `json:"publishedAt"`
}

// NovedadDeContacto es el cuerpo de POST /tenants/{t}/people/{p}/contact-updates (FR-024).
type NovedadDeContacto struct {
	Campo      string    `json:"field"` // Email | Mobile
	ValorNuevo string    `json:"newValue"`
	Reportada  time.Time `json:"reportedAt"`
}

// ContactoDeActivacion pide el contacto de la persona en su entidad validadora.
func (c *Cliente) ContactoDeActivacion(ctx context.Context, cooperativa, persona uuid.UUID) (ContactoDeActivacion, error) {
	var r ContactoDeActivacion
	ruta := fmt.Sprintf("/tenants/%s/people/%s/activation-contact", cooperativa, persona)
	err := c.Hacer(ctx, http.MethodGet, cooperativa.String(), ruta, nil, &r)
	return r, err
}

// TextosLegales pide la versión vigente de cada texto legal de la cooperativa.
func (c *Cliente) TextosLegales(ctx context.Context, cooperativa uuid.UUID) ([]TextoLegal, error) {
	var r []TextoLegal
	err := c.Hacer(ctx, http.MethodGet, cooperativa.String(), fmt.Sprintf("/tenants/%s/legal-texts", cooperativa), nil, &r)
	return r, err
}

// ReportarNovedadDeContacto avisa a la cooperativa que el asociado cambió y verificó un dato de contacto. Idempotente por
// persona, campo y fecha del reporte.
func (c *Cliente) ReportarNovedadDeContacto(ctx context.Context, cooperativa, persona uuid.UUID, n NovedadDeContacto) error {
	ruta := fmt.Sprintf("/tenants/%s/people/%s/contact-updates", cooperativa, persona)
	return c.Hacer(ctx, http.MethodPost, cooperativa.String(), ruta, n, nil)
}
