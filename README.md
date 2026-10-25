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
- Descarga las estadísticas de uso de Wild una vez al día, en segundo plano, con la misma conexión a HSReplay que usa HDT. No hace falta instalar nada más.
- Desde el menú del plugin en HDT se abre una **lista completa** con filtros por estado, clase, maná y búsqueda, y se puede exportar a CSV.

## Cómo decide

Para cada carta solo de Wild se toma su **% de mazos en que aparece** según [HSReplay](https://hsreplay.net). Se usa el máximo entre el global y cada clase, de los últimos 14 días y de la expansión actual, sumando todas sus versiones.

- **Conservar**: 5% o más, o aparece en algún mazo de la lista de mazos de la expansión actual.
- **Dudosa**: entre 2% y 5%, cartas de Dioses Antiguos o cartas vistas en mazos de Leyenda (lista revisada a mano + mazos de [HSGuru](https://www.hsguru.com)).
- **Romper**: menos de 2%.

Si todavía no hay estadísticas descargadas, el plugin nunca recomienda romper: marca esas cartas como dudosas.

Qué cartas son de Estándar y cuáles de Wild se calcula solo: un set es de Estándar cuando al menos la mitad de sus cartas tienen uso en las estadísticas de Estándar de HSReplay. Los datos de las cartas (sets, rarezas, copias regaladas) son los que HDT ya tiene cargados ([HearthSim/hsdata](https://github.com/HearthSim/hsdata) a través de HearthstoneJSON). Con cada descarga se regenera `reglas.json`; el plugin trae uno ya generado por si todavía no hay datos.

Los umbrales se cambian en `%APPDATA%\HearthstoneDeckTracker\RomperCartas\config.json` (`UmbralConservar`, `UmbralDudosa`).

## Requisitos

- Windows y Hearthstone Deck Tracker. Nada más: no hace falta Python ni ningún navegador.

## Instalación

1. Descarga `RomperCartas-<versión>.zip` de la sección *Releases*.
2. Descomprímelo dentro de `%APPDATA%\HearthstoneDeckTracker\Plugins\`. Debe quedar así:
   ```
   Plugins\
     RomperCartas.dll
     RomperCartas\
       datos\    (reglas.json, mazos_hsguru.json)
   ```
3. Abre (o reinicia) HDT y activa el plugin en **Opciones → Tracker → Plugins → Romper Cartas (Wild)**.

La primera vez el plugin baja en segundo plano las imágenes de las cartas que tienes desde [HearthstoneJSON](https://hearthstonejson.com) para poder reconocerlas en pantalla. Son unos minutos y, según el tamaño de tu colección, unos cientos de MB. De cada imagen solo guarda una miniatura (`arte.bin`, unos 8 MB).

## Limitaciones

- Las estadísticas gratis de HSReplay son de **Bronce a Oro**; en Diamante y Leyenda el meta puede ser distinto. Por eso las cartas vistas en mazos de Leyenda quedan como mínimo en «Dudosa».
- La posición de las cartas está medida para pantallas **16:9**. En otras proporciones los letreros pueden salir corridos.
- Las versiones **diamante y de firma** tienen otra ilustración y normalmente no se reconocen (no muestran letrero).
- Revisa siempre antes de romper: el plugin es una ayuda, no una garantía.

## Actualizar los mazos de Leyenda (opcional)

HSGuru tiene una verificación anti-bots, así que esta parte es manual:

1. Abre `https://www.hsguru.com/decks?format=1&rank=legend` en tu navegador y guárdala con **Ctrl+S → "Página web, solo HTML"**.
2. En HDT, menú del plugin → **Importar mazos de Leyenda de HSGuru…** y elige el archivo guardado.

Los mazos importados se suman a los que ya había (si un mazo se repite, queda el dato más nuevo), así que ninguna carta deja de contar como vista en Leyenda.

## Compilar desde el código

Requiere el SDK de .NET 8 o más nuevo y HDT instalado: las referencias se toman de `%LOCALAPPDATA%\HearthstoneDeckTracker\current` (se puede cambiar con `-p:HdtDir=<carpeta>`).

```
dotnet build src\RomperCartas -c Release -p:Instalar=true   # compila e instala en tu HDT
.\empaquetar.ps1                                            # arma dist\RomperCartas-<versión>.zip
```

`tools\Prueba` es una herramienta de consola para probar la clasificación y el reconocimiento sin abrir HDT:

```
Prueba.exe <coleccion.json>                          # clasifica una colección exportada de HSReplay
Prueba.exe --reconocer-coleccion <captura.png> <coleccion.json>
Prueba.exe --reglas <uso_estandar.json> <mazos_hsguru.json> datos\reglas.json   # regenera las reglas que trae el plugin
```

Los tests de la lógica que no depende de HDT (reglas, descarga, HSGuru) corren con .NET 8, también en Linux, usando los datos de cartas de HDT:

```
dotnet test tests\RomperCartas.Tests
```

Fuera de Windows hay que indicar dónde están HDT y sus datos de cartas: `-p:HdtDir=<carpeta current de HDT>` y la variable `HDT_CARDDEFS` con la ruta de `CardDefs.base.xml`.

### Estructura

| Ruta | Contenido |
|---|---|
| `src/RomperCartas` | El plugin: descarga de estadísticas, generación de reglas, clasificación, reconocimiento en pantalla, letreros y ventana. |
| `tests/RomperCartas.Tests` | Tests de la generación de reglas, la descarga y la importación de HSGuru. |
| `datos` | `reglas.json` y `mazos_hsguru.json` que se distribuyen con el plugin. |

## Fuentes y créditos

- Estadísticas de uso: [HSReplay.net](https://hsreplay.net) (consultas públicas gratuitas).
- Mazos de Leyenda: [HSGuru](https://www.hsguru.com).
- Imágenes y datos de cartas: [HearthstoneJSON](https://hearthstonejson.com), [HearthSim/hsdata](https://github.com/HearthSim/hsdata) y HearthDb de [HearthSim](https://github.com/HearthSim).
- Lista de cartas que no se pueden fabricar ni desencantar: [Hearthstone Wiki](https://hearthstone.wiki.gg/wiki/Uncraftable).

Este proyecto no está afiliado a Blizzard Entertainment, HearthSim, HSReplay ni HSGuru. Hearthstone es una marca de Blizzard Entertainment.

## Licencia

[MIT](LICENSE).

---

## English summary

HDT plugin that labels every card on your Hearthstone collection page, in-game, with whether it is safe to disenchant for **Wild**. It uses HSReplay play rates (downloaded with HDT's own HSReplay connection), HSGuru Legend decklists, and a list of uncraftable cards (free reward-track and Tavern Pass copies, and similar) that cannot be disenchanted. Standard sets are derived from HSReplay's Standard stats, and card data comes from what HDT already loads (HearthSim/hsdata). It recognizes the cards on screen by comparing their art with HearthstoneJSON renders. Nothing else is required: no Python and no browser. See the Spanish sections above for installation (unzip into `%APPDATA%\HearthstoneDeckTracker\Plugins`) and build steps.
