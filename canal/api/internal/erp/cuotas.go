package erp

import (
	"context"
	"fmt"
	"net/http"

	"github.com/google/uuid"
)

// CuotaProxima es una cuota que vence dentro de N días para una persona habilitada (DueInstallmentDto del ERP, FR-012).
type CuotaProxima struct {
	Persona     uuid.UUID `json:"personPublicId"`
	Clave       string    `json:"conceptKey"`
	Descripcion string    `json:"description"`
	Vence       string    `json:"dueDate"`
	Valor       float64   `json:"amount"`
}

// CuotasProximas pide al ERP las cuotas que vencen exactamente dentro de dias días.
func (c *Cliente) CuotasProximas(ctx context.Context, cooperativa uuid.UUID, dias int) ([]CuotaProxima, error) {
	var r []CuotaProxima
	err := c.Hacer(ctx, http.MethodGet, cooperativa.String(), fmt.Sprintf("/tenants/%s/due-installments?inDays=%d", cooperativa, dias), nil, &r)
	return r, err
}
