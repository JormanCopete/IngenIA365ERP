// Genera los íconos genéricos del canal en public/ (sin marca de ninguna cooperativa ni empresa): una tarjeta
// blanca sobre el color primario por defecto de src/marca/tokens.css. Sin dependencias: rasteriza con
// supermuestreo y escribe PNG con zlib.
//
//   node scripts/generar-iconos.mjs
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { deflateSync } from 'node:zlib'

const raiz = join(dirname(fileURLToPath(import.meta.url)), '..')
const tokens = readFileSync(join(raiz, 'src/marca/tokens.css'), 'utf8')
const primario = /--color-primario:\s*(#[0-9a-fA-F]{6})/.exec(tokens)?.[1]
if (!primario) throw new Error('No se encontró --color-primario en src/marca/tokens.css.')

const FONDO = hexARgb(primario)
const BLANCO = [255, 255, 255]

function hexARgb(hex) {
  return [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16))
}

// Geometría en un lienzo de 512 × 512. La tarjeta cabe en el círculo seguro de los íconos «maskable» (radio 40 %).
const TARJETA = { x: 136, y: 176, ancho: 240, alto: 160, radio: 20 }
const BANDA = { x: 136, y: 212, ancho: 240, alto: 30, radio: 0 }
const CHIP = { x: 168, y: 272, ancho: 52, alto: 36, radio: 6 }

function dentroDeRectRedondeado(px, py, { x, y, ancho, alto, radio }) {
  if (px < x || px > x + ancho || py < y || py > y + alto) return false
  const cx = Math.min(Math.max(px, x + radio), x + ancho - radio)
  const cy = Math.min(Math.max(py, y + radio), y + alto - radio)
  return (px - cx) ** 2 + (py - cy) ** 2 <= radio ** 2
}

/** Color (o null = transparente) de un punto del lienzo de 512. */
function colorEn(px, py, conFondoCompleto) {
  const fondo = { x: 0, y: 0, ancho: 512, alto: 512, radio: conFondoCompleto ? 0 : 96 }
  if (!dentroDeRectRedondeado(px, py, fondo)) return null
  if (dentroDeRectRedondeado(px, py, CHIP) || dentroDeRectRedondeado(px, py, BANDA)) return FONDO
  if (dentroDeRectRedondeado(px, py, TARJETA)) return BLANCO
  return FONDO
}

function rasterizar(lado, conFondoCompleto) {
  const muestras = 4
  const escala = 512 / lado
  const pixeles = Buffer.alloc(lado * lado * 4)
  for (let y = 0; y < lado; y++) {
    for (let x = 0; x < lado; x++) {
      let r = 0, g = 0, b = 0, a = 0
      for (let sy = 0; sy < muestras; sy++) {
        for (let sx = 0; sx < muestras; sx++) {
          const color = colorEn((x + (sx + 0.5) / muestras) * escala, (y + (sy + 0.5) / muestras) * escala, conFondoCompleto)
          if (!color) continue
          r += color[0]
          g += color[1]
          b += color[2]
          a += 1
        }
      }
      const i = (y * lado + x) * 4
      pixeles[i] = a ? Math.round(r / a) : 0
      pixeles[i + 1] = a ? Math.round(g / a) : 0
      pixeles[i + 2] = a ? Math.round(b / a) : 0
      pixeles[i + 3] = Math.round((a / (muestras * muestras)) * 255)
    }
  }
  return pixeles
}

const TABLA_CRC = Array.from({ length: 256 }, (_, n) => {
  let c = n
  for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1
  return c >>> 0
})

function crc32(buffer) {
  let c = 0xffffffff
  for (const byte of buffer) c = TABLA_CRC[(c ^ byte) & 0xff] ^ (c >>> 8)
  return (c ^ 0xffffffff) >>> 0
}

function bloque(tipo, datos) {
  const largo = Buffer.alloc(4)
  largo.writeUInt32BE(datos.length)
  const tipoYDatos = Buffer.concat([Buffer.from(tipo, 'ascii'), datos])
  const crc = Buffer.alloc(4)
  crc.writeUInt32BE(crc32(tipoYDatos))
  return Buffer.concat([largo, tipoYDatos, crc])
}

function png(lado, pixeles) {
  const cabecera = Buffer.alloc(13)
  cabecera.writeUInt32BE(lado, 0)
  cabecera.writeUInt32BE(lado, 4)
  cabecera[8] = 8 // bits por canal
  cabecera[9] = 6 // RGBA
  const filas = Buffer.alloc(lado * (lado * 4 + 1))
  for (let y = 0; y < lado; y++) {
    filas[y * (lado * 4 + 1)] = 0 // sin filtro
    pixeles.copy(filas, y * (lado * 4 + 1) + 1, y * lado * 4, (y + 1) * lado * 4)
  }
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    bloque('IHDR', cabecera),
    bloque('IDAT', deflateSync(filas, { level: 9 })),
    bloque('IEND', Buffer.alloc(0)),
  ])
}

const destino = join(raiz, 'public/iconos')
mkdirSync(destino, { recursive: true })

const iconos = [
  { archivo: 'icono-192.png', lado: 192, completo: false },
  { archivo: 'icono-512.png', lado: 512, completo: false },
  { archivo: 'icono-mascara-512.png', lado: 512, completo: true },
  { archivo: 'apple-touch-icon-180.png', lado: 180, completo: true },
]
for (const { archivo, lado, completo } of iconos) {
  writeFileSync(join(destino, archivo), png(lado, rasterizar(lado, completo)))
  console.log(`public/iconos/${archivo}`)
}

const rect = ({ x, y, ancho, alto, radio }, color) =>
  `<rect x="${x}" y="${y}" width="${ancho}" height="${alto}" rx="${radio}" fill="${color}"/>`
const svg = [
  '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512">',
  rect({ x: 0, y: 0, ancho: 512, alto: 512, radio: 96 }, primario),
  rect(TARJETA, '#ffffff'),
  rect(BANDA, primario),
  rect(CHIP, primario),
  '</svg>',
  '',
].join('\n')
writeFileSync(join(raiz, 'public/favicon.svg'), svg)
console.log('public/favicon.svg')
