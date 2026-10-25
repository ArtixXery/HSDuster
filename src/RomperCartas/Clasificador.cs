using System;
using System.Collections.Generic;
using System.Linq;
using HearthDb.Enums;

namespace RomperCartas
{
	public enum Categoria { Romper, CopiasExtra, Basico, Dudosa, Conservar, Irrompible }

	public class Fila
	{
		public Categoria Categoria { get; set; }
		public int DbfId { get; set; }
		public string Nombre { get; set; }
		public string NombreEn { get; set; }
		public string Rareza { get; set; }
		public string Clase { get; set; }
		public int Coste { get; set; }
		public double Uso { get; set; }
		public string Donde { get; set; }
		public bool EnMazos { get; set; }
		public int Normales { get; set; }
		public int Doradas { get; set; }
		public int Polvo { get; set; }
		public string Nota { get; set; }

		public string CategoriaTexto => Textos.Categoria(Categoria);
		public string UsoTexto => Uso > 0 ? $"{Uso:0.0}%" : "—";
		public string Copias => Doradas > 0 ? $"{Normales} + {Doradas} dor." : Normales.ToString();
	}

	public static class Textos
	{
		public static string Categoria(Categoria c)
		{
			switch (c)
			{
				case RomperCartas.Categoria.Romper: return "Romper";
				case RomperCartas.Categoria.CopiasExtra: return "Copia extra";
				case RomperCartas.Categoria.Basico: return "Dup. Básico";
				case RomperCartas.Categoria.Dudosa: return "Dudosa";
				case RomperCartas.Categoria.Conservar: return "Conservar";
				default: return "Irrompible";
			}
		}

		public static readonly Dictionary<CardClass, string> Clases = new Dictionary<CardClass, string>
		{
			[CardClass.DEATHKNIGHT] = "Caballero de la Muerte", [CardClass.DEMONHUNTER] = "Cazador de Demonios", [CardClass.DRUID] = "Druida",
			[CardClass.HUNTER] = "Cazador", [CardClass.MAGE] = "Mago", [CardClass.PALADIN] = "Paladín", [CardClass.PRIEST] = "Sacerdote",
			[CardClass.ROGUE] = "Pícaro", [CardClass.SHAMAN] = "Chamán", [CardClass.WARLOCK] = "Brujo", [CardClass.WARRIOR] = "Guerrero",
			[CardClass.NEUTRAL] = "Neutral",
		};

		public static readonly Dictionary<Rarity, string> Rarezas = new Dictionary<Rarity, string>
		{
			[Rarity.COMMON] = "Común", [Rarity.RARE] = "Rara", [Rarity.EPIC] = "Épica", [Rarity.LEGENDARY] = "Legendaria",
		};
	}

	public static class Clasificador
	{
		static readonly Dictionary<Rarity, (int Normal, int Dorada)> Polvo = new Dictionary<Rarity, (int, int)>
		{
			[Rarity.COMMON] = (5, 50), [Rarity.RARE] = (20, 100), [Rarity.EPIC] = (100, 400), [Rarity.LEGENDARY] = (400, 1600),
		};

		/// <param name="coleccion">dbfId → [normal, dorada, diamante, firma]</param>
		public static List<Fila> Clasificar(IDictionary<int, int[]> coleccion, Reglas reglas, Estadisticas est, Config cfg)
		{
			var filas = new List<Fila>();
			foreach (var kv in coleccion)
			{
				var dbf = kv.Key;
				var v = kv.Value;
				int n = v.Length > 0 ? v[0] : 0, g = v.Length > 1 ? v[1] : 0;
				int total = v.Take(4).Sum();
				if (total == 0) continue;
				var esEstandar = reglas.Estandar.Contains(dbf);
				if (!esEstandar && !reglas.Wild.Contains(dbf)) continue;
				var carta = HearthDb.Cards.GetFromDbfId(dbf, false);
				if (carta == null || !Polvo.TryGetValue(carta.Rarity, out var precio)) continue;

				var lockN = reglas.IrrompibleNormal.Contains(dbf);
				var lockG = reglas.IrrompibleDorada.Contains(dbf);
				int bn = lockN ? 0 : n, bg = lockG ? 0 : g;   // copias que el juego deja romper
				var max = carta.Rarity == Rarity.LEGENDARY ? 1 : 2;
				est.Uso.TryGetValue(carta.Name, out var uso);
				var enMazos = est.EnMazos.Contains(carta.Name);

				Fila Nueva(Categoria cat, int normales, int doradas, string nota) => new Fila
				{
					Categoria = cat, DbfId = dbf, Nombre = carta.GetLocName(Locale.esMX) ?? carta.Name, NombreEn = carta.Name,
					Rareza = Textos.Rarezas[carta.Rarity], Clase = Textos.Clases.TryGetValue(carta.Class, out var c) ? c : carta.Class.ToString(),
					Coste = carta.Cost, Uso = uso.Pct, Donde = uso.Donde ?? "", EnMazos = enMazos,
					Normales = normales, Doradas = doradas, Polvo = normales * precio.Normal + doradas * precio.Dorada, Nota = nota,
				};

				void AgregarExtras(string nota)
				{
					var extra = Math.Max(total - max, 0);
					var en = Math.Min(extra, bn);
					var eg = Math.Min(extra - en, bg);
					if (en + eg > 0) filas.Add(Nueva(Categoria.CopiasExtra, en, eg, nota));
				}

				if ((lockN && n > 0) || (lockG && g > 0))
				{
					var motivo = lockN && lockG ? "regalada (normal y dorada)" : lockN ? "la normal fue regalada" : "la dorada fue regalada";
					filas.Add(Nueva(Categoria.Irrompible, lockN ? n : 0, lockG ? g : 0, motivo));
				}

				if (esEstandar)
				{
					AgregarExtras("Sobran copias (carta de Estándar)");
					continue;
				}
				if (bn + bg == 0) continue;
				if (reglas.Basico.Contains(dbf))
				{
					filas.Add(Nueva(Categoria.Basico, bn, bg, "Tienes la versión gratuita del Básico"));
					continue;
				}

				var og = reglas.DiosesAntiguos.Contains(dbf);
				var leyenda = reglas.Leyenda.Contains(carta.Name);
				var sinDatos = est.Uso.Count == 0;   // sin estadísticas nunca se recomienda romper
				Categoria cat;
				if (enMazos || uso.Pct >= cfg.UmbralConservar) cat = Categoria.Conservar;
				else if (uso.Pct >= cfg.UmbralDudosa || og || leyenda || sinDatos) cat = Categoria.Dudosa;
				else cat = Categoria.Romper;

				if (cat != Categoria.Romper) AgregarExtras($"Sobran copias; la carta está en «{Textos.Categoria(cat)}»");
				var notas = new List<string>();
				if (enMazos) notas.Add("en mazos de la expansión nueva");
				if (sinDatos) notas.Add("sin estadísticas de uso todavía");
				else if (cat == Categoria.Dudosa && uso.Pct < cfg.UmbralDudosa)
				{
					if (og) notas.Add("Dios Antiguo");
					if (leyenda) notas.Add("usada en mazos de Leyenda");
				}
				if (lockN || lockG) notas.Add("solo cuenta la versión que se puede romper");
				filas.Add(Nueva(cat, bn, bg, string.Join("; ", notas)));
			}
			return filas;
		}
	}
}
