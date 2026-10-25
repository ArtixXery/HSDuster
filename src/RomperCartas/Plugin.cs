using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.API;
using Hearthstone_Deck_Tracker.Enums.Hearthstone;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Plugins;
using Hearthstone_Deck_Tracker.Utility;
using Core = Hearthstone_Deck_Tracker.API.Core;

namespace RomperCartas
{
	public class Plugin : IPlugin
	{
		public string Name => "Romper Cartas (Wild)";
		public string Description => "Pone un letrero encima de cada carta de la colección diciendo si es seguro romperla en Wild, " +
			"con su % de uso en HSReplay. Se actualiza solo al abrir sobres y descarga las estadísticas una vez al día.";
		public string ButtonText => "Abrir lista";
		public string Author => "ArtixXery";
		public Version Version => new Version(0, 3, 0);
		public MenuItem MenuItem { get; private set; }

		Config _cfg;
		Reglas _reglas;
		Estadisticas _est;
		Ventana _ventana;
		Etiquetas _etiquetas;
		readonly Reconocedor _reconocedor = new Reconocedor();
		CancellationTokenSource _cancelar;
		bool _activo, _descargando, _reconociendo, _etiquetasVisibles;
		DateTime _ultimoMana = DateTime.MinValue, _ultimoReconocimiento = DateTime.MinValue;

		// Resultado de la última clasificación
		List<Fila> _filas = new List<Fila>();
		Dictionary<string, List<Fila>> _porNombre = new Dictionary<string, List<Fila>>();
		Dictionary<string, string> _candidatos = new Dictionary<string, string>();   // cardId → nombre en inglés de las cartas que tienes
		string _estado = "";
		string _progresoArte;

		public void OnLoad()
		{
			_activo = true;
			_cancelar = new CancellationTokenSource();
			Datos.InstalarArchivosIncluidos();
			_cfg = Datos.LeerJson<Config>("config.json");
			Datos.GuardarJson("config.json", _cfg);   // deja el archivo con todos los valores para poder editarlo
			_reglas = Datos.LeerJson<Reglas>("reglas.json");
			_est = Datos.CargarEstadisticas();
			_reconocedor.Cargar();
			_etiquetas = new Etiquetas(Core.OverlayCanvas);

			MenuItem = new MenuItem { Header = "Romper cartas" };
			var abrir = new MenuItem { Header = "Abrir lista" };
			abrir.Click += (s, e) => Abrir();
			var actualizar = new MenuItem { Header = "Actualizar estadísticas ahora" };
			actualizar.Click += (s, e) => _ = ActualizarEstadisticas(true);
			var letreros = new MenuItem { Header = "Letreros sobre las cartas", IsCheckable = true, IsChecked = _cfg.MostrarEtiquetas };
			letreros.Click += (s, e) =>
			{
				_cfg.MostrarEtiquetas = letreros.IsChecked;
				Datos.GuardarJson("config.json", _cfg);
				if (!_cfg.MostrarEtiquetas) OcultarEtiquetas();
			};
			var hsguru = new MenuItem { Header = "Importar mazos de Leyenda de HSGuru…" };
			hsguru.Click += (s, e) => ImportarHsguru();
			MenuItem.Items.Add(abrir);
			MenuItem.Items.Add(actualizar);
			MenuItem.Items.Add(hsguru);
			MenuItem.Items.Add(letreros);

			CollectionHelpers.Hearthstone.OnCollectionChanged += Recalcular;
			GameEvents.OnModeChanged.Add(CambioDeModo);
			if (Datos.HaceFaltaDescargar(_cfg.HorasEntreDescargas)) _ = ActualizarEstadisticas(false);
			Recalcular();
			Datos.Log($"Plugin cargado. Reglas del {_reglas.Generado}, {_reglas.Wild.Count} cartas Wild, {_reconocedor.Cantidad} imágenes de cartas.");
		}

		public void OnUnload()
		{
			_activo = false;
			_cancelar?.Cancel();
			CollectionHelpers.Hearthstone.OnCollectionChanged -= Recalcular;
			_etiquetas?.Quitar();
			_ventana?.Close();
			_ventana = null;
		}

		public void OnButtonPress() => Abrir();

		public void OnUpdate()
		{
			if (!_activo) return;
			var enColeccion = Core.Game.CurrentMode == Mode.COLLECTIONMANAGER;

			// Letreros: reconoce la página como mucho cada 0,8 s mientras estás en la colección
			if (_cfg.MostrarEtiquetas && enColeccion && User32.IsHearthstoneInForeground())
			{
				if (!_reconociendo && DateTime.Now - _ultimoReconocimiento > TimeSpan.FromMilliseconds(800) && _candidatos.Count > 0)
					_ = ReconocerPagina();
			}
			else if (_etiquetasVisibles) OcultarEtiquetas();

			// Ventana: sigue el filtro de maná del juego (como mucho una lectura por segundo)
			if (_ventana == null || !_ventana.IsVisible || _ventana.SeguirMana.IsChecked != true || !enColeccion) return;
			if (DateTime.Now - _ultimoMana < TimeSpan.FromSeconds(1)) return;
			_ultimoMana = DateTime.Now;
			try
			{
				var m = HearthMirror.Reflection.Client.GetCurrentManaFilter();
				_ventana.FiltroMana(m >= 0 && m <= 10 ? m : (int?)null);
			}
			catch { _ventana.FiltroMana(null); }
		}

