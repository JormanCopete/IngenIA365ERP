package mensajeria

import "fmt"

// Proposito de un código de un solo uso; decide el texto.
type Proposito string

const (
	ParaActivar          Proposito = "activation"
	ParaDispositivoNuevo Proposito = "new_device"
	ParaRecuperar        Proposito = "recovery"
	ParaCambiarContacto  Proposito = "contact_change"
)

// MensajeDeCodigo arma el mensaje del código. cooperativa es el nombre que la persona reconoce; el texto no nombra
// ninguna otra marca y no lleva enlaces (un código nunca se pide por enlace).
func MensajeDeCodigo(canal Canal, destino, cooperativa string, p Proposito, codigo string, minutos int) Mensaje {
	accion := map[Proposito]string{
		ParaActivar:          "activar tu cuenta",
		ParaDispositivoNuevo: "ingresar desde un dispositivo nuevo",
		ParaRecuperar:        "recuperar tu clave",
		ParaCambiarContacto:  "confirmar tu nuevo dato de contacto",
	}[p]
	if canal == SMS {
		return Mensaje{Canal: SMS, Destino: destino,
			Texto: fmt.Sprintf("%s: tu código para %s es %s. Vence en %d min. No lo compartas.", cooperativa, accion, codigo, minutos)}
	}
	return Mensaje{
		Canal:   Correo,
		Destino: destino,
		Asunto:  fmt.Sprintf("Tu código para %s", accion),
		Texto: fmt.Sprintf("Hola:\n\nTu código para %s en el canal de asociados de %s es:\n\n    %s\n\n"+
			"Vence en %d minutos. Nadie de la cooperativa te lo va a pedir: no lo compartas.\n"+
			"Si no fuiste tú, ignora este mensaje; tu cuenta sigue igual.\n", accion, cooperativa, codigo, minutos),
	}
}

// AvisoDeBloqueo: la cuenta se bloqueó por intentos fallidos (FR-022).
func AvisoDeBloqueo(destino string, minutos int) Mensaje {
	return Mensaje{Canal: Correo, Destino: destino, Asunto: "Bloqueamos el ingreso a tu cuenta por unos minutos",
		Texto: fmt.Sprintf("Hola:\n\nHubo varios intentos fallidos de ingresar a tu cuenta del canal de asociados, así que "+
			"bloqueamos el ingreso por %d minutos.\n\nSi fuiste tú, espera y vuelve a intentarlo, o recupera tu clave. "+
			"Si no fuiste tú, cambia tu clave apenas puedas ingresar.\n", minutos)}
}

// AvisoDeClaveCambiada: se cambió o recuperó la clave.
func AvisoDeClaveCambiada(destino string) Mensaje {
	return Mensaje{Canal: Correo, Destino: destino, Asunto: "Cambiaste la clave de tu cuenta",
		Texto: "Hola:\n\nLa clave de tu cuenta del canal de asociados acaba de cambiar y cerramos tus otras sesiones.\n\n" +
			"Si no fuiste tú, recupera tu clave enseguida y comunícate con tu cooperativa.\n"}
}

// AvisoDeContactoCambiado: se avisa al dato anterior (FR-024).
func AvisoDeContactoCambiado(canal Canal, destino, dato string) Mensaje {
	texto := fmt.Sprintf("Hola:\n\nEl %s de tu cuenta del canal de asociados acaba de cambiar. Desde ahora los avisos y "+
		"códigos llegan al nuevo.\n\nSi no fuiste tú, recupera tu clave enseguida y comunícate con tu cooperativa.\n", dato)
	if canal == SMS {
		texto = fmt.Sprintf("El %s de tu cuenta del canal de asociados cambió. Si no fuiste tú, comunícate con tu cooperativa.", dato)
	}
	return Mensaje{Canal: canal, Destino: destino, Asunto: "Cambiaste un dato de contacto", Texto: texto}
}
