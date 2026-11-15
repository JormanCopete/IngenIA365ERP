package httpapi

import (
	"sync"
	"time"
)

// Limitador es una cubeta de fichas por clave («ip:…», y en C1 «doc:…» para activación, ingreso y recuperación).
//
// Vive en memoria de cada réplica: con dos réplicas el límite efectivo es hasta el doble. Para la IP basta; el
// límite por documento de C1 (R13) necesita ser compartido y se hará sobre la base del canal.
type Limitador struct {
	mu             sync.Mutex
	tasa           float64 // fichas por segundo
	rafaga         float64
	cubetas        map[string]*cubeta
	ahora          func() time.Time
	ultimaLimpieza time.Time
}

type cubeta struct {
	fichas float64
	ultima time.Time
}

// maximoDeCubetas acota la memoria: al pasarlo se limpia antes de crear otra.
const maximoDeCubetas = 100_000

// NuevoLimitador admite porMinuto peticiones sostenidas por clave con ráfagas de hasta rafaga.
func NuevoLimitador(porMinuto, rafaga int) *Limitador {
	if porMinuto < 1 {
		porMinuto = 1
	}
	if rafaga < 1 {
		rafaga = 1
	}
	return &Limitador{
		tasa:    float64(porMinuto) / 60,
		rafaga:  float64(rafaga),
		cubetas: map[string]*cubeta{},
		ahora:   time.Now,
	}
}

// Permitir gasta una ficha de la clave. Si no hay, dice cuánto esperar para la siguiente.
func (l *Limitador) Permitir(clave string) (bool, time.Duration) {
	l.mu.Lock()
	defer l.mu.Unlock()

	ahora := l.ahora()
	desde := ahora.Sub(l.ultimaLimpieza)
	if desde > time.Minute || (len(l.cubetas) >= maximoDeCubetas && desde > time.Second) {
		l.limpiar(ahora)
	}

	c, ok := l.cubetas[clave]
	if !ok {
		c = &cubeta{fichas: l.rafaga, ultima: ahora}
		l.cubetas[clave] = c
	} else {
		c.fichas = min(l.rafaga, c.fichas+ahora.Sub(c.ultima).Seconds()*l.tasa)
		c.ultima = ahora
	}
	if c.fichas >= 1 {
		c.fichas--
		return true, 0
	}
	falta := (1 - c.fichas) / l.tasa
	return false, time.Duration(falta * float64(time.Second))
}

// limpiar quita las cubetas que ya se habrían llenado: equivalen a una nueva.
func (l *Limitador) limpiar(ahora time.Time) {
	llenado := time.Duration(l.rafaga / l.tasa * float64(time.Second))
	for k, c := range l.cubetas {
		if ahora.Sub(c.ultima) >= llenado {
			delete(l.cubetas, k)
		}
	}
	l.ultimaLimpieza = ahora
}