		void OcultarEtiquetas()
		{
			_etiquetas?.Ocultar();
			_etiquetasVisibles = false;
		}

		async Task ReconocerPagina()
		{
			_reconociendo = true;
			try
			{
				var ventanaHs = User32.GetHearthstoneWindow();
				var rect = User32.GetHearthstoneRect(false);
				if (ventanaHs == IntPtr.Zero || rect.Width <= 0) return;
				using (var captura = await ScreenCapture.CaptureHearthstoneAsync(new System.Drawing.Point(0, 0), rect.Width, rect.Height, ventanaHs, true, null))
				{
					if (captura == null) return;
					var candidatos = _candidatos;
					var res = await Task.Run(() => _reconocedor.Reconocer(captura, candidatos, _cfg.ParecidoMinimo, _cfg.MargenMinimo));
					if (!_activo || !_cfg.MostrarEtiquetas || Core.Game.CurrentMode != Mode.COLLECTIONMANAGER) return;
					_etiquetas.Mostrar(res, captura.Width, captura.Height, _porNombre);
					_etiquetasVisibles = true;
				}
			}
			catch (Exception e)
			{
				Datos.Log("Error al reconocer la página: " + e.Message);
			}
			finally
			{
				_ultimoReconocimiento = DateTime.Now;
				_reconociendo = false;
			}
		}

		void CambioDeModo(Mode modo)
		{
			if (!_activo) return;
			if (modo == Mode.COLLECTIONMANAGER && _cfg.AbrirEnColeccion) Abrir();
			if (modo != Mode.COLLECTIONMANAGER)
			{
				_ventana?.FiltroMana(null);
				OcultarEtiquetas();
			}
		}

		void Abrir()
		{
			if (!_activo) return;
			if (_ventana == null)
			{
				_ventana = new Ventana();
				_ventana.Closed += (s, e) => _ventana = null;
				_ventana.ActualizarPedido += () => _ = ActualizarEstadisticas(true);
			}
			_ventana.Show();
			_ventana.Activate();
			_ventana.Mostrar(_filas, _estado);
		}

		async Task ActualizarEstadisticas(bool forzar)
		{
			if (_descargando) return;
			_descargando = true;
			try
			{
				Recalcular("Actualizando estadísticas en segundo plano…");
				var r = await Datos.Descargar(_cfg, forzar, _cancelar.Token);
				_est = await Task.Run(Datos.CargarEstadisticas);
				_reglas = Datos.LeerJson<Reglas>("reglas.json");
				Recalcular(r);
			}
			finally { _descargando = false; }
		}

		/// <summary>Pide la página de mazos de HSGuru guardada a mano (https://www.hsguru.com/decks?format=1&amp;rank=legend, Ctrl+S).</summary>
		async void ImportarHsguru()
		{
			var dialogo = new Microsoft.Win32.OpenFileDialog
			{
				Title = "Página de mazos de Leyenda de HSGuru guardada con Ctrl+S",
				Filter = "Página web (*.htm;*.html)|*.htm;*.html",
			};
			if (dialogo.ShowDialog() != true) return;
			var archivo = dialogo.FileName;
			var r = await Task.Run(() => Datos.ImportarHsguru(archivo));
			_reglas = Datos.LeerJson<Reglas>("reglas.json");
			Recalcular(r);
		}

		void Recalcular() => Recalcular(null);

		async void Recalcular(string aviso)
		{
			if (!_activo) return;
			try
			{
				if (!CollectionHelpers.Hearthstone.TryGetCollection(out var col) || col == null)
					col = await CollectionHelpers.Hearthstone.GetCollection();
				if (col == null)
				{
					_estado = "Abre Hearthstone para leer tu colección.";
					_ventana?.Mostrar(_filas, _estado);
					return;
				}
				var cartas = col.Cards.ToDictionary(kv => kv.Key, kv => kv.Value);
				var filas = await Task.Run(() => Clasificador.Clasificar(cartas, _reglas, _est, _cfg));
				_filas = filas;
				_porNombre = filas.GroupBy(f => f.NombreEn).ToDictionary(g => g.Key, g => g.ToList());
				_candidatos = cartas.Where(kv => kv.Value.Take(4).Sum() > 0)
					.Select(kv => HearthDb.Cards.GetFromDbfId(kv.Key, false))
					.Where(c => c != null && c.Collectible)
					.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First().Name);

				_estado = $"Colección: {col.Dust:N0} de polvo actual · Estadísticas HSReplay (Wild, Bronce–Oro) al " +
					$"{(_est.Fecha == default ? "—" : _est.Fecha.ToString("dd/MM/yyyy"))} · Reglas del {_reglas.Generado} · " +
					$"Umbrales: conservar ≥{_cfg.UmbralConservar}%, dudosa ≥{_cfg.UmbralDudosa}%";
				if (_reglas.Wild.Count == 0) _estado = "Falta reglas.json en " + Datos.Carpeta + ". " + _estado;
				if (_progresoArte != null) _estado = _progresoArte + " · " + _estado;
				if (aviso != null) _estado = aviso + " · " + _estado;
				_ventana?.Mostrar(_filas, _estado);

				// Imágenes de cartas nuevas (la primera vez son todas: unos minutos en segundo plano)
				_ = _reconocedor.Completar(_candidatos.Keys, p => { _progresoArte = p; Datos.Log(p ?? "Imágenes de cartas completas"); }, _cancelar.Token);
			}
			catch (Exception e)
			{
				Datos.Log("Error al recalcular: " + e);
			}
		}
	}
}
