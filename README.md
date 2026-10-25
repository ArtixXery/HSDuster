# HSDuster

[Hearthstone Deck Tracker](https://hsreplay.net/downloads/) plugin that labels every card in your Hearthstone collection with whether it is safe to disenchant for **Wild**. The in-game labels are in Spanish.

| Label | Meaning |
|---|---|
| ROMPER (green) | Wild-only card that is rarely played: disenchant. |
| SOBRA (blue) | Copies above the playable maximum. |
| ROMPER VIEJA (teal) | You own the free Core version. |
| DUDOSA (orange) | Borderline usage, Old Gods, or seen in Legend/Diamond decks: wait. |
| CONSERVAR (red) | Played in current Wild decks: keep. |
| NO SE PUEDE ROMPER (gray) | Free copy the game won't let you disenchant. |

## Install

Unzip the release into `%APPDATA%\HearthstoneDeckTracker\Plugins\`, restart HDT and enable it under **Options → Tracker → Plugins**. Nothing else is needed.

## How it decides

Once a day it downloads [HSReplay](https://hsreplay.net) free Wild stats (through HDT's own connection) and [Firestone](https://www.firestoneapp.com) Legend–Diamond decks. A Wild card is **kept** at ≥5% usage, **doubtful** at ≥2% or ≥0.5% of Legend–Diamond games, and **disenchanted** below that. Standard sets are derived from HSReplay's Standard stats, and card data comes from what HDT already loads ([HearthSim/hsdata](https://github.com/HearthSim/hsdata)). Thresholds live in `%APPDATA%\HearthstoneDeckTracker\RomperCartas\config.json`.

Always double-check before disenchanting.

## Build

```
dotnet build src\RomperCartas -c Release -p:Instalar=true   # build and install into HDT
dotnet test tests\RomperCartas.Tests                        # tests
.\empaquetar.ps1                                            # dist\RomperCartas-<version>.zip
```

Requires the .NET 8 SDK and HDT installed.

## Credits

Data from HSReplay, Firestone (Zero to Heroes), HearthstoneJSON and HearthSim. Not affiliated with Blizzard Entertainment or any of them. [MIT](LICENSE).
