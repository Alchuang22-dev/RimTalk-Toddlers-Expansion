using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_ToddlersPlayInCribReservation
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			Type type = AccessTools.TypeByName("Toddlers.JobGiver_ToddlerPlayInCrib");
			if (type == null)
			{
				return;
			}

			MethodInfo target = AccessTools.Method(type, "TryGiveJob");
			if (target == null)
			{
				return;
			}

			MethodInfo postfix = AccessTools.Method(typeof(Patch_ToddlersPlayInCribReservation), nameof(TryGiveJob_Postfix));
			harmony.Patch(target, postfix: new HarmonyMethod(postfix));
		}

		private static void TryGiveJob_Postfix(Pawn pawn, ref Job __result)
		{
			if (pawn == null || __result == null)
			{
				return;
			}

			Building_Bed bed = __result.targetC.Thing as Building_Bed ?? __result.targetA.Thing as Building_Bed;
			if (bed == null)
			{
				return;
			}

			if (pawn.CanReserve(bed, 1, -1, null, false))
			{
				return;
			}

			if (Prefs.DevMode)
			{
				Log.Message($"[RimTalk_ToddlersExpansion] ToddlerPlayInCrib job rejected: bed not reservable pawn={pawn.LabelShort} bed={bed.LabelShort}.");
			}

			__result = null;
		}
	}
}
