using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RomperCartas
{
	/// <summary>Reglas fijas generadas por scripts/generar_reglas.py.</summary>
	public class Reglas
	{
		[JsonProperty("estandar")] public HashSet<int> Estandar { get; set; } = new HashSet<int>();
		[JsonProperty("wild")] public HashSet<int> Wild { get; set; } = new HashSet<int>();
		[JsonProperty("basico")] public HashSet<int> Basico { get; set; } = new HashSet<int>();
		[JsonProperty("irrompible_normal")] public HashSet<int> IrrompibleNormal { get; set; } = new HashSet<int>();
		[JsonProperty("irrompible_dorada")] public HashSet<int> IrrompibleDorada { get; set; } = new HashSet<int>();
		[JsonProperty("dioses_antiguos")] public HashSet<int> DiosesAntiguos { get; set; } = new HashSet<int>();
		[JsonProperty("leyenda")] public HashSet<string> Leyenda { get; set; } = new HashSet<string>();
		[JsonProperty("generado")] public string Generado { get; set; } = "";
	}

	public class Config
	{
		public double UmbralConservar { get; set; } = 5.0;
		public double UmbralDudosa { get; set; } = 2.0;
		public int HorasEntreDescargas { get; set; } = 24;
		public bool AbrirEnColeccion { get; set; } = false;
		public bool MostrarEtiquetas { get; set; } = true;
		public float ParecidoMinimo { get; set; } = 0.85f;   // reconocimiento de cartas en pantalla
		public float MargenMinimo { get; set; } = 0.12f;
		public bool SincronizarFiltroMana { get; set; } = true;
		public string Python { get; set; } = "python";
		/// <summary>Vacío = el script que viene junto al plugin (scripts\actualizar_datos.py).</summary>
		public string ScriptActualizar { get; set; } = "";
	}

	/// <summary>Uso de cada carta en Wild (por nombre en inglés) y cartas vistas en mazos actuales.</summary>
	public class Estadisticas
	{
		public Dictionary<string, (double Pct, string Donde)> Uso { get; } = new Dictionary<string, (double, string)>();
		public HashSet<string> EnMazos { get; } = new HashSet<string>();
		public DateTime Fecha { get; set; }
	}

	public static class Datos
	{
		public static readonly string Carpeta = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HearthstoneDeckTracker", "RomperCartas");

		// Archivos que deja scripts/actualizar_datos.py
		static readonly (string Archivo, string Url)[] Consultas =
		{
			("uso_14dias.json", "card_list_free LAST_14_DAYS"),
			("uso_expansion.json", "card_list_free CURRENT_EXPANSION"),
			("mazos_expansion.json", "list_decks_by_win_rate_v2 CURRENT_EXPANSION"),
		};

		static readonly Dictionary<string, string> Clases = new Dictionary<string, string>
		{
			["ALL"] = "Todas", ["DEATHKNIGHT"] = "Caballero de la Muerte", ["DEMONHUNTER"] = "Cazador de Demonios", ["DRUID"] = "Druida",
			["HUNTER"] = "Cazador", ["MAGE"] = "Mago", ["PALADIN"] = "Paladín", ["PRIEST"] = "Sacerdote", ["ROGUE"] = "Pícaro",
			["SHAMAN"] = "Chamán", ["WARLOCK"] = "Brujo", ["WARRIOR"] = "Guerrero",
		};

		/// <summary>Carpeta donde está el .dll; ahí vienen reglas.json y los scripts de la versión instalada.</summary>
		public static readonly string CarpetaPlugin = Path.GetDirectoryName(typeof(Datos).Assembly.Location);

		public static string Ruta(string archivo) => Path.Combine(Carpeta, archivo);

		public static string Script(Config cfg) => string.IsNullOrWhiteSpace(cfg.ScriptActualizar)
			? Path.Combine(CarpetaPlugin, "RomperCartas", "scripts", "actualizar_datos.py")
			: Environment.ExpandEnvironmentVariables(cfg.ScriptActualizar);

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
				var guru = Path.Combine(origen, "mazos_hsguru.json");
				if (File.Exists(guru) && (!File.Exists(Ruta("mazos_hsguru.json")) || File.GetLastWriteTime(guru) > File.GetLastWriteTime(Ruta("mazos_hsguru.json"))))
					File.Copy(guru, Ruta("mazos_hsguru.json"), true);
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

		public static bool HaceFaltaDescargar(int horas) =>
			Consultas.Any(c => !File.Exists(Ruta(c.Archivo)) || DateTime.Now - File.GetLastWriteTime(Ruta(c.Archivo)) > TimeSpan.FromHours(horas));

		/// <summary>Lanza scripts/actualizar_datos.py, que descarga las estadísticas con Firefox headless (HSReplay rechaza
		/// clientes que no son navegadores). Devuelve un resumen del resultado.</summary>
		public static async Task<string> Descargar(Config cfg, bool forzar)
		{
			var script = Script(cfg);
			if (!File.Exists(script)) return Registrar($"No encuentro el script de actualización: {script}");
			var antes = Consultas.Select(c => File.Exists(Ruta(c.Archivo)) ? File.GetLastWriteTime(Ruta(c.Archivo)) : DateTime.MinValue).ToList();
			var psi = new ProcessStartInfo(cfg.Python, $"\"{script}\"{(forzar ? " --forzar" : "")}")
			{
				UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
				WorkingDirectory = Path.GetDirectoryName(script),
			};
			psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
			try
			{
				using (var p = Process.Start(psi))
				{
					var salida = p.StandardOutput.ReadToEndAsync();
					var errores = p.StandardError.ReadToEndAsync();
					await Task.Run(() => p.WaitForExit(20 * 60 * 1000));
					if (!p.HasExited) { p.Kill(); return Registrar("La actualización tardó demasiado y se canceló"); }
					if (p.ExitCode != 0) return Registrar("Error al actualizar: " + (await errores).Trim().Split('\n').LastOrDefault());
				}
			}
			catch (Exception e)
			{
				return Registrar($"No se pudo ejecutar {cfg.Python}: {e.Message}");
			}
			var nuevos = Consultas.Where((c, i) => File.Exists(Ruta(c.Archivo)) && File.GetLastWriteTime(Ruta(c.Archivo)) > antes[i]).Count();
			return nuevos > 0 ? "Estadísticas actualizadas" : "Estadísticas sin cambios (revisa log.txt si esperabas datos nuevos)";
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
