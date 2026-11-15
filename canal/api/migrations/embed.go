// Package migrations: migraciones goose de la base del canal (ingenia365_canal), embebidas en el binario.
// Cada archivo lleva Up y Down. Las tablas de River no están aquí: las crea rivermigrate (ver internal/trabajos).
package migrations

import "embed"

// FS contiene los archivos .sql de las migraciones.
//
//go:embed *.sql
var FS embed.FS
