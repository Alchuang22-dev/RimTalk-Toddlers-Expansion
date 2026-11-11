using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using Verse;
using Verse.AI.Group;

namespace RimTalk_ToddlersExpansion.Harmony
{
	/// <summary>
	/// A carried toddler reaches the map edge with its carrier, even when it cannot open doors.
	/// </summary>
	public static class Patch_CarriedToddlerMapEdgeReachability
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			var method = AccessTools.Method(typeof(Trigger_PawnCannotReachMapEdge), nameof(Trigger_PawnCannotReachMapEdge.ActivateOn));
			if (method != null)
			{
				harmony.Patch(method, prefix: new HarmonyMethod(typeof(Patch_CarriedToddlerMapEdgeReachability), nameof(ActivateOn_Prefix)));
			}
		}

		private static bool ActivateOn_Prefix(Lord lord, TriggerSignal signal, ref bool __result)
		{
			if (signal.type != TriggerSignalType.Tick || Find.TickManager.TicksGame % 197 != 0)
			{
				return true;
			}

			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				Pawn pawn = lord.ownedPawns[i];
				if (!pawn.Spawned || pawn.Dead || pawn.Downed)
				{
					continue;
				}

				if (IsCarriedByMemberOfLord(pawn, lord))
				{
					continue;
				}

				if (!pawn.CanReachMapEdge())
				{
					__result = true;
					return false;
				}
			}

			__result = false;
			return false;
		}

		private static bool IsCarriedByMemberOfLord(Pawn pawn, Lord lord)
		{
			if (!ToddlerCarryingUtility.IsBeingCarried(pawn))
			{
				return false;
			}

			Pawn carrier = ToddlerCarryingUtility.GetCarrier(pawn);
			if (carrier == null
				|| carrier.Dead
				|| carrier.Downed
				|| carrier.Destroyed
				|| !carrier.Spawned
				|| carrier.Map != pawn.Map
				|| carrier.GetLord() != lord)
			{
				return false;
			}

			return lord.ownedPawns.Contains(carrier);
		}
	}
}
