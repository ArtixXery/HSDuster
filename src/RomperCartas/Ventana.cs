using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace RomperCartas
{
	/// <summary>Ventana con la lista de cartas y su clasificación. Se arma en código para no depender de XAML.</summary>
	public class Ventana : Window
	{
		static readonly (Categoria Cat, bool Activa, string Color)[] Categorias =
		{
			(Categoria.Romper, true, "#2E7D32"), (Categoria.CopiasExtra, true, "#1565C0"), (Categoria.Basico, true, "#00838F"),
			(Categoria.Dudosa, false, "#EF6C00"), (Categoria.Conservar, false, "#6D4C41"), (Categoria.Irrompible, false, "#757575"),
		};
		static readonly HashSet<Categoria> Seguras = new HashSet<Categoria> { Categoria.Romper, Categoria.CopiasExtra, Categoria.Basico };

		readonly Dictionary<Categoria, CheckBox> _checks = new Dictionary<Categoria, CheckBox>();
		readonly TextBlock _resumen = new TextBlock { FontSize = 15, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
		readonly TextBlock _estado = new TextBlock { Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 6) };
		readonly TextBlock _mana = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
		readonly ComboBox _clase = new ComboBox { Width = 170, Margin = new Thickness(0, 0, 8, 0) };
		readonly TextBox _buscar = new TextBox { Width = 160 };
		readonly DataGrid _grid;
		List<Fila> _filas = new List<Fila>();
		ICollectionView _vista;
		int? _filtroMana;

		public event Action ActualizarPedido;
		public CheckBox SeguirMana { get; } = new CheckBox { Content = "Seguir filtro de maná del juego", IsChecked = true, VerticalAlignment = VerticalAlignment.Center };

		public Ventana()
		{
			Title = "Romper cartas (Wild)";
			Width = 900; Height = 640; MinWidth = 600; MinHeight = 300;
			WindowStartupLocation = WindowStartupLocation.Manual; Left = 40; Top = 80;
			Topmost = true;

			var raiz = new DockPanel { Margin = new Thickness(10) };
			var cabecera = new StackPanel();
			cabecera.Children.Add(_resumen);
			cabecera.Children.Add(_estado);

			var filtros = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
			foreach (var (cat, activa, color) in Categorias)
			{
				var cb = new CheckBox
				{
					Content = Textos.Categoria(cat), IsChecked = activa, Margin = new Thickness(0, 0, 12, 4),
					Foreground = (Brush)new BrushConverter().ConvertFromString(color), FontWeight = FontWeights.SemiBold,
				};
				cb.Checked += (s, e) => Refiltrar();
				cb.Unchecked += (s, e) => Refiltrar();
				_checks[cat] = cb;
				filtros.Children.Add(cb);
			}
			cabecera.Children.Add(filtros);

			var barra = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
			_clase.SelectionChanged += (s, e) => Refiltrar();
			_buscar.TextChanged += (s, e) => Refiltrar();
			barra.Children.Add(_clase);
			barra.Children.Add(new TextBlock { Text = "Buscar:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
			barra.Children.Add(_buscar);
			barra.Children.Add(new Border { Width = 12 });
			SeguirMana.Checked += (s, e) => Refiltrar();
			SeguirMana.Unchecked += (s, e) => Refiltrar();
			barra.Children.Add(SeguirMana);
			barra.Children.Add(_mana);
			cabecera.Children.Add(barra);

			var botones = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
			var actualizar = new Button { Content = "Actualizar estadísticas", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 8, 0) };
			actualizar.Click += (s, e) => ActualizarPedido?.Invoke();
			var exportar = new Button { Content = "Exportar CSV", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 8, 0) };
			exportar.Click += (s, e) => Exportar();
			var fijar = new CheckBox { Content = "Siempre visible", IsChecked = true, VerticalAlignment = VerticalAlignment.Center };
			fijar.Checked += (s, e) => Topmost = true;
			fijar.Unchecked += (s, e) => Topmost = false;
			botones.Children.Add(actualizar);
			botones.Children.Add(exportar);
			botones.Children.Add(fijar);
			cabecera.Children.Add(botones);

			DockPanel.SetDock(cabecera, Dock.Top);
			raiz.Children.Add(cabecera);

			_grid = new DataGrid
			{
				AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, HeadersVisibility = DataGridHeadersVisibility.Column,
				GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, RowHeaderWidth = 0, SelectionMode = DataGridSelectionMode.Extended,
			};
			Columna("Estado", nameof(Fila.CategoriaTexto), 90);
			Columna("Carta", nameof(Fila.Nombre), 200);
			Columna("Rareza", nameof(Fila.Rareza), 80);
			Columna("Clase", nameof(Fila.Clase), 120);
			Columna("Maná", nameof(Fila.Coste), 45);
			Columna("% uso", nameof(Fila.UsoTexto), 60, nameof(Fila.Uso));
			Columna("Dónde se usa más", nameof(Fila.Donde), 170);
			Columna("Romper", nameof(Fila.Copias), 80);
			Columna("Polvo", nameof(Fila.Polvo), 60);
			Columna("Nota", nameof(Fila.Nota), 260);
			var estilo = new Style(typeof(DataGridRow));
			foreach (var (cat, _, color) in Categorias)
			{
				var t = new DataTrigger { Binding = new Binding(nameof(Fila.Categoria)), Value = cat };
				var b = (SolidColorBrush)new BrushConverter().ConvertFromString(color);
				t.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(Color.FromArgb(28, b.Color.R, b.Color.G, b.Color.B))));
				estilo.Triggers.Add(t);
			}
			_grid.RowStyle = estilo;
			raiz.Children.Add(_grid);
			Content = raiz;
		}

		void Columna(string titulo, string propiedad, double ancho, string orden = null) =>
			_grid.Columns.Add(new DataGridTextColumn
			{
				Header = titulo, Binding = new Binding(propiedad), Width = ancho, SortMemberPath = orden ?? propiedad,
			});

		public void Mostrar(List<Fila> filas, string estado)
		{
			_filas = filas;
			var seguras = filas.Where(f => Seguras.Contains(f.Categoria)).ToList();
			_resumen.Text = $"Seguro romper: {seguras.Sum(f => f.Polvo):N0} de polvo  " +
				$"(Romper {Suma(Categoria.Romper):N0} · Copias extra {Suma(Categoria.CopiasExtra):N0} · Dup. Básico {Suma(Categoria.Basico):N0})";
			_estado.Text = estado;

			var claseActual = _clase.SelectedItem as string;
			_clase.ItemsSource = new[] { "Todas las clases" }.Concat(filas.Select(f => f.Clase).Distinct().OrderBy(c => c)).ToList();
			_clase.SelectedItem = claseActual ?? "Todas las clases";
			if (_clase.SelectedItem == null) _clase.SelectedIndex = 0;

			_vista = CollectionViewSource.GetDefaultView(filas.OrderByDescending(f => f.Polvo).ToList());
			_vista.Filter = Filtro;
			_grid.ItemsSource = _vista;
		}

		int Suma(Categoria c) => _filas.Where(f => f.Categoria == c).Sum(f => f.Polvo);

		/// <summary>Filtro de maná leído del juego: null = sin filtro, 7 = 7 o más.</summary>
		public void FiltroMana(int? mana)
		{
			if (mana == _filtroMana) return;
			_filtroMana = mana;
			_mana.Text = mana == null ? "" : $"(maná {(mana >= 7 ? "7+" : mana.ToString())})";
			Refiltrar();
		}

		void Refiltrar() => _vista?.Refresh();

		bool Filtro(object o)
		{
			var f = (Fila)o;
			if (!_checks[f.Categoria].IsChecked.GetValueOrDefault()) return false;
			if (_clase.SelectedIndex > 0 && f.Clase != (string)_clase.SelectedItem) return false;
			if (SeguirMana.IsChecked == true && _filtroMana != null && (_filtroMana >= 7 ? f.Coste < 7 : f.Coste != _filtroMana)) return false;
			var q = _buscar.Text.Trim();
			return q.Length == 0 || f.Nombre.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
				|| f.NombreEn.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
		}

		void Exportar()
		{
			var ruta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), $"romper_cartas_{DateTime.Now:yyyy-MM-dd}.csv");
			var sb = new StringBuilder("Estado;Carta;Carta (inglés);Rareza;Clase;Maná;% uso;Dónde;Normales;Doradas;Polvo;Nota\n");
			foreach (var f in _filas.OrderBy(f => f.Categoria).ThenByDescending(f => f.Polvo))
				sb.AppendLine(string.Join(";", f.CategoriaTexto, f.Nombre, f.NombreEn, f.Rareza, f.Clase, f.Coste, f.Uso, f.Donde, f.Normales, f.Doradas, f.Polvo, f.Nota));
			File.WriteAllText(ruta, sb.ToString(), new UTF8Encoding(true));
			MessageBox.Show(this, $"Exportado a:\n{ruta}", "Romper cartas");
		}
	}
}
