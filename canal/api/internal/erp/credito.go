package erp

import (
	"context"
	"encoding/json"
	"fmt"
	"net/http"

	"github.com/google/uuid"
)

// T144 (US13, C6): el crédito en línea pasa por el canal tal como lo responde el ERP (el canal no interpreta la simulación).

// PedidoDeSimulacionDeCredito es el cuerpo de POST /tenants/{t}/people/{p}/credit-simulations.
type PedidoDeSimulacionDeCredito struct {
	Linea  uuid.UUID `json:"creditLinePublicId"`
	Monto  int64     `json:"amount"`
	Cuotas int       `json:"installments"`
}

// PedidoDeSolicitudDeCredito es el cuerpo de POST /tenants/{t}/people/{p}/credit-applications.
type PedidoDeSolicitudDeCredito struct {
	Clave    uuid.UUID `json:"requestKey"`
	Linea    uuid.UUID `json:"creditLinePublicId"`
	Monto    int64     `json:"amount"`
	Cuotas   int       `json:"installments"`
	Destino  *string   `json:"purpose"`
	Ingresos *int64    `json:"monthlyIncome"`
	Gastos   *int64    `json:"monthlyExpenses"`
}

// LineasDeCredito: las líneas publicadas.
func (c *Cliente) LineasDeCredito(ctx context.Context, cooperativa uuid.UUID) (json.RawMessage, error) {
	var r json.RawMessage
	err := c.Hacer(ctx, http.MethodGet, cooperativa.String(), fmt.Sprintf("/tenants/%s/credit-lines/published", cooperativa), nil, &r)
	return r, err
}

// SimularCredito: la simulación del ERP para la persona.
func (c *Cliente) SimularCredito(ctx context.Context, cooperativa, persona uuid.UUID, p PedidoDeSimulacionDeCredito) (json.RawMessage, error) {
	var r json.RawMessage
	err := c.Hacer(ctx, http.MethodPost, cooperativa.String(), fmt.Sprintf("/tenants/%s/people/%s/credit-simulations", cooperativa, persona), p, &r)
	return r, err
}

// SolicitarCredito: la solicitud en borrador con canal de origen.
func (c *Cliente) SolicitarCredito(ctx context.Context, cooperativa, persona uuid.UUID, p PedidoDeSolicitudDeCredito) (json.RawMessage, error) {
	var r json.RawMessage
	err := c.Hacer(ctx, http.MethodPost, cooperativa.String(), fmt.Sprintf("/tenants/%s/people/%s/credit-applications", cooperativa, persona), p, &r)
	return r, err
}

// SolicitudesDeCredito: las solicitudes que la persona envió por el canal.
func (c *Cliente) SolicitudesDeCredito(ctx context.Context, cooperativa, persona uuid.UUID) (json.RawMessage, error) {
	var r json.RawMessage
	err := c.Hacer(ctx, http.MethodGet, cooperativa.String(), fmt.Sprintf("/tenants/%s/people/%s/credit-applications", cooperativa, persona), nil, &r)
	return r, err
}
