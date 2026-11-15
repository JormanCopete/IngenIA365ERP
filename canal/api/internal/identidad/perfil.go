package identidad

import (
	"context"
	"encoding/json"
	"errors"
	"net/http"
	"net/mail"
	"strings"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"
	"github.com/riverqueue/river"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/erp"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/mensajeria"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

var (
	ErrCooperativaNoDisponible = sobre.Nuevo(http.StatusForbidden, "Canal.Cooperativa.NoDisponible",
		"Esta cooperativa ya no está disponible para ti en el canal. Elige otra o comunícate con ella.")
	ErrClaveActualIncorrecta = sobre.Nuevo(http.StatusUnprocessableEntity, "Canal.Clave.ActualIncorrecta", "La clave actual no es correcta.")
	ErrContactoInvalido      = sobre.Nuevo(http.StatusUnprocessableEntity, "Canal.Contacto.Invalido",
		"Revisa el dato: el correo debe tener forma de correo y el celular, diez dígitos que empiecen por 3.")
	ErrContactoIgual = sobre.Nuevo(http.StatusUnprocessableEntity, "Canal.Contacto.Igual", "Ese ya es tu dato actual.")
	ErrNoEncontrado  = sobre.Nuevo(http.StatusNotFound, "Canal.Recurso.NoExiste", "No encontramos lo que buscas.")
)

// CooperativaDelAsociado: una cooperativa donde la persona está habilitada, con su marca (FR-025).
type CooperativaDelAsociado struct {
	TenantPublicID    uuid.UUID       `json:"tenantPublicId"`
	DisplayName       string          `json:"displayName"`
	Brand             json.RawMessage `json:"brand"`
	Support           json.RawMessage `json:"support"`
	InactivityMinutes int             `json:"inactivityMinutes"`
}

// perfil atiende GET /api/v1/me.
func (s *Servicio) perfil(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	activa, _ := SesionDe(ctx)
	cuenta, err := s.q.GetAccount(ctx, activa.CuentaID)
	if err != nil {
		s.fallo(w, r, "no se pudo leer la cuenta", err)
		return
	}
	cooperativas, err := s.cooperativasDe(ctx, activa.Documento)
	if err != nil {
		s.fallo(w, r, "no se pudieron leer las cooperativas", err)
		return
	}
	var elegida *uuid.UUID
	if activa.CooperativaID != nil {
		if coop, err := s.q.GetTenantByID(ctx, *activa.CooperativaID); err == nil {
			elegida = &coop.TenantPublicID
		}
	}
	cuentaJSON := map[string]any{
		"firstNames": cuenta.FirstNames, "lastNames": cuenta.LastNames, "documentType": cuenta.DocumentType,
		"documentNumberMasked": enmascararDocumento(cuenta.DocumentNumber), "emailMasked": nil, "mobileMasked": nil,
	}
	if cuenta.Email != nil {
		cuentaJSON["emailMasked"] = enmascararCorreo(*cuenta.Email)
	}
	if cuenta.Mobile != nil {
		cuentaJSON["mobileMasked"] = enmascararCelular(*cuenta.Mobile)
	}
	sobre.JSON(w, http.StatusOK, map[string]any{
		"account": cuentaJSON, "tenants": cooperativas, "tenant": elegida, "session": estadoDe(activa),
		"passkeysAvailable": s.pasekey != nil,
	})
}

func (s *Servicio) cooperativasDe(ctx context.Context, doc Documento) ([]CooperativaDelAsociado, error) {
	filas, err := s.q.EnabledTenantsForDocument(ctx, gen.EnabledTenantsForDocumentParams{DocumentType: doc.Tipo, DocumentNumber: doc.Numero})
	if err != nil {
		return nil, err
	}
	r := make([]CooperativaDelAsociado, 0, len(filas))
	for _, f := range filas {
		r = append(r, CooperativaDelAsociado{TenantPublicID: f.TenantPublicID, DisplayName: f.DisplayName, Brand: f.Brand, Support: f.Support,
			InactivityMinutes: inactividadDe(f.InactivityMinutes)})
	}
	return r, nil
}

// inactividadDe acota la inactividad de la cooperativa a 5..15 minutos (FR-027).
func inactividadDe(minutos int32) int {
	m := int(minutos)
	if m < InactividadPorDefecto {
		return InactividadPorDefecto
	}
	if m > InactividadMaxima {
		return InactividadMaxima
	}
	return m
}

// estadoDeLaSesion atiende GET /api/v1/session: los relojes de la sesión, sin contar como actividad (el aviso de sesión
// lo consulta sin alargarla).
func (s *Servicio) estadoDeLaSesion(w http.ResponseWriter, r *http.Request) {
	activa, _ := SesionDe(r.Context())
	sobre.JSON(w, http.StatusOK, estadoDe(activa))
}

// seguir atiende POST /api/v1/session/keepalive («Seguir trabajando»): la actividad ya la anotó el middleware.
func (s *Servicio) seguir(w http.ResponseWriter, r *http.Request) {
	activa, _ := SesionDe(r.Context())
	activa.UltimaActividad = s.ahora()
	sobre.JSON(w, http.StatusOK, estadoDe(activa))
}

// elegirCooperativa atiende POST /api/v1/session/tenant (FR-025, FR-028): acota la sesión a una cooperativa donde la
// persona está habilitada, sin volver a autenticar.
func (s *Servicio) elegirCooperativa(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	activa, _ := SesionDe(ctx)
	var cuerpo struct {
		TenantPublicID uuid.UUID `json:"tenantPublicId"`
	}
	if e := leer(w, r, &cuerpo); e != nil {
		sobre.Escribir(w, e)
		return
	}
	coop, err := s.q.GetTenantByPublicID(ctx, cuerpo.TenantPublicID)
	if errors.Is(err, pgx.ErrNoRows) {
		sobre.Escribir(w, ErrCooperativaNoDisponible)
		return
	}
	if err != nil {
		s.fallo(w, r, "no se pudo leer la cooperativa", err)
		return
	}
	h, err := s.q.EnablementForSession(ctx, gen.EnablementForSessionParams{TenantID: coop.ID, DocumentType: activa.Documento.Tipo,
		DocumentNumber: activa.Documento.Numero})
	if errors.Is(err, pgx.ErrNoRows) {
		sobre.Escribir(w, ErrCooperativaNoDisponible)
		return
	}
	if err != nil {
		s.fallo(w, r, "no se pudo leer la habilitación", err)
		return
	}
	inactividad := inactividadDe(h.InactivityMinutes)
	ahora := s.ahora()
	if err := s.q.SetSessionTenant(ctx, gen.SetSessionTenantParams{ID: activa.ID, TenantID: &coop.ID, InactivityMinutes: int32(inactividad), At: ahora}); err != nil {
		s.fallo(w, r, "no se pudo acotar la sesión", err)
		return
	}
	activa.CooperativaID = &coop.ID
	activa.Inactividad = time.Duration(inactividad) * time.Minute
	activa.UltimaActividad = ahora
	auditado := contexto.ConCooperativa(ctx, contexto.Cooperativa{ID: coop.ID, PublicID: coop.TenantPublicID})
	s.auditar(auditado, "Canal.Sesion.CooperativaElegida", activa.ID.String(), nil)
	sobre.JSON(w, http.StatusOK, map[string]any{
		"tenant":  CooperativaDelAsociado{TenantPublicID: coop.TenantPublicID, DisplayName: coop.DisplayName, Brand: coop.Brand, Support: coop.Support, InactivityMinutes: inactividad},
		"session": estadoDe(activa),
	})
}

// --------------------------------------------------------------------------------------------- contacto (FR-024) --

// pedirCambioDeContacto atiende POST /api/v1/me/contact: envía un código al dato nuevo.
func (s *Servicio) pedirCambioDeContacto(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	activa, _ := SesionDe(ctx)
	var cuerpo struct {
		Field string `json:"field"` // email | mobile
		Value string `json:"value"`
	}
	if e := leer(w, r, &cuerpo); e != nil {
		sobre.Escribir(w, e)
		return
	}
	valor, canal, ok := normalizarContacto(cuerpo.Field, cuerpo.Value)
	if !ok {
		sobre.Escribir(w, ErrContactoInvalido)
		return
	}
	if canal == mensajeria.SMS && !s.o.Mensajero.Disponible(mensajeria.SMS) {
		sobre.Escribir(w, ErrCanalNoDisponible.ConDatos(map[string]string{"motivo": "Por ahora no podemos enviar códigos por SMS para verificar el celular."}))
		return
	}
	cuenta, err := s.q.GetAccount(ctx, activa.CuentaID)
	if err != nil {
		s.fallo(w, r, "no se pudo leer la cuenta", err)
		return
	}
	if actual := map[string]*string{"email": cuenta.Email, "mobile": cuenta.Mobile}[cuerpo.Field]; actual != nil && strings.EqualFold(*actual, valor) {
		sobre.Escribir(w, ErrContactoIgual)
		return
	}
	llave := Documento{Tipo: cuenta.DocumentType, Numero: cuenta.DocumentNumber}.Llave()
	if e := s.limitarDesafios(ctx, llave); e != nil {
		sobre.Escribir(w, e)
		return
	}
	campo := cuerpo.Field
	id, err := s.nuevoDesafio(ctx, s.q, gen.InsertChallengeParams{Purpose: propositoContacto, AccountID: &cuenta.ID, DocumentKey: llave,
		ContactField: &campo, ContactValue: &valor})
	if err != nil {
		s.fallo(w, r, "no se pudo crear el desafío de contacto", err)
		return
	}
	// Se envía de una vez al dato nuevo.
	enviado, e := s.despachar(ctx, id, propositoContacto, canal)
	if e != nil {
		sobre.Escribir(w, e)
		return
	}
	sobre.JSON(w, http.StatusOK, map[string]any{"challengeId": id, "sentTo": enviado.SentTo, "expiresAt": enviado.ExpiresAt})
}

// confirmarCambioDeContacto atiende POST /api/v1/me/contact/verify: guarda el dato, avisa al anterior y encola la
// novedad para cada cooperativa donde la persona está habilitada.
func (s *Servicio) confirmarCambioDeContacto(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	activa, _ := SesionDe(ctx)
	var cuerpo struct {
		ChallengeID uuid.UUID `json:"challengeId"`
		Code        string    `json:"code"`
	}
	if e := leer(w, r, &cuerpo); e != nil {
		sobre.Escribir(w, e)
		return
	}
	var anterior *string
	var campo, valor string
	e := s.verificarCodigo(ctx, cuerpo.ChallengeID, strings.TrimSpace(cuerpo.Code), propositoContacto, nil,
		func(q *gen.Queries, tx pgx.Tx, d gen.OtpChallenge) error {
			if d.AccountID == nil || *d.AccountID != activa.CuentaID || d.ContactField == nil || d.ContactValue == nil {
				return ErrDesafioVencido
			}
			cuenta, err := q.GetAccount(ctx, activa.CuentaID)
			if err != nil {
				return err
			}
			campo, valor = *d.ContactField, *d.ContactValue
			if campo == "email" {
				anterior = cuenta.Email
				err = q.UpdateAccountEmail(ctx, gen.UpdateAccountEmailParams{ID: cuenta.ID, Email: &valor})
			} else {
				anterior = cuenta.Mobile
				err = q.UpdateAccountMobile(ctx, gen.UpdateAccountMobileParams{ID: cuenta.ID, Mobile: &valor})
			}
			if err != nil {
				return err
			}
			reportada := s.ahora()
			cooperativas, err := q.EnabledTenantsForDocument(ctx, gen.EnabledTenantsForDocumentParams{DocumentType: cuenta.DocumentType,
				DocumentNumber: cuenta.DocumentNumber})
			if err != nil {
				return err
			}
			for _, c := range cooperativas {
				if _, err := s.o.Encolador.InsertTx(ctx, tx, ArgsReportarNovedad{
					TenantPublicID: c.TenantPublicID, PersonPublicID: c.PersonPublicID, Campo: campo, Valor: valor, Reportada: reportada,
				}, nil); err != nil {
					return err
				}
			}
			return s.o.Auditoria.EnTransaccion(tx).Registrar(ctx, "Canal.Contacto.Cambiado", cuenta.ID.String(),
				map[string]any{"campo": campo, "cooperativas": len(cooperativas)})
		})
	if e != nil {
		sobre.Escribir(w, e)
		return
	}
	if anterior != nil && *anterior != "" {
		canal, dato := mensajeria.Correo, "correo"
		if campo == "mobile" {
			canal, dato = mensajeria.SMS, "celular"
		}
		s.avisar(ctx, mensajeria.AvisoDeContactoCambiado(canal, *anterior, dato))
	}
	mascara := enmascararCorreo(valor)
	if campo == "mobile" {
		mascara = enmascararCelular(valor)
	}
	sobre.JSON(w, http.StatusOK, map[string]string{"field": campo, "masked": mascara})
}

func normalizarContacto(campo, valor string) (string, mensajeria.Canal, bool) {
	valor = strings.TrimSpace(valor)
	switch campo {
	case "email":
		dir, err := mail.ParseAddress(valor)
		if err != nil || dir.Address != valor || len(valor) > 254 || !strings.Contains(valor[strings.LastIndex(valor, "@")+1:], ".") {
			return "", "", false
		}
		return strings.ToLower(valor), mensajeria.Correo, true
	case "mobile":
		var b strings.Builder
		for _, r := range valor {
			if r >= '0' && r <= '9' {
				b.WriteRune(r)
			}
		}
		n := b.String()
		if len(n) == 12 && strings.HasPrefix(n, "57") {
			n = n[2:]
		}
		if len(n) != 10 || n[0] != '3' {
			return "", "", false
		}
		return n, mensajeria.SMS, true
	}
	return "", "", false
}

// --------------------------------------------------------------------------------------------------- clave --

// cambiarClave atiende POST /api/v1/me/password: con la clave actual, cambia la clave y cierra las demás sesiones.
func (s *Servicio) cambiarClave(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	activa, _ := SesionDe(ctx)
	var cuerpo struct {
		CurrentPassword string `json:"currentPassword"`
		NewPassword     string `json:"newPassword"`
	}
	if e := leer(w, r, &cuerpo); e != nil {
		sobre.Escribir(w, e)
		return
	}
	cuenta, err := s.q.GetAccount(ctx, activa.CuentaID)
	if err != nil {
		s.fallo(w, r, "no se pudo leer la cuenta", err)
		return
	}
	if !CompararClave(cuenta.PasswordHash, cuerpo.CurrentPassword) {
		sobre.Escribir(w, ErrClaveActualIncorrecta)
		return
	}
	if err := ValidarClave(cuerpo.NewPassword, activa.Documento); err != nil {
		sobre.Escribir(w, sobre.Nuevo(http.StatusUnprocessableEntity, "Canal.Clave.Debil", err.Error()))
		return
	}
	hash, err := HashDeClave(cuerpo.NewPassword)
	if err != nil {
		s.fallo(w, r, "no se pudo calcular el hash de la clave", err)
		return
	}
	if e := s.enTransaccion(ctx, func(q *gen.Queries, tx pgx.Tx) error {
		ahora := s.ahora()
		if err := q.UpdateAccountPassword(ctx, gen.UpdateAccountPasswordParams{ID: cuenta.ID, PasswordHash: hash, ChangedAt: ahora}); err != nil {
			return err
		}
		motivo := "password_changed"
		if _, err := q.EndOtherSessions(ctx, gen.EndOtherSessionsParams{AccountID: cuenta.ID, At: ahora, EndReason: &motivo, KeepID: activa.ID}); err != nil {
			return err
		}
		return s.o.Auditoria.EnTransaccion(tx).Registrar(ctx, "Canal.Clave.Cambiada", cuenta.ID.String(), nil)
	}); e != nil {
		escribirError(w, s, r, e)
		return
	}
	if cuenta.Email != nil {
		s.avisar(ctx, mensajeria.AvisoDeClaveCambiada(*cuenta.Email))
	}
	w.WriteHeader(http.StatusNoContent)
}

// ------------------------------------------------------------------------------------- dispositivos y sesiones --

// dispositivos atiende GET /api/v1/me/devices (FR-027).
func (s *Servicio) dispositivos(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	activa, _ := SesionDe(ctx)
	ds, err := s.q.ListDevices(ctx, activa.CuentaID)
	if err != nil {
		s.fallo(w, r, "no se pudieron leer los dispositivos", err)
		return
	}
	ss, err := s.q.ListOpenSessions(ctx, gen.ListOpenSessionsParams{AccountID: activa.CuentaID, At: s.ahora()})
	if err != nil {
		s.fallo(w, r, "no se pudieron leer las sesiones", err)
		return
	}
	dispositivos := make([]map[string]any, 0, len(ds))
	for _, d := range ds {
		dispositivos = append(dispositivos, map[string]any{
			"id": d.ID, "kind": d.Kind, "label": d.Label, "createdAt": d.CreatedAt, "lastSeenAt": d.LastSeenAt,
			"trusted": d.CredentialHash != nil, "passkey": d.WebauthnID != nil,
			"current": activa.DispositivoID != nil && *activa.DispositivoID == d.ID,
		})
	}
	ahora := s.ahora()
	sesiones := make([]map[string]any, 0, len(ss))
	for _, x := range ss {
		if !ahora.Before(x.LastActivityAt.Add(time.Duration(x.InactivityMinutes) * time.Minute)) {
			continue // ya venció por inactividad; se termina en su siguiente uso
		}
		etiqueta := "Sesión"
		if x.UserAgent != nil {
			etiqueta = etiquetaDeDispositivo(Navegador, *x.UserAgent)
		}
		sesiones = append(sesiones, map[string]any{
			"id": x.ID, "deviceId": x.DeviceID, "label": etiqueta, "createdAt": x.CreatedAt, "lastActivityAt": x.LastActivityAt,
			"current": x.ID == activa.ID,
		})
	}
	sobre.JSON(w, http.StatusOK, map[string]any{"devices": dispositivos, "sessions": sesiones})
}

// revocarDispositivo atiende DELETE /api/v1/me/devices/{id}: deja de ser de confianza y se cierran sus sesiones.
func (s *Servicio) revocarDispositivo(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	activa, _ := SesionDe(ctx)
	id, err := uuid.Parse(r.PathValue("id"))
	if err != nil {
		sobre.Escribir(w, ErrNoEncontrado)
		return
	}
	if e := s.enTransaccion(ctx, func(q *gen.Queries, tx pgx.Tx) error {
		ahora := s.ahora()
		n, err := q.RevokeDevice(ctx, gen.RevokeDeviceParams{ID: id, AccountID: activa.CuentaID, At: ahora})
		if err != nil {
			return err
		}
		if n == 0 {
			return ErrNoEncontrado
		}
		if _, err := q.EndDeviceSessions(ctx, gen.EndDeviceSessionsParams{AccountID: activa.CuentaID, DeviceID: &id, At: ahora}); err != nil {
			return err
		}
		return s.o.Auditoria.EnTransaccion(tx).Registrar(ctx, "Canal.Dispositivo.Revocado", id.String(), nil)
	}); e != nil {
		escribirError(w, s, r, e)
		return
	}
	w.WriteHeader(http.StatusNoContent)
}

// cerrarSesion atiende DELETE /api/v1/me/sessions/{id}.
func (s *Servicio) cerrarSesion(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	activa, _ := SesionDe(ctx)
	id, err := uuid.Parse(r.PathValue("id"))
	if err != nil {
		sobre.Escribir(w, ErrNoEncontrado)
		return
	}
	motivo := "revoked"
	n, err := s.q.EndAccountSession(ctx, gen.EndAccountSessionParams{ID: id, AccountID: activa.CuentaID, At: s.ahora(), EndReason: &motivo})
	if err != nil {
		s.fallo(w, r, "no se pudo cerrar la sesión", err)
		return
	}
	if n == 0 {
		sobre.Escribir(w, ErrNoEncontrado)
		return
	}
	s.auditar(ctx, "Canal.Sesion.Revocada", id.String(), nil)
	if id == activa.ID && !PlataformaDe(r).esApp() {
		s.borrarCookieDeSesion(w)
	}
	w.WriteHeader(http.StatusNoContent)
}

func enmascararDocumento(numero string) string {
	if len(numero) <= 4 {
		return numero
	}
	return strings.Repeat("•", len(numero)-4) + numero[len(numero)-4:]
}

// -------------------------------------------------------------------------------- novedad de contacto al ERP --

// ArgsReportarNovedad: avisar a una cooperativa que el asociado cambió un dato de contacto (FR-024). River reintenta
// hasta que el ERP lo recibe; el ERP es idempotente por persona, campo y fecha.
type ArgsReportarNovedad struct {
	TenantPublicID uuid.UUID `json:"tenantPublicId"`
	PersonPublicID uuid.UUID `json:"personPublicId"`
	Campo          string    `json:"campo"`
	Valor          string    `json:"valor"`
	Reportada      time.Time `json:"reportada"`
}

func (ArgsReportarNovedad) Kind() string { return "send_contact_update" }

// ReportadorDeNovedades lo cumple *erp.Cliente.
type ReportadorDeNovedades interface {
	ReportarNovedadDeContacto(ctx context.Context, cooperativa, persona uuid.UUID, n erp.NovedadDeContacto) error
}

// TrabajadorDeNovedades entrega la novedad al ERP.
type TrabajadorDeNovedades struct {
	river.WorkerDefaults[ArgsReportarNovedad]
	ERP ReportadorDeNovedades
}

func (t *TrabajadorDeNovedades) Work(ctx context.Context, job *river.Job[ArgsReportarNovedad]) error {
	campo := "Email"
	if job.Args.Campo == "mobile" {
		campo = "Mobile"
	}
	err := t.ERP.ReportarNovedadDeContacto(ctx, job.Args.TenantPublicID, job.Args.PersonPublicID,
		erp.NovedadDeContacto{Campo: campo, ValorNuevo: job.Args.Valor, Reportada: job.Args.Reportada})
	var errERP *erp.ErrorDelERP
	if errors.As(err, &errERP) && errERP.Estado >= 400 && errERP.Estado < 500 && errERP.Estado != http.StatusTooManyRequests {
		// La persona ya no está habilitada o el canal está apagado: no hay a quién avisar; reintentar no sirve.
		return river.JobCancel(err)
	}
	return err
}
