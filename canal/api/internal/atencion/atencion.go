// Package atencion atiende al ERP cuando un funcionario de la cooperativa consulta y supervisa el canal (feature 021,
// US12; FR-058 a FR-060; contracts/canal-api.md §C): la cuenta del canal de una persona, desbloquearla, cerrar sus
// sesiones y los indicadores. Toda respuesta va acotada a la cooperativa: una cuenta sólo existe para ella si el
// documento está (o estuvo) habilitado allí; si no, la respuesta es la misma que si no existiera (FR-059).
package atencion

import (
	"context"
	"errors"
	"net/http"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/auditoria"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/identidad"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// Acciones de auditoría.
const (
	AccionCuentaLeida       = "Canal.Atencion.CuentaLeida"
	AccionCuentaDesbloqueda = "Canal.Atencion.CuentaDesbloqueada"
	AccionSesionesCerradas  = "Canal.Atencion.SesionesCerradas"
	AccionIndicadoresLeidos = "Canal.Atencion.IndicadoresLeidos"
	// RangoMaximo de los indicadores.
	RangoMaximo = 366 * 24 * time.Hour
)

// Errores.
var (
	ErrCuentaNoExiste = sobre.Nuevo(http.StatusNotFound, "Canal.Atencion.CuentaNoExiste",
		"No hay una cuenta del canal de una persona habilitada en esta cooperativa con ese identificador.")
	ErrRangoInvalido = sobre.Nuevo(http.StatusBadRequest, "Canal.Atencion.RangoInvalido",
		"El rango de fechas no es válido (desde ≤ hasta, máximo un año).")
)

// Dispositivo es un dispositivo de confianza de la cuenta (DispositivoDelCanalDto del ERP).
type Dispositivo struct {
	ID         uuid.UUID `json:"deviceId"`
	Tipo       string    `json:"kind"`
	Etiqueta   string    `json:"label"`
	UltimaVez  time.Time `json:"lastSeenAt"`
	Revocado   bool      `json:"revoked"`
	Biometrico bool      `json:"biometricEnabled"`
}

// Cuenta es la cuenta del canal vista por la cooperativa (CuentaDelCanalDto del ERP).
type Cuenta struct {
	ID                 uuid.UUID     `json:"accountId"`
	Estado             string        `json:"status"`
	Activada           time.Time     `json:"activatedAt"`
	UltimoIngreso      *time.Time    `json:"lastLoginAt"`
	BloqueadaHasta     *time.Time    `json:"lockedUntil"`
	Correo             *string       `json:"emailMasked"`
	Celular            *string       `json:"mobileMasked"`
	Dispositivos       []Dispositivo `json:"devices"`
	SesionesAbiertas   int64         `json:"openSessions"`
	EstadoHabilitacion string        `json:"enablementStatus"`
}

// Metricas son los indicadores del canal en la cooperativa (MetricasDelCanalDto del ERP). Los valores van en pesos.
type Metricas struct {
	Habilitadas        int64            `json:"habilitadas"`
	CuentasActivas     int64            `json:"cuentasActivas"`
	Ingresos           int64            `json:"ingresos"`
	IntentosAprobados  int64            `json:"intentosAprobados"`
	IntentosRechazados int64            `json:"intentosRechazados"`
	IntentosPendientes int64            `json:"intentosPendientes"`
	IntentosReversados int64            `json:"intentosReversados"`
	ValorAprobado      int64            `json:"valorAprobado"`
	RechazosPorMotivo  map[string]int64 `json:"rechazosPorMotivo"`
}

// Servicio de atención.
type Servicio struct {
	q         *gen.Queries
	auditoria *auditoria.Registro
	ahora     func() time.Time
}

// Nuevo crea el servicio.
func Nuevo(q *gen.Queries, a *auditoria.Registro, ahora func() time.Time) *Servicio {
	if ahora == nil {
		ahora = time.Now
	}
	return &Servicio{q: q, auditoria: a, ahora: ahora}
}

// BuscarCuenta devuelve la cuenta del documento si la persona está (o estuvo) habilitada en la cooperativa; nil si no.
func (s *Servicio) BuscarCuenta(ctx context.Context, cooperativa uuid.UUID, tipo, numero string) (*Cuenta, error) {
	doc, ok := identidad.NormalizarDocumento(tipo, numero)
	if !ok {
		return nil, nil
	}
	hab, err := s.q.EnablementOfTenantForDocument(ctx, gen.EnablementOfTenantForDocumentParams{TenantID: cooperativa, DocumentType: doc.Tipo,
		DocumentNumber: doc.Numero})
	if errors.Is(err, pgx.ErrNoRows) {
		return nil, nil
	}
	if err != nil {
		return nil, err
	}
	a, err := s.q.GetAccountByDocument(ctx, gen.GetAccountByDocumentParams{DocumentType: doc.Tipo, DocumentNumber: doc.Numero})
	if errors.Is(err, pgx.ErrNoRows) {
		return nil, nil
	}
	if err != nil {
		return nil, err
	}
	c, err := s.cuenta(ctx, a, doc, hab.Status)
	if err != nil {
		return nil, err
	}
	s.auditar(ctx, AccionCuentaLeida, a.ID.String(), map[string]any{"byDocument": true})
	return c, nil
}

func (s *Servicio) cuenta(ctx context.Context, a gen.Account, doc identidad.Documento, habilitacion string) (*Cuenta, error) {
	ahora := s.ahora()
	c := &Cuenta{ID: a.ID, Estado: a.Status, Activada: a.ActivatedAt, Dispositivos: []Dispositivo{}, EstadoHabilitacion: habilitacion}
	if ultimo, err := s.q.LastLoginOfAccount(ctx, a.ID); err == nil {
		c.UltimoIngreso = &ultimo
	} else if !errors.Is(err, pgx.ErrNoRows) {
		return nil, err
	}
	if freno, err := s.q.GetThrottle(ctx, doc.Llave()); err == nil && freno.LockedUntil != nil && ahora.Before(*freno.LockedUntil) {
		c.BloqueadaHasta = freno.LockedUntil
	} else if err != nil && !errors.Is(err, pgx.ErrNoRows) {
		return nil, err
	}
	if a.Email != nil {
		c.Correo = ptr(identidad.EnmascararCorreo(*a.Email))
	}
	if a.Mobile != nil {
		c.Celular = ptr(identidad.EnmascararCelular(*a.Mobile))
	}
	ds, err := s.q.ListDevices(ctx, a.ID)
	if err != nil {
		return nil, err
	}
	for _, d := range ds {
		c.Dispositivos = append(c.Dispositivos, Dispositivo{ID: d.ID, Tipo: d.Kind, Etiqueta: d.Label, UltimaVez: d.LastSeenAt,
			Revocado: d.RevokedAt != nil, Biometrico: d.BiometricEnabled})
	}
	if c.SesionesAbiertas, err = s.q.CountOpenSessionsOfAccount(ctx, gen.CountOpenSessionsOfAccountParams{AccountID: a.ID, At: ahora}); err != nil {
		return nil, err
	}
	return c, nil
}

// cuentaDeLaCooperativa lee la cuenta y comprueba que su documento esté (o haya estado) habilitado en la cooperativa.
func (s *Servicio) cuentaDeLaCooperativa(ctx context.Context, cooperativa, id uuid.UUID) (gen.Account, identidad.Documento, error) {
	a, err := s.q.GetAccount(ctx, id)
	if errors.Is(err, pgx.ErrNoRows) {
		return a, identidad.Documento{}, ErrCuentaNoExiste
	}
	if err != nil {
		return a, identidad.Documento{}, err
	}
	doc := identidad.Documento{Tipo: a.DocumentType, Numero: a.DocumentNumber}
	if _, err := s.q.EnablementOfTenantForDocument(ctx, gen.EnablementOfTenantForDocumentParams{TenantID: cooperativa, DocumentType: doc.Tipo,
		DocumentNumber: doc.Numero}); errors.Is(err, pgx.ErrNoRows) {
		return a, doc, ErrCuentaNoExiste
	} else if err != nil {
		return a, doc, err
	}
	return a, doc, nil
}

// Desbloquear quita el freno de ingresos del documento (FR-022): la persona puede volver a intentar de inmediato.
func (s *Servicio) Desbloquear(ctx context.Context, cooperativa, id uuid.UUID) error {
	a, doc, err := s.cuentaDeLaCooperativa(ctx, cooperativa, id)
	if err != nil {
		return err
	}
	if err := s.q.ClearThrottle(ctx, doc.Llave()); err != nil {
		return err
	}
	s.auditar(ctx, AccionCuentaDesbloqueda, a.ID.String(), nil)
	return nil
}

// CerrarSesiones termina todas las sesiones abiertas de la cuenta (en cualquier dispositivo). Devuelve cuántas cerró.
func (s *Servicio) CerrarSesiones(ctx context.Context, cooperativa, id uuid.UUID) (int64, error) {
	a, _, err := s.cuentaDeLaCooperativa(ctx, cooperativa, id)
	if err != nil {
		return 0, err
	}
	n, err := s.q.EndAllAccountSessions(ctx, gen.EndAllAccountSessionsParams{AccountID: a.ID, At: s.ahora()})
	if err != nil {
		return 0, err
	}
	s.auditar(ctx, AccionSesionesCerradas, a.ID.String(), map[string]any{"closed": n})
	return n, nil
}

// Indicadores entre desde (incluido) y hasta (excluido).
func (s *Servicio) Indicadores(ctx context.Context, cooperativa uuid.UUID, desde, hasta time.Time) (Metricas, error) {
	if !desde.Before(hasta) || hasta.Sub(desde) > RangoMaximo {
		return Metricas{}, ErrRangoInvalido
	}
	m := Metricas{RechazosPorMotivo: map[string]int64{}}
	var err error
	if m.Habilitadas, err = s.q.CountEnabledOfTenant(ctx, cooperativa); err != nil {
		return m, err
	}
	if m.CuentasActivas, err = s.q.CountActiveAccountsOfTenant(ctx, cooperativa); err != nil {
		return m, err
	}
	if m.Ingresos, err = s.q.CountLoginsOfTenant(ctx, gen.CountLoginsOfTenantParams{TenantID: &cooperativa, Desde: desde, Hasta: hasta}); err != nil {
		return m, err
	}
	filas, err := s.q.AttemptStatsOfTenant(ctx, gen.AttemptStatsOfTenantParams{TenantID: cooperativa, Desde: desde, Hasta: hasta})
	if err != nil {
		return m, err
	}
	for _, f := range filas {
		switch {
		case f.Status == "approved" && f.Reversed:
			m.IntentosReversados += f.Cantidad
		case f.Status == "approved":
			m.IntentosAprobados += f.Cantidad
			m.ValorAprobado += f.Valor
		case f.Status == "rejected" || f.Status == "failed":
			m.IntentosRechazados += f.Cantidad
		case f.Status == "pending" || f.Status == "created":
			m.IntentosPendientes += f.Cantidad
		}
	}
	motivos, err := s.q.RejectionReasonsOfTenant(ctx, gen.RejectionReasonsOfTenantParams{TenantID: cooperativa, Desde: desde, Hasta: hasta})
	if err != nil {
		return m, err
	}
	for _, r := range motivos {
		m.RechazosPorMotivo[r.Motivo] = r.Cantidad
	}
	s.auditar(ctx, AccionIndicadoresLeidos, cooperativa.String(), map[string]any{"from": desde, "to": hasta})
	return m, nil
}

func (s *Servicio) auditar(ctx context.Context, accion, sujeto string, datos any) {
	if s.auditoria == nil {
		return
	}
	_ = s.auditoria.Registrar(ctx, accion, sujeto, datos)
}

func ptr[T any](v T) *T { return &v }
