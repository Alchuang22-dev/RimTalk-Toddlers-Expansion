using System.Reflection;
using HarmonyLib;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	/// <summary>
	/// Applies a temporary coma-like hediff to Kiiro "entrust orphan" babies so they do not crawl away immediately.
	/// </summary>
	public static class Patch_KiiroRefugeeBabyStay
	{
		private const string KiiroRefugeeRootTypeName = "Kiiro_Event.QuestNode_Root_KiiroRefugeeBaby";
		private const string ConvulsionHediffDefName = "RimTalk_KiiroRefugeeConvulsion";

		private static bool _missingDefWarningLogged;

		public static void Init(HarmonyLib.Harmony harmony)
		{
			PatchKiiroQuestPawnGeneration(harmony);
		}

		private static void PatchKiiroQuestPawnGeneration(HarmonyLib.Harmony harmony)
		{
			var kiiroRefugeeRootType = AccessTools.TypeByName(KiiroRefugeeRootTypeName);
			if (kiiroRefugeeRootType == null)
			{
				return;
			}

			MethodInfo generatePawn = AccessTools.Method(kiiroRefugeeRootType, "GeneratePawn");
			if (generatePawn == null)
			{
				return;
			}

			harmony.Patch(
				generatePawn,
					postfix: new HarmonyMethod(typeof(Patch_KiiroRefugeeBabyStay), nameof(GeneratePawn_Postfix)));
		}

		private static void GeneratePawn_Postfix(ref Pawn __result)
		{
			if (!IsQuestBaby(__result))
			{
				return;
			}

			HediffDef convulsionDef = DefDatabase<HediffDef>.GetNamedSilentFail(ConvulsionHediffDefName);
			if (convulsionDef == null)
			{
				if (!_missingDefWarningLogged)
				{
					_missingDefWarningLogged = true;
					Log.Warning($"[RimTalk Toddlers Expansion] Missing hediff def: {ConvulsionHediffDefName}");
				}
				return;
			}

			if (__result.health?.hediffSet?.HasHediff(convulsionDef) == true)
			{
				return;
			}

			__result.health?.AddHediff(convulsionDef);
		}

		private static bool IsQuestBaby(Pawn pawn)
		{
			if (pawn == null)
			{
				return false;
			}

			DevelopmentalStage stage = pawn.DevelopmentalStage;
			return stage == DevelopmentalStage.Baby || stage == DevelopmentalStage.Newborn;
		}
	}
}
