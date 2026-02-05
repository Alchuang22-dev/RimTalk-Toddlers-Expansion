using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_ThinkNode_ConditionalHasLordDuty
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			var target = AccessTools.Method(typeof(ThinkNode_ConditionalHasLordDuty), "Satisfied", new[] { typeof(Pawn) });
			if (target == null)
			{
				return;
			}

			harmony.Patch(target, prefix: new HarmonyMethod(typeof(Patch_ThinkNode_ConditionalHasLordDuty), nameof(Satisfied_Prefix)));
		}

		private static bool Satisfied_Prefix(Pawn pawn, ref bool __result)
		{
			Lord lord = pawn?.GetLord();
			if (lord == null)
			{
				__result = false;
				return false;
			}

			LordToil toil = lord.CurLordToil;
			if (toil == null)
			{
				if (Prefs.DevMode)
				{
					Log.Warning($"[RimTalk_ToddlersExpansion][LordDutyGuard] {pawn.LabelShort} has lord {lord.loadID} but CurLordToil is null.");
				}

				__result = false;
				return false;
			}

			__result = toil.AssignsDuties;
			return false;
		}
	}
}
