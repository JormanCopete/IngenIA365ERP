# Feature 013, T0364: hoja de los casos dorados del puntaje (decimal de 40 digitos). Correrla imprime los puntos, aportes y totales
# de cada escenario; el puntaje es la suma de los aportes redondeados a cuatro decimales (mitad hacia arriba).
from decimal import Decimal as D, getcontext, ROUND_HALF_UP
getcontext().prec = 40
def puntos(rangos, v):
    if v is None:
        for r in rangos:
            if r.get('sinDato'): return D(r['puntos']), 'sin dato'
        return D(0), 'sin dato (0)'
    for r in rangos:
        if r.get('sinDato'): continue
        f, t = r.get('desde'), r.get('hasta')
        if (f is None or v >= D(f)) and (t is None or v < D(t)): return D(r['puntos']), 'rango'
    return D(0), 'fuera'
def calc(vars_, valores):
    total = D(0)
    for v in vars_:
        p, _ = puntos(v['rangos'], None if valores.get(v['fuente']) is None else D(valores[v['fuente']]))
        a = D(v['peso']) * p
        print('  ', v['codigo'], p, a)
        total += a
    print(' total', total, '->', total.quantize(D('0.0001'), rounding=ROUND_HALF_UP))
base = [
 {'codigo':'ANTIG','fuente':'Applicant.AssociateSeniorityMonths','peso':'0.4','rangos':[{'desde':'0','hasta':'12','puntos':'300'},{'desde':'12','hasta':'36','puntos':'600'},{'desde':'36','puntos':'900'}]},
 {'codigo':'RELCI','fuente':'Application.PaymentCapacityRatio','peso':'0.4','rangos':[{'desde':'0','hasta':'0.3','puntos':'900'},{'desde':'0.3','hasta':'0.5','puntos':'600'},{'desde':'0.5','puntos':'200'}]},
 {'codigo':'CENTRAL','fuente':'Bureau.Score','peso':'0.2','rangos':[{'desde':'0','hasta':'500','puntos':'200'},{'desde':'500','hasta':'700','puntos':'500'},{'desde':'700','puntos':'800'}]},
]
A='Applicant.AssociateSeniorityMonths'; R='Application.PaymentCapacityRatio'; C='Bureau.Score'
for vals in [{A:30,R:'0.25',C:720},{A:40,R:'0.45',C:650},{A:6,R:'0.6',C:450},{A:12,R:'0.45',C:500},{A:36,R:'0.5',C:700},{A:12,R:'0.3',C:700},{A:11,R:'0.2999',C:'699.9999'},{A:-1,R:'0.3',C:700}]:
    print(vals); calc(base, vals)
sd = [dict(base[0]), dict(base[1]), dict(base[2])]
sd[2] = dict(base[2]); sd[2]['rangos'] = base[2]['rangos'] + [{'sinDato':True,'puntos':'400'}]
for vals in [{A:30,R:'0.25',C:None},{A:30,R:None,C:None}]:
    print('SD', vals); calc(sd, vals)
esc = [
 {'codigo':'APORT','fuente':'Applicant.ContributionsBalance','peso':'0.333333','rangos':[{'desde':'0','hasta':'1000000','puntos':'333.3333'},{'desde':'1000000','puntos':'1000'}]},
 {'codigo':'EDAD','fuente':'Applicant.Age','peso':'0.333333','rangos':[{'desde':'18','hasta':'25','puntos':'412.5'},{'desde':'25','hasta':'60','puntos':'755.5555'},{'desde':'60','puntos':'600.0001'}]},
 {'codigo':'MORA','fuente':'Internal.MaxDaysPastDue12M','peso':'0.333334','rangos':[{'desde':'0','hasta':'1','puntos':'987.6543'},{'desde':'1','hasta':'31','puntos':'500'},{'desde':'31','puntos':'0'}]},
]
for vals in [{'Applicant.ContributionsBalance':2500000,'Applicant.Age':40,'Internal.MaxDaysPastDue12M':0},{'Applicant.ContributionsBalance':500000,'Applicant.Age':22,'Internal.MaxDaysPastDue12M':15}]:
    print('ESC', vals); calc(esc, vals)
