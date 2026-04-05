using System;
using System.Collections.Generic;
using System.Linq;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Language;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	/// <summary>
	/// 处理向商队/访客注入幼儿/儿童 pawn 的逻辑
	/// </summary>
	public static class TravelingPawnInjectionUtility
	{
		private const int DefaultMinBatchCount = 1;
		private const int DefaultMaxBatchCount = 3;
		private const float DefaultExtraBatchChance = 0.3f;
		private const int HardMaxGeneratedPerGroup = 12;
		private const int HardMaxExtraRolls = 12;
		private const int GenerationAttemptsPerRequestedPawn = 4;
		private const float WalkingToddlerSeverity = 0.6f;
		private const float MinPositiveAgeYears = 0.01f;
		private const float FallbackToddlerMinAgeYears = 1f;
		private const float FallbackToddlerMaxAgeYears = 3f;
		private const float MinParentAssignmentScore = 0.1f;
		private const float MinMaleParentAgeAtBirth = 14f;
		private const float MinFemaleParentAgeAtBirth = 16f;
		private const float PreferredMaleParentAgeAtBirth = 30f;
		private const float PreferredFemaleParentAgeAtBirth = 27f;

		private static bool _walkHediffChecked;
		private static HediffDef _learningToWalkDef;
		private static HediffDef _learningManipulationDef;
		private static int _childhoodFallbackScopeDepth;

		internal static bool UseInjectedChildhoodFallback => _childhoodFallbackScopeDepth > 0;

		public static void TryInjectToddlerOrChildPawns(PawnGroupMakerParms parms, ref IEnumerable<Pawn> pawns)
		{
			if (!IsEligibleGroup(parms) || pawns == null)
			{
				return;
			}

			// 检查设置是否启用生成
			if (!ToddlersExpansionMod.Settings.EnableCaravanToddlerGeneration)
			{
				return;
			}

			List<Pawn> pawnList = pawns as List<Pawn> ?? pawns.ToList();
			if (pawnList.Count == 0 || !HasAdultLeader(pawnList, out Pawn adultLeader))
			{
				return;
			}

			// 确保至少有一个成年人
			if (adultLeader == null)
			{
				return;
			}

			ThingDef sampleRace = GetSampleRace(pawnList);
			if (sampleRace == null)
			{
				// 找不到合适的 PawnKindDef（派系的 kindDef 可能有技能或工作标签要求，幼儿无法满足）
				if (Prefs.DevMode)
				{
					Log.Message($"[RimTalk_ToddlersExpansion] No humanlike pawn found in group, skipping injection");
				}
				return;
			}

			// 计算当前组中的成年人、幼儿和儿童数量
			int adultCount = CountAdults(pawnList);
			int existingToddlerCount = pawnList.Count(ToddlersCompatUtility.IsToddler);
			int existingChildCount = pawnList.Count(IsChildPawn);

			// 如果没有成年人，不生成幼儿（幼儿需要成年人照顾）
			if (adultCount == 0)
			{
				return;
			}

			// 计算可以生成的最大幼儿数量（不能超过成年人数量，确保每个幼儿都能被抱）
			int maxToddlersByAdults = adultCount - existingToddlerCount;
			if (maxToddlersByAdults <= 0)
			{
				// 成年人数量已经不足以照顾现有的幼儿，不再生成幼儿
				maxToddlersByAdults = 0;
			}

			// 如果已达到最大数量限制，不再生成
			if (existingToddlerCount >= ToddlersExpansionMod.Settings.MaxToddlersPerGroup &&
				existingChildCount >= ToddlersExpansionMod.Settings.MaxChildrenPerGroup)
			{
				return;
			}

			// 使用设置中的生成概率
			float childChance = existingChildCount > 0 ? 0.7f : ToddlersExpansionMod.Settings.ChildGenerationChance;
			float toddlerChance = existingToddlerCount > 0 ? 0.7f : ToddlersExpansionMod.Settings.ToddlerGenerationChance;

			Pawn samplePawn = GetSamplePawn(pawnList);
			int requestedAddCount = RollStackedCount();
			if (requestedAddCount <= 0)
			{
				return;
			}

			int remainingToddlerCapacity = Mathf.Max(0, ToddlersExpansionMod.Settings.MaxToddlersPerGroup - existingToddlerCount);
			int remainingChildCapacity = Mathf.Max(0, ToddlersExpansionMod.Settings.MaxChildrenPerGroup - existingChildCount);
			int maxPossibleAdditions = remainingToddlerCapacity + remainingChildCapacity;
			int targetAddCount = Mathf.Min(requestedAddCount, maxPossibleAdditions);
			if (targetAddCount <= 0)
			{
				return;
			}

			int added = 0;
			bool addedToddler = false;
			int toddlerToAdd = 0;
			int childToAdd = 0;
			List<Pawn> generatedToddlers = new List<Pawn>();
			int generationAttempts = 0;
			int maxGenerationAttempts = Math.Max(targetAddCount, targetAddCount * GenerationAttemptsPerRequestedPawn);

			// 根据概率决定生成幼儿还是儿童
			while (added < targetAddCount
				&& generationAttempts < maxGenerationAttempts
				&& (existingToddlerCount + toddlerToAdd < ToddlersExpansionMod.Settings.MaxToddlersPerGroup
					|| existingChildCount + childToAdd < ToddlersExpansionMod.Settings.MaxChildrenPerGroup))
			{
				generationAttempts++;
				bool wantChild;
				
				// 检查是否还能生成幼儿（受成人数量限制）
				bool canAddMoreToddlers = (existingToddlerCount + toddlerToAdd) < ToddlersExpansionMod.Settings.MaxToddlersPerGroup
					&& toddlerToAdd < maxToddlersByAdults;
				bool canAddMoreChildren = (existingChildCount + childToAdd) < ToddlersExpansionMod.Settings.MaxChildrenPerGroup;

				if (!canAddMoreToddlers && !canAddMoreChildren)
				{
					break;
				}
				
				if (!canAddMoreToddlers)
				{
					wantChild = true;
				}
				else if (!canAddMoreChildren)
				{
					wantChild = false;
				}
				else
				{
					float totalChance = toddlerChance + childChance;
					wantChild = Rand.Value < (childChance / totalChance);
				}

				if (!TryGetTargetAgeYears(sampleRace, samplePawn, wantChild, out float ageYears))
				{
					continue;
				}

				Pawn pawn = GeneratePawnWithRace(wantChild, sampleRace, parms, ageYears);
				if (pawn != null)
				{
					pawnList.Add(pawn);
					added++;
					if (!wantChild && ToddlersCompatUtility.IsToddler(pawn))
					{
						addedToddler = true;
						toddlerToAdd++;
						generatedToddlers.Add(pawn);
					}
					else if (wantChild)
					{
						childToAdd++;
					}
				}
			}

			if (added > 0)
			{
				if (addedToddler)
				{
					EnsureWalkDef();
					EnsureWalkingToddlers(pawnList);
					AssignTraderToddlerParents(parms, pawnList, generatedToddlers);
					
					// 注意：不再在这里分配背负关系
					// 因为Hospitality等mod可能会在之后重新处理pawns
					// 背负关系将在pawn实际spawn到地图后由Patch_VisitorToddlerBabyFood处理

				}

				NormalizeSilverToAdultInventories(pawnList);
				pawns = pawnList;
				if (Prefs.DevMode)
				{
					string kind = parms?.groupKind?.defName ?? "UnknownGroup";
					string faction = parms?.faction?.Name ?? "NoFaction";
					Log.Message($"[RimTalk_ToddlersExpansion] Added {added} toddler/child pawns to {kind} group ({faction}) based on settings. Requested={requestedAddCount}, Target={targetAddCount}, Attempts={generationAttempts}, Adults={adultCount}, MaxToddlersByAdults={maxToddlersByAdults}");
				}
			}
		}

		public static void NormalizeSilverToAdultInventories(IEnumerable<Pawn> pawns)
		{
			if (pawns == null)
			{
				return;
			}

			List<Pawn> pawnList = pawns as List<Pawn> ?? pawns.ToList();
			if (pawnList.Count == 0)
			{
				return;
			}

			List<Pawn> adultCarriers = new List<Pawn>();
			for (int i = 0; i < pawnList.Count; i++)
			{
				Pawn pawn = pawnList[i];
				if (IsAdultInventoryCarrier(pawn))
				{
					adultCarriers.Add(pawn);
				}
			}

			if (adultCarriers.Count == 0)
			{
				return;
			}

			for (int i = 0; i < pawnList.Count; i++)
			{
				Pawn pawn = pawnList[i];
				if (!ShouldMoveSilverOffPawn(pawn))
				{
					continue;
				}

				MoveSilverToAdults(pawn, adultCarriers);
			}
		}

		#region Helper Methods

		/// <summary>
		/// 计算列表中的成年人数量
		/// </summary>
		private static int CountAdults(List<Pawn> pawns)
		{
			int count = 0;
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				if (pawn?.RaceProps?.Humanlike != true)
				{
					continue;
				}

				if (pawn.DevelopmentalStage.Newborn() || pawn.DevelopmentalStage.Baby())
				{
					continue;
				}

				if (ToddlersCompatUtility.IsToddler(pawn) || pawn.DevelopmentalStage == DevelopmentalStage.Child)
				{
					continue;
				}

				count++;
			}

			return count;
		}

		private static bool IsEligibleGroup(PawnGroupMakerParms parms)
		{
			if (parms == null || parms.inhabitants)
			{
				return false;
			}

			if (parms.faction == null || parms.faction == Faction.OfPlayer)
			{
				return false;
			}

			if (parms.raidStrategy != null)
			{
				return false;
			}

			if (parms.faction.HostileTo(Faction.OfPlayer))
			{
				return false;
			}

			PawnGroupKindDef kind = parms.groupKind;
			if (kind == null)
			{
				return false;
			}

			if (kind == PawnGroupKindDefOf.Combat || kind == PawnGroupKindDefOf.Settlement)
			{
				return false;
			}

			return kind == PawnGroupKindDefOf.Trader
				|| kind == PawnGroupKindDefOf.Peaceful
				|| kind.defName == "Visitor"
				|| kind.defName == "Traveler";
		}

		private static int RollStackedCount()
		{
			int minBatch = Mathf.Clamp(ToddlersExpansionMod.Settings?.MinBatchCount ?? DefaultMinBatchCount, 1, HardMaxGeneratedPerGroup);
			int maxBatch = Mathf.Clamp(ToddlersExpansionMod.Settings?.MaxBatchCount ?? DefaultMaxBatchCount, minBatch, HardMaxGeneratedPerGroup);
			float extraChance = Mathf.Clamp01(ToddlersExpansionMod.Settings?.ExtraBatchChance ?? DefaultExtraBatchChance);

			int count = Rand.RangeInclusive(minBatch, maxBatch);
			int extraRolls = 0;
			while (extraRolls < HardMaxExtraRolls && count < HardMaxGeneratedPerGroup && Rand.Chance(extraChance))
			{
				count += Rand.RangeInclusive(minBatch, maxBatch);
				extraRolls++;
			}

			return Mathf.Clamp(count, 1, HardMaxGeneratedPerGroup);
		}

		private static bool HasAdultLeader(List<Pawn> pawns, out Pawn adultLeader)
		{
			adultLeader = null;
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				if (pawn?.RaceProps?.Humanlike != true)
				{
					continue;
				}

				if (pawn.DevelopmentalStage.Newborn() || pawn.DevelopmentalStage.Baby())
				{
					continue;
				}

				if (ToddlersCompatUtility.IsToddler(pawn) || pawn.DevelopmentalStage == DevelopmentalStage.Child)
				{
					continue;
				}

				adultLeader = pawn;
				return true;
			}

			return false;
		}

		private static ThingDef GetSampleRace(List<Pawn> pawns)
		{
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				if (pawn?.RaceProps?.Humanlike == true && pawn.def != null)
				{
					return pawn.def;
				}
			}

			return null;
		}

		private static Pawn GetSamplePawn(List<Pawn> pawns)
		{
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				if (pawn?.RaceProps?.Humanlike == true)
				{
					return pawn;
				}
			}

			return pawns.Count > 0 ? pawns[0] : null;
		}

		private static bool IsChildPawn(Pawn pawn)
		{
			return pawn != null && pawn.DevelopmentalStage == DevelopmentalStage.Child;
		}

		private static bool IsAdultInventoryCarrier(Pawn pawn)
		{
			if (pawn?.RaceProps?.Humanlike != true || pawn.inventory?.innerContainer == null)
			{
				return false;
			}

			if (pawn.DevelopmentalStage.Newborn() || pawn.DevelopmentalStage.Baby())
			{
				return false;
			}

			if (ToddlersCompatUtility.IsToddler(pawn) || pawn.DevelopmentalStage == DevelopmentalStage.Child)
			{
				return false;
			}

			return true;
		}

		private static bool ShouldMoveSilverOffPawn(Pawn pawn)
		{
			if (pawn?.RaceProps?.Humanlike != true || pawn.inventory?.innerContainer == null)
			{
				return false;
			}

			return pawn.DevelopmentalStage.Newborn()
				|| pawn.DevelopmentalStage.Baby()
				|| ToddlersCompatUtility.IsToddler(pawn)
				|| pawn.DevelopmentalStage == DevelopmentalStage.Child;
		}

		private static void MoveSilverToAdults(Pawn sourcePawn, List<Pawn> adultCarriers)
		{
			if (sourcePawn?.inventory?.innerContainer == null || adultCarriers == null || adultCarriers.Count == 0)
			{
				return;
			}

			for (int i = sourcePawn.inventory.innerContainer.Count - 1; i >= 0; i--)
			{
				Thing thing = sourcePawn.inventory.innerContainer[i];
				if (thing?.def != ThingDefOf.Silver)
				{
					continue;
				}

				Pawn targetPawn = SelectBestSilverCarrier(adultCarriers);
				if (targetPawn?.inventory?.innerContainer == null)
				{
					return;
				}

				if (!sourcePawn.inventory.innerContainer.Remove(thing))
				{
					continue;
				}

				if (!targetPawn.inventory.innerContainer.TryAdd(thing))
				{
					sourcePawn.inventory.innerContainer.TryAdd(thing);
					continue;
				}

				if (Prefs.DevMode)
				{
					Log.Message($"[RimTalk_ToddlersExpansion] Moved {thing.stackCount} silver from {sourcePawn.LabelShort} to {targetPawn.LabelShort}.");
				}
			}
		}

		private static Pawn SelectBestSilverCarrier(List<Pawn> adultCarriers)
		{
			Pawn bestPawn = null;
			int bestSilverCount = int.MaxValue;

			for (int i = 0; i < adultCarriers.Count; i++)
			{
				Pawn candidate = adultCarriers[i];
				if (candidate?.inventory?.innerContainer == null)
				{
					continue;
				}

				int silverCount = CountSilver(candidate);
				if (silverCount < bestSilverCount)
				{
					bestSilverCount = silverCount;
					bestPawn = candidate;
				}
			}

			return bestPawn;
		}

		private static int CountSilver(Pawn pawn)
		{
			if (pawn?.inventory?.innerContainer == null)
			{
				return 0;
			}

			int count = 0;
			for (int i = 0; i < pawn.inventory.innerContainer.Count; i++)
			{
				Thing thing = pawn.inventory.innerContainer[i];
				if (thing?.def == ThingDefOf.Silver)
				{
					count += thing.stackCount;
				}
			}

			return count;
		}

		#endregion

		#region Age Calculation

		private static bool TryGetTargetAgeYears(ThingDef raceDef, Pawn samplePawn, bool wantChild, out float ageYears)
		{
			ageYears = 0f;
			if (raceDef?.race == null)
			{
				return false;
			}

			if (wantChild)
			{
				if (!ModsConfig.BiotechActive)
				{
					return false;
				}

				if (!TryGetChildAgeRange(raceDef, out float minAge, out float maxAge))
				{
					return false;
				}

				ageYears = Rand.Range(minAge, maxAge);
				return true;
			}

			if (!ToddlersCompatUtility.IsToddlersActive)
			{
				return false;
			}

			float minToddler = ToddlersCompatUtility.GetToddlerMinAgeYears(samplePawn);
			float maxToddler = ToddlersCompatUtility.GetToddlerEndAgeYears(samplePawn);
			if (!IsValidAgeRange(minToddler, maxToddler))
			{
				if (Prefs.DevMode)
				{
					string raceName = raceDef?.defName ?? samplePawn?.def?.defName ?? "UnknownRace";
					Log.Warning($"[RimTalk_ToddlersExpansion] Invalid toddler age range ({minToddler:F2}-{maxToddler:F2}) for {raceName}. Falling back to {FallbackToddlerMinAgeYears:F2}-{FallbackToddlerMaxAgeYears:F2}.");
				}

				minToddler = FallbackToddlerMinAgeYears;
				maxToddler = FallbackToddlerMaxAgeYears;
			}

			if (maxToddler <= minToddler)
			{
				return false;
			}

			ageYears = Rand.Range(minToddler, maxToddler);
			if (ageYears <= MinPositiveAgeYears)
			{
				ageYears = MinPositiveAgeYears;
			}
			return true;
		}

		private static bool TryGetChildAgeRange(ThingDef raceDef, out float minAge, out float maxAge)
		{
			minAge = 0f;
			maxAge = 0f;

			List<LifeStageAge> ages = raceDef?.race?.lifeStageAges;
			if (ages == null || ages.Count == 0)
			{
				return false;
			}

			for (int i = 0; i < ages.Count; i++)
			{
				LifeStageAge stageAge = ages[i];
				if (stageAge?.def?.developmentalStage != DevelopmentalStage.Child)
				{
					continue;
				}

				minAge = stageAge.minAge;
				maxAge = i + 1 < ages.Count ? ages[i + 1].minAge - 0.01f : minAge + 1f;
				if (maxAge <= minAge)
				{
					maxAge = minAge + 0.5f;
				}

				return true;
			}

			return false;
		}

		private static bool IsValidAgeRange(float minAge, float maxAge)
		{
			if (float.IsNaN(minAge) || float.IsNaN(maxAge))
			{
				return false;
			}

			if (float.IsInfinity(minAge) || float.IsInfinity(maxAge))
			{
				return false;
			}

			if (minAge < MinPositiveAgeYears || maxAge <= MinPositiveAgeYears)
			{
				return false;
			}

			return maxAge > minAge;
		}

		#endregion

		#region Pawn Generation

		/// <summary>
		/// 生成幼儿/儿童 pawn，使用指定的种族
		/// </summary>
		private static Pawn GeneratePawnWithRace(bool wantChild, ThingDef raceDef, PawnGroupMakerParms parms, float ageYears)
		{
			try
			{
				if (float.IsNaN(ageYears) || float.IsInfinity(ageYears) || ageYears <= MinPositiveAgeYears)
				{
					if (Prefs.DevMode)
					{
						Log.Warning($"[RimTalk_ToddlersExpansion] Invalid ageYears ({ageYears:F2}). Clamping to {MinPositiveAgeYears:F2}.");
					}

					ageYears = MinPositiveAgeYears;
				}

				// 使用工具类获取或创建合适的 PawnKindDef
				PawnKindDef kindDefToUse = ToddlerPawnGenerationUtility.GetOrCreateKindDefForRace(raceDef);

				PawnGenerationRequest request = new PawnGenerationRequest(
					kindDefToUse,
					parms.faction,
					PawnGenerationContext.NonPlayer,
					tile: parms.tile,
					forceGenerateNewPawn: true,
					fixedBiologicalAge: ageYears,
					fixedChronologicalAge: ageYears);

				Pawn pawn;
				_childhoodFallbackScopeDepth++;
				try
				{
					pawn = PawnGenerator.GeneratePawn(request);
				}
				finally
				{
					_childhoodFallbackScopeDepth--;
				}

				// 为幼儿注入婴儿食品和服装
				if (pawn != null && ToddlersCompatUtility.IsToddler(pawn))
				{
					ToddlerPawnGenerationUtility.TryInjectBabyFood(pawn, ageYears);
					// Defer fallback apparel to spawned-map path to keep group generation lightweight.
				}

				if (Prefs.DevMode && pawn != null)
				{
					Log.Message($"[RimTalk_ToddlersExpansion] Generated {(wantChild ? "child" : "toddler")} {pawn.Name} of race {pawn.def.defName}, age {ageYears:F1}");
				}

				return pawn;
			}
			catch (Exception ex)
			{
				if (Prefs.DevMode)
				{
					Log.Warning($"[RimTalk_ToddlersExpansion] GeneratePawnWithRace failed for race={raceDef?.defName}, age={ageYears:F2}: {ex.Message}");
				}
				return null;
			}
		}

		#endregion

		#region Walking Hediffs

		private static void EnsureWalkDef()
		{
			if (_walkHediffChecked)
			{
				return;
			}

			_walkHediffChecked = true;
			_learningToWalkDef = DefDatabase<HediffDef>.GetNamedSilentFail("LearningToWalk");
			_learningManipulationDef = DefDatabase<HediffDef>.GetNamedSilentFail("LearningManipulation");
		}

		private static void EnsureWalkingToddlers(List<Pawn> pawns)
		{
			if (pawns == null)
			{
				return;
			}

			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				if (!ToddlersCompatUtility.IsToddler(pawn) || pawn.health == null)
				{
					continue;
				}

				// 添加蹒跚学步hediff（LearningToWalk）
				if (_learningToWalkDef != null)
				{
					Hediff walkHediff = pawn.health.hediffSet?.GetFirstHediffOfDef(_learningToWalkDef);
					if (walkHediff == null)
					{
						walkHediff = pawn.health.AddHediff(_learningToWalkDef);
					}

					if (walkHediff != null && walkHediff.Severity < WalkingToddlerSeverity)
					{
						walkHediff.Severity = WalkingToddlerSeverity;
					}
				}

				// 添加学习自理hediff（LearningManipulation）
				if (_learningManipulationDef != null)
				{
					Hediff manipulationHediff = pawn.health.hediffSet?.GetFirstHediffOfDef(_learningManipulationDef);
					if (manipulationHediff == null)
					{
						manipulationHediff = pawn.health.AddHediff(_learningManipulationDef);
					}

					if (manipulationHediff != null && manipulationHediff.Severity < WalkingToddlerSeverity)
					{
						manipulationHediff.Severity = WalkingToddlerSeverity;
					}
				}

				// 同步抬升语言学习进度，避免与学习自理初始进度不一致。
				if (LanguageLevelUtility.TryGetOrCreateLanguageHediff(pawn, out Hediff languageHediff)
					&& languageHediff.Severity < WalkingToddlerSeverity)
				{
					languageHediff.Severity = WalkingToddlerSeverity;
				}
			}
		}

		private static void AssignTraderToddlerParents(PawnGroupMakerParms parms, List<Pawn> allPawns, List<Pawn> generatedToddlers)
		{
			if (!ShouldAssignTraderParents(parms))
			{
				if (ToddlersExpansionSettings.ShouldEmitVerboseDebugLogs)
				{
					string kind = parms?.groupKind?.defName ?? "null";
					Log.Message($"[RimTalk_ToddlersExpansion][TraderParentDebug] Skip parent assignment: groupKind={kind}.");
				}

				return;
			}

			if (allPawns == null || generatedToddlers == null || generatedToddlers.Count == 0)
			{
				if (ToddlersExpansionSettings.ShouldEmitVerboseDebugLogs)
				{
					int allCount = allPawns?.Count ?? 0;
					int toddlerCount = generatedToddlers?.Count ?? 0;
					Log.Message($"[RimTalk_ToddlersExpansion][TraderParentDebug] Skip parent assignment: allPawns={allCount}, generatedToddlers={toddlerCount}.");
				}

				return;
			}

			List<Pawn> adultCandidates = new List<Pawn>();
			for (int i = 0; i < allPawns.Count; i++)
			{
				Pawn pawn = allPawns[i];
				if (IsEligibleGeneratedToddlerParent(pawn, parms?.faction))
				{
					adultCandidates.Add(pawn);
				}
			}

			if (adultCandidates.Count == 0)
			{
				if (ToddlersExpansionSettings.ShouldEmitVerboseDebugLogs)
				{
					Log.Message($"[RimTalk_ToddlersExpansion][TraderParentDebug] No adult candidates for {generatedToddlers.Count} generated trader toddlers in faction {parms?.faction?.Name ?? "null"}.");
				}

				return;
			}

			if (ToddlersExpansionSettings.ShouldEmitVerboseDebugLogs)
			{
				Log.Message($"[RimTalk_ToddlersExpansion][TraderParentDebug] Trying to assign parents for {generatedToddlers.Count} trader toddlers with {adultCandidates.Count} adult candidates.");
			}

			for (int i = 0; i < generatedToddlers.Count; i++)
			{
				AssignSingleTraderParent(generatedToddlers[i], adultCandidates);
			}
		}

		private static bool ShouldAssignTraderParents(PawnGroupMakerParms parms)
		{
			return parms?.groupKind == PawnGroupKindDefOf.Trader;
		}

		private static bool IsEligibleGeneratedToddlerParent(Pawn pawn, Faction faction)
		{
			if (pawn?.RaceProps?.Humanlike != true || pawn.relations == null || pawn.ageTracker == null)
			{
				return false;
			}

			if (pawn.Faction != faction)
			{
				return false;
			}

			if (pawn.DevelopmentalStage.Newborn() || pawn.DevelopmentalStage.Baby())
			{
				return false;
			}

			if (ToddlersCompatUtility.IsToddler(pawn) || pawn.DevelopmentalStage == DevelopmentalStage.Child)
			{
				return false;
			}

			return true;
		}

		private static void AssignSingleTraderParent(Pawn toddler, List<Pawn> adultCandidates)
		{
			if (toddler?.relations == null || adultCandidates == null || adultCandidates.Count == 0)
			{
				if (ToddlersExpansionSettings.ShouldEmitVerboseDebugLogs)
				{
					Log.Message($"[RimTalk_ToddlersExpansion][TraderParentDebug] Cannot assign parent: toddler={toddler?.LabelShort ?? "null"}, adultCandidates={adultCandidates?.Count ?? 0}.");
				}

				return;
			}

			Pawn bestParent = null;
			float bestScore = 0f;

			for (int i = 0; i < adultCandidates.Count; i++)
			{
				Pawn candidate = adultCandidates[i];
				if (candidate == null || candidate == toddler)
				{
					continue;
				}

				float score = GetParentAssignmentScore(toddler, candidate);
				if (ToddlersExpansionSettings.ShouldEmitVerboseDebugLogs)
				{
					Log.Message($"[RimTalk_ToddlersExpansion][TraderParentDebug] Candidate {candidate.LabelShort} ({candidate.gender}) for toddler {toddler.LabelShort}: score={score:F3}, age={candidate.ageTracker?.AgeBiologicalYearsFloat ?? -1f:F1}.");
				}

				if (score > bestScore)
				{
					bestScore = score;
					bestParent = candidate;
				}
			}

			if (bestParent == null || bestScore < MinParentAssignmentScore)
			{
				if (ToddlersExpansionSettings.ShouldEmitVerboseDebugLogs)
				{
					Log.Message($"[RimTalk_ToddlersExpansion][TraderParentDebug] No valid parent assigned for toddler {toddler.LabelShort}. bestParent={bestParent?.LabelShort ?? "null"}, bestScore={bestScore:F3}, threshold={MinParentAssignmentScore:F3}.");
				}

				return;
			}

			toddler.relations.AddDirectRelation(PawnRelationDefOf.Parent, bestParent);

			if (ToddlersExpansionSettings.ShouldEmitVerboseDebugLogs || Prefs.DevMode)
			{
				Log.Message($"[RimTalk_ToddlersExpansion][TraderParentDebug] Assigned generated trader toddler {toddler.LabelShort} parent {bestParent.LabelShort} ({bestParent.gender}, score={bestScore:F3}).");
			}
		}

		private static float GetParentAssignmentScore(Pawn toddler, Pawn candidate)
		{
			if (toddler == null || candidate == null)
			{
				return 0f;
			}

			if (candidate.relations == null || candidate.ageTracker == null || toddler.ageTracker == null)
			{
				return 0f;
			}

			if (candidate.gender != Gender.Female && candidate.gender != Gender.Male)
			{
				return 0f;
			}

			if (!IsParentRaceCompatible(toddler, candidate))
			{
				return 0f;
			}

			if (HasConflictingParentRelation(toddler, candidate))
			{
				return 0f;
			}

			float toddlerAge = toddler.ageTracker.AgeBiologicalYearsFloat;
			float candidateAge = candidate.ageTracker.AgeBiologicalYearsFloat;
			float ageAtBirth = candidateAge - toddlerAge;

			float minParentAge = candidate.gender == Gender.Female ? MinFemaleParentAgeAtBirth : MinMaleParentAgeAtBirth;
			float preferredParentAge = candidate.gender == Gender.Female ? PreferredFemaleParentAgeAtBirth : PreferredMaleParentAgeAtBirth;
			if (ageAtBirth < minParentAge)
			{
				return 0f;
			}

			float ageScore = Mathf.Clamp01(1f - Mathf.Abs(ageAtBirth - preferredParentAge) / 18f);
			float childCountPenalty = 1f / (1f + candidate.relations.ChildrenCount * 0.35f);
			float genderScore = candidate.gender == Gender.Female ? 1f : 0.92f;
			return Mathf.Max(0.05f, ageScore * childCountPenalty * genderScore);
		}

		private static bool IsParentRaceCompatible(Pawn toddler, Pawn candidate)
		{
			if (toddler?.def == null || candidate?.def == null)
			{
				return false;
			}

			return toddler.def == candidate.def;
		}

		private static bool HasConflictingParentRelation(Pawn toddler, Pawn candidate)
		{
			if (toddler == null || candidate == null)
			{
				return true;
			}

			if (candidate.gender == Gender.Female)
			{
				Pawn existingMother = toddler.GetMother();
				return existingMother != null && existingMother != candidate;
			}

			if (candidate.gender == Gender.Male)
			{
				Pawn existingFather = toddler.GetFather();
				return existingFather != null && existingFather != candidate;
			}

			return true;
		}

		#endregion
	}
}
