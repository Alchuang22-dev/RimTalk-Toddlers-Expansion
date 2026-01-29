using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public sealed class ToddlerSelfBathGameComponent : GameComponent
	{
		private const int CheckIntervalTicks = 1200;
		private int _nextCheckTick;

		private static ToddlerSelfBathGameComponent _instance;

		public static ToddlerSelfBathGameComponent Instance
		{
			get
			{
				if (_instance == null && Current.Game != null)
				{
					_instance = Current.Game.GetComponent<ToddlerSelfBathGameComponent>();
				}

				return _instance;
			}
		}

		public ToddlerSelfBathGameComponent(Game game)
		{
		}

		public override void GameComponentTick()
		{
			base.GameComponentTick();
			if (Find.TickManager == null || Find.Maps == null || Find.Maps.Count == 0)
			{
				return;
			}

			int tick = Find.TickManager.TicksGame;
			if (tick < _nextCheckTick)
			{
				return;
			}

			_nextCheckTick = tick + CheckIntervalTicks;
			for (int i = 0; i < Find.Maps.Count; i++)
			{
				Map map = Find.Maps[i];
				if (map == null || !map.IsPlayerHome)
				{
					continue;
				}

				TryAssignSelfBathJobs(map);
			}
		}

		private void TryAssignSelfBathJobs(Map map)
		{
			List<Pawn> pawns = map.mapPawns?.SpawnedPawnsInFaction(Faction.OfPlayer);
			if (pawns == null || pawns.Count == 0)
			{
				return;
			}

			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				if (!ToddlerSelfBathUtility.IsEligibleSelfBathPawn(pawn))
				{
					continue;
				}

				if (pawn.jobs?.jobQueue == null)
				{
					continue;
				}

				if (!ToddlerSelfBathUtility.TryCreateSelfBathJob(pawn, out Job job))
				{
					continue;
				}

				pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
			}
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref _nextCheckTick, "nextCheckTick");
		}
	}

	public static class ToddlerSelfBathUtility
	{
		private const float MaxPlayForSelfBath = 0.8f;
		private const float MaxHygieneForSelfBath = 0.5f;
		private const float SearchRadius = 9999f;

		private static Type _bathType;
		private static Type _washBucketType;
		private static MethodInfo _findBathOrTub;
		private static MethodInfo _findBestCleanWaterSource;
		private static NeedDef _hygieneNeedDef;
		private static bool _initialized;

		public static void RegisterGameComponent()
		{
			if (Current.Game == null)
			{
				return;
			}

			if (Current.Game.GetComponent<ToddlerSelfBathGameComponent>() == null)
			{
				Current.Game.components.Add(new ToddlerSelfBathGameComponent(Current.Game));
			}
		}

		public static bool IsEligibleSelfBathPawn(Pawn pawn)
		{
			if (pawn == null || pawn.Map == null || pawn.Downed || pawn.Drafted || pawn.InMentalState)
			{
				return false;
			}

			if (!pawn.Awake())
			{
				return false;
			}

			if (!ToddlersCompatUtility.IsToddler(pawn))
			{
				return false;
			}

			if (!ToddlersCompatUtility.CanSelfCare(pawn))
			{
				return false;
			}

			if (pawn.jobs?.curJob?.playerForced ?? false)
			{
				return false;
			}

			Need_Play play = pawn.needs?.play;
			Need_Joy joy = pawn.needs?.joy;
			float playLevel = play != null ? play.CurLevelPercentage : joy?.CurLevelPercentage ?? 1f;
			if (playLevel >= MaxPlayForSelfBath)
			{
				return false;
			}

			Need hygiene = GetHygieneNeed(pawn);
			if (hygiene == null || hygiene.CurLevelPercentage >= MaxHygieneForSelfBath)
			{
				return false;
			}

			JobDef current = pawn.CurJobDef;
			if (current != null
				&& current != JobDefOf.Wait
				&& current != JobDefOf.Wait_Wander
				&& current != JobDefOf.Wait_MaintainPosture)
			{
				return false;
			}

			return true;
		}

		public static bool TryCreateSelfBathJob(Pawn pawn, out Job job)
		{
			job = null;
			if (pawn == null || pawn.Map == null)
			{
				return false;
			}

			EnsureInitialized();
			if (_hygieneNeedDef == null)
			{
				return false;
			}

			if (!TryFindBathTarget(pawn, out LocalTargetInfo target))
			{
				return false;
			}

			if (!IsTargetReachable(pawn, target))
			{
				return false;
			}

			job = JobMaker.MakeJob(ToddlersExpansionJobDefOf.RimTalk_ToddlerSelfBath, target);
			job.ignoreJoyTimeAssignment = true;
			job.expiryInterval = 2800;
			return true;
		}

		private static bool TryFindBathTarget(Pawn pawn, out LocalTargetInfo target)
		{
			target = LocalTargetInfo.Invalid;
			if (TryFindBathOrTub(pawn, out Thing bath))
			{
				target = bath;
				return true;
			}

			Thing waterBottle = pawn.inventory?.innerContainer?.FirstOrDefault(t => t?.def?.defName == "DBH_WaterBottle");
			if (waterBottle != null)
			{
				target = waterBottle;
				return true;
			}

			if (_findBestCleanWaterSource == null)
			{
				return false;
			}

			try
			{
				object result = _findBestCleanWaterSource.Invoke(null, new object[] { pawn, pawn, false, SearchRadius, null, null });
				if (result is LocalTargetInfo lti && lti.IsValid)
				{
					target = lti;
					return true;
				}
			}
			catch
			{
				return false;
			}

			return false;
		}

		private static bool TryFindBathOrTub(Pawn pawn, out Thing bath)
		{
			bath = null;
			if (_findBathOrTub == null)
			{
				return false;
			}

			object[] args = { pawn, pawn, null };
			try
			{
				bool found = (bool)_findBathOrTub.Invoke(null, args);
				bath = args[2] as Thing;
				return found && bath != null;
			}
			catch
			{
				return false;
			}
		}

		private static bool IsTargetReachable(Pawn pawn, LocalTargetInfo target)
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

				return thing.Spawned
					? pawn.CanReserveAndReach(thing, PathEndMode.Touch, Danger.Some)
					: pawn.CanReserve(thing);
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

			return pawn.CanReach(cell, PathEndMode.ClosestTouch, Danger.Some);
		}

		public static Need GetHygieneNeed(Pawn pawn)
		{
			EnsureInitialized();
			return _hygieneNeedDef == null ? null : pawn?.needs?.TryGetNeed(_hygieneNeedDef);
		}

		public static bool IsBathFixture(Thing thing)
		{
			if (thing == null)
			{
				return false;
			}

			EnsureInitialized();
			if (_bathType != null && _bathType.IsInstanceOfType(thing))
			{
				return true;
			}

			return _washBucketType != null && _washBucketType.IsInstanceOfType(thing);
		}

		private static void EnsureInitialized()
		{
			if (_initialized)
			{
				return;
			}

			_initialized = true;
			_hygieneNeedDef = DefDatabase<NeedDef>.GetNamedSilentFail("Hygiene");

			Type washUtility = AccessTools.TypeByName("Toddlers.WashBabyUtility");
			if (washUtility != null)
			{
				_findBathOrTub = AccessTools.Method(washUtility, "FindBathOrTub", new[] { typeof(Pawn), typeof(Pawn), typeof(Thing).MakeByRefType() });
			}

			Type patchDbh = AccessTools.TypeByName("Toddlers.Patch_DBH");
			if (patchDbh != null)
			{
				_findBestCleanWaterSource = AccessTools.Field(patchDbh, "m_FindBestCleanWaterSource")?.GetValue(null) as MethodInfo;
			}

			_bathType = AccessTools.TypeByName("DubsBadHygiene.Building_bath");
			_washBucketType = AccessTools.TypeByName("DubsBadHygiene.Building_washbucket");
		}
	}
}
