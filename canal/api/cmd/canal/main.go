// Comando canal: servicio del canal digital del asociado (sistema satélite del ERP, feature 021).
//
// Arranca en este orden: configuración (falla si falta algo), base y migraciones (goose y River, bajo un cerrojo
// para que dos réplicas no migren a la vez), trabajos en segundo plano y servidor HTTP. Al recibir SIGTERM deja de
// aceptar peticiones, espera las que están en curso y detiene los trabajos.
package main

import (
	"context"
	"errors"
	"fmt"
	"log/slog"
	"net"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"

	"github.com/riverqueue/river"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/auditoria"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/cifrado"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/conceptos"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/config"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/entidades"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/erp"
	httpapi "github.com/jormancopete/ingenia365erp/canal/api/internal/http"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/identidad"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/mensajeria"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/pagos"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/pasarela"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/pasarela/placetopay"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/pasarela/simulada"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/trabajos"
	"github.com/jormancopete/ingenia365erp/canal/api/web"
)

func main() {
	if err := correr(); err != nil {
		slog.Error("el canal se detuvo con error", "error", err)
		os.Exit(1)
	}
}

func correr() error {
	cfg, err := config.Cargar()
	if err != nil {
		// Antes de tener logger configurado: a stderr, con todo lo que falta.
		fmt.Fprintln(os.Stderr, err)
		return err
	}
	logger := auditoria.NuevoLogger(os.Stdout, cfg.NivelLog)
	slog.SetDefault(logger)
	logger.Info("configuración cargada", "config", cfg)

	ctx, detener := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer detener()

	pool, err := db.Abrir(ctx, cfg.BaseURL)
	if err != nil {
		return fmt.Errorf("abrir la base del canal: %w", err)
	}
	defer pool.Close()

	if err := db.ConCerrojoDeMigracion(ctx, pool, func(ctx context.Context) error {
		if err := db.Migrar(ctx, pool, logger); err != nil {
			return err
		}
		return trabajos.MigrarRiver(ctx, pool)
	}); err != nil {
		return fmt.Errorf("migrar la base del canal: %w", err)
	}

	q := gen.New(pool)
	clienteERP := erp.NuevoCliente(cfg.ERPURL, cfg.LlavePrivada, cfg.LlaveKID, nil)
	verificador := erp.NuevoVerificador(cfg.ERPLlavePublica, erp.NuevoAlmacenDeJtiEnBase(q), logger)
	llavero, err := cifrado.NuevoLlavero(cfg.KEKs, cfg.KEKActiva)
	if err != nil {
		return err
	}
	registro := auditoria.Nuevo(pool)

	directorio := entidades.NuevoDirectorio(q, clienteERP, logger)
	// La conciliación de la política de habilitación se pide a lo sumo con la frecuencia del directorio.
	espejo := entidades.NuevoEspejo(pool, clienteERP, cfg.Frecuencias.Cooperativas, logger)

	// La pasarela: la simulada en desarrollo (con su página en /simulada/{requestId}), Placetopay en los demás.
	var pasarelaDePago pasarela.Pasarela
	var verificadorDeSitios pasarela.VerificadorDeCredenciales
	var pasarelaSimulada *simulada.Pasarela
	if cfg.Pasarela == config.PasarelaSimulada {
		pasarelaSimulada = simulada.Nueva(cfg.HostPublico)
		pasarelaSimulada.Esquema = esquemaDe(cfg.HostPublico)
		pasarelaDePago, verificadorDeSitios = pasarelaSimulada, pasarela.VerificadorSimulado{}
	} else {
		ptp := placetopay.Nueva(nil)
		pasarelaDePago, verificadorDeSitios = ptp, placetopay.Verificador{Pasarela: ptp, HostPublico: cfg.HostPublico}
	}
	sitios := pasarela.NuevosSitios(pool, llavero, verificadorDeSitios, registro, cfg.Pasarela == config.PasarelaSimulada, logger)

	workers := river.NewWorkers()
	river.AddWorker(workers, &entidades.TrabajadorDeDirectorio{Directorio: directorio})
	river.AddWorker(workers, &entidades.TrabajadorDeHabilitaciones{Espejo: espejo})
	river.AddWorker(workers, &entidades.TrabajadorDeResincronizacion{Espejo: espejo})
	river.AddWorker(workers, &entidades.TrabajadorDeCooperativa{Espejo: espejo, Directorio: directorio})
	river.AddWorker(workers, &erp.TrabajadorDePurga{Q: q, Logger: logger})
	river.AddWorker(workers, &identidad.TrabajadorDeNovedades{ERP: clienteERP})
	trabajadorDeConsulta := &pagos.TrabajadorDeConsulta{}
	trabajadorDeSonda := &pagos.TrabajadorDeSonda{}
	river.AddWorker(workers, trabajadorDeConsulta)
	river.AddWorker(workers, trabajadorDeSonda)
	clienteTrabajos, err := trabajos.NuevoCliente(pool, trabajos.Opciones{
		Logger:  logger,
		Workers: workers,
		Periodicos: []trabajos.Periodico{
			{ID: "sync_tenants", Intervalo: cfg.Frecuencias.Cooperativas, Args: func() river.JobArgs { return entidades.ArgsSincronizarCooperativas{} }},
			{ID: "pull_enablements", Intervalo: cfg.Frecuencias.Habilitaciones, Args: func() river.JobArgs { return entidades.ArgsArrastrarHabilitaciones{} }},
			// Resincronización completa nocturna (R8): 03:00 hora de Colombia, no en cada arranque.
			{ID: "full_resync", Horario: trabajos.Diario(3, 0, trabajos.HoraDeColombia), Args: func() river.JobArgs { return entidades.ArgsResincronizarHabilitaciones{} }},
			{ID: "purge_service_jti", Intervalo: cfg.Frecuencias.PurgaDeTokens, Args: func() river.JobArgs { return erp.ArgsPurgarJti{} }},
			// Consulta periódica de pendientes (FR-040): cada CANAL_FRECUENCIA_PENDIENTES; la sonda decide cuáles tocan.
			{ID: "probe_pending_attempts", Intervalo: cfg.Frecuencias.Pendientes, Args: func() river.JobArgs { return pagos.ArgsSondearPendientes{} }},
		},
	})
	if err != nil {
		return err
	}
	if err := clienteTrabajos.Start(ctx); err != nil {
		return fmt.Errorf("arrancar los trabajos: %w", err)
	}

	servidor := httpapi.Nuevo(httpapi.Opciones{
		Logger:          logger,
		Estaticos:       web.Archivos(),
		LimitePorMinuto: cfg.Limite.PorMinuto,
		LimiteRafaga:    cfg.Limite.Rafaga,
		Lista:           pool.Ping,
		Interno:         verificador.Middleware,
	})
	servidor.RegistrarInternasDeCooperativas(httpapi.RutasInternasDeCooperativas{
		Encolador:    clienteTrabajos,
		Codigos:      entidades.NuevosCodigos(pool, logger),
		Sitios:       sitios,
		Cooperativas: q,
		Auditoria:    registro,
		Logger:       logger,
	})
	ident, err := identidad.Nuevo(identidad.Opciones{
		Pool:            pool,
		ERP:             clienteERP,
		Mensajero:       mensajeroDe(cfg, logger),
		Auditoria:       registro,
		Encolador:       clienteTrabajos,
		Logger:          logger,
		ClaveDeSenuelos: identidad.ClaveDeSenuelosDe(cfg.KEKs[cfg.KEKActiva]),
		CookiesSeguras:  true,
		RPID:            hostSinPuerto(cfg.HostPublico),
		Origenes:        append([]string{"https://" + cfg.HostPublico}, cfg.OrigenesExtra...),
	})
	if err != nil {
		return err
	}
	ident.Registrar(servidor)
	conceptos.Nuevo(clienteERP, logger).Registrar(servidor, ident.Acotada)
	servicioDePagos, err := pagos.Nuevo(pagos.Opciones{
		Pool:              pool,
		ERP:               clienteERP,
		Sitios:            sitios,
		Pasarela:          pasarelaDePago,
		Codigos:           ident,
		Auditoria:         registro,
		Encolador:         clienteTrabajos,
		Logger:            logger,
		HostPublico:       cfg.HostPublico,
		EsquemaDeRetorno:  esquemaDe(cfg.HostPublico),
		ConsultaFrecuente: cfg.Frecuencias.Pendientes,
	})
	if err != nil {
		return err
	}
	trabajadorDeConsulta.Pagos, trabajadorDeSonda.Pagos = servicioDePagos, servicioDePagos
	servicioDePagos.Registrar(servidor, ident.Acotada)
	if pasarelaSimulada != nil {
		servidor.Manejar("GET /simulada/{requestId}", http.HandlerFunc(pasarelaSimulada.Pagina))
		servidor.Manejar("POST /simulada/{requestId}", http.HandlerFunc(pasarelaSimulada.Pagina))
	}

	servidorHTTP := httpapi.NuevoServidorHTTP(cfg.Direccion, servidor.Handler(), logger)

	errores := make(chan error, 1)
	go func() {
		logger.Info("canal escuchando", "direccion", cfg.Direccion)
		if err := servidorHTTP.ListenAndServe(); err != nil && !errors.Is(err, http.ErrServerClosed) {
			errores <- err
		}
		close(errores)
	}()

	select {
	case <-ctx.Done():
		logger.Info("señal de apagado recibida")
	case err := <-errores:
		if err != nil {
			return fmt.Errorf("el servidor HTTP se detuvo: %w", err)
		}
	}

	apagado, cancelar := context.WithTimeout(context.Background(), 20*time.Second)
	defer cancelar()
	if err := servidorHTTP.Shutdown(apagado); err != nil {
		logger.Error("apagado del servidor HTTP con error", "error", err)
	}
	if err := clienteTrabajos.Stop(apagado); err != nil {
		logger.Error("apagado de los trabajos con error", "error", err)
	}
	logger.Info("canal detenido")
	return nil
}

