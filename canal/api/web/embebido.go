//go:build embebido

// Package web entrega el build de la PWA (canal/web) embebido en el binario. El Dockerfile copia canal/web/dist a
// canal/api/web/dist y compila con -tags embebido.
package web

import (
	"embed"
	"io/fs"
)

//go:embed all:dist
var dist embed.FS

// Archivos devuelve el build de la PWA con dist como raíz.
func Archivos() fs.FS {
	sub, err := fs.Sub(dist, "dist")
	if err != nil {
		panic("web: el build embebido no tiene la carpeta dist: " + err.Error())
	}
	return sub
}
