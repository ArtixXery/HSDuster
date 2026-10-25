using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace RomperCartas
{
	/// <summary>Reglas fijas de las cartas: las genera GeneradorReglas y se guardan en reglas.json.</summary>
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
		[JsonProperty("sets_estandar")] public List<string> SetsEstandar { get; set; } = new List<string>();
	}

	public class Config
	{
		public double UmbralConservar { get; set; } = 5.0;
		public double UmbralDudosa { get; set; } = 2.0;
		/// <summary>% de partidas de Leyenda–Diamante (Firestone) desde el que una carta queda al menos como Dudosa.</summary>
		public double UmbralLeyenda { get; set; } = 0.5;
		public int HorasEntreDescargas { get; set; } = 24;
		public bool AbrirEnColeccion { get; set; } = false;
		public bool MostrarEtiquetas { get; set; } = true;
		public float ParecidoMinimo { get; set; } = 0.85f;   // reconocimiento de cartas en pantalla
		public float MargenMinimo { get; set; } = 0.12f;
		public bool SincronizarFiltroMana { get; set; } = true;
	}

	/// <summary>Uso de cada carta en Wild (por nombre en inglés), cartas vistas en mazos actuales y
	/// % de partidas de Leyenda–Diamante en que aparece cada carta (Firestone).</summary>
	public class Estadisticas
	{
		public Dictionary<string, (double Pct, string Donde)> Uso { get; } = new Dictionary<string, (double, string)>();
		public HashSet<string> EnMazos { get; } = new HashSet<string>();
		public Dictionary<string, double> Leyenda { get; } = new Dictionary<string, double>();
		public DateTime Fecha { get; set; }
	}
}
