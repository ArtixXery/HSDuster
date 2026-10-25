using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RomperCartas
{
	/// <summary>Aviso en el overlay de HDT mientras se preparan las imágenes de las cartas para reconocerlas.
	/// La primera vez son miles de imágenes y tarda unos minutos; mientras tanto los letreros van apareciendo de a poco.</summary>
	public class AvisoProgreso
	{
		readonly Canvas _canvas;
		Border _caja;
		TextBlock _titulo, _detalle;
		ProgressBar _barra;

		public AvisoProgreso(Canvas canvas)
		{
			_canvas = canvas;
		}

		public void Mostrar(int hechas, int total)
		{
			Preparar();
			_titulo.Text = "Romper Cartas · analizando tu colección";
			_detalle.Text = $"Preparando el reconocimiento de cartas: {hechas:N0} de {total:N0} (solo la primera vez, unos minutos). " +
				"Los letreros van apareciendo solos.";
			_barra.Maximum = total;
			_barra.Value = hechas;
			_barra.Visibility = Visibility.Visible;
			Ubicar();
		}

		public void MostrarListo()
		{
			Preparar();
			_titulo.Text = "Romper Cartas · colección lista";
			_detalle.Text = "Ya se reconocen todas tus cartas.";
			_barra.Visibility = Visibility.Collapsed;
			Ubicar();
		}

		public void Ocultar()
		{
			if (_caja != null) _caja.Visibility = Visibility.Collapsed;
		}

		public void Quitar()
		{
			if (_caja != null) _canvas.Children.Remove(_caja);
		}

		void Ubicar()
		{
			var ancho = System.Math.Max(_canvas.ActualWidth * 0.32, 360);
			_caja.Width = ancho;
			Canvas.SetLeft(_caja, (_canvas.ActualWidth - ancho) / 2);
			Canvas.SetTop(_caja, _canvas.ActualHeight * 0.03);
			_caja.Visibility = Visibility.Visible;
		}

		void Preparar()
		{
			if (_caja != null) return;
			_titulo = new TextBlock { Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 16, TextAlignment = TextAlignment.Center };
			_detalle = new TextBlock { Foreground = Brushes.White, FontSize = 13, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) };
			_barra = new ProgressBar
			{
				Height = 8, Margin = new Thickness(0, 6, 0, 0), Minimum = 0,
				Foreground = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)), Background = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
			};
			var panel = new StackPanel();
			panel.Children.Add(_titulo);
			panel.Children.Add(_detalle);
			panel.Children.Add(_barra);
			_caja = new Border
			{
				Child = panel, CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8, 12, 10),
				Background = new SolidColorBrush(Color.FromArgb(225, 0x30, 0x30, 0x30)),
				BorderBrush = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), BorderThickness = new Thickness(1.5),
				IsHitTestVisible = false, Visibility = Visibility.Collapsed,
			};
			Panel.SetZIndex(_caja, 1001);
			_canvas.Children.Add(_caja);
		}
	}
}
