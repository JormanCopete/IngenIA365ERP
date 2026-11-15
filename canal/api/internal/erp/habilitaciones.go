package erp

import (
	"context"
	"fmt"
	"net/http"
	"time"

	"github.com/google/uuid"
)

// Estados de una habilitación tal como los escribe el ERP (por nombre).
const (
	HabilitacionHabilitada   = "Enabled"
	HabilitacionInhabilitada = "Disabled"
)

// CambioDeHabilitacion es una fila de GET /tenants/{t}/enablements (contracts/erp-api-de-servicio.md).
type CambioDeHabilitacion struct {
	PersonPublicID uuid.UUID `json:"personPublicId"`
	DocumentType   string    `json:"documentType"`
	DocumentNumber string    `json:"documentNumber"`
	Status         string    `json:"status"`
	EnabledAt      time.Time `json:"enabledAt"`
	// ChangeSequence del ERP: ticks UTC (unidades de 100 ns).
	Sequence int64 `json:"sequence"`
}

// PaginaDeHabilitaciones es la respuesta de GET /tenants/{t}/enablements.
type PaginaDeHabilitaciones struct {
	Items   []CambioDeHabilitacion `json:"items"`
	Next    int64                  `json:"next"`
	HasMore bool                   `json:"hasMore"`
}

// ConciliacionDeHabilitaciones es la respuesta de POST /tenants/{t}/enablements/reconcile.
type ConciliacionDeHabilitaciones struct {
	Enabled  int `json:"enabled"`
	Disabled int `json:"disabled"`
}

// ListarCambiosDeHabilitacion trae los cambios de habilitación de la cooperativa posteriores a desde (R8).
func (c *Cliente) ListarCambiosDeHabilitacion(ctx context.Context, cooperativa uuid.UUID, desde int64, limite int) (PaginaDeHabilitaciones, error) {
	var pagina PaginaDeHabilitaciones
	ruta := fmt.Sprintf("/tenants/%s/enablements?since=%d&limit=%d", cooperativa, desde, limite)
	if err := c.Hacer(ctx, http.MethodGet, cooperativa.String(), ruta, nil, &pagina); err != nil {
		return PaginaDeHabilitaciones{}, err
	}
	return pagina, nil
}

// ConciliarHabilitaciones le pide al ERP que materialice la política automática de habilitación de la cooperativa
// (habilita a los asociados activos sin habilitación e inhabilita las de política de los retirados). Es idempotente.
func (c *Cliente) ConciliarHabilitaciones(ctx context.Context, cooperativa uuid.UUID) (ConciliacionDeHabilitaciones, error) {
	var resultado ConciliacionDeHabilitaciones
	ruta := fmt.Sprintf("/tenants/%s/enablements/reconcile", cooperativa)
	if err := c.Hacer(ctx, http.MethodPost, cooperativa.String(), ruta, struct{}{}, &resultado); err != nil {
		return ConciliacionDeHabilitaciones{}, err
	}
	return resultado, nil
}
