using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RomperCartas
{
	/// <summary>Credenciales con las que HDT consulta HSReplay: su clave de API, el token de subida de esta instalación
	/// y su User-Agent. Las mismas que usa HDT para sus propias estadísticas.</summary>
	public class CredencialesHsreplay
	{
		public CredencialesHsreplay(string claveApi, string token, string userAgent)
		{
			ClaveApi = claveApi;
			Token = token;
			UserAgent = userAgent;
		}

		public string ClaveApi { get; }
		public string Token { get; }
		public string UserAgent { get; }
	}

	/// <summary>Descarga las consultas gratis de HSReplay (Bronce–Oro) a la carpeta de datos del plugin.</summary>
	public class DescargaHsreplay
	{
		const string Base = "https://hsreplay.net/api/v1/analytics/query/";
		const int IntentosMientrasSePrepara = 6;

		public static readonly (string Archivo, string Ruta)[] Consultas =
		{
			("uso_14dias.json", "card_list_free/?GameType=RANKED_WILD&TimeRange=LAST_14_DAYS&LeagueRankRange=BRONZE_THROUGH_GOLD"),
			("uso_expansion.json", "card_list_free/?GameType=RANKED_WILD&TimeRange=CURRENT_EXPANSION&LeagueRankRange=BRONZE_THROUGH_GOLD"),
			("mazos_expansion.json", "list_decks_by_win_rate_v2/?GameType=RANKED_WILD&LeagueRankRange=BRONZE_THROUGH_GOLD&Region=ALL&TimeRange=CURRENT_EXPANSION"),
			// Solo para saber qué sets son de Estándar (GeneradorReglas.SetsEstandar)
			("uso_estandar.json", "card_list_free/?GameType=RANKED_STANDARD&TimeRange=LAST_14_DAYS&LeagueRankRange=BRONZE_THROUGH_GOLD"),
		};

		public class Resultado
		{
			public List<string> Descargados { get; } = new List<string>();
			public List<string> Errores { get; } = new List<string>();
		}

		readonly HttpClient _http;
		readonly CredencialesHsreplay _cred;
		readonly TimeSpan _espera;

		/// <param name="espera">Pausa entre intentos cuando HSReplay responde 202 (todavía prepara la consulta).</param>
		public DescargaHsreplay(HttpMessageHandler handler, CredencialesHsreplay cred, TimeSpan espera)
		{
			_http = new HttpClient(handler, false) { Timeout = TimeSpan.FromSeconds(60) };
			_cred = cred;
			_espera = espera;
		}

		/// <summary>Guarda cada consulta en <paramref name="carpeta"/> y una copia en historial\AAAA-MM-DD.
		/// Si una consulta falla, deja el archivo anterior como estaba.</summary>
		public async Task<Resultado> Descargar(string carpeta, DateTime fecha, CancellationToken ct)
		{
			var r = new Resultado();
			var historial = Path.Combine(carpeta, "historial", fecha.ToString("yyyy-MM-dd"));
			Directory.CreateDirectory(historial);
			foreach (var (archivo, ruta) in Consultas)
			{
				ct.ThrowIfCancellationRequested();
				try
				{
					var (codigo, texto) = await Consultar(Base + ruta, ct);
					if (codigo != HttpStatusCode.OK || !texto.TrimStart().StartsWith("{"))
					{
						r.Errores.Add($"{archivo}: HTTP {(int)codigo}");
						continue;
					}
					Guardar(Path.Combine(carpeta, archivo), texto);
					Guardar(Path.Combine(historial, archivo), texto);
					r.Descargados.Add(archivo);
				}
				catch (Exception e) when (!ct.IsCancellationRequested)
				{
					r.Errores.Add($"{archivo}: {e.GetType().Name} {e.Message}");
				}
			}
			return r;
		}

		async Task<(HttpStatusCode, string)> Consultar(string url, CancellationToken ct)
		{
			for (var intento = 1; ; intento++)
			{
				using (var pedido = new HttpRequestMessage(HttpMethod.Get, url))
				{
					pedido.Headers.TryAddWithoutValidation("X-Api-Key", _cred.ClaveApi);
					pedido.Headers.TryAddWithoutValidation("Authorization", "Token " + _cred.Token);
					pedido.Headers.TryAddWithoutValidation("User-Agent", _cred.UserAgent);
					using (var respuesta = await _http.SendAsync(pedido, ct))
					{
						if (respuesta.StatusCode != HttpStatusCode.Accepted || intento >= IntentosMientrasSePrepara)
							return (respuesta.StatusCode, await respuesta.Content.ReadAsStringAsync());
					}
				}
				await Task.Delay(_espera, ct);
			}
		}

		static void Guardar(string destino, string texto)
		{
			var tmp = destino + ".tmp";
			File.WriteAllText(tmp, texto);
			if (File.Exists(destino)) File.Replace(tmp, destino, null);
			else File.Move(tmp, destino);
		}
	}
}
