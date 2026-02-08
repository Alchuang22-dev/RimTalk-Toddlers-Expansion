using System;
using System.Collections.Generic;
using System.Linq;
using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	/// <summary>
	/// 处理向商队/访客注入幼儿/儿童 pawn 的逻辑
	/// </summary>
	public static class TravelingPawnInjectionUtility
	{
		private const int MinBatchCount = 1;
		private const int MaxBatchCount = 5;
		private const float ExtraBatchChance = 0.5f;
		private const float WalkingToddlerSeverity = 0.6f;
		private const float MinPositiveAgeYears = 0.01f;
		private const float FallbackToddlerMinAgeYears = 1f;
		private const float FallbackToddlerMaxAgeYears = 3f;

		private static bool _walkHediffChecked;
		private static HediffDef _learningToWalkDef;
		private static HediffDef _learningManipulationDef;

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
			int addCount = RollStackedCount();
			if (addCount <= 0)
			{
				return;
			}

			int added = 0;
			bool addedToddler = false;
			int toddlerToAdd = 0;
			int childToAdd = 0;

			// 根据概率决定生成幼儿还是儿童
			for (int i = 0; i < addCount && (existingToddlerCount + toddlerToAdd < ToddlersExpansionMod.Settings.MaxToddlersPerGroup ||
											existingChildCount + childToAdd < ToddlersExpansionMod.Settings.MaxChildrenPerGroup); i++)
			{
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
					
					// 注意：不再在这里分配背负关系
					// 因为Hospitality等mod可能会在之后重新处理pawns
					// 背负关系将在pawn实际spawn到地图后由Patch_VisitorToddlerBabyFood处理

				}

				pawns = pawnList;
				if (Prefs.DevMode)
				{
					string kind = parms?.groupKind?.defName ?? "UnknownGroup";
					string faction = parms?.faction?.Name ?? "NoFaction";
					Log.Message($"[RimTalk_ToddlersExpansion] Added {added} toddler/child pawns to {kind} group ({faction}) based on settings. Adults: {adultCount}, MaxToddlersByAdults: {maxToddlersByAdults}");
				}
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
			int count = Rand.RangeInclusive(MinBatchCount, MaxBatchCount);
			while (Rand.Chance(ExtraBatchChance))
			{
				count += Rand.RangeInclusive(MinBatchCount, MaxBatchCount);
			}

			return count;
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

				Pawn pawn = PawnGenerator.GeneratePawn(request);

				// 为幼儿注入婴儿食品和服装
				if (pawn != null && ToddlersCompatUtility.IsToddler(pawn))
				{
					ToddlerPawnGenerationUtility.TryInjectBabyFood(pawn, ageYears);
					ToddlerPawnGenerationUtility.EnsureToddlerFallbackApparel(pawn, parms?.tile ?? -1);
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
			}
		}

		#endregion
	}
}
