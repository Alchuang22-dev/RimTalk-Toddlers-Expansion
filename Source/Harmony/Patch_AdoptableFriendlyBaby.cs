using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_AdoptableFriendlyBaby
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			var target = AccessTools.Method(typeof(Pawn), nameof(Pawn.AdoptableBy), new[] { typeof(Faction), typeof(StringBuilder) });
			if (target != null)
			{
				harmony.Patch(target, postfix: new HarmonyMethod(typeof(Patch_AdoptableFriendlyBaby), nameof(AdoptableBy_Postfix)));
			}
		}

		private static void AdoptableBy_Postfix(Pawn __instance, Faction by, StringBuilder reason, ref bool __result)
		{
			if (__result)
			{
				return;
			}

			if (!ModsConfig.BiotechActive)
			{
				return;
			}

			if (__instance == null || by != Faction.OfPlayer)
			{
				return;
			}

			if (__instance.Faction == null || __instance.Faction == Faction.OfPlayer)
			{
				return;
			}

			if (__instance.Faction.HostileTo(Faction.OfPlayer))
			{
				return;
			}

			if (!(__instance.RaceProps?.Humanlike == true))
			{
				return;
			}

			if (__instance.IsPrisoner || __instance.IsPrisonerOfColony)
			{
				return;
			}

			if (!(__instance.DevelopmentalStage.Baby() || __instance.DevelopmentalStage.Newborn()))
			{
				return;
			}

			float ageYears = __instance.ageTracker?.AgeBiologicalYearsFloat ?? 999f;
			if (ageYears >= 1f)
			{
				return;
			}

			__result = true;
			reason?.Clear();
		}
	}
}
