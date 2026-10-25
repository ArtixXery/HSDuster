"""Genera reglas.json para el plugin RomperCartas de HDT.
Contiene lo que no cambia con cada sobre: tipo de set por carta, copias irrompibles, versiones del Básico,
Dioses Antiguos y cartas vistas en mazos de Leyenda (lista manual + mazos de HSGuru).
uso: python generar_reglas.py [salida] [mazos_hsguru.json ...]
Por defecto escribe en %APPDATA%\HearthstoneDeckTracker\RomperCartaseglas.json"""
import json, sys, re, os, datetime
from hearthstone import cardxml, utils
from irrompibles_wiki import REGULAR as WIKI_N, GOLDEN as WIKI_G
sys.stdout.reconfigure(encoding='utf-8')
OUT = sys.argv[1] if len(sys.argv) > 1 and sys.argv[1] else os.path.expandvars(r'%APPDATA%\HearthstoneDeckTracker\RomperCartas\reglas.json')

db, _ = cardxml.load_dbf()
STD = {s.name for s in utils.STANDARD_SETS[max(utils.STANDARD_SETS)]}
CRAFT = {s.name for s in type(next(iter(db.values())).card_set) if s.craftable} | {'EXPERT1', 'STORMWIND', 'REVENDRETH', 'YEAR_OF_THE_DRAGON'}
CRAFT -= STD
RAREZAS = {'COMMON', 'RARE', 'EPIC', 'LEGENDARY'}
OG_RE = re.compile(r"C'Thun|N'Zoth|Yogg|Y'Shaarj|Corrupt", re.I)
core_names = {c.name for c in db.values() if c.collectible and c.card_set.name == 'CORE'}

# Cartas vistas en mazos de Leyenda de Wild (revisión manual, octubre de 2026)
LEYENDA = {'Archmage Antonidas','Astalor Bloodsworn','Blademaster Okani','E.T.C., Band Manager','E.T.C., God of Metal','Edwin, Defias Kingpin',
  'Exarch Maladaar','Final Showdown','Kazakus, Golem Shaper','Kazakusan','Kerrigan, Queen of Blades','Loatheb','Magatha, Bane of Music',
  'Patches the Pilot','Patches the Pirate','Prince Renathal','Raza the Chained','Reno Jackson','Reno, Lone Ranger','Reska, the Pit Boss',
  'Seek Guidance','Shadow of Demise','Sir Finley, Sea Guide','The Ceaseless Expanse','Theotar, the Mad Duke','Thunderbringer','Tickatus',
  'Timewinder Zarimi','Yogg-Saron, Master of Fate','Zephrys the Great','Zilliax Deluxe 3000',
  'Brawl','Darkglare','Dirty Rat','Doomsayer','Elwynn Boar','Embiggen','Felosophy','Force of Nature','Glowstone Technician','Ice Block',
  'Libram of Hope','Mecha-Leaper','Murloc Warleader','Playhouse Giant','Potion of Illusion','Preparation','Psychic Scream','Razorscale',
  'Sea Giant','Secret Passage','Shadow Visions','Shield Slam','Smothering Starfish','Southsea Captain'}
# Mazos de Leyenda de HSGuru extraídos con hsguru_decks.py (páginas guardadas a mano)
mazos_hsguru = 0
for f in sys.argv[2:]:
    for mazo in json.load(open(f, encoding='utf-8')):
        LEYENDA.update(mazo['cartas']); mazos_hsguru += 1

def grant(s):
    s = re.sub('<[^>]+>', '', s or '')
    return bool(s) and 'crafting' not in s.lower() and 'adventure' not in s.lower()

r = dict(estandar=[], wild=[], basico=[], irrompible_normal=[], irrompible_dorada=[], dioses_antiguos=[])
for c in db.values():
    if not c.collectible or c.rarity.name not in RAREZAS: continue
    s = c.card_set.name
    if s in ('CORE', 'EVENT', 'VANILLA'): continue
    if s in STD: r['estandar'].append(c.dbf_id)
    elif s in CRAFT: r['wild'].append(c.dbf_id)
    else: continue
    if c.name in core_names: r['basico'].append(c.dbf_id)
    if c.id in WIKI_N or s == 'DEMON_HUNTER_INITIATE' or grant(c.how_to_earn): r['irrompible_normal'].append(c.dbf_id)
    if c.id in WIKI_G or grant(c.how_to_earn_golden): r['irrompible_dorada'].append(c.dbf_id)
    if OG_RE.search(c.name) or OG_RE.search(c.description or ''): r['dioses_antiguos'].append(c.dbf_id)
r = {k: sorted(v) for k, v in r.items()}
r['leyenda'] = sorted(LEYENDA)
r['generado'] = datetime.date.today().isoformat()
r['fuentes'] = dict(base_cartas='hearthstone_data', mazos_hsguru=mazos_hsguru, wiki_irrompibles='hearthstone.wiki.gg/wiki/Uncraftable')
os.makedirs(os.path.dirname(OUT), exist_ok=True)
json.dump(r, open(OUT, 'w', encoding='utf-8'), ensure_ascii=False)
print(OUT, {k: len(v) for k, v in r.items() if isinstance(v, list)}, 'mazos de HSGuru:', mazos_hsguru)
