using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HearthDb;
using HearthDb.Deckstrings;
using HearthDb.Enums;
using Newtonsoft.Json.Linq;
using Xunit;

namespace RomperCartas.Tests
{
	public class FirestoneTests : IDisposable
	{
		readonly string _carpeta = Path.Combine(Path.GetTempPath(), "romper-cartas-tests-" + Guid.NewGuid().ToString("N"));

		public FirestoneTests() => DatosHdt.Cargar();

		public void Dispose()
		{
			if (Directory.Exists(_carpeta)) Directory.Delete(_carpeta, true);
		}

		static int Dbf(string nombre) => Cards.Collectible.Values.First(c => c.GetLocName(Locale.enUS) == nombre).DbfId;

		static string Codigo(params string[] cartas) => DeckSerializer.Serialize(new Deck
		{
			HeroDbfId = Cards.All["HERO_03"].DbfId,
			Format = FormatType.FT_WILD,
			CardDbfIds = cartas.ToDictionary(Dbf, c => 2),
		}, false);

		static JObject Mazos(params (string Codigo, int Partidas)[] mazos) => new JObject
		{
			["deckStats"] = new JArray(mazos.Select(m => new JObject { ["decklist"] = m.Codigo, ["totalGames"] = m.Partidas })),
		};

		[Fact]
		public void UsoPorCarta_IsTheShareOfGamesWhoseDeckHasTheCard()
		{
			var datos = Mazos((Codigo("Loatheb", "Doomsayer"), 100), (Codigo("Doomsayer", "Brawl"), 300), ("not-a-deck", 600));

			var uso = Firestone.UsoPorCarta(datos);

			Assert.Equal(25.0, uso["Loatheb"], 3);
			Assert.Equal(100.0, uso["Doomsayer"], 3);
			Assert.Equal(75.0, uso["Brawl"], 3);
			Assert.Equal(3, uso.Count);
		}

		[Fact]
		public void UsoPorCarta_WithoutDecksIsEmpty()
		{
			Assert.Empty(Firestone.UsoPorCarta(new JObject()));
		}

		class Falso : HttpMessageHandler
		{
			readonly HttpStatusCode _codigo;
			readonly string _cuerpo;
			public HttpRequestMessage Pedido;
			public Falso(HttpStatusCode codigo, string cuerpo) { _codigo = codigo; _cuerpo = cuerpo; }

			protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken ct)
			{
				Pedido = pedido;
				return Task.FromResult(new HttpResponseMessage(_codigo) { Content = new StringContent(_cuerpo, Encoding.UTF8) });
			}
		}

		[Fact]
		public async Task Descargar_SavesTheFileAndACopyInHistory()
		{
			const string json = "{\"deckStats\":[]}";
			var falso = new Falso(HttpStatusCode.OK, json);

			var error = await Firestone.Descargar(falso, _carpeta, new DateTime(2026, 10, 25), CancellationToken.None);

			Assert.Null(error);
			Assert.Equal(Firestone.Url, falso.Pedido.RequestUri.ToString());
			Assert.Equal(json, File.ReadAllText(Path.Combine(_carpeta, Firestone.Archivo)));
			Assert.Equal(json, File.ReadAllText(Path.Combine(_carpeta, "historial", "2026-10-25", Firestone.Archivo)));
		}

		[Fact]
		public async Task Descargar_KeepsPreviousFileOnError()
		{
			Directory.CreateDirectory(_carpeta);
			var anterior = Path.Combine(_carpeta, Firestone.Archivo);
			File.WriteAllText(anterior, "previo");

			var error = await Firestone.Descargar(new Falso(HttpStatusCode.NotFound, "<Error/>"), _carpeta, DateTime.Today, CancellationToken.None);

			Assert.Contains("404", error);
			Assert.Equal("previo", File.ReadAllText(anterior));
		}
	}
}
