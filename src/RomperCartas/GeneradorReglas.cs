using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HearthDb;
using HearthDb.Enums;
using Newtonsoft.Json.Linq;

namespace RomperCartas
{
	/// <summary>Genera reglas.json con los datos de cartas que HDT ya tiene cargados (HearthDb, que viene de HearthSim/hsdata)
	/// y los sets de Estándar que se deducen de las estadísticas de Estándar de HSReplay.</summary>
	public static class GeneradorReglas
	{
		// Sets que no se fabrican ni cuentan como Wild: Básico actual, eventos, Clásico y cartas especiales
		static readonly HashSet<CardSet> Excluidos = new HashSet<CardSet>
		{
			CardSet.CORE, CardSet.EVENT, CardSet.VANILLA, CardSet.PLACEHOLDER_202204, CardSet.HERO_SKINS,
		};

		static readonly HashSet<Rarity> Rarezas = new HashSet<Rarity> { Rarity.COMMON, Rarity.RARE, Rarity.EPIC, Rarity.LEGENDARY };

		static readonly Regex DiosesAntiguos = new Regex("C'Thun|N'Zoth|Yogg|Y'Shaarj|Corrupt", RegexOptions.IgnoreCase);
		static readonly Regex Etiquetas = new Regex("<[^>]+>");

		/// <summary>Cartas vistas en mazos de Leyenda de Wild (revisión manual, octubre de 2026). Los mazos actuales de Leyenda
		/// salen de Firestone y se aplican con las estadísticas, no aquí.</summary>
		public static readonly string[] LeyendaManual =
		{
			"Archmage Antonidas", "Astalor Bloodsworn", "Blademaster Okani", "E.T.C., Band Manager", "E.T.C., God of Metal", "Edwin, Defias Kingpin",
			"Exarch Maladaar", "Final Showdown", "Kazakus, Golem Shaper", "Kazakusan", "Kerrigan, Queen of Blades", "Loatheb", "Magatha, Bane of Music",
			"Patches the Pilot", "Patches the Pirate", "Prince Renathal", "Raza the Chained", "Reno Jackson", "Reno, Lone Ranger", "Reska, the Pit Boss",
			"Seek Guidance", "Shadow of Demise", "Sir Finley, Sea Guide", "The Ceaseless Expanse", "Theotar, the Mad Duke", "Thunderbringer", "Tickatus",
			"Timewinder Zarimi", "Yogg-Saron, Master of Fate", "Zephrys the Great", "Zilliax Deluxe 3000",
			"Brawl", "Darkglare", "Dirty Rat", "Doomsayer", "Elwynn Boar", "Embiggen", "Felosophy", "Force of Nature", "Glowstone Technician", "Ice Block",
			"Libram of Hope", "Mecha-Leaper", "Murloc Warleader", "Playhouse Giant", "Potion of Illusion", "Preparation", "Psychic Scream", "Razorscale",
			"Sea Giant", "Secret Passage", "Shadow Visions", "Shield Slam", "Smothering Starfish", "Southsea Captain",
		};

		/// <summary>Sets de Estándar: los que tienen al menos la mitad de sus cartas con uso en las estadísticas de Estándar.
		/// HSReplay también lista con 0% algunas cartas de sets rotados, por eso se exige uso mayor que cero.</summary>
		public static CardSet[] SetsEstandar(IEnumerable<Card> coleccionables, JObject usoEstandar)
		{
			var cartas = coleccionables.Where(c => Rarezas.Contains(c.Rarity) && !Excluidos.Contains(c.Set)).ToDictionary(c => c.DbfId);
			var tamano = cartas.Values.GroupBy(c => c.Set).ToDictionary(g => g.Key, g => g.Count());
			var jugadas = new HashSet<int>();
			if (usoEstandar?["series"]?["data"] is JObject series)
				foreach (var serie in series.Properties())
				foreach (var fila in serie.Value)
					if ((double?)fila["included_popularity"] > 0 && fila["dbf_id"] != null) jugadas.Add((int)fila["dbf_id"]);
			return jugadas.Where(cartas.ContainsKey).GroupBy(d => cartas[d].Set)
				.Where(g => g.Count() * 2 >= tamano[g.Key]).Select(g => g.Key).OrderBy(s => s).ToArray();
		}

		/// <summary>Estándar tiene entre 5 y 12 sets según el momento del año. Fuera de un margen amplio, la respuesta de HSReplay
		/// no es de Estándar (por ejemplo, datos de Wild darían casi todos los sets) y no se usa.</summary>
		public static bool SonSetsEstandarCreibles(IReadOnlyCollection<CardSet> sets) => sets.Count >= 3 && sets.Count <= 15;

		public static Reglas Generar(IEnumerable<Card> coleccionables, ISet<CardSet> estandar)
		{
			var todas = coleccionables.ToList();
			var nombresBasico = new HashSet<string>(todas.Where(c => c.Set == CardSet.CORE).Select(Nombre));
			var r = new Reglas
			{
				Generado = DateTime.Today.ToString("yyyy-MM-dd"),
				Leyenda = new HashSet<string>(LeyendaManual),
				SetsEstandar = estandar.Select(s => s.ToString()).OrderBy(s => s).ToList(),
			};
			foreach (var c in todas)
			{
				if (!Rarezas.Contains(c.Rarity) || Excluidos.Contains(c.Set)) continue;
				var dbf = c.DbfId;
				if (estandar.Contains(c.Set)) r.Estandar.Add(dbf);
				else r.Wild.Add(dbf);
				var nombre = Nombre(c);
				if (nombresBasico.Contains(nombre)) r.Basico.Add(dbf);
				if (Irrompibles.Normal.Contains(c.Id) || c.Set == CardSet.DEMON_HUNTER_INITIATE || Regalada(c, GameTag.HOW_TO_EARN))
					r.IrrompibleNormal.Add(dbf);
				if (Irrompibles.Dorada.Contains(c.Id) || Regalada(c, GameTag.HOW_TO_EARN_GOLDEN)) r.IrrompibleDorada.Add(dbf);
				if (DiosesAntiguos.IsMatch(nombre) || DiosesAntiguos.IsMatch(c.Entity.GetLocString(GameTag.CARDTEXT, Locale.enUS) ?? ""))
					r.DiosesAntiguos.Add(dbf);
			}
			return r;
		}

		static string Nombre(Card c) => c.GetLocName(Locale.enUS) ?? "";

		/// <summary>La carta (o su dorada) se consigue gratis: el texto de cómo obtenerla no habla de fabricarla ni de una aventura.</summary>
		static bool Regalada(Card c, GameTag tag)
		{
			var texto = Etiquetas.Replace(c.Entity.GetLocString(tag, Locale.enUS) ?? "", "");
			if (texto.Length == 0) return false;
			var t = texto.ToLowerInvariant();
			return !t.Contains("crafting") && !t.Contains("adventure");
		}
	}
}
