// Comando canal: servicio del canal digital del asociado (sistema satélite del ERP, feature 021).
package main

import (
	"context"
	"errors"
	"log/slog"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"
)

func main() {
	logger := slog.New(slog.NewJSONHandler(os.Stdout, nil))
	slog.SetDefault(logger)

	mux := http.NewServeMux()
	mux.HandleFunc("GET /salud", func(w http.ResponseWriter, _ *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		_, _ = w.Write([]byte(`{"estado":"ok"}`))
	})

	direccion := os.Getenv("CANAL_DIRECCION")
	if direccion == "" {
		direccion = ":8080"
	}
	servidor := &http.Server{Addr: direccion, Handler: mux, ReadHeaderTimeout: 10 * time.Second}

	ctx, detener := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer detener()

	go func() {
		slog.Info("canal escuchando", "direccion", direccion)
		if err := servidor.ListenAndServe(); err != nil && !errors.Is(err, http.ErrServerClosed) {
			slog.Error("el servidor se detuvo", "error", err)
			os.Exit(1)
		}
	}()

	<-ctx.Done()
	apagado, cancelar := context.WithTimeout(context.Background(), 15*time.Second)
	defer cancelar()
	if err := servidor.Shutdown(apagado); err != nil {
		slog.Error("apagado con error", "error", err)
	}
}
