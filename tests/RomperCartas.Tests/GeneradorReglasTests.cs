using System.Collections.Generic;
using System.Linq;
using HearthDb;
using HearthDb.Enums;
using Newtonsoft.Json.Linq;
using Xunit;

namespace RomperCartas.Tests
{
	public class GeneradorReglasTests
	{
		static readonly CardSet[] EstandarOctubre2026 =
		{
			CardSet.BE, CardSet.CATACLYSM, CardSet.EMERALD_DREAM, CardSet.ESCAPEFROM_VIOLET_HOLD, CardSet.THE_LOST_CITY, CardSet.TIME_TRAVEL,
		};

		public GeneradorReglasTests() => DatosHdt.Cargar();

		static JObject Uso(params (int Dbf, double Pct)[] filas) => new JObject
		{
			["series"] = new JObject
			{
				["data"] = new JObject
				{
					["ALL"] = new JArray(filas.Select(f => new JObject { ["dbf_id"] = f.Dbf, ["included_popularity"] = f.Pct })),
				},
			},
		};

		static int[] DelSet(CardSet set) => Cards.CollectibleByDbfId.Values.Where(c => c.Set == set).Select(c => c.DbfId).ToArray();

		[Fact]
		public void Generar_MatchesThePythonGeneratedRules()
		{
			var esperado = DatosHdt.Json("datos/reglas.json");

			var reglas = GeneradorReglas.Generar(Cards.Collectible.Values, new HashSet<CardSet>(EstandarOctubre2026), DatosHdt.NombresHsguru());

			Assert.Equal(esperado["estandar"].Values<int>().OrderBy(x => x), reglas.Estandar.OrderBy(x => x));
			Assert.Equal(esperado["wild"].Values<int>().OrderBy(x => x), reglas.Wild.OrderBy(x => x));
			Assert.Equal(esperado["basico"].Values<int>().OrderBy(x => x), reglas.Basico.OrderBy(x => x));
			Assert.Equal(esperado["irrompible_normal"].Values<int>().OrderBy(x => x), reglas.IrrompibleNormal.OrderBy(x => x));
			Assert.Equal(esperado["irrompible_dorada"].Values<int>().OrderBy(x => x), reglas.IrrompibleDorada.OrderBy(x => x));
			Assert.Equal(esperado["dioses_antiguos"].Values<int>().OrderBy(x => x), reglas.DiosesAntiguos.OrderBy(x => x));
			Assert.Equal(esperado["leyenda"].Values<string>().OrderBy(x => x, System.StringComparer.Ordinal), reglas.Leyenda.OrderBy(x => x, System.StringComparer.Ordinal));
		}

		[Fact]
		public void SetsEstandar_KeepsSetsWithMostCardsPlayed()
		{
			var esmeralda = DelSet(CardSet.EMERALD_DREAM);
			var titanes = DelSet(CardSet.TITANS);
			var core = DelSet(CardSet.CORE);
			// A whole set played, a rotated set with a few 0% rows (noise HSReplay reports) and one played card, and the Core set
			var uso = Uso(esmeralda.Select(d => (d, 1.5))
				.Concat(titanes.Take(5).Select(d => (d, 0.0)))
				.Concat(new[] { (titanes[10], 0.4) })
				.Concat(core.Select(d => (d, 2.0))).ToArray());

			var sets = GeneradorReglas.SetsEstandar(Cards.Collectible.Values, uso);

			Assert.Equal(new[] { CardSet.EMERALD_DREAM }, sets);
		}

		[Fact]
		public void SetsEstandar_WithoutStatsReturnsNoSets()
		{
			Assert.Empty(GeneradorReglas.SetsEstandar(Cards.Collectible.Values, Uso()));
		}
	}
}
