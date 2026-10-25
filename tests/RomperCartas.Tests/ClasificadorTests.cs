using System.Collections.Generic;
using System.Linq;
using HearthDb;
using HearthDb.Enums;
using Xunit;

namespace RomperCartas.Tests
{
	public class ClasificadorTests
	{
		public ClasificadorTests() => DatosHdt.Cargar();

		static Card Carta(string nombre) => Cards.Collectible.Values.First(c => c.GetLocName(Locale.enUS) == nombre);

		/// <summary>One Wild card owned once, with the given HSReplay (Bronze–Gold) and Firestone (Legend–Diamond) usage.</summary>
		static Fila Clasificar(double usoHsreplay, double? usoLeyenda)
		{
			var carta = Carta("Bloodmage Thalnos");
			var reglas = new Reglas { Wild = { carta.DbfId } };
			var est = new Estadisticas();
			est.Uso[carta.Name] = (usoHsreplay, "Todas (14 días)");
			est.Uso["Some other card"] = (10, "Todas (14 días)");
			if (usoLeyenda != null) est.Leyenda[carta.Name] = usoLeyenda.Value;
			var col = new Dictionary<int, int[]> { [carta.DbfId] = new[] { 1, 0, 0, 0 } };
			return Assert.Single(Clasificador.Clasificar(col, reglas, est, new Config()));
		}

		[Fact]
		public void CardPlayedInLegendAboveThresholdIsDoubtful()
		{
			var f = Clasificar(0.3, 0.6);

			Assert.Equal(Categoria.Dudosa, f.Categoria);
			Assert.Contains("usada en mazos de Leyenda", f.Nota);
		}

		[Fact]
		public void CardBarelyPlayedInLegendCanBeBroken()
		{
			Assert.Equal(Categoria.Romper, Clasificar(0.3, 0.4).Categoria);
		}

		[Fact]
		public void CardWithoutLegendDataKeepsHsreplayRule()
		{
			Assert.Equal(Categoria.Romper, Clasificar(0.3, null).Categoria);
			Assert.Equal(Categoria.Dudosa, Clasificar(3, null).Categoria);
			Assert.Equal(Categoria.Conservar, Clasificar(6, null).Categoria);
		}
	}
}
