using System;
using System.IO;
using System.Linq;
using HearthDb;
using Newtonsoft.Json.Linq;

namespace RomperCartas.Tests
{
	/// <summary>Loads the same card data HDT uses (CardDefs.base.xml from HearthstoneJSON) once per test run.</summary>
	public static class DatosHdt
	{
		static readonly Lazy<bool> Cargado = new Lazy<bool>(() =>
		{
			var ruta = Environment.GetEnvironmentVariable("HDT_CARDDEFS");
			if (string.IsNullOrEmpty(ruta))
				ruta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
					"HearthstoneDeckTracker", "CardDefs", "CardDefs.base.xml");
			if (!File.Exists(ruta))
				throw new FileNotFoundException("Set HDT_CARDDEFS to HDT's CardDefs.base.xml", ruta);
			using (var f = File.OpenRead(ruta)) Cards.LoadBaseData(f);
			return true;
		});

		public static void Cargar() => _ = Cargado.Value;

		public static string Ruta(string relativa) => Path.Combine(AppContext.BaseDirectory, relativa);

		public static JObject Json(string relativa) => JObject.Parse(File.ReadAllText(Ruta(relativa)));

		public static string[] NombresHsguru() =>
			JArray.Parse(File.ReadAllText(Ruta("datos/mazos_hsguru.json"))).SelectMany(m => m["cartas"].Values<string>()).ToArray();
	}
}
