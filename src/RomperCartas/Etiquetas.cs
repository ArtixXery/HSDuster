using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RomperCartas
{
	/// <summary>Letreros dibujados en el overlay de HDT encima de cada carta reconocida de la colección.</summary>
	public class Etiquetas
	{
		readonly Canvas _canvas;
		readonly Border[] _letreros = new Border[8];

		static readonly Dictionary<Categoria, Color> Colores = new Dictionary<Categoria, Color>
		{
			[Categoria.Romper] = Color.FromRgb(0x2E, 0x7D, 0x32), [Categoria.CopiasExtra] = Color.FromRgb(0x15, 0x65, 0xC0),
			[Categoria.Basico] = Color.FromRgb(0x00, 0x83, 0x8F), [Categoria.Dudosa] = Color.FromRgb(0xEF, 0x6C, 0x00),
			[Categoria.Conservar] = Color.FromRgb(0x8E, 0x24, 0x24), [Categoria.Irrompible] = Color.FromRgb(0x61, 0x61, 0x61),
		};
		static readonly Color Neutro = Color.FromRgb(0x45, 0x45, 0x45);

		public Etiquetas(Canvas canvas)
		{
			_canvas = canvas;
		}

		public void Ocultar()
		{
			foreach (var l in _letreros) if (l != null) l.Visibility = Visibility.Collapsed;
		}

		public void Quitar()
		{
			foreach (var l in _letreros) if (l != null) _canvas.Children.Remove(l);
		}

		/// <param name="filasPorNombre">filas de la clasificación agrupadas por nombre en inglés</param>
		public void Mostrar(Reconocedor.Resultado[] res, int anchoCaptura, int altoCaptura, IDictionary<string, List<Fila>> filasPorNombre)
		{
			if (_canvas.ActualWidth <= 0 || altoCaptura <= 0) return;
			var ex = _canvas.ActualWidth / anchoCaptura;
			var ey = _canvas.ActualHeight / altoCaptura;
			for (var i = 0; i < 8; i++)
			{
				var r = res[i];
				var carta = r == null ? null : HearthDb.Cards.GetFromDbfId(HearthDb.Cards.CardIdToDbfId.TryGetValue(r.CardId, out var d) ? d : 0, false);
				if (carta == null)
				{
					if (_letreros[i] != null) _letreros[i].Visibility = Visibility.Collapsed;
					continue;
				}
				filasPorNombre.TryGetValue(carta.Name, out var filas);
				var (titulo, detalle, color) = Texto(filas, carta);
				var l = _letreros[i] ?? (_letreros[i] = Nuevo());
				var ancho = r.Carta.Width * ex;
				var alto = r.Carta.Height * ey;
				l.Width = ancho * 0.86;
				Canvas.SetLeft(l, r.Carta.X * ex + ancho * 0.07);
				Canvas.SetTop(l, r.Carta.Y * ey + alto * 0.66);
				l.Background = new SolidColorBrush(Color.FromArgb(235, color.R, color.G, color.B));
				var panel = (StackPanel)l.Child;
				var t1 = (TextBlock)panel.Children[0];
				var t2 = (TextBlock)panel.Children[1];
				t1.Text = titulo;
				t1.FontSize = alto * 0.052;
				t2.Text = detalle;
				t2.FontSize = alto * 0.038;
				t2.Visibility = string.IsNullOrEmpty(detalle) ? Visibility.Collapsed : Visibility.Visible;
				l.Visibility = Visibility.Visible;
			}
		}

		Border Nuevo()
		{
			var panel = new StackPanel();
			panel.Children.Add(new TextBlock { Foreground = Brushes.White, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
			panel.Children.Add(new TextBlock { Foreground = Brushes.White, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Opacity = 0.95 });
			var b = new Border
			{
				Child = panel, CornerRadius = new CornerRadius(6), Padding = new Thickness(4, 2, 4, 3),
				BorderBrush = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), BorderThickness = new Thickness(1.5),
				IsHitTestVisible = false, Visibility = Visibility.Collapsed,
			};
			Panel.SetZIndex(b, 1000);
			_canvas.Children.Add(b);
			return b;
		}

		static (string Titulo, string Detalle, Color Color) Texto(List<Fila> filas, HearthDb.Card carta)
		{
			if (filas == null || filas.Count == 0)
				return carta.Set == HearthDb.Enums.CardSet.CORE ? ("Básico · gratis", "", Neutro) : ("Estándar · no romper", "", Neutro);

			var seguras = filas.Where(f => f.Categoria == Categoria.Romper || f.Categoria == Categoria.CopiasExtra || f.Categoria == Categoria.Basico).ToList();
			var uso = filas.Max(f => f.Uso);
			var usoTexto = uso > 0 ? $"uso {uso:0.0}% · {filas.First(f => f.Uso == uso).Donde}" : "sin uso en Wild";
			if (seguras.Count > 0)
			{
				var principal = seguras.OrderBy(f => f.Categoria).First().Categoria;
				var polvo = seguras.Sum(f => f.Polvo);
				var copias = seguras.Sum(f => f.Normales + f.Doradas);
				var titulo = principal == Categoria.Romper ? $"ROMPER  +{polvo}"
					: principal == Categoria.CopiasExtra ? $"SOBRA{(copias > 1 ? "N" : "")} {copias}  +{polvo}"
					: $"ROMPER VIEJA  +{polvo}";
				var detalle = principal == Categoria.Basico ? "tienes la del Básico" : usoTexto;
				return (titulo, detalle, Colores[principal]);
			}
			var cat = filas.Min(f => f.Categoria == Categoria.Irrompible ? Categoria.Irrompible + 1 : f.Categoria);
			if (cat > Categoria.Irrompible) cat = Categoria.Irrompible;
			switch (cat)
			{
				case Categoria.Conservar: return ($"CONSERVAR", usoTexto, Colores[cat]);
				case Categoria.Dudosa:
					var nota = filas.Select(f => f.Nota).FirstOrDefault(n => !string.IsNullOrEmpty(n));
					return ("DUDOSA · esperar", uso >= 0.05 ? usoTexto : nota ?? usoTexto, Colores[cat]);
				default: return ("NO SE PUEDE ROMPER", "regalada", Colores[Categoria.Irrompible]);
			}
		}
	}
}
