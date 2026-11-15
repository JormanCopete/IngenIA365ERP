package identidad

import (
	"context"
	"errors"
	"net/http"
	"strings"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/erp"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// Textos que hay que aceptar al activar (FR-020).
var textosObligatorios = []string{"Terms", "DataPolicy"}

var (
	ErrCuentaExistente = sobre.Nuevo(http.StatusConflict, "Canal.Activacion.CuentaExistente",
		"Ya tienes una cuenta en el canal. Ingresa con tu documento y tu clave, o recupera tu clave.")
	ErrTextosDesactualizados = sobre.Nuevo(http.StatusConflict, "Canal.Activacion.TextosDesactualizados",
		"Los términos o la política de datos cambiaron mientras activabas. Léelos de nuevo y acéptalos.")
	ErrContactoCambio = sobre.Nuevo(http.StatusConflict, "Canal.Activacion.ContactoCambio",
		"Tus datos de contacto cambiaron en la cooperativa mientras activabas. Empieza de nuevo.")
	ErrSinValidadora = sobre.Nuevo(http.StatusConflict, "Canal.Activacion.SinEntidad",
		"Ya no tienes una entidad habilitada para activar la cuenta. Comunícate con tu cooperativa.")
)

// iniciarActivacion atiende POST /api/v1/activation/start (FR-016, FR-018, FR-019).
func (s *Servicio) iniciarActivacion(w http.ResponseWriter, r *http.Request) {
	inicio := time.Now()
	ctx := r.Context()
	var cuerpo struct {
		DocumentType   string `json:"documentType"`
		DocumentNumber string `json:"documentNumber"`
	}
	if e := leer(w, r, &cuerpo); e != nil {
		sobre.Escribir(w, e)
		return
	}
	defer s.igualar(ctx, inicio)

	doc, valido := NormalizarDocumento(cuerpo.DocumentType, cuerpo.DocumentNumber)
	if !valido {
		sobre.Escribir(w, ErrDocumentoInvalido)
		return
	}
	llave := doc.Llave()
	if e := s.limitarDesafios(ctx, llave); e != nil {
		sobre.Escribir(w, e)
		return
	}

	v, err := BuscarValidadora(ctx, s.q, doc)
	if err != nil {
		s.fallo(w, r, "no se pudo buscar la entidad validadora", err)
		return
	}
	param := gen.InsertChallengeParams{Purpose: propositoActivacion, DocumentKey: llave, Decoy: true}
	var respuesta RespuestaDeInicio
	if v != nil {
		contacto, err := s.o.ERP.ContactoDeActivacion(ctx, v.CooperativaPublicID, v.PersonaPublicID)
		var errERP *erp.ErrorDelERP
		switch {
		case errors.As(err, &errERP) && errERP.Estado == http.StatusNotFound:
			// La inhabilitaron y el espejo todavía no lo sabe: se responde como a un desconocido.
			v = nil
		case err != nil:
			s.erpFallo(w, r, "el ERP no dio el contacto de activación", err)
			return
		default:
			param.Decoy = false
			param.TenantID = &v.CooperativaID
			param.PersonPublicID = &v.PersonaPublicID
			param.DocumentType = &doc.Tipo
			param.DocumentNumber = &doc.Numero
			respuesta.Channels = s.canalesDeContacto(contacto.Entrega.Correo, contacto.Entrega.Celular, v.SMS)
			if len(respuesta.Channels) == 0 {
				respuesta.NeedsEntityUpdate = true
				respuesta.EntityName = v.Nombre
				respuesta.Support = v.Soporte
			}
		}
	}
	if param.Decoy {
		respuesta.Channels = s.canalesSenuelo(llave)
	}
	id, err := s.nuevoDesafio(ctx, s.q, param)
	if err != nil {
		s.fallo(w, r, "no se pudo crear el desafío de activación", err)
		return
	}
	respuesta.ChallengeID = id
	sobre.JSON(w, http.StatusOK, respuesta)
}

// TextoParaAceptar es un texto legal vigente que la persona debe aceptar.
type TextoParaAceptar struct {
	Kind    string `json:"kind"`
	Version int    `json:"version"`
	Content string `json:"content"`
}

// verificarActivacion atiende POST /api/v1/activation/verify: con el código correcto entrega el permiso de un solo uso
// para terminar y los textos que hay que aceptar. Si la cuenta ya existe, lo dice (sólo a quien tiene el código).
func (s *Servicio) verificarActivacion(w http.ResponseWriter, r *http.Request) {
	inicio := time.Now()
	ctx := r.Context()
	var cuerpo struct {
		ChallengeID uuid.UUID `json:"challengeId"`
		Code        string    `json:"code"`
	}
	if e := leer(w, r, &cuerpo); e != nil {
		sobre.Escribir(w, e)
		return
	}
	defer s.igualar(ctx, inicio)

	permiso, hash, err := NuevoSecreto()
	if err != nil {
		s.fallo(w, r, "no se pudo generar el permiso", err)
		return
	}
	var desafio gen.OtpChallenge
	if e := s.verificarCodigo(ctx, cuerpo.ChallengeID, strings.TrimSpace(cuerpo.Code), propositoActivacion, hash,
		func(_ *gen.Queries, _ pgx.Tx, d gen.OtpChallenge) error { desafio = d; return nil }); e != nil {
		sobre.Escribir(w, e)
		return
	}

	_, err = s.q.GetAccountByDocument(ctx, gen.GetAccountByDocumentParams{DocumentType: *desafio.DocumentType, DocumentNumber: *desafio.DocumentNumber})
	if err == nil {
		sobre.JSON(w, http.StatusOK, map[string]any{"alreadyActive": true})
		return
	}
	if !errors.Is(err, pgx.ErrNoRows) {
		s.fallo(w, r, "no se pudo buscar la cuenta", err)
		return
	}
	textos, e := s.textosParaAceptar(ctx, *desafio.TenantID)
	if e != nil {
		sobre.Escribir(w, e)
		return
	}
	coop, _ := s.q.GetTenantByID(ctx, *desafio.TenantID)
	sobre.JSON(w, http.StatusOK, map[string]any{
		"activationToken": permiso,
		"entityName":      coop.DisplayName,
		"legalTexts":      textos,
	})
}

// textosParaAceptar pide al ERP las versiones vigentes de los textos obligatorios.
func (s *Servicio) textosParaAceptar(ctx context.Context, cooperativa uuid.UUID) ([]TextoParaAceptar, *sobre.Error) {
	coop, err := s.q.GetTenantByID(ctx, cooperativa)
	if err != nil {
		s.o.Logger.ErrorContext(ctx, "no se pudo leer la cooperativa", "error", err)
		return nil, sobre.ErrInesperado
	}
	todos, err := s.o.ERP.TextosLegales(ctx, coop.TenantPublicID)
	if err != nil {
		s.o.Logger.WarnContext(ctx, "el ERP no dio los textos legales", "error", err)
		return nil, ErrERPNoDisponible
	}
	var textos []TextoParaAceptar
	for _, tipo := range textosObligatorios {
		for _, t := range todos {
			if t.Tipo == tipo {
				textos = append(textos, TextoParaAceptar{Kind: t.Tipo, Version: t.Version, Content: t.Contenido})
			}
		}
	}
	if len(textos) != len(textosObligatorios) {
		// Encender el canal exige los dos textos; si faltan, la cooperativa cambió algo que no debía.
		s.o.Logger.ErrorContext(ctx, "la cooperativa no tiene publicados los textos obligatorios", "cooperativa", coop.TenantPublicID)
		return nil, ErrERPNoDisponible
	}
	return textos, nil
}

// Aceptacion: un texto aceptado con su versión.
type Aceptacion struct {
	Kind    string `json:"kind"`
	Version int    `json:"version"`
}

// completarActivacion atiende POST /api/v1/activation/complete: crea la cuenta con la clave, registra las aceptaciones,
// confía en este dispositivo (acaba de pasar un código) y abre la sesión (FR-020).
func (s *Servicio) completarActivacion(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	var cuerpo struct {
		ActivationToken string       `json:"activationToken"`
		Password        string       `json:"password"`
		Acceptances     []Aceptacion `json:"acceptances"`
	}
	if e := leer(w, r, &cuerpo); e != nil {
		sobre.Escribir(w, e)
		return
	}

	// Lo que pide al ERP va antes de la transacción; se vuelve a comprobar dentro.
	var previo gen.OtpChallenge
	if e := s.enTransaccion(ctx, func(q *gen.Queries, _ pgx.Tx) error {
		d, e := s.tomarPermiso(ctx, q, cuerpo.ActivationToken, propositoActivacion)
		if e != nil {
			return e
		}
		previo = d
		return nil
	}); e != nil {
		escribirError(w, s, r, e)
		return
	}
	doc := Documento{Tipo: *previo.DocumentType, Numero: *previo.DocumentNumber}
	if err := ValidarClave(cuerpo.Password, doc); err != nil {
		sobre.Escribir(w, sobre.Nuevo(http.StatusUnprocessableEntity, "Canal.Clave.Debil", err.Error()))
		return
	}
	textos, e := s.textosParaAceptar(ctx, *previo.TenantID)
	if e != nil {
		sobre.Escribir(w, e)
		return
	}
	for _, t := range textos {
		if !aceptado(cuerpo.Acceptances, t) {
			sobre.Escribir(w, ErrTextosDesactualizados.ConDatos(map[string]any{"legalTexts": textos}))
			return
		}
	}
	coop, err := s.q.GetTenantByID(ctx, *previo.TenantID)
	if err != nil {
		s.fallo(w, r, "no se pudo leer la cooperativa", err)
		return
	}
	contacto, err := s.o.ERP.ContactoDeActivacion(ctx, coop.TenantPublicID, *previo.PersonPublicID)
	if err != nil {
		var errERP *erp.ErrorDelERP
		if errors.As(err, &errERP) && errERP.Estado == http.StatusNotFound {
			sobre.Escribir(w, ErrSinValidadora)
			return
		}
		s.erpFallo(w, r, "el ERP no dio el contacto al completar la activación", err)
		return
	}
	var correo, celular *string
	switch {
	case previo.Channel != nil && *previo.Channel == "email" && contacto.Entrega.Correo != nil &&
		string(HashDeDestino(*contacto.Entrega.Correo)) == string(previo.DestinationHash):
		correo = contacto.Entrega.Correo
	case previo.Channel != nil && *previo.Channel == "sms" && contacto.Entrega.Celular != nil &&
		string(HashDeDestino(*contacto.Entrega.Celular)) == string(previo.DestinationHash):
		celular = contacto.Entrega.Celular
	default:
		sobre.Escribir(w, ErrContactoCambio)
		return
	}
	hash, err := HashDeClave(cuerpo.Password)
	if err != nil {
		s.fallo(w, r, "no se pudo calcular el hash de la clave", err)
		return
	}
	credencial, hashCredencial, err := NuevoSecreto()
	if err != nil {
		s.fallo(w, r, "no se pudo generar la credencial de dispositivo", err)
		return
	}

	var resp RespuestaDeSesion
	cuentaID := nuevoID()
	if e := s.enTransaccion(ctx, func(q *gen.Queries, tx pgx.Tx) error {
		d, e := s.tomarPermiso(ctx, q, cuerpo.ActivationToken, propositoActivacion)
		if e != nil {
			return e
		}
		if _, err := q.GetAccountByDocument(ctx, gen.GetAccountByDocumentParams{DocumentType: doc.Tipo, DocumentNumber: doc.Numero}); err == nil {
			return ErrCuentaExistente
		} else if !errors.Is(err, pgx.ErrNoRows) {
			return err
		}
		ahora := s.ahora()
		nombres := strings.TrimSpace(contacto.Nombres)
		if nombres == "" {
			nombres = "Asociado"
		}
		cuenta := gen.Account{ID: cuentaID, DocumentType: doc.Tipo, DocumentNumber: doc.Numero, FirstNames: recorte(nombres, 300),
			LastNames: recorte(strings.TrimSpace(contacto.Apellidos), 300), Email: correo, Mobile: celular, PasswordHash: hash,
			Status: "active", ValidatingTenantID: coop.ID}
		if err := q.InsertAccount(ctx, gen.InsertAccountParams{
			ID: cuenta.ID, DocumentType: cuenta.DocumentType, DocumentNumber: cuenta.DocumentNumber, FirstNames: cuenta.FirstNames,
			LastNames: cuenta.LastNames, Email: correo, Mobile: celular, PasswordHash: hash, ActivatedAt: ahora, ValidatingTenantID: coop.ID,
		}); err != nil {
			return err
		}
		ip, agente := recortar(contexto.IP(ctx), 64), recortar(contexto.Agente(ctx), 512)
		for _, t := range textos {
			if err := q.InsertLegalAcceptance(ctx, gen.InsertLegalAcceptanceParams{ID: nuevoID(), AccountID: cuenta.ID, TenantID: coop.ID,
				Kind: t.Kind, Version: int32(t.Version), AcceptedAt: ahora, Ip: ip, UserAgent: agente}); err != nil {
				return err
			}
		}
		plataforma := PlataformaDe(r)
		dispositivo := nuevoID()
		if err := q.InsertDevice(ctx, gen.InsertDeviceParams{ID: dispositivo, AccountID: cuenta.ID, Kind: string(plataforma),
			Label: etiquetaDeDispositivo(plataforma, contexto.Agente(ctx)), CredentialHash: hashCredencial, LastSeenAt: ahora}); err != nil {
			return err
		}
		if err := q.UseGrant(ctx, gen.UseGrantParams{ID: d.ID, At: ahora}); err != nil {
			return err
		}
		var err error
		resp, err = s.abrirSesion(contexto.ConCuenta(ctx, cuenta.ID), q, w, r, cuenta, &dispositivo)
		if err != nil {
			return err
		}
		auditado := contexto.ConCooperativa(contexto.ConActor(contexto.ConCuenta(ctx, cuenta.ID), contexto.ActorAsociado),
			contexto.Cooperativa{ID: coop.ID, PublicID: coop.TenantPublicID})
		return s.o.Auditoria.EnTransaccion(tx).Registrar(auditado, "Canal.Cuenta.Activada", cuenta.ID.String(), map[string]any{
			"canalDelCodigo": *d.Channel, "textos": textos2(textos),
		})
	}); e != nil {
		escribirError(w, s, r, e)
		return
	}
	s.entregarCredencialDeDispositivo(w, r, &resp, credencial)
	sobre.JSON(w, http.StatusCreated, resp)
}

func aceptado(aceptaciones []Aceptacion, t TextoParaAceptar) bool {
	for _, a := range aceptaciones {
		if a.Kind == t.Kind && a.Version == t.Version {
			return true
		}
	}
	return false
}

func textos2(textos []TextoParaAceptar) []map[string]any {
	r := make([]map[string]any, 0, len(textos))
	for _, t := range textos {
		r = append(r, map[string]any{"tipo": t.Kind, "version": t.Version})
	}
	return r
}

func recorte(s string, maximo int) string {
	if len(s) <= maximo {
		return s
	}
	return s[:maximo]
}

// escribirError responde un *sobre.Error que vino como error de una transacción, o el genérico.
func escribirError(w http.ResponseWriter, s *Servicio, r *http.Request, err error) {
	var e *sobre.Error
	if errors.As(err, &e) {
		sobre.Escribir(w, e)
		return
	}
	s.fallo(w, r, "falló una operación de identidad", err)
}
