using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_ToddlerAgeSettings
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			MethodInfo toddlerMinAge = AccessTools.Method("Toddlers.ToddlerUtility:ToddlerMinAge", new[] { typeof(Pawn) });
			if (toddlerMinAge != null)
			{
				harmony.Patch(toddlerMinAge, prefix: new HarmonyMethod(typeof(Patch_ToddlerAgeSettings), nameof(ToddlerMinAge_Prefix)));
			}

			MethodInfo recalculateLifeStageIndex = AccessTools.Method(typeof(Pawn_AgeTracker), "RecalculateLifeStageIndex");
			if (recalculateLifeStageIndex != null)
			{
				if (ToddlerAgeSettingsUtility.IsRatkinToddlerAgeAdjustmentLoaded())
				{
					Log.Message("[RimTalk_ToddlersExpansion][ToddlerAge] RatkinToddlerAgeAdjustment detected; skipping duplicate RecalculateLifeStageIndex float-age transpiler.");
				}
				else
				{
					harmony.Patch(recalculateLifeStageIndex, transpiler: new HarmonyMethod(typeof(Patch_ToddlerAgeSettings), nameof(RecalculateLifeStageIndex_Transpiler)));
				}
			}

			MethodInfo calculateToddlerMinAge = AccessTools.Method("Toddlers.AlienRaceToddlerInfo:CalculateToddlerMinAge");
			if (calculateToddlerMinAge != null)
			{
				harmony.Patch(calculateToddlerMinAge, prefix: new HarmonyMethod(typeof(Patch_ToddlerAgeSettings), nameof(CalculateToddlerMinAge_Prefix)));
			}
		}

		private static bool ToddlerMinAge_Prefix(Pawn p, ref float __result)
		{
			if (ToddlerAgeSettingsUtility.IsExternalDetailedToddlerAgeHandled(p))
			{
				if (ToddlerAgeSettingsUtility.TryGetConfiguredToddlerMinAge(p, out float externalMinAge))
				{
					__result = externalMinAge;
					return false;
				}

				return true;
			}

			if (!ToddlerAgeSettingsUtility.TryGetConfiguredToddlerMinAge(p, out float minAge))
			{
				minAge = ToddlerAgeSettingsUtility.GetConfiguredToddlerMinAgeYears();
			}

			__result = minAge;
			return false;
		}

		private static IEnumerable<CodeInstruction> RecalculateLifeStageIndex_Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
			MethodInfo ageYearsGetter = AccessTools.PropertyGetter(typeof(Pawn_AgeTracker), nameof(Pawn_AgeTracker.AgeBiologicalYears));
			MethodInfo ageYearsFloatGetter = AccessTools.PropertyGetter(typeof(Pawn_AgeTracker), nameof(Pawn_AgeTracker.AgeBiologicalYearsFloat));
			int replacements = 0;

			for (int i = 0; i < codes.Count; i++)
			{
				if (codes[i].Calls(ageYearsGetter))
				{
					codes[i].operand = ageYearsFloatGetter;
					replacements++;
					if (i + 1 < codes.Count && codes[i + 1].opcode == OpCodes.Conv_R4)
					{
						codes.RemoveAt(i + 1);
					}
				}
			}

			if (replacements == 0)
			{
				Log.Warning("[RimTalk_ToddlersExpansion][ToddlerAge] Pawn_AgeTracker.RecalculateLifeStageIndex transpiler found no AgeBiologicalYears call; runtime age fallback remains active.");
			}
			else
			{
				Log.Message($"[RimTalk_ToddlersExpansion][ToddlerAge] Patched RecalculateLifeStageIndex to use float biological age ({replacements} replacement(s)).");
			}

			return codes;
		}

		private static bool CalculateToddlerMinAge_Prefix(object __instance, ref float __result)
		{
			if (!ToddlerAgeSettingsUtility.TryGetConfiguredToddlerMinAgeForAlienInfo(__instance, out float minAge))
			{
				return true;
			}

			__result = minAge;
			return false;
		}

	}
}
