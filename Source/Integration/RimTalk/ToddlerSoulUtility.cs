using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Defs;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.RimTalk
{
	public static class ToddlerSoulUtility
	{
		public static bool TryGetSoulForPawn(Pawn pawn, out string personality, out float chattiness)
		{
			personality = null;
			chattiness = 0.5f;

			if (!TryGetAgeGroup(pawn, out YoungPawnSoulAgeGroup ageGroup))
			{
				return false;
			}

			List<ToddlerSoulEntry> candidates = GetCandidates(ageGroup);
			if (candidates.Count == 0)
			{
				return false;
			}

			ToddlerSoulEntry selected = candidates.RandomElementByWeight(entry => Mathf.Max(0.01f, entry.weight));
			if (selected == null)
			{
				return false;
			}

			personality = ResolveSoulText(selected);
			if (string.IsNullOrWhiteSpace(personality))
			{
				return false;
			}

			chattiness = Mathf.Clamp01(selected.chattiness);
			return true;
		}

		public static bool TryGetAgeGroup(Pawn pawn, out YoungPawnSoulAgeGroup ageGroup)
		{
			ageGroup = YoungPawnSoulAgeGroup.Toddler;
			if (pawn?.RaceProps?.Humanlike != true)
			{
				return false;
			}

			if (pawn.DevelopmentalStage.Newborn() || pawn.DevelopmentalStage.Baby())
			{
				ageGroup = YoungPawnSoulAgeGroup.Baby;
				return true;
			}

			if (ToddlersCompatUtility.IsToddler(pawn) || IsToddlerLifeStageFallback(pawn))
			{
				ageGroup = YoungPawnSoulAgeGroup.Toddler;
				return true;
			}

			if (pawn.DevelopmentalStage == DevelopmentalStage.Child)
			{
				ageGroup = YoungPawnSoulAgeGroup.Child;
				return true;
			}

			return false;
		}

		private static List<ToddlerSoulEntry> GetCandidates(YoungPawnSoulAgeGroup ageGroup)
		{
			List<ToddlerSoulEntry> candidates = new List<ToddlerSoulEntry>();
			List<ToddlerSoulPoolDef> defs = DefDatabase<ToddlerSoulPoolDef>.AllDefsListForReading;
			for (int i = 0; i < defs.Count; i++)
			{
				List<ToddlerSoulEntry> entries = defs[i]?.entries;
				if (entries == null)
				{
					continue;
				}

				for (int j = 0; j < entries.Count; j++)
				{
					ToddlerSoulEntry entry = entries[j];
					if (entry == null || entry.ageGroup != ageGroup || !HasResolvedText(entry) || entry.weight <= 0f)
					{
						continue;
					}

					candidates.Add(entry);
				}
			}

			return candidates;
		}

		private static bool IsToddlerLifeStageFallback(Pawn pawn)
		{
			string defName = pawn?.ageTracker?.CurLifeStage?.defName;
			return !defName.NullOrEmpty() && defName.IndexOf("Toddler", System.StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static bool HasUsableText(ToddlerSoulEntry entry)
		{
			return !string.IsNullOrWhiteSpace(entry?.textKey) || !string.IsNullOrWhiteSpace(entry?.text);
		}

		private static bool HasResolvedText(ToddlerSoulEntry entry)
		{
			if (!HasUsableText(entry))
			{
				return false;
			}

			return !string.IsNullOrWhiteSpace(ResolveSoulText(entry));
		}

		private static string ResolveSoulText(ToddlerSoulEntry entry)
		{
			if (entry == null)
			{
				return null;
			}

			if (!string.IsNullOrWhiteSpace(entry.textKey))
			{
				string translated = entry.textKey.Translate().ToString().Trim();
				if (!string.IsNullOrWhiteSpace(translated) && translated != entry.textKey)
				{
					return translated;
				}
			}

			return entry.text?.Trim();
		}
	}
}
