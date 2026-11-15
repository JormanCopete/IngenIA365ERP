package pagos

import (
	"fmt"
	"strconv"
	"strings"
	"time"
)

// T089 (research R12, FR-035): la referencia de un intento es {código de la cooperativa (3)}{AAMMDD}{secuencia en base
// 36 (8)}{dígito de control}: 18 caracteres alfanuméricos en mayúscula, cortos para dictarlos por teléfono. La
// secuencia sale de payment_reference_seq, que nunca se reinicia, así que la referencia nunca se repite. El dígito de
// control es Luhn módulo 36 sobre los 17 anteriores: detecta un carácter mal dictado y casi toda transposición.

const alfabeto36 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"

// LargoDeReferencia es el largo fijo de una referencia.
const LargoDeReferencia = 18

// MaximaSecuencia es la mayor secuencia que cabe en 8 caracteres base 36.
const MaximaSecuencia = int64(2821109907455) // 36^8 - 1

// Referencia arma la referencia de un intento. fecha va en hora de Colombia.
func Referencia(codigo string, fecha time.Time, secuencia int64) (string, error) {
	if len(codigo) != 3 || strings.ToUpper(codigo) != codigo || strings.ContainsAny(codigo, "0123456789") {
		return "", fmt.Errorf("pagos: código de canal inválido %q", codigo)
	}
	if secuencia < 0 || secuencia > MaximaSecuencia {
		return "", fmt.Errorf("pagos: secuencia de referencia fuera de rango: %d", secuencia)
	}
	base := strings.ToUpper(strconv.FormatInt(secuencia, 36))
	cuerpo := codigo + fecha.Format("060102") + strings.Repeat("0", 8-len(base)) + base
	return cuerpo + string(DigitoDeControl(cuerpo)), nil
}

// DigitoDeControl calcula Luhn módulo 36 sobre una cadena de 0-9A-Z.
func DigitoDeControl(cuerpo string) byte {
	const n = 36
	factor, suma := 2, 0
	for i := len(cuerpo) - 1; i >= 0; i-- {
		sumando := factor * strings.IndexByte(alfabeto36, cuerpo[i])
		factor = 3 - factor
		suma += sumando/n + sumando%n
	}
	return alfabeto36[(n-suma%n)%n]
}

// ReferenciaValida comprueba el formato y el dígito de control (la dictada por teléfono, la de un webhook).
func ReferenciaValida(ref string) bool {
	if len(ref) != LargoDeReferencia {
		return false
	}
	for i := 0; i < LargoDeReferencia; i++ {
		c := ref[i]
		switch {
		case i < 3 && (c < 'A' || c > 'Z'):
			return false
		case i >= 3 && i < 9 && (c < '0' || c > '9'):
			return false
		case i >= 9 && strings.IndexByte(alfabeto36, c) < 0:
			return false
		}
	}
	return DigitoDeControl(ref[:LargoDeReferencia-1]) == ref[LargoDeReferencia-1]
}
