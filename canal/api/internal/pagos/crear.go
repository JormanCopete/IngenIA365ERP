package pagos

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"math"
	"net/http"
	"strconv"
	"strings"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/contexto"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/erp"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/pasarela"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/sobre"
)

// T090 (FR-032 a FR-037, FR-041): POST /api/v1/payments.

// MaximoDeConceptos por pago.
const MaximoDeConceptos = 20

// Textos que el asociado acepta antes de pagar si su versión cambió (FR-034).
var textosDelPago = []string{"Terms", "DataPolicy"}

// PedidoDeItem es un concepto del cuerpo del pago.
type PedidoDeItem struct {
	Clave     string  `json:"conceptKey"`
	Modalidad string  `json:"modality"`
	Valor     int64   `json:"amount"`
	Eleccion  *string `json:"prepaymentChoice"`
}

// Aceptacion es un texto aceptado con su versión.
type Aceptacion struct {
	Tipo    string `json:"kind"`
	Version int    `json:"version"`
}

// Pedido es el cuerpo de POST /api/v1/payments.
type Pedido struct {
	Items        []PedidoDeItem `json:"items"`
	Aceptaciones []Aceptacion   `json:"acceptances"`
	Desafio      *uuid.UUID     `json:"otpChallengeId"`
}

var modalidades = map[string]bool{"Due": true, "Partial": true, "Prepayment": true, "Payoff": true}

