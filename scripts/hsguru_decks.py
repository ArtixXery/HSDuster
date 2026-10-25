"""Extrae los mazos de una página de mazos de HSGuru guardada a mano (Ctrl+S) y los guarda en JSON.
uso: python hsguru_decks.py "Wild Decks.htm" salida.json"""
import re, sys, html, json
from hearthstone import cardxml, deckstrings
sys.stdout.reconfigure(encoding='utf-8')
t = open(sys.argv[1], encoding='utf-8', errors='replace').read()
db, _ = cardxml.load_dbf()
bloques = t.split('class="decklist-info')[1:]
mazos = []
for b in bloques:
    m = re.search(r'data-clipboard-text="###\s*(.*?)\s+(AAE[A-Za-z0-9+/=]+)', b)
    if not m: continue
    nombre, codigo = html.unescape(m.group(1)), m.group(2)
    texto = html.unescape(re.sub(r'<[^>]+>', ' ', re.sub(r'<script.*?</script>|<style.*?</style>', '', b, flags=re.S)))
    g = re.search(r'([\d.]+)\s+Games:\s*([\d,]+)', texto)
    try:
        deck = deckstrings.Deck.from_deckstring(codigo)
        cartas = [db[i].name for i, _ in deck.cards if i in db] + [db[i].name for i, _, _ in (deck.sideboards or []) if i in db]
    except Exception:
        cartas = []
    mazos.append(dict(nombre=nombre, codigo=codigo, winrate=float(g.group(1)) if g else None,
                      partidas=int(g.group(2).replace(',', '')) if g else None, cartas=cartas))
json.dump(mazos, open(sys.argv[2], 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
for x in mazos: print(f"{x['nombre']:<32} {x['winrate']}% {x['partidas']} partidas, {len(x['cartas'])} cartas")
