using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using HearthDb;
using HearthDb.Deckstrings;
using HearthDb.Enums;
using Newtonsoft.Json;

namespace RomperCartas
{
	public class MazoHsguru
	{
		[JsonProperty("nombre")] public string Nombre { get; set; }
		[JsonProperty("codigo")] public string Codigo { get; set; }
		[JsonProperty("winrate")] public double? Winrate { get; set; }
		[JsonProperty("partidas")] public int? Partidas { get; set; }
		[JsonProperty("cartas")] public List<string> Cartas { get; set; } = new List<string>();
	}

	/// <summary>Lee los mazos de una página de mazos de HSGuru guardada a mano (Ctrl+S, "Página web, solo HTML").
	/// HSGuru tiene una verificación anti-bots, así que la página no se descarga sola.</summary>
	public static class ImportadorHsguru
	{
		static readonly Regex Codigo = new Regex(@"data-clipboard-text=""###\s*(.*?)\s+(AAE[A-Za-z0-9+/=]+)");
		static readonly Regex ScriptsYEstilos = new Regex(@"<script.*?</script>|<style.*?</style>", RegexOptions.Singleline);
		static readonly Regex Etiquetas = new Regex("<[^>]+>");
		static readonly Regex Partidas = new Regex(@"([\d.]+)\s+Games:\s*([\d,]+)");

		public static List<MazoHsguru> Extraer(string html)
		{
			var mazos = new List<MazoHsguru>();
			foreach (var bloque in html.Split(new[] { "class=\"decklist-info" }, System.StringSplitOptions.None).Skip(1))
			{
				var m = Codigo.Match(bloque);
				if (!m.Success) continue;
				var texto = WebUtility.HtmlDecode(Etiquetas.Replace(ScriptsYEstilos.Replace(bloque, ""), " "));
				var g = Partidas.Match(texto);
				mazos.Add(new MazoHsguru
				{
					Nombre = WebUtility.HtmlDecode(m.Groups[1].Value),
					Codigo = m.Groups[2].Value,
					Winrate = g.Success && double.TryParse(g.Groups[1].Value, System.Globalization.NumberStyles.Float,
						System.Globalization.CultureInfo.InvariantCulture, out var w) ? w : (double?)null,
					Partidas = g.Success ? int.Parse(g.Groups[2].Value.Replace(",", "")) : (int?)null,
					Cartas = Cartas(m.Groups[2].Value),
				});
			}
			return mazos;
		}

		static List<string> Cartas(string codigo)
		{
			try
			{
				var mazo = DeckSerializer.Deserialize(codigo);
				var ids = mazo.CardDbfIds.Keys.Concat(mazo.Sideboards.Values.SelectMany(s => s.Keys));
				return ids.Select(id => Cards.GetFromDbfId(id, false)).Where(c => c != null).Select(c => c.GetLocName(Locale.enUS)).ToList();
			}
			catch
			{
				return new List<string>();
			}
		}
	}
}
