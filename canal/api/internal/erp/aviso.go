package erp

import (
	"context"
	"fmt"
	"net/http"
	"time"

	"github.com/google/uuid"
)

// AvisoDePago es el cuerpo de POST /tenants/{t}/people/{p}/payment-notices (contracts/erp-api-de-servicio.md, FR-044). Los
// valores van en pesos enteros; nunca hay datos de tarjeta (FR-042).
type AvisoDePago struct {
	Referencia string             `json:"reference"`
	Destino    uuid.UUID          `json:"destinationPublicId"`
	Total      int64              `json:"total"`
	Moneda     string             `json:"currency"`
	Aprobado   time.Time          `json:"approvedAt"`
	Pasarela   AprobacionDelAviso `json:"gateway"`
	Items      []ItemDelAviso     `json:"items"`
	Pagador    PagadorDelAviso    `json:"payer"`
}

// AprobacionDelAviso es el intento aprobado en la pasarela.
type AprobacionDelAviso struct {
	RequestID         int64  `json:"requestId"`
	ReferenciaInterna *int64 `json:"internalReference"`
	Autorizacion      string `json:"authorization"`
	Recibo            string `json:"receipt"`
	Franquicia        string `json:"franchise"`
	Medio             string `json:"paymentMethodName"`
	Emisor            string `json:"issuerName"`
}

// ItemDelAviso es un concepto con la modalidad del canal.
type ItemDelAviso struct {
	Clave     string  `json:"conceptKey"`
	Modalidad string  `json:"modality"`
	Valor     int64   `json:"amount"`
	Eleccion  *string `json:"prepaymentChoice"`
}

// PagadorDelAviso es desde dónde pagó el asociado (auditoría del ERP).
type PagadorDelAviso struct {
	IP     *string   `json:"ip"`
	Agente *string   `json:"userAgent"`
	Cuenta uuid.UUID `json:"accountId"`
}

// ResultadoDelAviso es lo que responde el ERP: Applied o InReview, con el recibo si lo hubo.
type ResultadoDelAviso struct {
	Resultado    string     `json:"outcome"`
	Recibo       *uuid.UUID `json:"receiptPublicId"`
	NumeroRecibo *string    `json:"receiptNumber"`
	Motivo       *string    `json:"reason"`
}

// AvisarPago avisa al ERP un pago aprobado. Es idempotente por referencia: repetirlo devuelve el mismo resultado.
func (c *Cliente) AvisarPago(ctx context.Context, cooperativa, persona uuid.UUID, a AvisoDePago) (ResultadoDelAviso, error) {
	var r ResultadoDelAviso
	ruta := fmt.Sprintf("/tenants/%s/people/%s/payment-notices", cooperativa, persona)
	err := c.Hacer(ctx, http.MethodPost, cooperativa.String(), ruta, a, &r)
	return r, err
}

// Comprobante descarga el PDF del recibo de un pago del canal de la persona (FR-048).
func (c *Cliente) Comprobante(ctx context.Context, cooperativa, persona, recibo uuid.UUID) ([]byte, string, error) {
	ruta := fmt.Sprintf("/tenants/%s/people/%s/receipts/%s/document", cooperativa, persona, recibo)
	return c.Descargar(ctx, cooperativa.String(), ruta)
}
