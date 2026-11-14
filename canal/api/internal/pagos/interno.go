package pagos

import (
	"context"
	"encoding/json"
	"errors"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/erp"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/pasarela"
)

// T112, T120 (parte; contracts/canal-api.md §C): los intentos de una cooperativa vistos desde el ERP —la atención (FR-058) y la conciliación del
// archivo de la pasarela, que aplica un aprobado que nunca se avisó con los conceptos que eligió el asociado—. Nunca devuelve intentos de otra
// cooperativa (FR-059).

// IntentoInterno es un intento como lo lee el ERP (IntentoDelCanalDto).
type IntentoInterno struct {
	Referencia   string                  `json:"reference"`
	Persona      uuid.UUID               `json:"personPublicId"`
	Estado       string                  `json:"status"`
	Total        int64                   `json:"total"`
	Creado       time.Time               `json:"createdAt"`
	Terminado    *time.Time              `json:"finishedAt"`
	Autorizacion *string                 `json:"authorization"`
	Medio        *string                 `json:"paymentMethodName"`
	ResultadoERP string                  `json:"erpOutcome"`
	ReciboDelERP *uuid.UUID              `json:"erpReceiptPublicId"`
	Destino      uuid.UUID               `json:"destinationPublicId"`
	Items        []erp.ItemDelAviso      `json:"items"`
	Pasarela     *erp.AprobacionDelAviso `json:"gateway"`
	Aprobado     *time.Time              `json:"approvedAt"`
	Cuenta       uuid.UUID               `json:"accountId"`
	Reversado    *time.Time              `json:"reversedAt"`
	TipoReverso  *string                 `json:"reversalKind"`
}

// MaximoDeIntentosInternos por consulta por documento.
const MaximoDeIntentosInternos = 50

// BuscarIntentos busca por referencia o, si no viene, por documento, sólo en la cooperativa (Id del canal).
func (s *Servicio) BuscarIntentos(ctx context.Context, cooperativa uuid.UUID, tipoDocumento, documento, referencia string) ([]IntentoInterno, error) {
	var filas []gen.PaymentAttempt
	switch {
	case referencia != "":
		a, err := s.q.GetPaymentAttemptByReference(ctx, referencia)
		if errors.Is(err, pgx.ErrNoRows) {
			return []IntentoInterno{}, nil
		}
		if err != nil {
			return nil, err
		}
		filas = append(filas, a)
	case tipoDocumento != "" && documento != "":
		cuenta, err := s.q.GetAccountByDocument(ctx, gen.GetAccountByDocumentParams{DocumentType: tipoDocumento, DocumentNumber: documento})
		if errors.Is(err, pgx.ErrNoRows) {
			return []IntentoInterno{}, nil
		}
		if err != nil {
			return nil, err
		}
		if filas, err = s.q.ListAccountAttempts(ctx, gen.ListAccountAttemptsParams{AccountID: cuenta.ID, TenantID: cooperativa,
			MaxRows: MaximoDeIntentosInternos}); err != nil {
			return nil, err
		}
	default:
		return []IntentoInterno{}, nil
	}
	salida := make([]IntentoInterno, 0, len(filas))
	for _, a := range filas {
		if a.TenantID != cooperativa {
			continue
		}
		salida = append(salida, intentoInterno(a))
	}
	return salida, nil
}

func intentoInterno(a gen.PaymentAttempt) IntentoInterno {
	i := IntentoInterno{
		Referencia: a.Reference, Persona: a.PersonPublicID, Estado: a.Status, Total: a.Total, Creado: a.CreatedAt, Terminado: a.FinalAt,
		ResultadoERP: a.ErpOutcome, ReciboDelERP: a.ErpReceiptPublicID, Destino: a.DestinationPublicID, Cuenta: a.AccountID, Items: []erp.ItemDelAviso{},
		Reversado: a.ReversedAt, TipoReverso: a.ReversalKind,
	}
	var items []Item
	if json.Unmarshal(a.Items, &items) == nil {
		for _, it := range items {
			i.Items = append(i.Items, erp.ItemDelAviso{Clave: it.Clave, Modalidad: it.Modalidad, Valor: it.Valor, Eleccion: it.Eleccion})
		}
	}
	if aviso, err := AvisoDelIntento(a); err == nil && a.Status == "approved" {
		i.Pasarela = &aviso.Pasarela
		i.Aprobado = &aviso.Aprobado
		i.Autorizacion = &aviso.Pasarela.Autorizacion
		i.Medio = &aviso.Pasarela.Medio
	} else if len(a.Approval) > 0 {
		var ap pasarela.Aprobacion
		if json.Unmarshal(a.Approval, &ap) == nil {
			i.Autorizacion, i.Medio = &ap.Autorizacion, &ap.Medio
		}
	}
	return i
}