func (s *Servicio) crear(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	coop, persona, cuenta, ok := s.alcance(w, r)
	if !ok {
		return
	}
	llave := strings.TrimSpace(r.Header.Get("Idempotency-Key"))
	if len(llave) < 8 || len(llave) > 100 {
		sobre.Escribir(w, ErrSinLlave)
		return
	}
	var p Pedido
	if e := leer(w, r, &p); e != nil {
		sobre.Escribir(w, e)
		return
	}

	// Doble clic (FR-036): la misma llave devuelve el mismo intento, sin volver a validar nada.
	if previo, err := s.q.GetPaymentAttemptByIdempotency(ctx, gen.GetPaymentAttemptByIdempotencyParams{AccountID: cuenta, IdempotencyKey: llave}); err == nil {
		if previo.TenantID != coop.ID {
			sobre.Escribir(w, ErrPedidoInvalido)
			return
		}
		sobre.JSON(w, http.StatusOK, s.respuesta(ctx, previo))
		return
	} else if !errors.Is(err, pgx.ErrNoRows) {
		s.fallo(w, r, "no se pudo buscar el intento por llave", err)
		return
	}

	if e := validarForma(p); e != nil {
		sobre.Escribir(w, e)
		return
	}

	tenant, err := s.q.GetTenantByID(ctx, coop.ID)
	if err != nil {
		s.fallo(w, r, "no se pudo leer la cooperativa", err)
		return
	}

	// Lo que se cobra lo deciden los valores del ERP a este momento, no los que trae el navegador.
	conceptos, err := s.o.ERP.Conceptos(ctx, coop.PublicID, persona)
	if err != nil {
		s.fallaDelERP(w, r, "el ERP no dio los conceptos para el pago", err)
		return
	}
	items, destino, e := armarItems(p.Items, conceptos)
	if e != nil {
		sobre.Escribir(w, e)
		return
	}
	var total int64
	for _, i := range items {
		total += i.Valor
	}

	sitio, estadoDelSitio, err := s.o.Sitios.Cargar(ctx, destino.Sitio)
	if err != nil || estadoDelSitio != pasarela.EstadoActiva || sitio.Cooperativa != coop.PublicID {
		s.o.Logger.WarnContext(ctx, "el destino no tiene un sitio de pasarela activo", "destino", destino.ID, "sitio", destino.Sitio,
			"estado", estadoDelSitio, "error", err)
		sobre.Escribir(w, ErrPasarelaNoDisp)
		return
	}

	// FR-034: términos y política, si su versión cambió desde la última aceptación.
	pendientesDeAceptar, e := s.textosPorAceptar(ctx, coop, cuenta, p.Aceptaciones)
	if e != nil {
		sobre.Escribir(w, e)
		return
	}

	cuentaCanal, err := s.q.GetAccount(ctx, cuenta)
	if err != nil {
		s.fallo(w, r, "no se pudo leer la cuenta", err)
		return
	}
	comprador := pasarela.Comprador{
		TipoDeDocumento: cuentaCanal.DocumentType, NumeroDeDocumento: cuentaCanal.DocumentNumber,
		Nombres: cuentaCanal.FirstNames, Apellidos: cuentaCanal.LastNames,
	}
	if cuentaCanal.Email != nil {
		comprador.Correo = *cuentaCanal.Email
	}
	if cuentaCanal.Mobile != nil {
		comprador.Celular = *cuentaCanal.Mobile
	}
	if _, err := pasarela.ValidarComprador(comprador); err != nil {
		var invalido *pasarela.ErrCompradorInvalido
		if errors.As(err, &invalido) {
			sobre.Escribir(w, sobre.Nuevo(http.StatusUnprocessableEntity, "Canal.Pago.DatosDelComprador", invalido.Mensaje).
				ConDatos(map[string]string{"field": invalido.Campo}))
			return
		}
		s.fallo(w, r, "no se pudo validar el comprador", err)
		return
	}

	// FR-041: un concepto con un intento abierto no se vuelve a pagar. Antes de decidir, los abiertos se refrescan
	// contra la pasarela: si ya terminaron, no estorban.
	claves := make([]string, len(items))
	for i, it := range items {
		claves[i] = it.Clave
	}
	if e := s.bloqueoPorPendiente(ctx, coop.ID, cuenta, claves); e != nil {
		sobre.Escribir(w, e)
		return
	}

	ahora := s.ahora()
	intentoID := nuevoID()
	vence := ahora.Add(time.Duration(tenant.PaymentSessionMinutes) * time.Minute)
	itemsJSON, err := json.Marshal(items)
	if err != nil {
		s.fallo(w, r, "no se pudo serializar el pago", err)
		return
	}
	var referencia string
	var conflicto *sobre.Error
	err = s.enTransaccion(ctx, func(q *gen.Queries, tx pgx.Tx) error {
		if tenant.RequireOtpToPay {
			if p.Desafio == nil {
				return errCodigoRequerido
			}
			if err := s.o.Codigos.TomarCodigoDePago(ctx, q, cuenta, *p.Desafio); err != nil {
				return err
			}
		}
		seq, err := q.NextPaymentReferenceSequence(ctx)
		if err != nil {
			return err
		}
		referencia, err = Referencia(tenant.ChannelCode, ahora.In(horaDeColombia), seq)
		if err != nil {
			return err
		}
		if err := q.InsertPaymentAttempt(ctx, gen.InsertPaymentAttemptParams{
			ID: intentoID, Reference: referencia, TenantID: coop.ID, AccountID: cuenta, PersonPublicID: persona,
			SiteID: sitio.ID, DestinationPublicID: destino.ID, IdempotencyKey: llave, Items: itemsJSON, Total: total,
			ExpiresAt: vence, BuyerIp: textoONulo(contexto.IP(ctx), 64), BuyerUserAgent: textoONulo(contexto.Agente(ctx), 512),
			CreatedAt: ahora,
		}); err != nil {
			return err
		}
		for _, clave := range claves {
			n, err := q.ClaimPendingConcept(ctx, gen.ClaimPendingConceptParams{AccountID: cuenta, TenantID: coop.ID, ConceptKey: clave, AttemptID: intentoID})
			if err != nil {
				return err
			}
			if n == 0 {
				// Otro intento ganó la carrera entre la revisión y la escritura.
				conflicto = s.errPendiente(ctx, coop.ID, cuenta, claves)
				return errCarrera
			}
		}
		for _, t := range pendientesDeAceptar {
			if err := q.InsertLegalAcceptance(ctx, gen.InsertLegalAcceptanceParams{ID: nuevoID(), AccountID: cuenta, TenantID: coop.ID,
				Kind: t.Tipo, Version: int32(t.Version), AcceptedAt: ahora, Ip: textoONulo(contexto.IP(ctx), 64),
				UserAgent: textoONulo(contexto.Agente(ctx), 512)}); err != nil {
				return err
			}
		}
		return s.o.Auditoria.EnTransaccion(tx).Registrar(ctx, "Canal.Pago.IntentoCreado", intentoID.String(), map[string]any{
			"reference": referencia, "total": total, "items": len(items), "destinationPublicId": destino.ID, "siteId": sitio.ID,
		})
	})
	switch {
	case errors.Is(err, errCarrera):
		sobre.Escribir(w, conflicto)
		return
	case err != nil:
		var e *sobre.Error
		if errors.As(err, &e) {
			sobre.Escribir(w, e)
			return
		}
		if errors.Is(err, errCodigoRequerido) {
			sobre.Escribir(w, errCodigoDePago)
			return
		}
		if mismo, err2 := s.q.GetPaymentAttemptByIdempotency(ctx, gen.GetPaymentAttemptByIdempotencyParams{AccountID: cuenta, IdempotencyKey: llave}); err2 == nil {
			// Dos peticiones con la misma llave a la vez: la otra creó el intento.
			sobre.JSON(w, http.StatusOK, s.respuesta(ctx, mismo))
			return
		}
		s.fallo(w, r, "no se pudo crear el intento", err)
		return
	}

	// La sesión en la pasarela se crea fuera de la transacción: si falla, el intento queda fallido y libera los
	// conceptos.
	sesion, err := s.o.Pasarela.CrearSesion(ctx, sitio, pasarela.SolicitudDeSesion{
		Referencia: referencia, Descripcion: fmt.Sprintf("Pago en línea %s · %s", tenant.DisplayName, referencia), Total: total,
		Comprador: comprador, IP: contexto.IP(ctx), Agente: contexto.Agente(ctx), Vence: vence,
		URLDeRetorno: fmt.Sprintf("%s://%s/pagos/%s/retorno", s.o.EsquemaDeRetorno, s.o.HostPublico, intentoID),
		Medios:       medios(destino),
	})
	if err != nil {
		s.o.Logger.WarnContext(ctx, "la pasarela no creó la sesión", "intento", intentoID, "error", err)
		mensaje := "La pasarela no respondió."
		var rechazo *pasarela.ErrRechazo
		if errors.As(err, &rechazo) {
			mensaje = recortar("La pasarela rechazó la solicitud: "+rechazo.Mensaje, 500)
		}
		if _, err := s.transicion(ctx, intentoID, "create", Cambio{Estado: pasarela.EstadoFallido, Mensaje: mensaje,
			Respuesta: json.RawMessage(`{"error":"session_not_created"}`)}); err != nil {
			s.o.Logger.ErrorContext(ctx, "no se pudo marcar fallido el intento", "intento", intentoID, "error", err)
		}
		sobre.Escribir(w, ErrPasarelaNoDisp)
		return
	}
	intento, err := s.sesionCreada(ctx, intentoID, sesion)
	if err != nil {
		s.fallo(w, r, "no se pudo guardar la sesión de la pasarela", err)
		return
	}
	sobre.JSON(w, http.StatusCreated, s.respuesta(ctx, intento))
}

