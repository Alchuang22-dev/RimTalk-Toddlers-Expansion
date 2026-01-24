using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_ToddlersWashBaby
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			Type type = AccessTools.TypeByName("Toddlers.JobGiver_WashBaby");
			if (type == null)
			{
				return;
			}

			MethodInfo target = AccessTools.Method(type, "TryGiveJob");
			if (target == null)
			{
				return;
			}

			MethodInfo postfix = AccessTools.Method(typeof(Patch_ToddlersWashBaby), nameof(TryGiveJob_Postfix));
			harmony.Patch(target, postfix: new HarmonyMethod(postfix));
		}

		private static void TryGiveJob_Postfix(Pawn pawn, ref Job __result)
		{
			if (pawn == null || __result == null || pawn.Map == null)
			{
				return;
			}

			Pawn baby = __result.targetA.Thing as Pawn;
			if (baby == null)
			{
				return;
			}

			if (!IsChildcareEnabled(pawn))
			{
				RejectJob(pawn, baby, "childcare disabled");
				__result = null;
				return;
			}

			if (!IsTargetAllowed(pawn, baby, PathEndMode.Touch))
			{
				RejectJob(pawn, baby, "baby not allowed/reachable");
				__result = null;
				return;
			}

			LocalTargetInfo waterTarget = __result.targetB;
			if (waterTarget.IsValid && !IsTargetAllowed(pawn, waterTarget, PathEndMode.ClosestTouch))
			{
				RejectJob(pawn, baby, "water not allowed/reachable");
				__result = null;
			}
		}

		private static bool IsChildcareEnabled(Pawn pawn)
		{
			if (pawn == null)
			{
				return false;
			}

			if (pawn.WorkTagIsDisabled(WorkTags.Caring))
			{
				return false;
			}

			if (pawn.workSettings == null)
			{
				return false;
			}

			return pawn.workSettings.WorkIsActive(WorkTypeDefOf.Childcare);
		}

		private static bool IsTargetAllowed(Pawn pawn, LocalTargetInfo target, PathEndMode pathEndMode)
		{
			if (!target.IsValid || pawn == null)
			{
				return false;
			}

			if (target.HasThing)
			{
				Thing thing = target.Thing;
				if (thing == null || thing.Map != pawn.Map)
				{
					return false;
				}

				if (!ForbidUtility.InAllowedArea(thing.Position, pawn))
				{
					return false;
				}

				return pawn.CanReserveAndReach(thing, pathEndMode, Danger.Some);
			}

			IntVec3 cell = target.Cell;
			if (!cell.IsValid || !cell.InBounds(pawn.Map))
			{
				return false;
			}

			if (!ForbidUtility.InAllowedArea(cell, pawn))
			{
				return false;
			}

			return pawn.CanReach(cell, pathEndMode, Danger.Some);
		}

		private static bool IsTargetAllowed(Pawn pawn, Pawn target, PathEndMode pathEndMode)
		{
			if (pawn == null || target == null)
			{
				return false;
			}

			if (target.Map != pawn.Map || target.IsForbidden(pawn))
			{
				return false;
			}

			if (!ForbidUtility.InAllowedArea(target.Position, pawn))
			{
				return false;
			}

			return pawn.CanReserveAndReach(target, pathEndMode, Danger.Some);
		}

		private static void RejectJob(Pawn pawn, Pawn baby, string reason)
		{
			if (Prefs.DevMode)
			{
				Log.Message($"[RimTalk_ToddlersExpansion] WashBaby job rejected: {reason} carer={pawn?.LabelShort ?? "null"} baby={baby?.LabelShort ?? "null"}.");
			}
		}
	}
}
