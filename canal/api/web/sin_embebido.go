//go:build !embebido

// Package web entrega el build de la PWA. Sin la etiqueta embebido (desarrollo, pruebas) no hay build: la PWA la
// sirve Vite (npm run dev) y el servidor responde la ruta raíz sin estáticos.
package web

import "io/fs"

// Archivos devuelve nil: el binario se compiló sin la PWA.
func Archivos() fs.FS { return nil }
