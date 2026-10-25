using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RomperCartas
{
	/// <summary>Reconoce las cartas de la página de colección comparando la ilustración de cada casilla
	/// con las imágenes oficiales de las cartas (art.hearthstonejson.com), reducidas a una miniatura de 12×12.</summary>
	public class Reconocedor
	{
		const int N = 12;
		const int Dim = N * N * 3;
		// Región de la ilustración dentro de la carta (relativa al rectángulo de la carta)
		static readonly RectangleF Arte = new RectangleF(0.30f, 0.14f, 0.40f, 0.26f);
		// Las cartas del juego no siempre caen exactas en la casilla (las legendarias especiales se ven más grandes)
		static readonly float[] Desplazamientos = { -0.09f, -0.06f, -0.03f, 0f, 0.03f, 0.06f, 0.09f };
		static readonly float[] Escalas = { 0.9f, 1f, 1.1f, 1.2f };
		static readonly (float Dx, float Dy, float Escala)[] Gruesos =
			(from dx in new[] { -0.06f, 0f, 0.06f } from dy in new[] { -0.06f, 0f, 0.06f } from e in new[] { 1f, 1.1f } select (dx, dy, e)).ToArray();

		// Geometría de la página de colección en unidades del alto de la ventana, con x relativa al centro
		// (medida a 2560×1440: 4 columnas × 2 filas)
		const float X0 = -0.515f, DX = 0.2243f, Y0 = 0.307f, DY = 0.351f, AnchoCarta = 0.191f, AltoCarta = 0.293f;

		readonly string _archivo = Datos.Ruta("arte.bin");
		readonly ConcurrentDictionary<string, float[]> _rasgos = new ConcurrentDictionary<string, float[]>();
		readonly (float[] Rasgo, Resultado Res)[] _ultimo = new (float[], Resultado)[8];

		public int Cantidad => _rasgos.Count;

		public class Resultado
		{
			public string CardId;
			public float Parecido;
			public float Segundo;
			public RectangleF Carta;   // en píxeles de la captura
		}

		public static RectangleF[] Casillas(int ancho, int alto)
		{
			var r = new RectangleF[8];
			for (var fila = 0; fila < 2; fila++)
			for (var col = 0; col < 4; col++)
			{
				var cx = ancho / 2f + (X0 + col * DX) * alto;
				var cy = (Y0 + fila * DY) * alto;
				r[fila * 4 + col] = new RectangleF(cx - AnchoCarta * alto / 2, cy - AltoCarta * alto / 2, AnchoCarta * alto, AltoCarta * alto);
			}
			return r;
		}

		/// <summary>Miniatura normalizada (media 0, norma 1) de la ilustración de una carta.</summary>
		public static float[] Rasgo(Bitmap img, RectangleF carta, float dx = 0, float dy = 0, float escala = 1)
		{
			var cx = carta.X + (Arte.X + Arte.Width / 2 + dx) * carta.Width;
			var cy = carta.Y + (Arte.Y + Arte.Height / 2 + dy) * carta.Height;
			var w = Arte.Width * carta.Width * escala;
			var h = Arte.Height * carta.Height * escala;
			var src = new RectangleF(cx - w / 2, cy - h / 2, w, h);
			var v = new float[Dim];
			using (var mini = new Bitmap(N, N, PixelFormat.Format24bppRgb))
			{
				using (var g = Graphics.FromImage(mini))
				{
					g.InterpolationMode = InterpolationMode.HighQualityBilinear;
					g.PixelOffsetMode = PixelOffsetMode.HighQuality;
					g.DrawImage(img, new Rectangle(0, 0, N, N), src, GraphicsUnit.Pixel);
				}
				var datos = mini.LockBits(new Rectangle(0, 0, N, N), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
				try
				{
					var fila = new byte[datos.Stride];
					for (var y = 0; y < N; y++)
					{
						System.Runtime.InteropServices.Marshal.Copy(datos.Scan0 + y * datos.Stride, fila, 0, datos.Stride);
						for (var x = 0; x < N * 3; x++) v[y * N * 3 + x] = fila[x];
					}
				}
				finally { mini.UnlockBits(datos); }
			}
			var media = v.Average();
			double norma = 0;
			for (var i = 0; i < Dim; i++) { v[i] -= media; norma += v[i] * v[i]; }
			var k = (float)(1 / (Math.Sqrt(norma) + 1e-6));
			for (var i = 0; i < Dim; i++) v[i] *= k;
			return v;
		}

		static float Producto(float[] a, float[] b)
		{
			float s = 0;
			for (var i = 0; i < Dim; i++) s += a[i] * b[i];
			return s;
		}

		/// <summary>Rectángulo de la carta dentro de su imagen. Las imágenes oficiales (256×388) tienen siempre la carta
		/// en el mismo lugar, como las casillas del juego; se usa el marco de esbirro sin dragón de legendaria.</summary>
		static RectangleF Contenido(Bitmap img)
		{
			if (img.Width == 256 && img.Height == 388) return new RectangleF(9, 32, 226, 315);
			return Opaco(img);
		}

		static RectangleF Opaco(Bitmap img)
		{
			var datos = img.LockBits(new Rectangle(0, 0, img.Width, img.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			try
			{
				int x0 = img.Width, y0 = img.Height, x1 = -1, y1 = -1;
				var fila = new byte[datos.Stride];
				for (var y = 0; y < img.Height; y++)
				{
					System.Runtime.InteropServices.Marshal.Copy(datos.Scan0 + y * datos.Stride, fila, 0, datos.Stride);
					for (var x = 0; x < img.Width; x++)
						if (fila[x * 4 + 3] > 10)
						{
							if (x < x0) x0 = x; if (x > x1) x1 = x;
							if (y < y0) y0 = y; if (y > y1) y1 = y;
						}
				}
				return x1 < 0 ? new RectangleF(0, 0, img.Width, img.Height) : new RectangleF(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
			}
			finally { img.UnlockBits(datos); }
		}

		public void Agregar(string cardId, Bitmap imagenCarta) => _rasgos[cardId] = Rasgo(imagenCarta, Contenido(imagenCarta));

		public void Cargar()
		{
			if (!File.Exists(_archivo)) return;
			try
			{
				using (var br = new BinaryReader(File.OpenRead(_archivo)))
				{
					var n = br.ReadInt32();
					for (var i = 0; i < n; i++)
					{
						var id = br.ReadString();
						var v = new float[Dim];
						for (var j = 0; j < Dim; j++) v[j] = br.ReadSingle();
						_rasgos[id] = v;
					}
				}
			}
			catch (Exception e) { Datos.Log("No se pudo leer arte.bin: " + e.Message); }
		}

		void Guardar()
		{
			var tmp = _archivo + ".tmp";
			using (var bw = new BinaryWriter(File.Create(tmp)))
			{
				var copia = _rasgos.ToArray();
				bw.Write(copia.Length);
				foreach (var kv in copia)
				{
					bw.Write(kv.Key);
					foreach (var f in kv.Value) bw.Write(f);
				}
			}
			if (File.Exists(_archivo)) File.Delete(_archivo);
			File.Move(tmp, _archivo);
		}

		/// <summary>Descarga las imágenes de las cartas que falten y guarda solo su miniatura (unos 2 KB por carta).</summary>
		public async Task Completar(IEnumerable<string> cardIds, Action<string> progreso, CancellationToken ct)
		{
			var faltan = cardIds.Where(id => !_rasgos.ContainsKey(id)).Distinct().ToList();
			if (faltan.Count == 0) return;
			progreso?.Invoke($"Preparando reconocimiento: 0/{faltan.Count} imágenes de cartas…");
			var hechas = 0;
			using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
			using (var sem = new SemaphoreSlim(4))
			{
				http.DefaultRequestHeaders.UserAgent.ParseAdd("RomperCartas-HDT-plugin/0.2");
				var tareas = faltan.Select(async id =>
				{
					await sem.WaitAsync(ct);
					try
					{
						for (var intento = 0; intento < 3 && !ct.IsCancellationRequested; intento++)
						{
							try
							{
								var bytes = await http.GetByteArrayAsync($"https://art.hearthstonejson.com/v1/render/latest/enUS/256x/{id}.png");
								using (var ms = new MemoryStream(bytes))
								using (var img = new Bitmap(ms))
									_rasgos[id] = Rasgo(img, Contenido(img));
								break;
							}
							catch (HttpRequestException) { await Task.Delay(2000, ct); }
						}
					}
					catch (Exception e) when (!(e is OperationCanceledException)) { Datos.Log($"Imagen {id}: {e.Message}"); }
					finally
					{
						sem.Release();
						var n = Interlocked.Increment(ref hechas);
						if (n % 100 == 0) { progreso?.Invoke($"Preparando reconocimiento: {n}/{faltan.Count} imágenes de cartas…"); Guardar(); }
					}
				});
				await Task.WhenAll(tareas);
			}
			Guardar();
			progreso?.Invoke(null);
			Datos.Log($"Reconocimiento listo: {_rasgos.Count} cartas");
		}

		/// <summary>Reconoce las 8 casillas de la página. candidatos: cardId → nombre (para no confundir versiones con el mismo arte).</summary>
		public Resultado[] Reconocer(Bitmap captura, IDictionary<string, string> candidatos, float minimo, float margen)
		{
			var lista = candidatos.Keys.Where(_rasgos.ContainsKey).ToArray();
			var matriz = lista.Select(id => _rasgos[id]).ToArray();
			var res = new Resultado[8];
			var casillas = Casillas(captura.Width, captura.Height);
			for (var s = 0; s < 8; s++)
			{
				var r0 = Rasgo(captura, casillas[s]);
				var previo = _ultimo[s];
				if (previo.Rasgo != null && Producto(previo.Rasgo, r0) > 0.995f)
				{
					res[s] = previo.Res;   // la casilla no cambió
					continue;
				}
				// Preselección con pocos desplazamientos para quedarse con 6 candidatas; después se ajusta la posición solo con esas
				var gruesos = Gruesos.Select(d => Rasgo(captura, casillas[s], d.Dx, d.Dy, d.Escala)).ToArray();
				// Las 2 mejores de cada recorte grueso (una carta desplazada solo destaca en su recorte)
				var top = gruesos.SelectMany(g => Enumerable.Range(0, lista.Length).Select(i => (i, p: Producto(matriz[i], g)))
					.OrderByDescending(x => x.p).Take(2).Select(x => x.i)).Distinct().ToArray();
				var mejores = new Dictionary<int, float>();
				foreach (var dx in Desplazamientos)
				foreach (var dy in Desplazamientos)
				foreach (var esc in Escalas)
				{
					var r = Rasgo(captura, casillas[s], dx, dy, esc);
					foreach (var i in top)
					{
						var p = Producto(matriz[i], r);
						if (!mejores.TryGetValue(i, out var m) || p > m) mejores[i] = p;
					}
				}
				var orden = mejores.OrderByDescending(kv => kv.Value).ToList();
				Resultado resultado = null;
				if (orden.Count > 0)
				{
					var id = lista[orden[0].Key];
					var nombre = candidatos[id];
					var segundo = orden.Skip(1).Where(kv => candidatos[lista[kv.Key]] != nombre).Select(kv => kv.Value).DefaultIfEmpty(0).First();
					if (orden[0].Value >= minimo && orden[0].Value - segundo >= margen)
						resultado = new Resultado { CardId = id, Parecido = orden[0].Value, Segundo = segundo, Carta = casillas[s] };
				}
				res[s] = resultado;
				_ultimo[s] = (r0, resultado);
			}
			return res;
		}
	}
}
