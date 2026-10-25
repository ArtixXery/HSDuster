using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HearthDb;
using HearthDb.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RomperCartas
{
	public static class Datos
	{
		public static readonly string Carpeta = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HearthstoneDeckTracker", "RomperCartas");

		static readonly Dictionary<string, string> Clases = new Dictionary<string, string>
		{
			["ALL"] = "Todas", ["DEATHKNIGHT"] = "Caballero de la Muerte", ["DEMONHUNTER"] = "Cazador de Demonios", ["DRUID"] = "Druida",
			["HUNTER"] = "Cazador", ["MAGE"] = "Mago", ["PALADIN"] = "Paladín", ["PRIEST"] = "Sacerdote", ["ROGUE"] = "Pícaro",
			["SHAMAN"] = "Chamán", ["WARLOCK"] = "Brujo", ["WARRIOR"] = "Guerrero",
		};

		/// <summary>Carpeta donde está el .dll; ahí viene el reglas.json de la versión instalada.</summary>
		public static readonly string CarpetaPlugin = Path.GetDirectoryName(typeof(Datos).Assembly.Location);

		public static string Ruta(string archivo) => Path.Combine(Carpeta, archivo);

		/// <summary>Copia a la carpeta de datos las reglas que vienen con el plugin si faltan o si son más nuevas.</summary>
		public static void InstalarArchivosIncluidos()
		{
			try
			{
				Directory.CreateDirectory(Carpeta);
				var origen = Path.Combine(CarpetaPlugin, "RomperCartas", "datos");
				var reglas = Path.Combine(origen, "reglas.json");
				if (File.Exists(reglas))
				{
					var incluida = JsonConvert.DeserializeObject<Reglas>(File.ReadAllText(reglas));
					var actual = LeerJson<Reglas>("reglas.json");
					if (string.CompareOrdinal(incluida?.Generado ?? "", actual.Generado ?? "") > 0)
					{
						File.Copy(reglas, Ruta("reglas.json"), true);
						Log($"Reglas actualizadas con las del plugin ({incluida.Generado})");
					}
				}
			}
			catch (Exception e) { Log("No se pudieron copiar los archivos incluidos: " + e.Message); }
		}

		public static void Log(string msg)
		{
			try
			{
				Directory.CreateDirectory(Carpeta);
				File.AppendAllText(Ruta("log.txt"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {msg}{Environment.NewLine}");
			}
			catch { }
		}

		public static T LeerJson<T>(string archivo) where T : new()
		{
			try
			{
				var ruta = Ruta(archivo);
				return File.Exists(ruta) ? JsonConvert.DeserializeObject<T>(File.ReadAllText(ruta)) ?? new T() : new T();
			}
			catch (Exception e)
			{
				Log($"No se pudo leer {archivo}: {e.Message}");
				return new T();
			}
		}

		public static void GuardarJson(string archivo, object valor)
		{
			Directory.CreateDirectory(Carpeta);
			File.WriteAllText(Ruta(archivo), JsonConvert.SerializeObject(valor, Formatting.Indented));
		}

		static IEnumerable<string> Descargables => DescargaHsreplay.Consultas.Select(c => c.Archivo).Concat(new[] { Firestone.Archivo });

		public static bool HaceFaltaDescargar(int horas) => Descargables.Any(a =>
			!File.Exists(Ruta(a)) || DateTime.Now - File.GetLastWriteTime(Ruta(a)) > TimeSpan.FromHours(horas));

		/// <summary>Descarga los mazos de Leyenda–Diamante de Firestone y las estadísticas de HSReplay (con las credenciales de HDT),
		/// y regenera reglas.json. Devuelve un resumen del resultado.</summary>
		public static async Task<string> Descargar(Config cfg, bool forzar, CancellationToken ct = default)
		{
			if (!forzar && !HaceFaltaDescargar(cfg.HorasEntreDescargas)) return "Estadísticas al día";
			var descargados = new List<string>();
			var errores = new List<string>();
			using (var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate })
			{
				var errorFirestone = await Firestone.Descargar(handler, Carpeta, DateTime.Today, ct);
				if (errorFirestone == null) descargados.Add(Firestone.Archivo);
				else errores.Add(errorFirestone);

				CredencialesHsreplay cred = null;
				try { cred = await CredencialesHdt.Obtener(); }
				catch (Exception e)
				{
					Log("No se pudieron leer las credenciales de HSReplay de HDT: " + e);
					errores.Add("HSReplay: sin credenciales de HDT");
				}
				if (cred != null)
				{
					var r = await new DescargaHsreplay(handler, cred, TimeSpan.FromSeconds(5)).Descargar(Carpeta, DateTime.Today, ct);
					descargados.AddRange(r.Descargados);
					errores.AddRange(r.Errores);
				}
			}
			foreach (var a in descargados) Log($"{a}: ok");
			foreach (var e in errores) Log("Error al descargar " + e);
			var reglas = descargados.Contains("uso_estandar.json") ? RegenerarReglas() : null;
			var resumen = errores.Count == 0 ? "Estadísticas actualizadas"
				: descargados.Count == 0 ? "No se pudieron descargar las estadísticas (revisa log.txt)"
				: $"Estadísticas actualizadas a medias: {errores.Count} fallaron (revisa log.txt)";
			return reglas == null ? resumen : resumen + " · " + reglas;
		}

		/// <summary>Regenera reglas.json con las cartas que tiene HDT y los sets de Estándar de uso_estandar.json.
		/// Si los datos no alcanzan para saber qué es Estándar, deja las reglas que había.</summary>
		public static string RegenerarReglas()
		{
			try
			{
				var ruta = Ruta("uso_estandar.json");
				if (!File.Exists(ruta)) return null;
				var sets = GeneradorReglas.SetsEstandar(Cards.Collectible.Values, JObject.Parse(File.ReadAllText(ruta)));
				if (!GeneradorReglas.SonSetsEstandarCreibles(sets))
				{
					Log($"Reglas no regeneradas: {sets.Length} sets de Estándar en uso_estandar.json no es creíble");
					return null;
				}
				var reglas = GeneradorReglas.Generar(Cards.Collectible.Values, new HashSet<CardSet>(sets));
				GuardarJson("reglas.json", reglas);
				return Registrar($"Reglas regeneradas: Estándar = {string.Join(", ", sets)}; {reglas.Wild.Count} cartas Wild");
			}
			catch (Exception e)
			{
				Log("Error al regenerar las reglas: " + e);
				return null;
			}
		}

		static string Registrar(string msg)
		{
			Log(msg);
			return msg;
		}

		/// <summary>Lee las estadísticas descargadas. El uso de una carta es el máximo entre el global y cada clase,
		/// sumando todas sus versiones (por ejemplo la del Básico y la original).</summary>
		public static Estadisticas CargarEstadisticas()
		{
			var est = new Estadisticas();
			foreach (var (archivo, periodo) in new[] { ("uso_14dias.json", "14 días"), ("uso_expansion.json", "expansión nueva") })
			{
				var ruta = Ruta(archivo);
				if (!File.Exists(ruta)) continue;
				try
				{
					var j = JObject.Parse(File.ReadAllText(ruta));
					var asOf = j["as_of"];   // Newtonsoft ya lo convierte a fecha
					var f = asOf?.Type == JTokenType.Date ? (DateTime)asOf : DateTime.MinValue;
					if (f > est.Fecha) est.Fecha = f;
					foreach (var serie in (JObject)j["series"]["data"])
					{
						var acc = new Dictionary<string, double>();
						foreach (var x in serie.Value)
						{
							var carta = HearthDb.Cards.GetFromDbfId((int)x["dbf_id"], false);
							if (carta == null) continue;
							acc.TryGetValue(carta.Name, out var p);
							acc[carta.Name] = p + (double)x["included_popularity"];
						}
						var clase = Clases.TryGetValue(serie.Key, out var c) ? c : serie.Key;
						foreach (var kv in acc)
							if (!est.Uso.TryGetValue(kv.Key, out var actual) || kv.Value > actual.Pct)
								est.Uso[kv.Key] = (Math.Round(kv.Value, 2), $"{clase} ({periodo})");
					}
				}
				catch (Exception e) { Log($"Error leyendo {archivo}: {e.Message}"); }
			}
			var rutaFirestone = Ruta(Firestone.Archivo);
			if (File.Exists(rutaFirestone))
			{
				try
				{
					foreach (var kv in Firestone.UsoPorCarta(JObject.Parse(File.ReadAllText(rutaFirestone))))
					{
						est.Leyenda[kv.Key] = kv.Value;
						if (!est.Uso.TryGetValue(kv.Key, out var actual) || kv.Value > actual.Pct)
							est.Uso[kv.Key] = (Math.Round(kv.Value, 2), Firestone.Donde);
					}
				}
				catch (Exception e) { Log($"Error leyendo {Firestone.Archivo}: {e.Message}"); }
			}
			var rutaMazos = Ruta("mazos_expansion.json");
			if (File.Exists(rutaMazos))
			{
				try
				{
					var j = JObject.Parse(File.ReadAllText(rutaMazos));
					foreach (var clase in (JObject)j["series"]["data"])
					foreach (var mazo in clase.Value)
					{
						var ids = JArray.Parse((string)mazo["deck_list"]).Select(e => (int)e[0]).ToList();
						var sideboard = (string)mazo["deck_sideboard"];
						if (!string.IsNullOrEmpty(sideboard))
							foreach (var sb in JArray.Parse(sideboard))   // [dueño, [carta, n], ...]
								ids.AddRange(sb.Skip(1).Select(e => (int)e[0]));
						foreach (var id in ids)
						{
							var carta = HearthDb.Cards.GetFromDbfId(id, false);
							if (carta != null) est.EnMazos.Add(carta.Name);
						}
					}
				}
				catch (Exception e) { Log($"Error leyendo mazos: {e.Message}"); }
			}
			return est;
		}
	}
}