var (
	errCarrera         = errors.New("pagos: concepto tomado por otro intento")
	errCodigoRequerido = errors.New("pagos: falta el código de pago")
	errCodigoDePago    = sobre.Nuevo(http.StatusForbidden, "Canal.Pago.CodigoRequerido",
		"Para confirmar el pago necesitas el código que te enviamos.").ConDatos(map[string]bool{"otpRequired": true})
)

// horaDeColombia: Colombia no tiene horario de verano.
var horaDeColombia = time.FixedZone("COT", -5*60*60)

// validarForma revisa el cuerpo sin mirar al ERP.
func validarForma(p Pedido) *sobre.Error {
	if len(p.Items) == 0 || len(p.Items) > MaximoDeConceptos {
		return ErrPedidoInvalido
	}
	vistos := map[string]bool{}
	for _, i := range p.Items {
		if strings.TrimSpace(i.Clave) == "" || len(i.Clave) > 100 || !modalidades[i.Modalidad] || vistos[i.Clave] {
			return ErrPedidoInvalido
		}
		vistos[i.Clave] = true
		if i.Valor <= 0 {
			return errConcepto("ValorInvalido", "El valor debe ser un número entero de pesos mayor que cero.", i.Clave, nil)
		}
	}
	return nil
}

// armarItems valida cada concepto contra los valores vigentes del ERP (FR-032, FR-032a) y devuelve el destino común.
func armarItems(pedidos []PedidoDeItem, c erp.Conceptos) ([]Item, erp.Destino, *sobre.Error) {
	porClave := make(map[string]erp.Concepto, len(c.Items))
	for _, it := range c.Items {
		porClave[it.Clave] = it
	}
	var destino *uuid.UUID
	items := make([]Item, 0, len(pedidos))
	for _, p := range pedidos {
		con, ok := porClave[p.Clave]
		if !ok || !con.Pagable || con.Destino == nil {
			return nil, erp.Destino{}, errConcepto("ConceptoNoDisponible", "Este producto no se puede pagar en línea en este momento.", p.Clave, nil)
		}
		if destino == nil {
			destino = con.Destino
		} else if *destino != *con.Destino {
			return nil, erp.Destino{}, ErrDestinosDistintos
		}
		item := Item{Clave: con.Clave, Producto: con.Producto, Tipo: con.Tipo, Modalidad: p.Modalidad, Valor: p.Valor, Descripcion: con.Descripcion}
		if e := validarModalidad(p, con, &item); e != nil {
			return nil, erp.Destino{}, e
		}
		items = append(items, item)
	}
	for _, d := range c.Destinos {
		if d.ID == *destino {
			return items, d, nil
		}
	}
	return nil, erp.Destino{}, errConcepto("ConceptoNoDisponible", "Este producto no se puede pagar en línea en este momento.", pedidos[0].Clave, nil)
}

