# Romper Cartas (Wild) — plugin para Hearthstone Deck Tracker

Plugin para [Hearthstone Deck Tracker](https://hsreplay.net/downloads/) (HDT) que pone un letrero encima de cada carta de tu colección, dentro del juego, diciendo si es seguro romperla (desencantarla) sin perder cartas útiles en el formato **Salvaje (Wild)**.

*English summary at the end.*

## Qué hace

Al entrar a la colección de Hearthstone, cada carta de la página muestra un letrero:

| Letrero | Significado |
|---|---|
| **ROMPER +polvo** (verde) | Carta solo de Wild con muy poco uso. Muestra su % de uso y la clase donde más se juega. |
| **SOBRA N +polvo** (azul) | Copias por encima del máximo jugable (1 legendaria, 2 el resto). Polvo libre. |
| **ROMPER VIEJA +polvo** (turquesa) | Tienes gratis la versión del set Básico; la copia vieja sobra. |
| **DUDOSA · esperar** (naranja) | Uso medio, se juega en Leyenda o tiene que ver con los Dioses Antiguos. Mejor esperar. |
| **CONSERVAR** (rojo) | Se usa en mazos actuales de Wild. |
| **NO SE PUEDE ROMPER** (gris) | Copia regalada (Pase de Taberna, pista de recompensas, legendarias automáticas, logros, Galakrond, Cazador de Demonios inicial). El juego no deja desencantarla. |

Además:

- Se recalcula solo cuando cambia tu colección (al abrir sobres, crear o romper cartas). La colección se lee de HDT, no hace falta exportar nada.
- Descarga las estadísticas de uso de Wild una vez al día, en segundo plano.
- Desde el menú del plugin en HDT se abre una **lista completa** con filtros por estado, clase, maná y búsqueda, y se puede exportar a CSV.

## Cómo decide

Para cada carta solo de Wild se toma su **% de mazos en que aparece** según [HSReplay](https://hsreplay.net). Se usa el máximo entre el global y cada clase, de los últimos 14 días y de la expansión actual, sumando todas sus versiones.

- **Conservar**: 5% o más, o aparece en algún mazo de la lista de mazos de la expansión actual.
- **Dudosa**: entre 2% y 5%, cartas de Dioses Antiguos o cartas vistas en mazos de Leyenda (lista revisada a mano + mazos de [HSGuru](https://www.hsguru.com)).
- **Romper**: menos de 2%.

Si todavía no hay estadísticas descargadas, el plugin nunca recomienda romper: marca esas cartas como dudosas.

Los umbrales se cambian en `%APPDATA%\HearthstoneDeckTracker\RomperCartas\config.json` (`UmbralConservar`, `UmbralDudosa`).

## Requisitos

- Windows y Hearthstone Deck Tracker.
- **Python 3.10 o más nuevo** en el PATH, con las dependencias de `scripts\requirements.txt`.
- **Firefox**. Las estadísticas gratis de HSReplay solo responden a navegadores, así que se descargan con Firefox en modo invisible (headless) y un perfil temporal limpio. No usa tu sesión ni tus cookies.

## Instalación

1. Descarga `RomperCartas-<versión>.zip` de la sección *Releases*.
2. Descomprímelo dentro de `%APPDATA%\HearthstoneDeckTracker\Plugins\`. Debe quedar así:
   ```
   Plugins\
     RomperCartas.dll
     RomperCartas\
       datos\    (reglas.json, mazos_hsguru.json)
       scripts\  (actualizar_datos.py, ...)
   ```
3. Instala las dependencias de Python:
   ```
   pip install -r "%APPDATA%\HearthstoneDeckTracker\Plugins\RomperCartas\scripts\requirements.txt"
   ```
4. Abre (o reinicia) HDT y activa el plugin en **Opciones → Tracker → Plugins → Romper Cartas (Wild)**.

La primera vez el plugin baja en segundo plano las imágenes de las cartas que tienes desde [HearthstoneJSON](https://hearthstonejson.com) para poder reconocerlas en pantalla. Son unos minutos y, según el tamaño de tu colección, unos cientos de MB. De cada imagen solo guarda una miniatura (`arte.bin`, unos 8 MB).

## Limitaciones

- Las estadísticas gratis de HSReplay son de **Bronce a Oro**; en Diamante y Leyenda el meta puede ser distinto. Por eso las cartas vistas en mazos de Leyenda quedan como mínimo en «Dudosa».
- La posición de las cartas está medida para pantallas **16:9**. En otras proporciones los letreros pueden salir corridos.
- Las versiones **diamante y de firma** tienen otra ilustración y normalmente no se reconocen (no muestran letrero).
- Revisa siempre antes de romper: el plugin es una ayuda, no una garantía.

## Actualizar los mazos de Leyenda (opcional)

HSGuru tiene una verificación anti-bots, así que esta parte es manual:

1. Abre `https://www.hsguru.com/decks?format=1&rank=legend` en tu navegador y guárdala con **Ctrl+S → "Página web, solo HTML"**.
2. Ejecuta, desde la carpeta `scripts`:
   ```
   python hsguru_decks.py "Wild Decks.htm" "%APPDATA%\HearthstoneDeckTracker\RomperCartas\mazos_hsguru.json"
   python generar_reglas.py "" "%APPDATA%\HearthstoneDeckTracker\RomperCartas\mazos_hsguru.json"
   ```
3. En HDT, menú del plugin → **Actualizar estadísticas ahora**.

## Compilar desde el código

Requiere el SDK de .NET 8 o más nuevo y HDT instalado: las referencias se toman de `%LOCALAPPDATA%\HearthstoneDeckTracker\current`.

```
dotnet build src\RomperCartas -c Release -p:Instalar=true   # compila e instala en tu HDT
.\empaquetar.ps1                                            # arma dist\RomperCartas-<versión>.zip
```

`tools\Prueba` es una herramienta de consola para probar la clasificación y el reconocimiento sin abrir HDT:

```
Prueba.exe <coleccion.json>                          # clasifica una colección exportada de HSReplay
Prueba.exe --reconocer-coleccion <captura.png> <coleccion.json>
```

### Estructura

| Ruta | Contenido |
|---|---|
| `src/RomperCartas` | El plugin: clasificación, reconocimiento en pantalla, letreros y ventana. |
| `scripts` | Descarga de estadísticas (`actualizar_datos.py`) y generación de reglas (`generar_reglas.py`, `irrompibles_wiki.py`, `hsguru_decks.py`). |
| `datos` | `reglas.json` y `mazos_hsguru.json` que se distribuyen con el plugin. |

## Fuentes y créditos

- Estadísticas de uso: [HSReplay.net](https://hsreplay.net) (consultas públicas gratuitas).
- Mazos de Leyenda: [HSGuru](https://www.hsguru.com).
- Imágenes y datos de cartas: [HearthstoneJSON](https://hearthstonejson.com) y las bibliotecas de [HearthSim](https://github.com/HearthSim) (`hearthstone`, `hearthstone_data`, HearthDb).
- Lista de cartas que no se pueden fabricar ni desencantar: [Hearthstone Wiki](https://hearthstone.wiki.gg/wiki/Uncraftable).

Este proyecto no está afiliado a Blizzard Entertainment, HearthSim, HSReplay ni HSGuru. Hearthstone es una marca de Blizzard Entertainment.

## Licencia

[MIT](LICENSE).

---

## English summary

HDT plugin that labels every card on your Hearthstone collection page, in-game, with whether it is safe to disenchant for **Wild**. It uses HSReplay play rates, HSGuru Legend decklists, and a list of uncraftable cards (free reward-track and Tavern Pass copies, and similar) that cannot be disenchanted. It recognizes the cards on screen by comparing their art with HearthstoneJSON renders. It needs Python (with `scripts/requirements.txt`) and Firefox, which runs headless with a clean profile to fetch the free stats. See the Spanish sections above for installation (unzip into `%APPDATA%\HearthstoneDeckTracker\Plugins`) and build steps.
