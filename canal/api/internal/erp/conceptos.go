package erp

import (
	"context"
	"encoding/json"
	"fmt"
	"net/http"

	"github.com/google/uuid"
)

// Los valores en pesos viajan como json.Number: el canal los muestra tal cual los calculó el ERP, sin redondeos de
// coma flotante.

// Conceptos es la respuesta de GET /tenants/{t}/people/{p}/payables (research R5): los conceptos publicados de la persona
// con los valores del ERP a la fecha. El canal no guarda nada de esto (FR-030).
type Conceptos struct {
	Fecha    string     `json:"asOf"`
	Items    []Concepto `json:"items"`
	Avisos   []string   `json:"notices"`
	Destinos []Destino  `json:"destinations"`
}

// Destino es un destino de pago activo con su sitio de pasarela (R10): el pago se crea contra ese sitio.
type Destino struct {
	ID            uuid.UUID `json:"destinationPublicId"`
	Sitio         uuid.UUID `json:"sitePublicId"`
	Nombre        string    `json:"name"`
	OfrecePSE     bool      `json:"offerPse"`
	OfreceTarjeta bool      `json:"offerCard"`
}

// Concepto es un crédito, un aporte o un ahorro publicado.
type Concepto struct {
	Clave             string         `json:"conceptKey"`
	Tipo              string         `json:"kind"`
	Linea             string         `json:"lineCode"`
	Descripcion       string         `json:"description"`
	Producto          uuid.UUID      `json:"productPublicId"`
	NumeroDelProducto string         `json:"productNumber"`
	Saldo             json.Number    `json:"balance"`
	Vencido           json.Number    `json:"pastDue"`
	InteresDeMora     json.Number    `json:"defaultInterest"`
	Cuota             json.Number    `json:"cycleInstallment"`
	ExigibleHoy       json.Number    `json:"dueToday"`
	ParaCancelar      *json.Number   `json:"payoffAmount"`
	ProximaFecha      *string        `json:"nextDueDate"`
	DiasDeMora        int            `json:"daysPastDue"`
	Destino           *uuid.UUID     `json:"destinationPublicId"`
	Pagable           bool           `json:"payable"`
	Modos             ModosDePago    `json:"modes"`
	Combinable        bool           `json:"canCombine"`
	CuotaDeCuenta     *CuotaDeCuenta `json:"installment"`
	MotivosDeBloqueo  []string       `json:"blockReasons"`
}

// ModosDePago son las modalidades con que se puede pagar el concepto.
type ModosDePago struct {
	Exigible     bool           `json:"due"`
	Parcial      *PagoParcial   `json:"partial"`
	AbonoCapital *AbonoACapital `json:"prepayment"`
	Cancelacion  bool           `json:"payoff"`
}

// PagoParcial lleva el mínimo de un pago parcial.
type PagoParcial struct {
	Minimo json.Number `json:"minimum"`
}

// AbonoACapital lleva la opción preseleccionada: ReduceTerm o ReduceInstallment.
type AbonoACapital struct {
	Preseleccion string `json:"defaultChoice"`
}

// CuotaDeCuenta es la cuota pendiente más antigua de un aporte o un ahorro.
type CuotaDeCuenta struct {
	Periodo   string      `json:"period"`
	Vence     string      `json:"dueDate"`
	Valor     json.Number `json:"amount"`
	Pendiente json.Number `json:"pending"`
}

// PedidoDeSimulacion es el cuerpo de POST /tenants/{t}/people/{p}/payables/simulate.
type PedidoDeSimulacion struct {
	Clave     string  `json:"conceptKey"`
	Modalidad string  `json:"modality"`
	Valor     int64   `json:"amount"` // pesos enteros
	Eleccion  *string `json:"prepaymentChoice,omitempty"`
}

// Simulacion es lo que pasaría con el pago (research R7). No escribe nada en el ERP.
type Simulacion struct {
	Clave         string                `json:"conceptKey"`
	Modalidad     string                `json:"modality"`
	Valor         json.Number           `json:"amount"`
	Admitida      bool                  `json:"allowed"`
	Sobrante      json.Number           `json:"surplus"`
	Distribucion  []ParteSimulada       `json:"distribution"`
	SaldosDespues SaldosSimulados       `json:"balancesAfter"`
	PlanNuevo     *PlanSimulado         `json:"newPlan"`
	Eleccion      *string               `json:"prepaymentChoice"`
	Alternativas  []AlternativaSimulada `json:"alternatives"`
	Avisos        []string              `json:"notices"`
}

// ParteSimulada es una parte de la distribución del pago.
type ParteSimulada struct {
	Concepto string      `json:"concept"`
	Cuota    *int        `json:"installmentNumber"`
	Periodo  *string     `json:"period"`
	Valor    json.Number `json:"amount"`
}

// SaldosSimulados: en créditos capital, vencido y lo que cancela; en cuentas, el saldo.
type SaldosSimulados struct {
	Capital      *json.Number `json:"principal"`
	Vencido      *json.Number `json:"pastDue"`
	ParaCancelar *json.Number `json:"payoffAmount"`
	Saldo        *json.Number `json:"balance"`
}

// PlanSimulado es el plan después del pago.
type PlanSimulado struct {
	Cuota           json.Number `json:"installment"`
	CuotasRestantes int         `json:"remainingInstallments"`
	ProximaFecha    *string     `json:"nextDueDate"`
}

// AlternativaSimulada es reducir plazo o reducir cuota con su plan.
type AlternativaSimulada struct {
	Eleccion  string        `json:"choice"`
	PlanNuevo *PlanSimulado `json:"newPlan"`
	Sobrante  json.Number   `json:"surplus"`
}

// Conceptos pide al ERP los conceptos publicados de la persona a hoy.
func (c *Cliente) Conceptos(ctx context.Context, cooperativa, persona uuid.UUID) (Conceptos, error) {
	var r Conceptos
	ruta := fmt.Sprintf("/tenants/%s/people/%s/payables", cooperativa, persona)
	err := c.Hacer(ctx, http.MethodGet, cooperativa.String(), ruta, nil, &r)
	return r, err
}

// Simular pide al ERP la simulación de un pago sobre un concepto.
func (c *Cliente) Simular(ctx context.Context, cooperativa, persona uuid.UUID, p PedidoDeSimulacion) (Simulacion, error) {
	var r Simulacion
	ruta := fmt.Sprintf("/tenants/%s/people/%s/payables/simulate", cooperativa, persona)
	err := c.Hacer(ctx, http.MethodPost, cooperativa.String(), ruta, p, &r)
	return r, err
}
