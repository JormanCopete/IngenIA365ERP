package identidad

import (
	"context"
	"encoding/json"
	"errors"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
)

// Validadora es la entidad que valida a la persona al activar (D5, FR-017): la primera que la habilitó y la mantiene
// habilitada, con el canal encendido. Se calcula en cada consulta, no se guarda: si la inhabilita, la siguiente pasa a
// serlo; si la vuelve a habilitar, entra con la fecha nueva.
type Validadora struct {
	CooperativaID       uuid.UUID
	CooperativaPublicID uuid.UUID
	PersonaPublicID     uuid.UUID
	Nombre              string
	Soporte             json.RawMessage
	SMS                 bool
}

// BuscarValidadora devuelve la entidad validadora del documento, o nil si ninguna lo tiene habilitado.
func BuscarValidadora(ctx context.Context, q *gen.Queries, doc Documento) (*Validadora, error) {
	f, err := q.ValidatingEnablement(ctx, gen.ValidatingEnablementParams{DocumentType: doc.Tipo, DocumentNumber: doc.Numero})
	if errors.Is(err, pgx.ErrNoRows) {
		return nil, nil
	}
	if err != nil {
		return nil, err
	}
	return &Validadora{
		CooperativaID: f.TenantID, CooperativaPublicID: f.TenantPublicID, PersonaPublicID: f.PersonPublicID,
		Nombre: f.DisplayName, Soporte: f.Support, SMS: f.SmsEnabled,
	}, nil
}