// mensajeroDe arma la mensajería: correo por SMTP si está configurado; sin SMTP y con la pasarela simulada (desarrollo),
// a la consola; sin SMTP en un ambiente real, el correo no está disponible y los códigos no salen (se registra). SMS no
// tiene proveedor todavía (FR-011a).
func mensajeroDe(cfg *config.Config, logger *slog.Logger) mensajeria.Compuesto {
	switch {
	case cfg.SMTP != nil:
		return mensajeria.Compuesto{PorCorreo: mensajeria.NuevoSMTP(mensajeria.ConfigSMTP{
			Host: cfg.SMTP.Host, Puerto: cfg.SMTP.Puerto, Usuario: cfg.SMTP.Usuario, Clave: cfg.SMTP.Clave, Remitente: cfg.SMTP.Remitente,
		})}
	case cfg.Pasarela == config.PasarelaSimulada:
		logger.Warn("sin SMTP: los códigos se escriben en el log (sólo desarrollo)")
		return mensajeria.Compuesto{PorCorreo: mensajeria.Consola{Logger: logger}}
	default:
		logger.Error("sin SMTP en un ambiente con pasarela real: los códigos de activación e ingreso no se pueden enviar")
		return mensajeria.Compuesto{}
	}
}

// esquemaDe: http sólo para el propio equipo (desarrollo local); todo lo demás va por https.
func esquemaDe(host string) string {
	h := hostSinPuerto(host)
	if h == "localhost" || h == "127.0.0.1" {
		return "http"
	}
	return "https"
}

func hostSinPuerto(host string) string {
	if h, _, err := net.SplitHostPort(host); err == nil {
		return h
	}
	return host
}