// validarModalidad: la modalidad está entre las del concepto y el valor la respeta. «Exigible» y «cancelación» cobran
// exactamente lo que dice el ERP (en pesos); «parcial» desde su mínimo; el abono a capital pide la elección (FR-032a).
func validarModalidad(p PedidoDeItem, c erp.Concepto, item *Item) *sobre.Error {
	noAdmitida := errConcepto("ModalidadNoAdmitida", "Este producto no admite esa forma de pago.", p.Clave, nil)
	switch p.Modalidad {
	case "Due":
		if !c.Modos.Exigible {
			return noAdmitida
		}
		if esperado := pesos(c.ExigibleHoy); esperado <= 0 || p.Valor != esperado {
			return errConcepto("ValorInvalido", "El valor no coincide con lo que tienes para pagar hoy. Actualiza la información.", p.Clave,
				map[string]any{"expected": esperado})
		}
	case "Payoff":
		if !c.Modos.Cancelacion || c.ParaCancelar == nil {
			return noAdmitida
		}
		if esperado := pesos(*c.ParaCancelar); esperado <= 0 || p.Valor != esperado {
			return errConcepto("ValorInvalido", "El valor no coincide con lo que cancela el producto. Actualiza la información.", p.Clave,
				map[string]any{"expected": esperado})
		}
	case "Partial":
		if c.Modos.Parcial == nil {
			return noAdmitida
		}
		if minimo := pesos(c.Modos.Parcial.Minimo); p.Valor < minimo {
			return errConcepto("ValorInvalido", "El valor es menor que el mínimo que admite este producto.", p.Clave,
				map[string]any{"minimum": minimo})
		}
	case "Prepayment":
		if c.Modos.AbonoCapital == nil {
			return noAdmitida
		}
		eleccion := c.Modos.AbonoCapital.Preseleccion
		if p.Eleccion != nil {
			eleccion = *p.Eleccion
		}
		if eleccion != "ReduceTerm" && eleccion != "ReduceInstallment" {
			return errConcepto("ModalidadNoAdmitida", "Elige si el abono reduce el plazo o la cuota.", p.Clave, nil)
		}
		item.Eleccion = &eleccion
	}
	return nil
}

// pesos lleva un valor del ERP a pesos enteros (redondeo al peso, como se muestra al asociado).
func pesos(n json.Number) int64 {
	f, err := strconv.ParseFloat(string(n), 64)
	if err != nil {
		return 0
	}
	return int64(math.Round(f))
}

func medios(d erp.Destino) []string {
	var m []string
	if d.OfrecePSE {
		m = append(m, "pse")
	}
	if d.OfreceTarjeta {
		m = append(m, "visa", "master", "amex", "diners")
	}
	return m
}

