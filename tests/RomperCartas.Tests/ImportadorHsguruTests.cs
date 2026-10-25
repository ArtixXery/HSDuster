using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace RomperCartas.Tests
{
	public class ImportadorHsguruTests
	{
		public ImportadorHsguruTests() => DatosHdt.Cargar();

		[Fact]
		public void Extraer_ReadsNameStatsAndCardsOfEachDeck()
		{
			var python = (JObject)JArray.Parse(System.IO.File.ReadAllText(DatosHdt.Ruta("datos/mazos_hsguru.json")))[0];
			var codigo = (string)python["codigo"];
			var html = "<html><body>" +
				"<div class=\"decklist-info rogue\"><button data-clipboard-text=\"### N&#39;Zoth Rogue\n" + codigo + "\n# rest\">Copy</button>" +
				"<script>var x = '99.9 Games: 1';</script><span>55.3</span> <span>Games: 1,234</span></div>" +
				"<div class=\"decklist-info mage\"><span>no deck code here</span></div>" +
				"</body></html>";

			var mazos = ImportadorHsguru.Extraer(html);

			var m = Assert.Single(mazos);
			Assert.Equal("N'Zoth Rogue", m.Nombre);
			Assert.Equal(codigo, m.Codigo);
			Assert.Equal(55.3, m.Winrate);
			Assert.Equal(1234, m.Partidas);
			Assert.Equal(python["cartas"].Values<string>().OrderBy(x => x), m.Cartas.OrderBy(x => x));
		}

		[Fact]
		public void Extraer_KeepsDeckWithInvalidCodeWithoutCards()
		{
			var html = "<div class=\"decklist-info\"><a data-clipboard-text=\"### Broken AAEnotadeck\">x</a></div>";

			var m = Assert.Single(ImportadorHsguru.Extraer(html));

			Assert.Equal("Broken", m.Nombre);
			Assert.Empty(m.Cartas);
			Assert.Null(m.Winrate);
		}
	}
}
