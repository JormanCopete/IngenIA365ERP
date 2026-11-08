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
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"

	"github.com/riverqueue/river"

	"github.com/jormancopete/ingenia365erp/canal/api/internal/auditoria"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/config"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/db/gen"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/entidades"
	"github.com/jormancopete/ingenia365erp/canal/api/internal/erp"
	httpapi "github.com/jormancopete/ingenia365erp/canal/api/internal/http"
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

	workers := river.NewWorkers()
	river.AddWorker(workers, &entidades.TrabajadorDeDirectorio{Directorio: entidades.NuevoDirectorio(q, clienteERP, logger)})
	river.AddWorker(workers, &erp.TrabajadorDePurga{Q: q, Logger: logger})
	clienteTrabajos, err := trabajos.NuevoCliente(pool, trabajos.Opciones{
		Logger:  logger,
		Workers: workers,
		Periodicos: []trabajos.Periodico{
			{ID: "sync_tenants", Intervalo: cfg.Frecuencias.Cooperativas, Args: func() river.JobArgs { return entidades.ArgsSincronizarCooperativas{} }},
			{ID: "purge_service_jti", Intervalo: cfg.Frecuencias.PurgaDeTokens, Args: func() river.JobArgs { return erp.ArgsPurgarJti{} }},
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
