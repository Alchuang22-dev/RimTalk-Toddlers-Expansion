using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RatBabyMod
{
	internal static class RatBabyResolver
	{
		private static readonly string[] PreferredFactionDefNames =
		{
			"Rakinia",
		};

		private static readonly string[] PreferredBabyKindDefNames =
		{
			"RatkinVagabond",
			"RatkinColonist",
			"RatkinServant",
			"RatkinMercenaryLight",
		};

		private static readonly string[] PreferredMotherKindDefNames =
		{
			"RatkinColonist",
			"RatkinVagabond",
			"RatkinServant",
			"RatkinNoble",
			"RatkinMercenaryLight",
		};

		private static readonly string[] PreferredAnimalKindDefNames =
		{
			"Rottie",
			"Rat",
		};

		private const string RatkinRaceDefName = "Ratkin";

		public static bool TryResolve(out ResolvedDefs resolved)
		{
			resolved = null;

			Faction faction = ResolveFaction();
			PawnKindDef babyKind = ResolvePawnKind(PreferredBabyKindDefNames, faction, allowViolent: true);
			PawnKindDef motherKind = ResolvePawnKind(PreferredMotherKindDefNames, faction, allowViolent: true);
			PawnKindDef animalKind = ResolveAnimalKind();

			if (faction == null || babyKind == null || motherKind == null || animalKind == null)
			{
				return false;
			}

			resolved = new ResolvedDefs(faction, babyKind, motherKind, animalKind);
			return true;
		}

		private static Faction ResolveFaction()
		{
			foreach (string defName in PreferredFactionDefNames)
			{
				FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail(defName);
				if (factionDef == null)
				{
					continue;
				}

				Faction faction = Find.FactionManager?.FirstFactionOfDef(factionDef);
				if (IsUsableFaction(faction))
				{
					return faction;
				}
			}

			List<Faction> allFactions = Find.FactionManager?.AllFactionsListForReading;
			if (allFactions == null)
			{
				return null;
			}

			return allFactions
				.Where(IsUsableFaction)
				.Where(faction => faction.def != null && IsRatkinFaction(faction.def))
				.OrderByDescending(faction => faction.PlayerGoodwill)
				.FirstOrDefault();
		}

		private static bool IsUsableFaction(Faction faction)
		{
			return faction != null
				&& !faction.IsPlayer
				&& !faction.def.hidden
				&& faction.RelationKindWith(Faction.OfPlayer) != FactionRelationKind.Hostile;
		}

		private static bool IsRatkinFaction(FactionDef factionDef)
		{
			if (factionDef == null)
			{
				return false;
			}

			if (factionDef.defName.IndexOf("Rat", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}

			if (factionDef.pawnGroupMakers == null)
			{
				return false;
			}

			foreach (PawnGroupMaker maker in factionDef.pawnGroupMakers)
			{
				if (maker?.options == null)
				{
					continue;
				}

				foreach (PawnGenOption option in maker.options)
				{
					if (IsRatkinKind(option?.kind))
					{
						return true;
					}
				}
			}

			return false;
		}

		private static PawnKindDef ResolvePawnKind(IEnumerable<string> preferredDefNames, Faction faction, bool allowViolent)
		{
			foreach (string defName in preferredDefNames)
			{
				PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(defName);
				if (IsUsableHumanlikeKind(kind, faction, allowViolent))
				{
					return kind;
				}
			}

			return DefDatabase<PawnKindDef>.AllDefsListForReading
				.Where(kind => IsUsableHumanlikeKind(kind, faction, allowViolent))
				.OrderBy(kind => kind.combatPower)
				.FirstOrDefault();
		}

		private static bool IsUsableHumanlikeKind(PawnKindDef kind, Faction faction, bool allowViolent)
		{
			if (!IsRatkinKind(kind) || kind.RaceProps == null || kind.RaceProps.Animal || !kind.RaceProps.Humanlike)
			{
				return false;
			}

			if (!allowViolent && kind.isFighter)
			{
				return false;
			}

			if (faction == null)
			{
				return true;
			}

			FactionDef factionDef = faction.def;
			return kind.defaultFactionDef == null
				|| kind.defaultFactionDef == factionDef
				|| string.Equals(kind.defaultFactionDef.defName, factionDef.defName, StringComparison.OrdinalIgnoreCase);
		}

		private static bool IsRatkinKind(PawnKindDef kind)
		{
			return kind?.race != null
				&& kind.race.defName.IndexOf(RatkinRaceDefName, StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static PawnKindDef ResolveAnimalKind()
		{
			foreach (string defName in PreferredAnimalKindDefNames)
			{
				PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(defName);
				if (kind?.RaceProps?.Animal == true)
				{
					return kind;
				}
			}

			return DefDatabase<PawnKindDef>.AllDefsListForReading
				.FirstOrDefault(kind => kind?.RaceProps?.Animal == true
					&& kind.race?.defName.IndexOf("Rat", StringComparison.OrdinalIgnoreCase) >= 0);
		}

		internal sealed class ResolvedDefs
		{
			public ResolvedDefs(Faction faction, PawnKindDef babyKindDef, PawnKindDef motherKindDef, PawnKindDef animalKindDef)
			{
				Faction = faction;
				BabyKindDef = babyKindDef;
				MotherKindDef = motherKindDef;
				AnimalKindDef = animalKindDef;
			}

			public Faction Faction { get; }

			public PawnKindDef BabyKindDef { get; }

			public PawnKindDef MotherKindDef { get; }

			public PawnKindDef AnimalKindDef { get; }
		}
	}
}
