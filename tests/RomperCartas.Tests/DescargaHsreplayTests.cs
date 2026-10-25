using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RomperCartas.Tests
{
	public class DescargaHsreplayTests : IDisposable
	{
		readonly string _carpeta = Path.Combine(Path.GetTempPath(), "romper-cartas-tests-" + Guid.NewGuid().ToString("N"));
		static readonly CredencialesHsreplay Cred = new CredencialesHsreplay("clave", "token-123", "HDT/1.0 (Windows 11)");
		const string Json = "{\"render_as\":\"table\",\"series\":{\"data\":{}}}";

		public void Dispose()
		{
			if (Directory.Exists(_carpeta)) Directory.Delete(_carpeta, true);
		}

		/// <summary>Answers each request with the next queued response and records what was sent.</summary>
		class Falso : HttpMessageHandler
		{
			public readonly List<HttpRequestMessage> Pedidos = new List<HttpRequestMessage>();
			readonly Func<HttpRequestMessage, int, HttpResponseMessage> _responder;
			public Falso(Func<HttpRequestMessage, int, HttpResponseMessage> responder) => _responder = responder;

			protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken ct)
			{
				Pedidos.Add(pedido);
				return Task.FromResult(_responder(pedido, Pedidos.Count));
			}
		}

		static HttpResponseMessage Respuesta(HttpStatusCode codigo, string cuerpo) =>
			new HttpResponseMessage(codigo) { Content = new StringContent(cuerpo, Encoding.UTF8) };

		DescargaHsreplay Descarga(Falso falso) => new DescargaHsreplay(falso, Cred, TimeSpan.Zero);

		[Fact]
		public async Task Descargar_SavesEveryQueryWithHdtCredentials()
		{
			var falso = new Falso((p, n) => Respuesta(HttpStatusCode.OK, Json));

			var r = await Descarga(falso).Descargar(_carpeta, new DateTime(2026, 10, 25), CancellationToken.None);

			Assert.Empty(r.Errores);
			Assert.Equal(DescargaHsreplay.Consultas.Select(c => c.Archivo), r.Descargados);
			foreach (var (archivo, _) in DescargaHsreplay.Consultas)
			{
				Assert.Equal(Json, File.ReadAllText(Path.Combine(_carpeta, archivo)));
				Assert.Equal(Json, File.ReadAllText(Path.Combine(_carpeta, "historial", "2026-10-25", archivo)));
			}
			var pedido = falso.Pedidos[0];
			Assert.Equal("clave", pedido.Headers.GetValues("X-Api-Key").Single());
			Assert.Equal("Token token-123", pedido.Headers.GetValues("Authorization").Single());
			Assert.Equal("HDT/1.0 (Windows 11)", string.Join(" ", pedido.Headers.GetValues("User-Agent")));
			Assert.StartsWith("https://hsreplay.net/api/v1/analytics/query/card_list_free/?GameType=RANKED_WILD", pedido.RequestUri.ToString());
		}

		[Fact]
		public async Task Descargar_RetriesWhileHsreplayIsPreparingTheQuery()
		{
			var falso = new Falso((p, n) => n == 1 ? Respuesta(HttpStatusCode.Accepted, "") : Respuesta(HttpStatusCode.OK, Json));

			var r = await Descarga(falso).Descargar(_carpeta, DateTime.Today, CancellationToken.None);

			Assert.Empty(r.Errores);
			Assert.Equal(DescargaHsreplay.Consultas.Length + 1, falso.Pedidos.Count);
		}

		[Fact]
		public async Task Descargar_KeepsPreviousFileWhenHsreplayRejectsTheQuery()
		{
			Directory.CreateDirectory(_carpeta);
			var anterior = Path.Combine(_carpeta, DescargaHsreplay.Consultas[0].Archivo);
			File.WriteAllText(anterior, "previo");
			var falso = new Falso((p, n) => n == 1
				? Respuesta(HttpStatusCode.Forbidden, "<!DOCTYPE html><title>Just a moment...</title>")
				: Respuesta(HttpStatusCode.OK, Json));

			var r = await Descarga(falso).Descargar(_carpeta, DateTime.Today, CancellationToken.None);

			Assert.Equal("previo", File.ReadAllText(anterior));
			Assert.Equal(DescargaHsreplay.Consultas.Length - 1, r.Descargados.Count);
			var error = Assert.Single(r.Errores);
			Assert.Contains("403", error);
			Assert.Contains(DescargaHsreplay.Consultas[0].Archivo, error);
		}

		[Fact]
		public async Task Descargar_RejectsAnOkResponseThatIsNotJson()
		{
			var falso = new Falso((p, n) => Respuesta(HttpStatusCode.OK, "<html>maintenance</html>"));

			var r = await Descarga(falso).Descargar(_carpeta, DateTime.Today, CancellationToken.None);

			Assert.Empty(r.Descargados);
			Assert.Equal(DescargaHsreplay.Consultas.Length, r.Errores.Count);
			Assert.False(File.Exists(Path.Combine(_carpeta, DescargaHsreplay.Consultas[0].Archivo)));
		}
	}
}
