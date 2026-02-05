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
		private static ThingDef _toyBoxDef;
		private static ThingDef _babyDecorationDef;
		private const string LogPrefix = "[RimTalk_ToddlersExpansion][ToyMenu]";

		public static void Init(HarmonyLib.Harmony harmony)
		{
			MethodInfo target = AccessTools.Method(typeof(FloatMenuMakerMap), "AddHumanlikeOrders",
				new[] { typeof(Vector3), typeof(Pawn), typeof(List<FloatMenuOption>) });
			if (target == null)
			{
				Log.Warning($"{LogPrefix} FloatMenuMakerMap.AddHumanlikeOrders not found (expected on older RW versions). Using FloatMenuOptionProvider path.");
				return;
			}

			MethodInfo postfix = AccessTools.Method(typeof(Patch_FloatMenu_ToddlerToyPlay), nameof(AddHumanlikeOrders_Postfix));
			harmony.Patch(target, postfix: new HarmonyMethod(postfix));
		}

		public static bool TryCreateToyPlayOptionForBuilding(Pawn pawn, Thing clickedThing, out FloatMenuOption option, out string reason)
		{
			option = null;
			reason = "not building";
			if (pawn == null || clickedThing is not Building building)
			{
				return false;
			}

			return TryCreateToyPlayOption(pawn, building, out option, out reason);
		}

		private static void AddHumanlikeOrders_Postfix(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts)
		{
			if (pawn?.Map == null)
			{
				DebugLog("skip: pawn or map is null");
				return;
			}

			if (opts == null)
			{
				DebugLog($"skip: options list is null for pawn={pawn.LabelShort}");
				return;
			}

			if (pawn.Downed || pawn.Drafted || ToddlerMentalStateUtility.HasBlockingMentalState(pawn))
			{
				DebugLog($"skip: pawn unavailable pawn={pawn.LabelShort} downed={pawn.Downed} drafted={pawn.Drafted} blockingMental={ToddlerMentalStateUtility.HasBlockingMentalState(pawn)}");
				return;
			}

			if (!ToddlersCompatUtility.IsToddler(pawn))
			{
				DebugLog($"skip: selected pawn is not toddler pawn={pawn.LabelShort} stage={pawn.DevelopmentalStage}");
				return;
			}

			IntVec3 cell = IntVec3.FromVector3(clickPos);
			if (!cell.InBounds(pawn.Map))
			{
				// DebugLog($"skip: click out of bounds pawn={pawn.LabelShort} cell={cell}");
				return;
			}

			List<Thing> things = cell.GetThingList(pawn.Map);
			// DebugLog($"scan: pawn={pawn.LabelShort} cell={cell} things={things.Count} toddlersActive={ToddlersCompatUtility.IsToddlersActive}");
			for (int i = 0; i < things.Count; i++)
			{
				if (things[i] is not Building building)
				{
					continue;
				}

				if (!TryCreateToyPlayOption(pawn, building, out FloatMenuOption option, out string reason))
				{
					DebugLog($"skip building: pawn={pawn.LabelShort} building={building.LabelShort} def={building.def?.defName ?? "null"} reason={reason}");
					continue;
				}

				opts.Add(option);
				DebugLog($"added option: pawn={pawn.LabelShort} building={building.LabelShort} def={building.def?.defName ?? "null"} source={reason}");
				return;
			}

			DebugLog($"no option added: pawn={pawn.LabelShort} cell={cell}");
		}

		private static bool TryCreateToyPlayOption(Pawn pawn, Building building, out FloatMenuOption option, out string reason)
		{
			option = null;
			reason = "none";
			if (TryCreateRimTalkToyOption(pawn, building, out option, out reason))
			{
				return true;
			}

			if (!ToddlersCompatUtility.IsToddlersActive)
			{
				reason = "toddlers mod not active";
				return false;
			}

			if (TryCreateToddlersToyBoxOption(pawn, building, out option, out reason))
			{
				return true;
			}

			return TryCreateToddlersDecorOption(pawn, building, out option, out reason);
		}

		private static bool TryCreateRimTalkToyOption(Pawn pawn, Building building, out FloatMenuOption option, out string reason)
		{
			option = null;
			reason = "not rimtalk toy";
			CompToddlerToy comp = building.TryGetComp<CompToddlerToy>();
			if (comp == null)
			{
				return false;
			}

			if (!comp.Allows(pawn))
			{
				reason = "CompToddlerToy disallows pawn";
				return false;
			}

			if (RequiresGroundToy(pawn) && !comp.GroundToy)
			{
				reason = "requires ground toy";
				return false;
			}

			if (!pawn.CanReserveAndReach(building, PathEndMode.InteractionCell, Danger.Some))
			{
				reason = "cannot reserve/reach interaction cell";
				return false;
			}

			string label = "RimTalk_ToddlersExpansion_ToddlerToyPlayOrder".Translate(building.LabelShort);
			option = new FloatMenuOption(label, () =>
			{
				Job job = JobMaker.MakeJob(ToddlersExpansionJobDefOf.RimTalk_ToddlerPlayAtToy, building);
				job.ignoreJoyTimeAssignment = true;
				job.playerForced = true;
				if (comp.UseDurationTicks > 0)
				{
					job.expiryInterval = comp.UseDurationTicks;
				}

				IssueToyJobNow(pawn, job, $"rimtalk:{building.def?.defName ?? "null"}");
			});
			reason = "rimtalk comp toy";
			return true;
		}

		private static bool TryCreateToddlersToyBoxOption(Pawn pawn, Building building, out FloatMenuOption option, out string reason)
		{
			option = null;
			reason = "not toddlers toybox";
			EnsureToddlersToyDefs();
			if (_toyBoxDef == null || building.def != _toyBoxDef)
			{
				if (_toyBoxDef == null)
				{
					reason = "ToyBox def missing";
				}
				return false;
			}

			if (ToddlersExpansionJobDefOf.RimTalk_ToddlerPlayAtToy == null)
			{
				reason = "RimTalk_ToddlerPlayAtToy job def missing";
				return false;
			}

			if (!pawn.CanReserveAndReach(building, PathEndMode.Touch, Danger.Some))
			{
				reason = "cannot reserve/reach toybox";
				return false;
			}

			string label = "RimTalk_ToddlersExpansion_ToddlerToyPlayOrder".Translate(building.LabelShort);
			option = new FloatMenuOption(label, () =>
			{
				Job job = JobMaker.MakeJob(ToddlersExpansionJobDefOf.RimTalk_ToddlerPlayAtToy, building);
				job.ignoreJoyTimeAssignment = true;
				job.playerForced = true;
				job.expiryInterval = job.def.joyDuration;
				IssueToyJobNow(pawn, job, $"toybox:{building.def?.defName ?? "null"}");
			});
			reason = "toddlers toybox";
			return true;
		}

		private static bool TryCreateToddlersDecorOption(Pawn pawn, Building building, out FloatMenuOption option, out string reason)
		{
			option = null;
			reason = "not toddlers decoration";
			EnsureToddlersToyDefs();
			if (_babyDecorationDef == null || building.def != _babyDecorationDef)
			{
				if (_babyDecorationDef == null)
				{
					reason = "BabyDecoration def missing";
				}
				return false;
			}

			if (ToddlersExpansionJobDefOf.RimTalk_ToddlerPlayAtToy == null)
			{
				reason = "RimTalk_ToddlerPlayAtToy job def missing";
				return false;
			}

			PathEndMode pathEndMode = ShouldPlayDecorOnCell(building) ? PathEndMode.OnCell : PathEndMode.Touch;
			if (!pawn.CanReserveAndReach(building, pathEndMode, Danger.Some))
			{
				reason = $"cannot reserve/reach decor ({pathEndMode})";
				return false;
			}

			string label = "RimTalk_ToddlersExpansion_ToddlerToyPlayOrder".Translate(building.LabelShort);
			option = new FloatMenuOption(label, () =>
			{
				Job job = JobMaker.MakeJob(ToddlersExpansionJobDefOf.RimTalk_ToddlerPlayAtToy, building);
				job.ignoreJoyTimeAssignment = true;
				job.playerForced = true;
				job.expiryInterval = job.def.joyDuration;
				IssueToyJobNow(pawn, job, $"decoration:{building.def?.defName ?? "null"}");
			});
			reason = "toddlers decoration";
			return true;
		}

		private static bool ShouldPlayDecorOnCell(Thing decor)
		{
			return decor != null && (decor.thingIDNumber % 5 == 0);
		}

		private static void EnsureToddlersToyDefs()
		{
			_toyBoxDef ??= DefDatabase<ThingDef>.GetNamedSilentFail("ToyBox");
			_babyDecorationDef ??= DefDatabase<ThingDef>.GetNamedSilentFail("BabyDecoration");
		}

		private static void IssueToyJobNow(Pawn pawn, Job job, string source)
		{
			if (pawn?.jobs == null || job == null)
			{
				return;
			}

			pawn.jobs.StartJob(job, JobCondition.InterruptForced, tag: JobTag.Misc);
			DebugLog($"issued: pawn={pawn.LabelShort} source={source} job={job.def?.defName ?? "null"} forced={job.playerForced} expiry={job.expiryInterval}");
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

		internal static void DebugLog(string message)
		{
			if (!Prefs.DevMode)
			{
				return;
			}

			Log.Message($"{LogPrefix} {message}");
		}
	}
}
