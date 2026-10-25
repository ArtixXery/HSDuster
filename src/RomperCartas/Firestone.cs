using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HearthDb;
using HearthDb.Deckstrings;
using Newtonsoft.Json.Linq;

namespace RomperCartas
{
	/// <summary>Mazos de Wild de Leyenda y Diamante de los últimos 20 días que publica Firestone (Zero to Heroes).
	/// Es un archivo público que su app descarga sin credenciales; se baja como mucho una vez al día.</summary>
	public static class Firestone
	{
		public const string Url = "https://static.zerotoheroes.com/api/constructed/stats/decks/wild/legend-diamond/past-20/overview-from-hourly.gz.json";
		public const string Archivo = "firestone_wild.json";
		public const string Donde = "Leyenda–Diamante (Firestone, 20 días)";

		/// <summary>% de partidas cuyo mazo lleva la carta, por nombre en inglés (como el resto de las estadísticas).</summary>
		public static Dictionary<string, double> UsoPorCarta(JObject datos)
		{
			var partidas = new Dictionary<string, long>();
			long total = 0;
			foreach (var mazo in datos?["deckStats"] ?? new JArray())
			{
				Deck deck;
				try { deck = DeckSerializer.Deserialize((string)mazo["decklist"]); }
				catch { continue; }
				var juegos = (long?)mazo["totalGames"] ?? 0;
				total += juegos;
				var nombres = deck.CardDbfIds.Keys.Concat(deck.Sideboards.Values.SelectMany(s => s.Keys))
					.Select(id => Cards.GetFromDbfId(id, false)?.Name).Where(n => n != null).Distinct();
				foreach (var n in nombres)
					partidas[n] = (partidas.TryGetValue(n, out var p) ? p : 0) + juegos;
			}
			return total == 0 ? new Dictionary<string, double>() : partidas.ToDictionary(kv => kv.Key, kv => 100.0 * kv.Value / total);
		}

		/// <summary>Guarda el archivo en <paramref name="carpeta"/> y una copia en historial\AAAA-MM-DD.
		/// Devuelve null si salió bien o el motivo del error; si falla deja el archivo anterior.</summary>
		public static async Task<string> Descargar(HttpMessageHandler handler, string carpeta, DateTime fecha, CancellationToken ct)
		{
			using (var http = new HttpClient(handler, false) { Timeout = TimeSpan.FromSeconds(60) })
			using (var pedido = new HttpRequestMessage(HttpMethod.Get, Url))
			{
				pedido.Headers.TryAddWithoutValidation("User-Agent", "RomperCartas (Hearthstone Deck Tracker plugin)");
				try
				{
					using (var respuesta = await http.SendAsync(pedido, ct))
					{
						var texto = await respuesta.Content.ReadAsStringAsync();
						if (respuesta.StatusCode != HttpStatusCode.OK || !texto.TrimStart().StartsWith("{"))
							return $"{Archivo}: HTTP {(int)respuesta.StatusCode}";
						var historial = Path.Combine(carpeta, "historial", fecha.ToString("yyyy-MM-dd"));
						Directory.CreateDirectory(historial);
						DescargaHsreplay.Guardar(Path.Combine(carpeta, Archivo), texto);
						DescargaHsreplay.Guardar(Path.Combine(historial, Archivo), texto);
						return null;
					}
				}
				catch (Exception e) when (!ct.IsCancellationRequested)
				{
					return $"{Archivo}: {e.GetType().Name} {e.Message}";
				}
			}
		}
	}
}
