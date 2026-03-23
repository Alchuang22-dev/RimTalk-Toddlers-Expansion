using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_PawnBioAndNameGenerator_ChildhoodFallback
	{
		private const string ToddlerBackstoryDefName = "Toddler";

		private static readonly PropertyInfo ChildhoodProperty = AccessTools.Property(typeof(Pawn_StoryTracker), "Childhood");
		private static readonly FieldInfo BackstorySlotField = AccessTools.Field(typeof(BackstoryDef), "slot");
		private static readonly FieldInfo SpawnCategoriesField = AccessTools.Field(typeof(BackstoryDef), "spawnCategories");
		private static readonly FieldInfo FilterCategoriesField = AccessTools.Field(typeof(BackstoryCategoryFilter), "categories");

		public static void Init(HarmonyLib.Harmony harmony)
		{
			MethodInfo target = AccessTools.Method(
				typeof(PawnBioAndNameGenerator),
				"FillBackstorySlotShuffled",
				new[] { typeof(Pawn), typeof(BackstorySlot), typeof(List<BackstoryCategoryFilter>), typeof(FactionDef), typeof(BackstorySlot?) })
				?? AccessTools.Method(
					typeof(PawnBioAndNameGenerator),
					"FillBackstorySlotShuffled",
					new[] { typeof(Pawn), typeof(BackstorySlot), typeof(List<BackstoryCategoryFilter>), typeof(FactionDef) });
			if (target == null)
			{
				return;
			}

			MethodInfo prefix = AccessTools.Method(typeof(Patch_PawnBioAndNameGenerator_ChildhoodFallback), nameof(FillBackstorySlotShuffled_Prefix));
			harmony.Patch(target, prefix: new HarmonyMethod(prefix));
		}

		private static bool FillBackstorySlotShuffled_Prefix(Pawn pawn, BackstorySlot slot, List<BackstoryCategoryFilter> filters, FactionDef faction)
		{
			if (!TravelingPawnInjectionUtility.UseInjectedChildhoodFallback || slot != BackstorySlot.Childhood || pawn?.story == null || ChildhoodProperty == null)
			{
				return true;
			}

			BackstoryDef childhood = GetAssignedChildhood(pawn);
			bool assignedFallback = false;
			if (childhood == null)
			{
				childhood = FindFallbackChildhood(filters);
				if (childhood == null)
				{
					return true;
				}

				SetAssignedChildhood(pawn, childhood);
				assignedFallback = true;
			}

			if (assignedFallback && Prefs.DevMode)
			{
				string raceName = pawn.def?.defName ?? "UnknownRace";
				Log.Message($"[RimTalk_ToddlersExpansion] Using fallback childhood {childhood.defName} for injected pawn {pawn.Name?.ToStringShort ?? pawn.LabelShort} ({raceName}).");
			}

			return false;
		}

		private static BackstoryDef GetAssignedChildhood(Pawn pawn)
		{
			return ChildhoodProperty?.GetValue(pawn.story) as BackstoryDef;
		}

		private static void SetAssignedChildhood(Pawn pawn, BackstoryDef childhood)
		{
			ChildhoodProperty?.SetValue(pawn.story, childhood);
			pawn.Notify_DisabledWorkTypesChanged();
		}

		private static BackstoryDef FindFallbackChildhood(List<BackstoryCategoryFilter> filters)
		{
			if (RequestedCategory(filters, "Toddler"))
			{
				BackstoryDef toddler = DefDatabase<BackstoryDef>.GetNamedSilentFail(ToddlerBackstoryDefName);
				if (IsChildhood(toddler))
				{
					return toddler;
				}
			}

			BackstoryDef filtered = FindFirstChildhoodMatchingFilters(filters);
			if (filtered != null)
			{
				return filtered;
			}

			BackstoryDef neutral = FindFirstChildhood(requireCategories: false);
			if (neutral != null)
			{
				return neutral;
			}

			return FindFirstChildhood(requireCategories: null);
		}

		private static BackstoryDef FindFirstChildhoodMatchingFilters(List<BackstoryCategoryFilter> filters)
		{
			HashSet<string> requestedCategories = CollectRequestedCategories(filters);
			if (requestedCategories == null || requestedCategories.Count == 0)
			{
				return null;
			}

			List<BackstoryDef> allBackstories = DefDatabase<BackstoryDef>.AllDefsListForReading;
			for (int i = 0; i < allBackstories.Count; i++)
			{
				BackstoryDef backstory = allBackstories[i];
				if (!IsChildhood(backstory))
				{
					continue;
				}

				List<string> spawnCategories = SpawnCategoriesField?.GetValue(backstory) as List<string>;
				if (spawnCategories == null)
				{
					continue;
				}

				for (int j = 0; j < spawnCategories.Count; j++)
				{
					if (requestedCategories.Contains(spawnCategories[j]))
					{
						return backstory;
					}
				}
			}

			return null;
		}

		private static BackstoryDef FindFirstChildhood(bool? requireCategories)
		{
			List<BackstoryDef> allBackstories = DefDatabase<BackstoryDef>.AllDefsListForReading;
			for (int i = 0; i < allBackstories.Count; i++)
			{
				BackstoryDef backstory = allBackstories[i];
				if (!IsChildhood(backstory))
				{
					continue;
				}

				List<string> spawnCategories = SpawnCategoriesField?.GetValue(backstory) as List<string>;
				bool hasCategories = spawnCategories != null && spawnCategories.Count > 0;
				if (requireCategories.HasValue && hasCategories != requireCategories.Value)
				{
					continue;
				}

				return backstory;
			}

			return null;
		}

		private static bool RequestedCategory(List<BackstoryCategoryFilter> filters, string category)
		{
			HashSet<string> requestedCategories = CollectRequestedCategories(filters);
			return requestedCategories != null && requestedCategories.Contains(category);
		}

		private static HashSet<string> CollectRequestedCategories(List<BackstoryCategoryFilter> filters)
		{
			if (filters == null || FilterCategoriesField == null)
			{
				return null;
			}

			HashSet<string> categories = new HashSet<string>();
			for (int i = 0; i < filters.Count; i++)
			{
				List<string> filterCategories = FilterCategoriesField.GetValue(filters[i]) as List<string>;
				if (filterCategories == null)
				{
					continue;
				}

				for (int j = 0; j < filterCategories.Count; j++)
				{
					if (!string.IsNullOrEmpty(filterCategories[j]))
					{
						categories.Add(filterCategories[j]);
					}
				}
			}

			return categories;
		}

		private static bool IsChildhood(BackstoryDef backstory)
		{
			return backstory != null
				&& BackstorySlotField != null
				&& BackstorySlotField.GetValue(backstory) is BackstorySlot slot
				&& slot == BackstorySlot.Childhood;
		}
	}
}
