package erp

import (
	"context"
	"fmt"
	"net/http"

	"github.com/google/uuid"
)

// Documento es un extracto o certificado que la cooperativa publica para la persona (ChannelDocumentDto del ERP, FR-055).
// Parametro: "period" (desde y hasta), "year" (un año terminado) o "none".
type Documento struct {
	Tipo      string     `json:"kind"`
	Producto  *uuid.UUID `json:"productPublicId"`
	Numero    *string    `json:"productNumber"`
	Nombre    string     `json:"description"`
	Parametro string     `json:"parameter"`
}

// PedidoDeDocumento es el cuerpo de POST /tenants/{t}/people/{p}/documents.
type PedidoDeDocumento struct {
	Tipo     string     `json:"kind"`
	Producto *uuid.UUID `json:"productPublicId,omitempty"`
	Año      *int       `json:"year,omitempty"`
	Desde    *string    `json:"from,omitempty"`
	Hasta    *string    `json:"to,omitempty"`
}

// Documentos lista los documentos publicados de la persona.
func (c *Cliente) Documentos(ctx context.Context, cooperativa, persona uuid.UUID) ([]Documento, error) {
	var d []Documento
	err := c.Hacer(ctx, http.MethodGet, cooperativa.String(), fmt.Sprintf("/tenants/%s/people/%s/documents", cooperativa, persona), nil, &d)
	return d, err
}

// EmitirDocumento pide al ERP el PDF del documento, generado en el momento.
func (c *Cliente) EmitirDocumento(ctx context.Context, cooperativa, persona uuid.UUID, p PedidoDeDocumento) ([]byte, string, error) {
	return c.ejecutar(ctx, http.MethodPost, cooperativa.String(), fmt.Sprintf("/tenants/%s/people/%s/documents", cooperativa, persona), p, "*/*")
}
