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
				harmony.Patch(recalculateLifeStageIndex, transpiler: new HarmonyMethod(typeof(Patch_ToddlerAgeSettings), nameof(RecalculateLifeStageIndex_Transpiler)));
			}
		}

		private static bool ToddlerMinAge_Prefix(ref float __result)
		{
			__result = ToddlerAgeSettingsUtility.GetConfiguredToddlerMinAgeYears();
			return false;
		}

		private static IEnumerable<CodeInstruction> RecalculateLifeStageIndex_Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			MethodInfo ageYearsGetter = AccessTools.PropertyGetter(typeof(Pawn_AgeTracker), nameof(Pawn_AgeTracker.AgeBiologicalYears));
			MethodInfo ageYearsFloatGetter = AccessTools.PropertyGetter(typeof(Pawn_AgeTracker), nameof(Pawn_AgeTracker.AgeBiologicalYearsFloat));

			foreach (CodeInstruction instruction in instructions)
			{
				if (instruction.Calls(ageYearsGetter))
				{
					instruction.operand = ageYearsFloatGetter;
				}

				yield return instruction;
			}
		}
	}
}
