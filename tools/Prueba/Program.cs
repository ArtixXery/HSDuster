using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using RomperCartas;

class Program
{
	static void Main(string[] args)
	{
		Console.OutputEncoding = System.Text.Encoding.UTF8;
		if (args[0] == "--arte")
		{
			var col0 = JObject.Parse(File.ReadAllText(args[1]))["collection"].ToObject<Dictionary<int, int[]>>();
			var ids = col0.Where(kv => kv.Value.Take(4).Sum() > 0).Select(kv => HearthDb.Cards.GetFromDbfId(kv.Key, false))
				.Where(c => c != null && c.Collectible).Select(c => c.Id).Distinct().ToList();
			var rec0 = new Reconocedor();
			rec0.Cargar();
			Console.WriteLine($"{ids.Count} cartas, {rec0.Cantidad} ya preparadas");
			rec0.Completar(ids, p => Console.WriteLine(p ?? "listo"), System.Threading.CancellationToken.None).Wait();
			Console.WriteLine($"{rec0.Cantidad} imágenes en {Datos.Ruta("arte.bin")}");
			return;
		}
		if (args[0] == "--reconocer-coleccion")
		{
			var col1 = JObject.Parse(File.ReadAllText(args[2]))["collection"].ToObject<Dictionary<int, int[]>>();
			var cand1 = col1.Where(kv => kv.Value.Take(4).Sum() > 0).Select(kv => HearthDb.Cards.GetFromDbfId(kv.Key, false))
				.Where(c => c != null && c.Collectible).GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First().Name);
			var rec1 = new Reconocedor();
			rec1.Cargar();
			var sw1 = System.Diagnostics.Stopwatch.StartNew();
			using (var cap = new System.Drawing.Bitmap(args[1]))
			{
				var r = rec1.Reconocer(cap, cand1, 0.85f, 0.12f);
				Console.WriteLine($"{cand1.Count} candidatas, {sw1.ElapsedMilliseconds} ms");
				for (var i = 0; i < 8; i++) Console.WriteLine($"casilla {i}: {(r[i] == null ? "—" : $"{r[i].CardId} {cand1[r[i].CardId]} {r[i].Parecido:0.000} (2.º {r[i].Segundo:0.000})")}");
			}
			return;
		}
		if (args[0] == "--reconocer")
		{
			var rec = new Reconocedor();
			var cand = new Dictionary<string, string>();
			foreach (var f in Directory.GetFiles(args[2], "*.png"))
			{
				var id = Path.GetFileNameWithoutExtension(f);
				using (var img = new System.Drawing.Bitmap(f)) rec.Agregar(id, img);
				cand[id] = HearthDb.Cards.All.TryGetValue(id, out var c) ? c.Name : id;
			}
			var sw = System.Diagnostics.Stopwatch.StartNew();
			using (var cap = new System.Drawing.Bitmap(args[1]))
			{
				var r = rec.Reconocer(cap, cand, args.Length > 3 ? 0f : 0.85f, args.Length > 3 ? -1f : 0.12f);
				Console.WriteLine($"{sw.ElapsedMilliseconds} ms");
				for (var i = 0; i < 8; i++) Console.WriteLine($"casilla {i}: {(r[i] == null ? "—" : $"{r[i].CardId} {cand[r[i].CardId]} {r[i].Parecido:0.000} (2.º {r[i].Segundo:0.000})")}");
			}
			return;
		}
		if (args.Contains("--descargar")) Console.WriteLine(Datos.Descargar(new Config(), true).Result);
		var est = Datos.CargarEstadisticas();
		var reglas = Datos.LeerJson<Reglas>("reglas.json");
		var cfg = new Config();
		var col = JObject.Parse(File.ReadAllText(args[0]))["collection"].ToObject<Dictionary<int, int[]>>();
		Console.WriteLine($"Estadísticas al {est.Fecha:yyyy-MM-dd}: {est.Uso.Count} cartas con uso, {est.EnMazos.Count} en mazos");
		var filas = Clasificador.Clasificar(col, reglas, est, cfg);
		foreach (var g in filas.GroupBy(f => f.Categoria).OrderBy(g => g.Key))
			Console.WriteLine($"{g.Key}: {g.Count()} cartas, {g.Sum(f => f.Polvo)} polvo");
		foreach (var f in filas.Where(f => f.NombreEn.StartsWith("Galakrond") || f.NombreEn == "Wildheart Guff" || f.NombreEn == "Edwin, Defias Kingpin"))
			Console.WriteLine($"  {f.CategoriaTexto,-12} {f.Nombre} ({f.NombreEn}) {f.UsoTexto} {f.Donde} {f.Copias} {f.Polvo} {f.Nota}");
	}
}
