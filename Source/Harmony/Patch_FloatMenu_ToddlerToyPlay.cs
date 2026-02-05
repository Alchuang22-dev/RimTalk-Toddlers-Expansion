using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_FloatMenu_ToddlerToyPlay
	{
		private static bool _walkHediffChecked;
		private static HediffDef _learningToWalkDef;

		public static void Init(HarmonyLib.Harmony harmony)
		{
			MethodInfo target = AccessTools.Method(typeof(FloatMenuMakerMap), "AddHumanlikeOrders",
				new[] { typeof(Vector3), typeof(Pawn), typeof(List<FloatMenuOption>) });
			if (target == null)
			{
				return;
			}

			MethodInfo postfix = AccessTools.Method(typeof(Patch_FloatMenu_ToddlerToyPlay), nameof(AddHumanlikeOrders_Postfix));
			harmony.Patch(target, postfix: new HarmonyMethod(postfix));
		}

		private static void AddHumanlikeOrders_Postfix(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts)
		{
			if (pawn?.Map == null || opts == null || pawn.Downed || pawn.Drafted || ToddlerMentalStateUtility.HasBlockingMentalState(pawn))
			{
				return;
			}

			if (!ToddlersCompatUtility.IsToddler(pawn))
			{
				return;
			}

			IntVec3 cell = IntVec3.FromVector3(clickPos);
			if (!cell.InBounds(pawn.Map))
			{
				return;
			}

			List<Thing> things = cell.GetThingList(pawn.Map);
			for (int i = 0; i < things.Count; i++)
			{
				if (things[i] is not Building building)
				{
					continue;
				}

				CompToddlerToy comp = building.TryGetComp<CompToddlerToy>();
				if (comp == null || !comp.Allows(pawn))
				{
					continue;
				}

				if (RequiresGroundToy(pawn) && !comp.GroundToy)
				{
					continue;
				}

				if (!pawn.CanReserveAndReach(building, PathEndMode.InteractionCell, Danger.Some))
				{
					continue;
				}

				string label = "RimTalk_ToddlersExpansion_ToddlerToyPlayOrder".Translate(building.LabelShort);
				opts.Add(new FloatMenuOption(label, () =>
				{
					Job job = JobMaker.MakeJob(ToddlersExpansionJobDefOf.RimTalk_ToddlerPlayAtToy, building);
					job.ignoreJoyTimeAssignment = false;
					if (comp.UseDurationTicks > 0)
					{
						job.expiryInterval = comp.UseDurationTicks;
					}

					pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
				}));

				return;
			}
		}

		private static bool RequiresGroundToy(Pawn pawn)
		{
			if (pawn == null)
			{
				return false;
			}

			if (pawn.DevelopmentalStage.Newborn() || pawn.DevelopmentalStage.Baby())
			{
				return true;
			}

			EnsureWalkDef();
			if (_learningToWalkDef == null || pawn.health?.hediffSet == null)
			{
				return false;
			}

			Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(_learningToWalkDef);
			return hediff != null && hediff.Severity < 0.5f;
		}

		private static void EnsureWalkDef()
		{
			if (_walkHediffChecked)
			{
				return;
			}

			_walkHediffChecked = true;
			_learningToWalkDef = DefDatabase<HediffDef>.GetNamedSilentFail("LearningToWalk");
		}
	}
}
