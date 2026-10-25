"""Actualiza los datos que usa el plugin RomperCartas de HDT.
- Estadísticas gratis de HSReplay (Wild, Bronce–Oro): uso por carta (14 días y expansión) y lista de mazos.
- reglas.json, como mucho una vez por semana, con el último HSGuru guardado a mano (mazos_hsguru.json).
Usa Firefox headless con un perfil limpio (sin tu sesión). HDT lo lanza solo; también se puede correr a mano.
uso: python actualizar_datos.py [--forzar]"""
import sys, os, time, json, datetime, subprocess
sys.stdout.reconfigure(encoding='utf-8')
AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
from headless import driver

CARPETA = os.path.expandvars(r'%APPDATA%\HearthstoneDeckTracker\RomperCartas')
HISTORIAL = os.path.join(CARPETA, 'historial')
BASE = '/analytics/query/'
CONSULTAS = {
    'uso_14dias.json': BASE + 'card_list_free/?GameType=RANKED_WILD&TimeRange=LAST_14_DAYS&LeagueRankRange=BRONZE_THROUGH_GOLD',
    'uso_expansion.json': BASE + 'card_list_free/?GameType=RANKED_WILD&TimeRange=CURRENT_EXPANSION&LeagueRankRange=BRONZE_THROUGH_GOLD',
    'mazos_expansion.json': BASE + 'list_decks_by_win_rate_v2/?GameType=RANKED_WILD&LeagueRankRange=BRONZE_THROUGH_GOLD&Region=ALL&TimeRange=CURRENT_EXPANSION',
}
FORZAR = '--forzar' in sys.argv

def edad_dias(ruta):
    return (time.time() - os.path.getmtime(ruta)) / 86400 if os.path.exists(ruta) else 1e9

def log(msg):
    linea = f'{datetime.datetime.now():%Y-%m-%d %H:%M:%S} [actualizar] {msg}'
    print(linea)
    with open(os.path.join(CARPETA, 'log.txt'), 'a', encoding='utf-8') as f: f.write(linea + '\n')

def hsreplay():
    d = driver()
    try:
        for intento in range(4):
            try: d.get('https://hsreplay.net/'); break
            except Exception as e: log(f'reintento {intento + 1}: {e.__class__.__name__}'); time.sleep(15)
        time.sleep(4)
        d.set_script_timeout(90)
        hoy = datetime.date.today().isoformat()
        os.makedirs(os.path.join(HISTORIAL, hoy), exist_ok=True)
        for archivo, ruta in CONSULTAS.items():
            st, texto = d.execute_async_script(
                "const cb=arguments[1];fetch(arguments[0]).then(r=>r.text().then(t=>cb([r.status,t]))).catch(e=>cb([0,String(e)]))", ruta)
            if st != 200 or not texto.lstrip().startswith('{'):
                log(f'{archivo}: HTTP {st}'); continue
            for destino in (os.path.join(CARPETA, archivo), os.path.join(HISTORIAL, hoy, archivo)):
                with open(destino + '.tmp', 'w', encoding='utf-8') as f: f.write(texto)
                os.replace(destino + '.tmp', destino)
            log(f'{archivo}: ok ({len(texto) // 1024} KB)')
    finally:
        d.quit()

def reglas():
    """Regenera reglas.json con la base de cartas actual y el último HSGuru guardado (mazos_hsguru.json)."""
    extra = [os.path.join(CARPETA, 'mazos_hsguru.json')] if os.path.exists(os.path.join(CARPETA, 'mazos_hsguru.json')) else []
    r = subprocess.run([sys.executable, os.path.join(AQUI, 'generar_reglas.py'), os.path.join(CARPETA, 'reglas.json')] + extra,
                       cwd=AQUI, capture_output=True, text=True, encoding='utf-8')
    log('reglas: ' + (r.stdout.strip() or r.stderr.strip()[-300:]))

if __name__ == '__main__':
    os.makedirs(CARPETA, exist_ok=True)
    if FORZAR or min(edad_dias(os.path.join(CARPETA, a)) for a in CONSULTAS) > 0.9:
        hsreplay()
    else:
        log('estadísticas de HSReplay al día, no se descargan')
    if FORZAR or edad_dias(os.path.join(CARPETA, 'reglas.json')) > 7:
        reglas()
