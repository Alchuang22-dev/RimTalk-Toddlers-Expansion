using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using LudeonTK;
using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using UnityEngine;
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
			// Include colony prisoners as well, otherwise prisoner toddlers never get auto self-bath jobs.
			List<Pawn> pawns = map.mapPawns?.FreeColonistsAndPrisonersSpawned;
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

		/// <summary>
		/// 为选中的幼儿强制分配自我洗澡任务（用于调试）
		/// </summary>
		[DebugAction("RimTalk Toddlers", "Force Self Bath Job", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void DebugForceSelfBathJob(Pawn pawn)
		{
			if (pawn == null)
			{
				Messages.Message("[Debug] No pawn selected", MessageTypeDefOf.RejectInput);
				return;
			}

			if (!ToddlersCompatUtility.IsToddler(pawn))
			{
				Messages.Message($"[Debug] {pawn.LabelShort} is not a toddler", MessageTypeDefOf.RejectInput);
				return;
			}

			if (ToddlerSelfBathUtility.TryCreateSelfBathJob(pawn, out Job job, debugLog: true))
			{
				pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
				Messages.Message($"[Debug] Self bath job assigned to {pawn.LabelShort}", MessageTypeDefOf.PositiveEvent);
			}
			else
			{
				Messages.Message($"[Debug] Failed to create self bath job for {pawn.LabelShort}. Check log for details.", MessageTypeDefOf.RejectInput);
			}
		}

		/// <summary>
		/// 显示选中幼儿的洗澡资格诊断信息
		/// </summary>
		[DebugAction("RimTalk Toddlers", "Diagnose Self Bath Eligibility", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void DebugDiagnoseSelfBathEligibility(Pawn pawn)
		{
			if (pawn == null)
			{
				Messages.Message("[Debug] No pawn selected", MessageTypeDefOf.RejectInput);
				return;
			}

			string diagnosis = ToddlerSelfBathUtility.GetEligibilityDiagnosis(pawn);
			Log.Message($"[RimTalk Toddlers] Self Bath Eligibility Diagnosis for {pawn.LabelShort}:\n{diagnosis}");
			Messages.Message($"[Debug] Diagnosis logged for {pawn.LabelShort}. Check dev console.", MessageTypeDefOf.NeutralEvent);
		}

		/// <summary>
		/// 显示当前初始化状态和检测到的卫生mod
		/// </summary>
		[DebugAction("RimTalk Toddlers", "Show Hygiene Integration Status", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void DebugShowHygieneIntegrationStatus()
		{
			string status = ToddlerSelfBathUtility.GetIntegrationStatus();
			Log.Message($"[RimTalk Toddlers] Hygiene Integration Status:\n{status}");
			Messages.Message("[Debug] Integration status logged. Check dev console.", MessageTypeDefOf.NeutralEvent);
		}
	}

	public static class ToddlerSelfBathUtility
	{
		private const float MaxPlayForSelfBath = 0.8f;
		private const float MaxHygieneForSelfBath = 0.5f;
		private const float SearchRadius = 9999f;

		private static Type _bathType;
		private static Type _washBucketType;
		private static Type _showerType;
		private static FieldInfo _bathOccupantField;
		private static PropertyInfo _bathOccupantProperty;
		private static MethodInfo _findBestHygieneSource;
		private static MethodInfo _findBathOrTub;
		private static MethodInfo _findBestCleanWaterSource;
		private static MethodInfo _needHygieneClean;
		private static MethodInfo _bathTryFillBath;
		private static MethodInfo _bathTryPullPlug;
		private static PropertyInfo _bathIsFull;
		private static MethodInfo _showerTryUseWater;
		private static FieldInfo _bathOccupantCachedField;
		private static PropertyInfo _bathOccupantCachedProperty;
		private static FieldInfo _cleanedPerTickField;
		private static PropertyInfo _cleanedPerTickProperty;
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
			if (pawn == null || pawn.Map == null || pawn.Downed || pawn.Drafted || ToddlerMentalStateUtility.HasBlockingMentalState(pawn))
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

			// 只在玩家强制的任务下不中断
			if (pawn.jobs?.curJob?.playerForced ?? false)
			{
				return false;
			}

			// 已经在执行洗澡任务了
			if (pawn.CurJobDef == ToddlersExpansionJobDefOf.RimTalk_ToddlerSelfBath)
			{
				return false;
			}

			// 主要条件：卫生值低于阈值就去洗澡
			Need hygiene = GetHygieneNeed(pawn);
			if (hygiene == null || hygiene.CurLevelPercentage >= MaxHygieneForSelfBath)
			{
				return false;
			}

			return true;
		}

		/// <summary>
		/// 获取 pawn 洗澡资格的详细诊断信息
		/// </summary>
		public static string GetEligibilityDiagnosis(Pawn pawn)
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine($"=== Self Bath Eligibility Diagnosis for {pawn?.LabelShort ?? "NULL"} ===");

			if (pawn == null)
			{
				sb.AppendLine("✗ Pawn is NULL");
				return sb.ToString();
			}

			// 基础状态检查
			sb.AppendLine("\n[Basic State Checks]");
			sb.AppendLine($"  Map: {(pawn.Map != null ? "✓ Has map" : "✗ No map")}");
			sb.AppendLine($"  Downed: {(pawn.Downed ? "✗ YES (blocked)" : "✓ No")}");
			sb.AppendLine($"  Drafted: {(pawn.Drafted ? "✗ YES (blocked)" : "✓ No")}");
			sb.AppendLine($"  InMentalState: {(ToddlerMentalStateUtility.HasBlockingMentalState(pawn) ? "✗ YES (blocked)" : "✓ No / non-blocking")}");
			sb.AppendLine($"  Awake: {(pawn.Awake() ? "✓ Yes" : "✗ NO (blocked)")}");

			// 身份检查
			sb.AppendLine("\n[Identity Checks]");
			bool isToddler = ToddlersCompatUtility.IsToddler(pawn);
			bool canSelfCare = ToddlersCompatUtility.CanSelfCare(pawn);
			sb.AppendLine($"  IsToddler: {(isToddler ? "✓ Yes" : "✗ NO (blocked)")}");
			sb.AppendLine($"  CanSelfCare: {(canSelfCare ? "✓ Yes" : "✗ NO (blocked)")}");

			// 当前任务检查
			sb.AppendLine("\n[Job Checks]");
			bool playerForced = pawn.jobs?.curJob?.playerForced ?? false;
			JobDef current = pawn.CurJobDef;
			sb.AppendLine($"  Current Job: {current?.defName ?? "None"}");
			sb.AppendLine($"  PlayerForced: {(playerForced ? "✗ YES (blocked)" : "✓ No")}");
			bool alreadyBathing = current == ToddlersExpansionJobDefOf.RimTalk_ToddlerSelfBath;
			sb.AppendLine($"  Already Bathing: {(alreadyBathing ? "✗ YES (blocked)" : "✓ No")}");

			// 需求检查 - 只检查卫生，移除 Play 限制
			sb.AppendLine("\n[Need Checks]");
			EnsureInitialized();
			Need hygiene = GetHygieneNeed(pawn);
			sb.AppendLine($"  Hygiene Need Def: {(_hygieneNeedDef != null ? "✓ Found" : "✗ NOT FOUND")}");
			if (hygiene != null)
			{
				sb.AppendLine($"  Hygiene Level: {hygiene.CurLevelPercentage:P1} (max threshold: {MaxHygieneForSelfBath:P1})");
				sb.AppendLine($"  Hygiene Check: {(hygiene.CurLevelPercentage < MaxHygieneForSelfBath ? "✓ Below threshold - WILL TRIGGER" : "✗ ABOVE threshold (blocked)")}");
			}
			else
			{
				sb.AppendLine($"  Hygiene Need: ✗ NULL (blocked - maybe DBH not installed?)");
			}

			// 洗澡目标检查
			sb.AppendLine("\n[Bath Target Checks]");
			if (TryFindBathTarget(pawn, out LocalTargetInfo target, debugLog: true))
			{
				sb.AppendLine($"  Target Found: ✓ {DescribeTarget(target)}");
				bool reachable = IsTargetReachable(pawn, target);
				sb.AppendLine($"  Target Reachable: {(reachable ? "✓ Yes" : "✗ NO")}");
			}
			else
			{
				sb.AppendLine("  Target Found: ✗ NO (no valid bath/water source)");
			}

			// 最终结论
			sb.AppendLine("\n[Final Result]");
			bool eligible = IsEligibleSelfBathPawn(pawn);
			sb.AppendLine($"  IsEligibleSelfBathPawn: {(eligible ? "✓ ELIGIBLE" : "✗ NOT ELIGIBLE")}");

			return sb.ToString();
		}

		/// <summary>
		/// 获取卫生 mod 集成状态
		/// </summary>
		public static string GetIntegrationStatus()
		{
			EnsureInitialized();
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("=== Hygiene Integration Status ===");
			sb.AppendLine($"  Initialized: {_initialized}");
			sb.AppendLine($"  Hygiene NeedDef: {(_hygieneNeedDef != null ? $"✓ Found ({_hygieneNeedDef.defName})" : "✗ NOT FOUND")}");
			sb.AppendLine($"  Bath Type (DBH): {(_bathType != null ? $"✓ Found ({_bathType.FullName})" : "✗ NOT FOUND")}");
			sb.AppendLine($"  WashBucket Type (DBH): {(_washBucketType != null ? $"✓ Found ({_washBucketType.FullName})" : "✗ NOT FOUND")}");
			sb.AppendLine($"  FindBathOrTub Method: {(_findBathOrTub != null ? $"✓ Found ({_findBathOrTub.DeclaringType?.Name}.{_findBathOrTub.Name})" : "✗ NOT FOUND")}");
			sb.AppendLine($"  FindBestCleanWaterSource Method: {(_findBestCleanWaterSource != null ? $"✓ Found ({_findBestCleanWaterSource.DeclaringType?.Name}.{_findBestCleanWaterSource.Name})" : "✗ NOT FOUND")}");

			// 检查已加载的 mod
			sb.AppendLine("\n[Loaded Mods Check]");
			bool hasDubsBadHygiene = ModsConfig.ActiveModsInLoadOrder.Any(m => m.PackageId.ToLower().Contains("dubsbadhy"));
			bool hasToddlers = ModsConfig.ActiveModsInLoadOrder.Any(m => m.PackageId.ToLower().Contains("toddler"));
			sb.AppendLine($"  Dubs Bad Hygiene: {(hasDubsBadHygiene ? "✓ Loaded" : "✗ NOT LOADED")}");
			sb.AppendLine($"  Toddlers Mod: {(hasToddlers ? "✓ Loaded" : "✗ NOT LOADED")}");

			return sb.ToString();
		}

		private static string DescribeTarget(LocalTargetInfo target)
		{
			if (!target.IsValid)
			{
				return "Invalid";
			}

			if (target.HasThing)
			{
				Thing t = target.Thing;
				return $"Thing: {t.LabelShort} ({t.def.defName}) at {t.Position}";
			}

			return $"Cell: {target.Cell}";
		}

		public static bool TryCreateSelfBathJob(Pawn pawn, out Job job, bool debugLog = false)
		{
			job = null;
			if (pawn == null || pawn.Map == null)
			{
				if (debugLog) Log.Message($"[SelfBath Debug] {pawn?.LabelShort ?? "NULL"}: pawn or map is null");
				return false;
			}

			EnsureInitialized();
			if (_hygieneNeedDef == null)
			{
				if (debugLog) Log.Message($"[SelfBath Debug] {pawn.LabelShort}: Hygiene NeedDef not found (is Dubs Bad Hygiene installed?)");
				return false;
			}

			if (!TryFindBathTarget(pawn, out LocalTargetInfo target, debugLog))
			{
				if (debugLog) Log.Message($"[SelfBath Debug] {pawn.LabelShort}: No valid bath target found");
				return false;
			}

			return TryCreateSelfBathJobForTarget(pawn, target, out job, ignoreAllowedArea: false, debugLog);
		}

		public static bool TryCreateSelfBathJobForTarget(Pawn pawn, LocalTargetInfo target, out Job job, bool ignoreAllowedArea = false, bool debugLog = false)
		{
			job = null;
			if (pawn == null || pawn.Map == null || !target.IsValid)
			{
				return false;
			}

			EnsureInitialized();
			if (_hygieneNeedDef == null)
			{
				return false;
			}

			if (!IsTargetReachable(pawn, target, ignoreAllowedArea))
			{
				if (debugLog) Log.Message($"[SelfBath Debug] {pawn.LabelShort}: Target {DescribeTarget(target)} is not reachable");
				return false;
			}

			job = JobMaker.MakeJob(ToddlersExpansionJobDefOf.RimTalk_ToddlerSelfBath, target);
			if (target.HasThing && IsBath(target.Thing) && TryGetBathLayCell(target.Thing, out IntVec3 layCell))
			{
				if (layCell.IsValid
					&& layCell.InBounds(pawn.Map)
					&& (ignoreAllowedArea || ForbidUtility.InAllowedArea(layCell, pawn))
					&& CanReachCell(pawn, layCell, ignoreAllowedArea))
				{
					job.SetTarget(TargetIndex.B, layCell);
				}
				else if (debugLog)
				{
					Log.Message($"[SelfBath Debug] {pawn.LabelShort}: Bath lay cell {layCell} not reachable/allowed; using bath touch instead.");
				}
			}

			job.ignoreJoyTimeAssignment = true;
			job.expiryInterval = 2800;
			if (ignoreAllowedArea)
			{
				job.playerForced = true;
				job.ignoreForbidden = true;
			}

			if (debugLog) Log.Message($"[SelfBath Debug] {pawn.LabelShort}: Successfully created self bath job targeting {DescribeTarget(target)}");
			return true;
		}

		private static bool TryFindBathTarget(Pawn pawn, out LocalTargetInfo target, bool debugLog = false)
		{
			target = LocalTargetInfo.Invalid;

			if (_findBestHygieneSource != null)
			{
				if (TryFindBestAdultHygieneSource(pawn, out target, debugLog))
				{
					return true;
				}

				if (debugLog)
				{
					Log.Message($"[SelfBath Debug] {pawn.LabelShort}: Adult hygiene source failed, trying toddler/clean-water fallback");
				}
			}


			if (TryFindBathOrTub(pawn, out Thing bath, debugLog))
			{
				target = bath;
				if (debugLog) Log.Message($"[SelfBath Debug] {pawn.LabelShort}: Found bath/tub: {bath.LabelShort}");
				return true;
			}
			if (debugLog) Log.Message($"[SelfBath Debug] {pawn.LabelShort}: No bath/tub found");

			if (_findBestCleanWaterSource == null)
			{
				if (debugLog) Log.Message($"[SelfBath Debug] {pawn.LabelShort}: FindBestCleanWaterSource method not available");
				return false;
			}

			try
			{
				object result = _findBestCleanWaterSource.Invoke(null, new object[] { pawn, pawn, false, SearchRadius, null, null });
				if (result is LocalTargetInfo lti && lti.IsValid)
				{
					target = lti;
					if (debugLog) Log.Message($"[SelfBath Debug] {pawn.LabelShort}: Found clean water source: {DescribeTarget(lti)}");
					return true;
				}
				if (debugLog) Log.Message($"[SelfBath Debug] {pawn.LabelShort}: FindBestCleanWaterSource returned invalid/null result");
			}
			catch (Exception ex)
			{
				if (debugLog) Log.Message($"[SelfBath Debug] {pawn.LabelShort}: FindBestCleanWaterSource threw exception: {ex.Message}");
				return false;
			}

			return false;
		}

		private static bool TryFindBestAdultHygieneSource(Pawn pawn, out LocalTargetInfo target, bool debugLog)
		{
			target = LocalTargetInfo.Invalid;
			if (_findBestHygieneSource == null || pawn == null)
			{
				return false;
			}

			try
			{
				object result = _findBestHygieneSource.Invoke(null, new object[] { pawn, false, 100f });
				if (result is LocalTargetInfo lti && lti.IsValid)
				{
					target = lti;
					if (debugLog) Log.Message($"[SelfBath Debug] {pawn.LabelShort}: DBH adult hygiene source: {DescribeTarget(lti)}");
					return true;
				}
				if (debugLog) Log.Message($"[SelfBath Debug] {pawn.LabelShort}: DBH adult hygiene source not found");
			}
			catch (Exception ex)
			{
				if (debugLog) Log.Message($"[SelfBath Debug] {pawn.LabelShort}: DBH FindBestHygieneSource threw exception: {ex.Message}");
			}

			return false;
		}

		private static bool TryFindBathOrTub(Pawn pawn, out Thing bath, bool debugLog = false)
		{
			bath = null;
			if (_findBathOrTub == null)
			{
				if (debugLog) Log.Message($"[SelfBath Debug] {pawn.LabelShort}: FindBathOrTub method not available");
				return false;
			}

			object[] args = { pawn, pawn, null };
			try
			{
				bool found = (bool)_findBathOrTub.Invoke(null, args);
				bath = args[2] as Thing;
				if (debugLog) Log.Message($"[SelfBath Debug] {pawn.LabelShort}: FindBathOrTub returned {found}, bath={bath?.LabelShort ?? "null"}");
				return found && bath != null;
			}
			catch (Exception ex)
			{
				if (debugLog) Log.Message($"[SelfBath Debug] {pawn.LabelShort}: FindBathOrTub threw exception: {ex.Message}");
				return false;
			}
		}

		private static bool IsTargetReachable(Pawn pawn, LocalTargetInfo target, bool ignoreAllowedArea = false)
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

				if (!ignoreAllowedArea && !ForbidUtility.InAllowedArea(thing.Position, pawn))
				{
					return false;
				}

				if (!thing.Spawned)
				{
					return pawn.CanReserve(thing);
				}

				PathEndMode pathEndMode = GetPathEndModeForTarget(thing);
				if (ignoreAllowedArea)
				{
					return CanReachThing(pawn, thing, pathEndMode) && pawn.CanReserve(thing, 1, -1, null, true);
				}

				return pawn.CanReserveAndReach(thing, pathEndMode, Danger.Some);
			}

			IntVec3 cell = target.Cell;
			if (!cell.IsValid || !cell.InBounds(pawn.Map))
			{
				return false;
			}

			if (!ignoreAllowedArea && !ForbidUtility.InAllowedArea(cell, pawn))
			{
				return false;
			}

			return CanReachCell(pawn, cell, ignoreAllowedArea);
		}

		private static bool CanReachThing(Pawn pawn, Thing thing, PathEndMode pathEndMode)
		{
			if (pawn == null || thing == null || pawn.Map == null || thing.Map != pawn.Map)
			{
				return false;
			}

			return pawn.Map.reachability.CanReach(pawn.Position, thing, pathEndMode, TraverseParms.For(TraverseMode.ByPawn, Danger.Some));
		}

		private static bool CanReachCell(Pawn pawn, IntVec3 cell, bool ignoreAllowedArea)
		{
			if (pawn == null || pawn.Map == null || !cell.IsValid || !cell.InBounds(pawn.Map))
			{
				return false;
			}

			if (!ignoreAllowedArea)
			{
				return pawn.CanReach(cell, PathEndMode.ClosestTouch, Danger.Some);
			}

			return pawn.Map.reachability.CanReach(pawn.Position, cell, PathEndMode.ClosestTouch, TraverseParms.For(TraverseMode.ByPawn, Danger.Some));
		}

		private static PathEndMode GetPathEndModeForTarget(Thing thing)
		{
			if (IsWashBucket(thing))
			{
				return PathEndMode.InteractionCell;
			}

			if (IsShower(thing))
			{
				return PathEndMode.OnCell;
			}

			return PathEndMode.Touch;
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
			return IsBath(thing) || IsShower(thing) || IsWashBucket(thing);
		}

		public static bool IsBath(Thing thing)
		{
			return _bathType != null && thing != null && _bathType.IsInstanceOfType(thing);
		}

		public static bool IsShower(Thing thing)
		{
			return _showerType != null && thing != null && _showerType.IsInstanceOfType(thing);
		}

		public static bool IsWashBucket(Thing thing)
		{
			return _washBucketType != null && thing != null && _washBucketType.IsInstanceOfType(thing);
		}

		public static bool TryGetBathLayCell(Thing bath, out IntVec3 cell)
		{
			cell = IntVec3.Invalid;
			if (bath == null || bath.Map == null)
			{
				return false;
			}

			EnsureInitialized();
			cell = bath.Position;
			return cell.IsValid && cell.InBounds(bath.Map);
		}

		public static bool IsBathFull(Thing bath)
		{
			if (bath == null || _bathIsFull == null)
			{
				return false;
			}

			object value = _bathIsFull.GetValue(bath);
			return value is bool b && b;
		}

		public static bool TryFillBath(Thing bath)
		{
			if (bath == null || _bathTryFillBath == null)
			{
				return true;
			}

			object result = _bathTryFillBath.Invoke(bath, Array.Empty<object>());
			return result is bool b && b;
		}

		public static void TryPullBathPlug(Thing bath)
		{
			if (bath == null || _bathTryPullPlug == null)
			{
				return;
			}

			_bathTryPullPlug.Invoke(bath, Array.Empty<object>());
		}

		public static bool TryUseShowerWater(Thing shower, out bool cold)
		{
			cold = false;
			if (shower == null || _showerTryUseWater == null)
			{
				return true;
			}

			ParameterInfo[] parms = _showerTryUseWater.GetParameters();
			if (parms.Length < 1 || parms.Length > 2)
			{
				return false;
			}

			try
			{
				object coldArg = false;
				object contamArg = null;
				if (parms.Length > 1)
				{
					Type contamType = parms[1].ParameterType;
					if (contamType.IsByRef)
					{
						contamType = contamType.GetElementType();
					}
					if (contamType != null)
					{
						contamArg = contamType.IsValueType ? Activator.CreateInstance(contamType) : null;
					}
				}
				object[] args = parms.Length == 1 ? new[] { coldArg } : new[] { coldArg, contamArg };
				object result = _showerTryUseWater.Invoke(shower, args);
				if (args.Length > 0 && args[0] is bool b)
				{
					cold = b;
				}
				return result is bool ok && ok;
			}
			catch
			{
				return false;
			}
		}

		public static void ApplyHygieneClean(Pawn pawn, Thing source, float fallbackPerTick, int delta)
		{
			if (pawn == null)
			{
				return;
			}

			Need hygiene = GetHygieneNeed(pawn);
			if (hygiene == null)
			{
				return;
			}

			float perTick = GetCleanedPerTick(source, fallbackPerTick);
			float amount = perTick * delta;
			if (_needHygieneClean != null)
			{
				_needHygieneClean.Invoke(hygiene, new object[] { amount });
				return;
			}

			hygiene.CurLevel = Mathf.Min(hygiene.CurLevel + amount, 1f);
		}

		private static float GetCleanedPerTick(Thing source, float fallback)
		{
			if (source == null)
			{
				return fallback;
			}

			Type type = source.GetType();
			FieldInfo field = type.GetField("cleanedPerTick", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				?? type.GetField("CleanedPerTick", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field != null && field.FieldType == typeof(float))
			{
				return (float)field.GetValue(source);
			}

			PropertyInfo prop = type.GetProperty("CleanedPerTick", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				?? type.GetProperty("cleanedPerTick", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (prop != null && prop.PropertyType == typeof(float))
			{
				return (float)prop.GetValue(source);
			}

			if (_cleanedPerTickField != null
				&& _cleanedPerTickField.FieldType == typeof(float)
				&& _cleanedPerTickField.DeclaringType != null
				&& _cleanedPerTickField.DeclaringType.IsInstanceOfType(source))
			{
				return (float)_cleanedPerTickField.GetValue(source);
			}

			if (_cleanedPerTickProperty != null
				&& _cleanedPerTickProperty.PropertyType == typeof(float)
				&& _cleanedPerTickProperty.DeclaringType != null
				&& _cleanedPerTickProperty.DeclaringType.IsInstanceOfType(source))
			{
				return (float)_cleanedPerTickProperty.GetValue(source);
			}

			return fallback;
		}

		public static void BeginBathVisuals(Thing bath, Pawn pawn)
		{
			if (bath == null || pawn == null || bath.Map == null)
			{
				return;
			}

			EnsureInitialized();
			TrySetBathOccupant(bath, pawn);
			bath.Map.mapDrawer.MapMeshDirty(bath.Position, MapMeshFlagDefOf.Buildings);
		}

		public static void EndBathVisuals(Thing bath)
		{
			if (bath == null)
			{
				return;
			}

			EnsureInitialized();
			TrySetBathOccupant(bath, null);
		}

		public static bool ShouldSuppressBathRender(Pawn pawn)
		{
			if (pawn == null || pawn.CurJobDef != ToddlersExpansionJobDefOf.RimTalk_ToddlerSelfBath)
			{
				return false;
			}

			Job job = pawn.jobs?.curJob;
			Thing bath = job?.GetTarget(TargetIndex.A).Thing;
			if (bath == null || !IsBath(bath))
			{
				return false;
			}

			Pawn occupant = GetBathOccupant(bath);
			return occupant == pawn;
		}

		private static void TrySetBathOccupant(Thing bath, Pawn pawn)
		{
			if (bath == null)
			{
				return;
			}

			if (_bathType == null || !_bathType.IsInstanceOfType(bath))
			{
				return;
			}

			if (_bathOccupantField != null)
			{
				_bathOccupantField.SetValue(bath, pawn);
				return;
			}

			if (_bathOccupantProperty != null && _bathOccupantProperty.CanWrite)
			{
				_bathOccupantProperty.SetValue(bath, pawn);
			}
		}

		private static Pawn GetBathOccupant(Thing bath)
		{
			if (bath == null)
			{
				return null;
			}

			if (_bathOccupantCachedField != null)
			{
				return _bathOccupantCachedField.GetValue(bath) as Pawn;
			}

			if (_bathOccupantCachedProperty != null)
			{
				return _bathOccupantCachedProperty.GetValue(bath) as Pawn;
			}

			return null;
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

			Type closestSanitation = AccessTools.TypeByName("DubsBadHygiene.ClosestSanitation");
			if (closestSanitation != null)
			{
				_findBestHygieneSource = AccessTools.Method(closestSanitation, "FindBestHygieneSource", new[] { typeof(Pawn), typeof(bool), typeof(float) });
				_findBestCleanWaterSource ??= AccessTools.Method(
					closestSanitation,
					"FindBestCleanWaterSource",
					new[] { typeof(Pawn), typeof(Pawn), typeof(bool), typeof(float), typeof(ThingDef), typeof(Pawn) });
			}

			Type patchDbh = AccessTools.TypeByName("Toddlers.Patch_DBH");
			if (patchDbh != null)
			{
				MethodInfo patchMethod = AccessTools.Field(patchDbh, "m_FindBestCleanWaterSource")?.GetValue(null) as MethodInfo;
				if (patchMethod != null)
				{
					_findBestCleanWaterSource = patchMethod;
				}
			}

			_bathType = AccessTools.TypeByName("DubsBadHygiene.Building_bath");
			_showerType = AccessTools.TypeByName("DubsBadHygiene.Building_shower");
			_washBucketType = AccessTools.TypeByName("DubsBadHygiene.Building_washbucket");
			if (_bathType != null)
			{
				_bathOccupantField = AccessTools.Field(_bathType, "occupant");
				_bathOccupantProperty = AccessTools.Property(_bathType, "occupant");
				_bathTryFillBath = AccessTools.Method(_bathType, "TryFillBath");
				_bathTryPullPlug = AccessTools.Method(_bathType, "TryPullPlug");
				_bathIsFull = AccessTools.Property(_bathType, "IsFull");
				_bathOccupantCachedField = _bathOccupantField;
				_bathOccupantCachedProperty = _bathOccupantProperty;
			}

			if (_showerType != null)
			{
				_showerTryUseWater = AccessTools.Method(_showerType, "TryUseWater");
			}

			Type hygieneNeedType = AccessTools.TypeByName("DubsBadHygiene.Need_Hygiene");
			if (hygieneNeedType != null)
			{
				_needHygieneClean = AccessTools.Method(hygieneNeedType, "clean", new[] { typeof(float) });
			}

			_cleanedPerTickField = AccessTools.Field(_bathType, "cleanedPerTick")
				?? AccessTools.Field(_showerType, "CleanedPerTick")
				?? AccessTools.Field(_washBucketType, "CleanedPerTick");
			_cleanedPerTickProperty = AccessTools.Property(_bathType, "CleanedPerTick")
				?? AccessTools.Property(_showerType, "CleanedPerTick")
				?? AccessTools.Property(_washBucketType, "CleanedPerTick");
		}
	}
}
