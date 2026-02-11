using System;
using System.Collections.Generic;
using RimWorld;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers.HAR
{
	/// <summary>
	/// HAR race whitelist aligned with MiliraKiiroCuddle race routing.
	/// Source: JobDriverKiiroCuddle.getCuddleInteractionDefForRace.
	/// </summary>
	public static class HarRaceWhitelistUtility
	{
		public enum MiliraAlignedRaceGroup
		{
			Common,
			Milira,
			Ratkin,
			MoeLotl,
			Kiiro,
			Bunny,
			Cinder
		}

		private const string CanonicalRatkinDefName = "Ratkin";
		private const string MiliraRaceDefName = "Milira_Race";
		private const string AxolotlRaceDefName = "Axolotl";
		private const string KiiroRaceDefName = "Kiiro_Race";
		private const string YuranRaceDefName = "Yuran_Race";
		private const string RabbieRaceDefName = "Rabbie";

		public static readonly HashSet<string> MiliraAlignedRaceDefNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			MiliraRaceDefName,
			CanonicalRatkinDefName,
			AxolotlRaceDefName,
			KiiroRaceDefName,
			YuranRaceDefName,
			RabbieRaceDefName
		};

		public static ThingDef GetToddlerRaceDef(Pawn pawn)
		{
			return ToddlersCompatUtility.IsToddler(pawn) ? GetMiliraAlignedRaceDef(pawn) : null;
		}

		public static ThingDef GetNewbornRaceDef(Pawn pawn)
		{
			return pawn?.DevelopmentalStage.Newborn() == true ? GetMiliraAlignedRaceDef(pawn) : null;
		}

		public static ThingDef GetChildRaceDef(Pawn pawn)
		{
			return pawn?.DevelopmentalStage == DevelopmentalStage.Child ? GetMiliraAlignedRaceDef(pawn) : null;
		}

		public static string GetToddlerRaceDefName(Pawn pawn)
		{
			return GetToddlerRaceDef(pawn)?.defName;
		}

		public static string GetNewbornRaceDefName(Pawn pawn)
		{
			return GetNewbornRaceDef(pawn)?.defName;
		}

		public static string GetChildRaceDefName(Pawn pawn)
		{
			return GetChildRaceDef(pawn)?.defName;
		}

		/// <summary>
		/// MiliraKiiroCuddle style race routing:
		/// Cinder(kindDef) > specific race defs > common.
		/// </summary>
		public static MiliraAlignedRaceGroup GetMiliraAlignedRaceGroup(Pawn pawn)
		{
			if (IsMiliraAlignedCinderPawnKind(pawn))
			{
				return MiliraAlignedRaceGroup.Cinder;
			}

			string raceDefName = GetMiliraAlignedRaceDef(pawn)?.defName;
			if (raceDefName.NullOrEmpty())
			{
				return MiliraAlignedRaceGroup.Common;
			}

			if (raceDefName.Equals(MiliraRaceDefName, StringComparison.OrdinalIgnoreCase))
			{
				return MiliraAlignedRaceGroup.Milira;
			}

			if (raceDefName.Equals(CanonicalRatkinDefName, StringComparison.OrdinalIgnoreCase))
			{
				return MiliraAlignedRaceGroup.Ratkin;
			}

			if (raceDefName.Equals(AxolotlRaceDefName, StringComparison.OrdinalIgnoreCase))
			{
				return MiliraAlignedRaceGroup.MoeLotl;
			}

			if (raceDefName.Equals(KiiroRaceDefName, StringComparison.OrdinalIgnoreCase))
			{
				return MiliraAlignedRaceGroup.Kiiro;
			}

			if (raceDefName.Equals(YuranRaceDefName, StringComparison.OrdinalIgnoreCase)
				|| raceDefName.Equals(RabbieRaceDefName, StringComparison.OrdinalIgnoreCase))
			{
				return MiliraAlignedRaceGroup.Bunny;
			}

			return MiliraAlignedRaceGroup.Common;
		}

		public static bool HasSpecialHarSelfPlay(Pawn pawn)
		{
			return GetMiliraAlignedRaceGroup(pawn) != MiliraAlignedRaceGroup.Common;
		}

		/// <summary>
		/// Use MiliraKiiroCuddle style source:
		/// pawn.kindDef.race first, then fallback to pawn.def.
		/// Ratkin variants are normalized to canonical "Ratkin".
		/// </summary>
		public static ThingDef GetMiliraAlignedRaceDef(Pawn pawn)
		{
			ThingDef race = pawn?.kindDef?.race ?? pawn?.def;
			if (race == null)
			{
				return null;
			}

			string normalized = NormalizeRaceDefName(race.defName);
			if (normalized.Equals(CanonicalRatkinDefName, StringComparison.OrdinalIgnoreCase))
			{
				return DefDatabase<ThingDef>.GetNamedSilentFail(CanonicalRatkinDefName) ?? race;
			}

			return race;
		}

		public static string NormalizeRaceDefName(string raceDefName)
		{
			if (raceDefName.NullOrEmpty())
			{
				return string.Empty;
			}

			return IsRatkinVariant(raceDefName) ? CanonicalRatkinDefName : raceDefName;
		}

		public static bool IsRatkinVariant(string raceDefName)
		{
			if (raceDefName.NullOrEmpty())
			{
				return false;
			}

			if (raceDefName.Equals(CanonicalRatkinDefName, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			return raceDefName.IndexOf("Ratkin", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		public static bool IsMiliraAlignedHarRace(ThingDef raceDef)
		{
			if (raceDef == null)
			{
				return false;
			}

			string normalized = NormalizeRaceDefName(raceDef.defName);
			return !normalized.NullOrEmpty() && MiliraAlignedRaceDefNames.Contains(normalized);
		}

		public static bool IsMiliraAlignedHarRace(Pawn pawn)
		{
			ThingDef race = GetMiliraAlignedRaceDef(pawn);
			return IsMiliraAlignedHarRace(race);
		}

		/// <summary>
		/// MiliraKiiroCuddle has an additional non-race branch:
		/// pawn.kindDef.defName contains "Cinder".
		/// </summary>
		public static bool IsMiliraAlignedCinderPawnKind(Pawn pawn)
		{
			return pawn?.kindDef?.defName?.IndexOf("Cinder", StringComparison.OrdinalIgnoreCase) >= 0;
		}
	}
}
