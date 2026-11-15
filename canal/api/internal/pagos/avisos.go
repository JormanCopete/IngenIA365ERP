package pagos

import (
	"context"
	"fmt"

	"github.com/jackc/pgx/v5"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/avisos"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
)

// T133 (FR-056): los avisos de un pago. Rechazado o fallido avisa siempre; un pendiente que la pasarela resolvió por la
// notificación o la consulta (el asociado ya no estaba mirando) avisa que quedó aprobado; el aviso al ERP aplicado o en
// revisión avisa que la cooperativa lo recibió. Se encolan en la misma transacción del cambio, con el intento en la llave.

func (s *Servicio) urlDelHistorial() string {
	return s.o.EsquemaDeRetorno + "://" + s.o.HostPublico + "/cooperativa/pagos"
}

// avisoDeTransicion arma el aviso de un cambio de estado; ok = false si no hay que avisar.
func (s *Servicio) avisoDeTransicion(a gen.PaymentAttempt, nuevo, fuente, mensaje string) (avisos.ArgsEnviarAviso, bool) {
	base := avisos.ArgsEnviarAviso{Cuenta: a.AccountID, URL: s.urlDelHistorial(), Llave: "intento:" + a.ID.String() + ":" + nuevo}
	valor := avisos.Pesos(float64(a.Total))
	switch {
	case nuevo == "rejected" || nuevo == "failed":
		base.Clase, base.Titulo = avisos.PagoRechazado, "Tu pago no fue aprobado"
		base.Cuerpo = fmt.Sprintf("El pago %s por %s no fue aprobado y no se hizo ningún cobro.", a.Reference, valor)
		if mensaje != "" {
			base.Cuerpo += " " + mensaje
		}
		return base, true
	case nuevo == "approved" && (fuente == FuenteWebhook || fuente == FuenteSonda):
		base.Clase, base.Titulo = avisos.PendienteResuelto, "Tu pago quedó aprobado"
		base.Cuerpo = fmt.Sprintf("El pago %s por %s que estaba pendiente quedó aprobado. Tu cooperativa lo registrará en tus productos.", a.Reference, valor)
		return base, true
	}
	return base, false
}

// encolarAvisoDeTransicion encola el aviso de la transición en su transacción.
func (s *Servicio) encolarAvisoDeTransicion(ctx context.Context, tx pgx.Tx, a gen.PaymentAttempt, nuevo, fuente, mensaje string) error {
	args, ok := s.avisoDeTransicion(a, nuevo, fuente, mensaje)
	if !ok {
		return nil
	}
	if _, err := s.o.Encolador.InsertTx(ctx, tx, args, nil); err != nil {
		return fmt.Errorf("pagos: encolar el aviso del pago: %w", err)
	}
	return nil
}

// encolarAvisoAplicado avisa que la cooperativa recibió el pago (aplicado o en revisión).
func (s *Servicio) encolarAvisoAplicado(ctx context.Context, a gen.PaymentAttempt, resultado string) {
	cuerpo := fmt.Sprintf("Tu cooperativa registró el pago %s por %s en tus productos. Ya puedes descargar el comprobante.", a.Reference,
		avisos.Pesos(float64(a.Total)))
	if resultado == AvisoEnRevision {
		cuerpo = fmt.Sprintf("Tu cooperativa recibió el pago %s por %s y lo está revisando. Si tienes dudas, comunícate con ella.", a.Reference,
			avisos.Pesos(float64(a.Total)))
	}
	if _, err := s.o.Encolador.Insert(ctx, avisos.ArgsEnviarAviso{Cuenta: a.AccountID, Clase: avisos.PagoAplicado, Titulo: "Tu pago quedó registrado",
		Cuerpo: cuerpo, URL: s.urlDelHistorial(), Llave: "intento:" + a.ID.String() + ":erp"}, nil); err != nil {
		s.o.Logger.ErrorContext(ctx, "no se pudo encolar el aviso de pago aplicado", "intento", a.ID, "error", err)
	}
}