// textosPorAceptar devuelve los textos cuya versión vigente el asociado no ha aceptado. Si alguno no viene aceptado
// en el pedido con esa versión, responde 409 con los textos para mostrarlos.
func (s *Servicio) textosPorAceptar(ctx context.Context, coop contexto.Cooperativa, cuenta uuid.UUID, enviadas []Aceptacion) ([]Aceptacion, *sobre.Error) {
	vigentes, err := s.o.ERP.TextosLegales(ctx, coop.PublicID)
	if err != nil {
		s.o.Logger.WarnContext(ctx, "el ERP no dio los textos legales para el pago", "error", err)
		return nil, ErrEntidadNoDisp
	}
	aceptadas, err := s.q.LatestLegalAcceptances(ctx, gen.LatestLegalAcceptancesParams{AccountID: cuenta, TenantID: coop.ID})
	if err != nil {
		s.o.Logger.ErrorContext(ctx, "no se pudieron leer las aceptaciones", "error", err)
		return nil, sobre.ErrInesperado
	}
	ultima := map[string]int{}
	for _, a := range aceptadas {
		ultima[a.Kind] = int(a.Version)
	}
	var porAceptar []Aceptacion
	var faltan []erp.TextoLegal
	for _, t := range vigentes {
		if !contiene(textosDelPago, t.Tipo) || ultima[t.Tipo] >= t.Version {
			continue
		}
		if contieneAceptacion(enviadas, t.Tipo, t.Version) {
			porAceptar = append(porAceptar, Aceptacion{Tipo: t.Tipo, Version: t.Version})
		} else {
			faltan = append(faltan, t)
		}
	}
	if len(faltan) > 0 {
		return nil, ErrTextosPorAceptar.ConDatos(map[string]any{"legalTexts": faltan})
	}
	return porAceptar, nil
}

func contiene(l []string, v string) bool {
	for _, x := range l {
		if x == v {
			return true
		}
	}
	return false
}

func contieneAceptacion(l []Aceptacion, tipo string, version int) bool {
	for _, a := range l {
		if a.Tipo == tipo && a.Version == version {
			return true
		}
	}
	return false
}

// bloqueoPorPendiente refresca los intentos abiertos de los conceptos y, si alguno sigue abierto, devuelve el 409
// Canal.Pago.Pendiente (FR-041).
func (s *Servicio) bloqueoPorPendiente(ctx context.Context, cooperativa, cuenta uuid.UUID, claves []string) *sobre.Error {
	abiertos, err := s.q.OpenAttemptsForConcepts(ctx, gen.OpenAttemptsForConceptsParams{AccountID: cuenta, TenantID: cooperativa, ConceptKeys: claves})
	if err != nil {
		s.o.Logger.ErrorContext(ctx, "no se pudieron leer los intentos abiertos", "error", err)
		return sobre.ErrInesperado
	}
	if len(abiertos) == 0 {
		return nil
	}
	for _, a := range abiertos {
		if _, err := s.Actualizar(ctx, a.ID, "probe"); err != nil {
			s.o.Logger.WarnContext(ctx, "no se pudo refrescar el intento abierto", "intento", a.ID, "error", err)
		}
	}
	return s.errPendiente(ctx, cooperativa, cuenta, claves)
}

// errPendiente arma el 409 con el primer intento que sigue abierto; nil si ninguno.
func (s *Servicio) errPendiente(ctx context.Context, cooperativa, cuenta uuid.UUID, claves []string) *sobre.Error {
	abiertos, err := s.q.OpenAttemptsForConcepts(ctx, gen.OpenAttemptsForConceptsParams{AccountID: cuenta, TenantID: cooperativa, ConceptKeys: claves})
	if err != nil || len(abiertos) == 0 {
		return nil
	}
	r := s.respuesta(ctx, abiertos[0])
	datos := map[string]any{"attemptId": r.ID, "reference": r.Referencia, "total": r.Total, "status": r.Estado, "support": r.Soporte}
	if r.Aprobacion != nil {
		datos["authorization"] = r.Aprobacion.Autorizacion
	}
	return sobre.Nuevo(http.StatusConflict, "Canal.Pago.Pendiente",
		"Ya tienes un pago en proceso para este producto. Espera su resultado antes de pagar de nuevo.").ConDatos(datos)
}

// fallaDelERP traduce un error del ERP (los mismos códigos que los conceptos).
func (s *Servicio) fallaDelERP(w http.ResponseWriter, r *http.Request, que string, err error) {
	var e *erp.ErrorDelERP
	if errors.As(err, &e) && e.Codigo == "Channel.Person.NotEnabled" {
		sobre.Escribir(w, ErrCoopNoDisponible)
		return
	}
	s.o.Logger.WarnContext(r.Context(), que, "error", err)
	sobre.Escribir(w, ErrEntidadNoDisp)
}

func textoONulo(v string, n int) *string {
	v = strings.TrimSpace(v)
	if v == "" {
		return nil
	}
	return ptr(recortar(v, n))
}
