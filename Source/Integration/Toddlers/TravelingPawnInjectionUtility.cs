using System;
using System.Collections.Generic;
using System.Linq;
using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public static class TravelingPawnInjectionUtility
	{
		private const int MinBatchCount = 1;
		private const int MaxBatchCount = 5;
		private const float ExtraBatchChance = 0.5f;
		private const float ChildBiasWhenAlreadyPresent = 0.7f;
		private const float WalkingToddlerSeverity = 0.6f;
		private const float BabyFoodBaseUnits = 5f;
		private const float BabyFoodUnitsPerToddlerAgeYear = 2f;
		private const float MinBabyFoodUnits = 3f;
		private const float MaxBabyFoodUnits = 15f;
		private const float MinPositiveAgeYears = 0.01f;
		private const float FallbackToddlerMinAgeYears = 1f;
		private const float FallbackToddlerMaxAgeYears = 3f;
		private const float WarmHeadgearThreshold = 15f;

		private static readonly string[] BabyBodyFallback_Industrial = { "Apparel_BabyOnesie", "Apparel_BabyTribal" };
		private static readonly string[] BabyBodyFallback_Tribal = { "Apparel_BabyTribal", "Apparel_BabyOnesie" };
		private static readonly string[] BabyHeadFallback_Cold = { "Apparel_BabyTuque", "Apparel_BabyShadecone" };
		private static readonly string[] BabyHeadFallback_Hot = { "Apparel_BabyShadecone", "Apparel_BabyTuque" };

		private static readonly string[] ChildBodyFallback_Industrial = { "Apparel_KidRomper", "Apparel_KidTribal" };
		private static readonly string[] ChildBodyFallback_Tribal = { "Apparel_KidTribal", "Apparel_KidRomper" };
		private static readonly string[] ChildHeadFallback_Cold = { "Apparel_Tuque", "Apparel_Shadecone" };
		private static readonly string[] ChildHeadFallback_Hot = { "Apparel_Shadecone", "Apparel_Tuque" };

		private static bool _walkHediffChecked;
		private static HediffDef _learningToWalkDef;
		private static HediffDef _learningManipulationDef;

		// 技能-最低年龄缓存：key = (SkillDef, ThingDef raceDef), value = minAge
		// 缓存在 Def 数据库解析后填充，游戏重载时自动清空（因为 static 变量在程序集重载时会重置）
		private static readonly Dictionary<(SkillDef, ThingDef), int> _skillMinAgeCache = new Dictionary<(SkillDef, ThingDef), int>();

		/// <summary>
		/// 清除技能年龄缓存。可在游戏重载或 mod 配置改变时调用。
		/// </summary>
		public static void ClearSkillAgeCache()
		{
			_skillMinAgeCache.Clear();
		}

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

			PawnKindDef baseKind = GetBaseKind(pawnList, parms);
			if (baseKind == null)
			{
				// 找不到合适的 PawnKindDef（派系的 kindDef 可能有技能或工作标签要求，幼儿无法满足）
				if (Prefs.DevMode)
				{
					string factionName = parms?.faction?.Name ?? "UnknownFaction";
					Log.Message($"[RimTalk_ToddlersExpansion] Skipping toddler/child generation for {factionName}: no suitable PawnKindDef without skill/work requirements");
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
					break; // 都不能再添加了
				}
				
				if (!canAddMoreToddlers)
				{
					wantChild = true; // 幼儿受限，只能生成儿童
				}
				else if (!canAddMoreChildren)
				{
					wantChild = false; // 儿童已达上限，只能生成幼儿
				}
				else
				{
					// 两者都没有达上限，根据概率决定
					float totalChance = toddlerChance + childChance;
					wantChild = Rand.Value < (childChance / totalChance);
				}

				if (!TryGetTargetAgeYears(baseKind, samplePawn, wantChild, out float ageYears))
				{
					continue;
				}

				// 检查该年龄是否能满足 kindDef 的技能要求
				ThingDef raceDef = baseKind?.race ?? samplePawn?.def ?? ThingDefOf.Human;
				if (!CanAgeSatisfySkillRequirements(baseKind, ageYears, raceDef))
				{
					// 年龄不足以满足技能要求，跳过本次生成
					if (Prefs.DevMode)
					{
						Log.Message($"[RimTalk_ToddlersExpansion] Skipping generation: age {ageYears:F1} cannot satisfy skill requirements for {baseKind?.defName}");
					}
					continue;
				}

				Pawn pawn = GeneratePawn(baseKind, parms, ageYears);
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

		/// <summary>
		/// 获取技能需要的最低年龄（通过查找关联的工作类型的 lifeStageWorkSettings）
		/// </summary>
		private static int GetMinAgeForSkill(SkillDef skillDef, ThingDef raceDef)
		{
			if (skillDef == null || raceDef?.race?.lifeStageWorkSettings == null)
			{
				return 0;
			}

			// 检查缓存
			var cacheKey = (skillDef, raceDef);
			if (_skillMinAgeCache.TryGetValue(cacheKey, out int cachedAge))
			{
				return cachedAge;
			}

			int maxMinAge = 0;
			List<LifeStageWorkSettings> workSettings = raceDef.race.lifeStageWorkSettings;
			List<WorkTypeDef> allWorkTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading;

			// 遍历所有工作类型，查找与该技能关联的工作类型
			for (int i = 0; i < allWorkTypes.Count; i++)
			{
				WorkTypeDef workType = allWorkTypes[i];
				if (workType.relevantSkills == null || !workType.relevantSkills.Contains(skillDef))
				{
					continue;
				}

				// 查找该工作类型在 lifeStageWorkSettings 中的最低年龄
				for (int j = 0; j < workSettings.Count; j++)
				{
					if (workSettings[j].workType == workType)
					{
						if (workSettings[j].minAge > maxMinAge)
						{
							maxMinAge = workSettings[j].minAge;
						}
						break;
					}
				}
			}

			// 存入缓存
			_skillMinAgeCache[cacheKey] = maxMinAge;
			return maxMinAge;
		}

		/// <summary>
		/// 检查指定年龄是否能满足 PawnKindDef 的技能要求
		/// </summary>
		private static bool CanAgeSatisfySkillRequirements(PawnKindDef kind, float ageYears, ThingDef raceDef)
		{
			if (kind?.skills == null || kind.skills.Count == 0)
			{
				return true; // 无技能要求
			}

			int ageInYears = Mathf.FloorToInt(ageYears);

			for (int i = 0; i < kind.skills.Count; i++)
			{
				SkillRange skillRange = kind.skills[i];
				if (skillRange.Range.min > 0)
				{
					// 需要这个技能，检查该年龄是否可以拥有
					int minAgeForSkill = GetMinAgeForSkill(skillRange.Skill, raceDef);
					if (ageInYears < minAgeForSkill)
					{
						return false; // 年龄不足以拥有这个技能
					}
				}
			}

			return true;
		}

		/// <summary>
		/// 检查 PawnKindDef 是否适合生成幼儿/儿童（无工作标签要求）
		/// </summary>
		private static bool IsKindSuitableForChildren(PawnKindDef kind)
		{
			if (kind == null)
			{
				return false;
			}

			// 检查工作标签要求
			if (kind.requiredWorkTags != WorkTags.None)
			{
				return false;
			}

			return true;
		}

		/// <summary>
		/// 获取用于生成幼儿/儿童的 PawnKindDef
		/// 优先使用派系的 basicMemberKind，因为它通常没有工作标签要求
		/// 如果都不合适，使用 PawnKindDefOf.Villager 作为后备
		/// </summary>
		private static PawnKindDef GetBaseKind(List<Pawn> pawns, PawnGroupMakerParms parms)
		{
			// 优先使用派系的 basicMemberKind（通常没有工作标签要求）
			PawnKindDef basicKind = parms?.faction?.def?.basicMemberKind;
			
			if (IsKindSuitableForChildren(basicKind))
			{
				return basicKind;
			}
			
			// 如果 basicMemberKind 不可用或有工作标签要求，尝试从商队成员中找一个没有要求的
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				if (pawn?.RaceProps?.Humanlike == true && pawn.kindDef != null)
				{
					if (IsKindSuitableForChildren(pawn.kindDef))
					{
						return pawn.kindDef;
					}
				}
			}
			
			// 使用 PawnKindDefOf.Villager作为回退
			if (PawnKindDefOf.Villager != null && IsKindSuitableForChildren(PawnKindDefOf.Villager))
			{
				if (Prefs.DevMode)
				{
					string factionName = parms?.faction?.Name ?? "UnknownFaction";
					Log.Message($"[RimTalk_ToddlersExpansion] Using fallback PawnKindDefOf.Villager for {factionName} (faction's kindDefs have work requirements)");
				}
				return PawnKindDefOf.Villager;
			}
			
			// 如果连 Villager 都不行（不太可能），返回 null
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

		private static bool TryGetTargetAgeYears(PawnKindDef kind, Pawn samplePawn, bool wantChild, out float ageYears)
		{
			ageYears = 0f;
			if (kind?.RaceProps == null)
			{
				return false;
			}

			if (wantChild)
			{
				if (!ModsConfig.BiotechActive)
				{
					return false;
				}

				if (!TryGetChildAgeRange(kind, out float minAge, out float maxAge))
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
					string kindName = kind?.defName ?? "UnknownKind";
					string raceName = kind?.race?.defName ?? samplePawn?.def?.defName ?? "UnknownRace";
					Log.Warning($"[RimTalk_ToddlersExpansion] Invalid toddler age range ({minToddler:F2}-{maxToddler:F2}) for {raceName}/{kindName}. Falling back to {FallbackToddlerMinAgeYears:F2}-{FallbackToddlerMaxAgeYears:F2}.");
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

		private static bool TryGetChildAgeRange(PawnKindDef kind, out float minAge, out float maxAge)
		{
			minAge = 0f;
			maxAge = 0f;

			List<LifeStageAge> ages = kind?.RaceProps?.lifeStageAges;
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

		private static Pawn GeneratePawn(PawnKindDef kind, PawnGroupMakerParms parms, float ageYears)
		{
			try
			{
				if (float.IsNaN(ageYears) || float.IsInfinity(ageYears) || ageYears <= MinPositiveAgeYears)
				{
					if (Prefs.DevMode)
					{
						Log.Warning($"[RimTalk_ToddlersExpansion] Invalid ageYears ({ageYears:F2}) for kindDef={kind?.defName ?? "UnknownKind"}. Clamping to {MinPositiveAgeYears:F2}.");
					}

					ageYears = MinPositiveAgeYears;
				}

				PawnGenerationRequest request = new PawnGenerationRequest(
					kind,
					parms.faction,
					PawnGenerationContext.NonPlayer,
					tile: parms.tile,
					forceGenerateNewPawn: true,
					fixedBiologicalAge: ageYears,
					fixedChronologicalAge: ageYears);

				Pawn pawn = PawnGenerator.GeneratePawn(request);

				// Inject baby food for toddlers
				if (pawn != null && ToddlersCompatUtility.IsToddler(pawn))
				{
					TryInjectBabyFood(pawn, ageYears);
					EnsureToddlerFallbackApparel(pawn, parms?.tile ?? -1);
				}

				return pawn;
			}
			catch (Exception ex)
			{
				if (Prefs.DevMode)
				{
					Log.Warning($"[RimTalk_ToddlersExpansion] GeneratePawn failed for kindDef={kind?.defName}, age={ageYears:F2}: {ex.Message}");
				}
				return null;
			}
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

		private static bool IsChildPawn(Pawn pawn)
		{
			return pawn != null && pawn.DevelopmentalStage == DevelopmentalStage.Child;
		}

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

		public static void EnsureToddlerFallbackApparel(Pawn pawn, int tile = -1)
		{
			if (pawn?.apparel == null || !ToddlersCompatUtility.IsToddler(pawn))
			{
				return;
			}

			ToddlersCompatUtility.ToddlerApparelSetting apparelSetting = ToddlersCompatUtility.GetToddlerApparelSetting();
			if (apparelSetting == ToddlersCompatUtility.ToddlerApparelSetting.Nude)
			{
				return;
			}

			bool preferTribal = pawn.Faction?.def?.techLevel <= TechLevel.Neolithic;
			if (apparelSetting == ToddlersCompatUtility.ToddlerApparelSetting.NudeTribal && preferTribal)
			{
				return;
			}

			ToddlersCompatUtility.ToddlerApparelSetting effectiveSetting =
				apparelSetting == ToddlersCompatUtility.ToddlerApparelSetting.NudeTribal
					? ToddlersCompatUtility.ToddlerApparelSetting.BabyApparel
					: apparelSetting;

			bool needsBody = !HasBodyApparel(pawn);
			bool needsHead = !HasHeadApparel(pawn);
			if (!needsBody && !needsHead)
			{
				return;
			}

			bool preferWarmHeadgear = ShouldUseWarmHeadgear(pawn, tile);

			if (needsBody && !TryEquipFallbackBody(pawn, effectiveSetting, preferTribal))
			{
				// If child apparel fallback is unavailable, fallback to baby apparel.
				TryEquipFallbackBody(pawn, ToddlersCompatUtility.ToddlerApparelSetting.BabyApparel, preferTribal);
			}

			if (needsHead && !TryEquipFallbackHead(pawn, effectiveSetting, preferWarmHeadgear))
			{
				// Same fallback policy for headgear.
				TryEquipFallbackHead(pawn, ToddlersCompatUtility.ToddlerApparelSetting.BabyApparel, preferWarmHeadgear);
			}
		}

		private static bool TryEquipFallbackBody(Pawn pawn, ToddlersCompatUtility.ToddlerApparelSetting apparelSetting, bool preferTribal)
		{
			string[] candidates = apparelSetting == ToddlersCompatUtility.ToddlerApparelSetting.ChildApparel
				? (preferTribal ? ChildBodyFallback_Tribal : ChildBodyFallback_Industrial)
				: (preferTribal ? BabyBodyFallback_Tribal : BabyBodyFallback_Industrial);

			if (TryWearFirstAvailable(pawn, candidates))
			{
				return true;
			}

			return TryWearGenericFallback(
				pawn,
				apparelSetting == ToddlersCompatUtility.ToddlerApparelSetting.ChildApparel ? DevelopmentalStage.Child : DevelopmentalStage.Baby,
				requireHead: false,
				preferTribal: preferTribal);
		}

		private static bool TryEquipFallbackHead(Pawn pawn, ToddlersCompatUtility.ToddlerApparelSetting apparelSetting, bool preferWarmHeadgear)
		{
			string[] candidates = apparelSetting == ToddlersCompatUtility.ToddlerApparelSetting.ChildApparel
				? (preferWarmHeadgear ? ChildHeadFallback_Cold : ChildHeadFallback_Hot)
				: (preferWarmHeadgear ? BabyHeadFallback_Cold : BabyHeadFallback_Hot);

			if (TryWearFirstAvailable(pawn, candidates))
			{
				return true;
			}

			bool preferTribal = pawn?.Faction?.def?.techLevel <= TechLevel.Neolithic;
			return TryWearGenericFallback(
				pawn,
				apparelSetting == ToddlersCompatUtility.ToddlerApparelSetting.ChildApparel ? DevelopmentalStage.Child : DevelopmentalStage.Baby,
				requireHead: true,
				preferTribal: preferTribal);
		}

		private static bool TryWearFirstAvailable(Pawn pawn, string[] defNames)
		{
			if (pawn?.apparel == null || defNames == null)
			{
				return false;
			}

			for (int i = 0; i < defNames.Length; i++)
			{
				ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defNames[i]);
				if (!CanWearFallbackDef(pawn, def))
				{
					continue;
				}

				if (TryWearFallbackDef(pawn, def))
				{
					return true;
				}
			}

			return false;
		}

		private static bool CanWearFallbackDef(Pawn pawn, ThingDef def)
		{
			if (pawn?.apparel == null || def?.apparel == null || !def.IsApparel)
			{
				return false;
			}

			if (!pawn.apparel.CanWearWithoutDroppingAnything(def))
			{
				return false;
			}

			return ApparelUtility.HasPartsToWear(pawn, def);
		}

		private static bool TryWearFallbackDef(Pawn pawn, ThingDef def)
		{
			ThingDef stuff = def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null;
			Apparel apparel = ThingMaker.MakeThing(def, stuff) as Apparel;
			if (apparel == null)
			{
				return false;
			}

			try
			{
				if (!apparel.PawnCanWear(pawn) || !ApparelUtility.HasPartsToWear(pawn, def))
				{
					apparel.Destroy(DestroyMode.Vanish);
					return false;
				}

				PawnGenerator.PostProcessGeneratedGear(apparel, pawn);
				pawn.apparel.Wear(apparel, false);
				return true;
			}
			catch
			{
				if (!apparel.Destroyed)
				{
					apparel.Destroy(DestroyMode.Vanish);
				}

				return false;
			}
		}

		private static bool TryWearGenericFallback(Pawn pawn, DevelopmentalStage stage, bool requireHead, bool preferTribal)
		{
			if (pawn?.apparel == null)
			{
				return false;
			}

			ThingDef firstBest = null;
			ThingDef secondBest = null;
			List<ThingDef> defs = DefDatabase<ThingDef>.AllDefsListForReading;
			for (int i = 0; i < defs.Count; i++)
			{
				ThingDef def = defs[i];
				if (!CanUseGenericFallbackDef(pawn, def, stage, requireHead))
				{
					continue;
				}

				bool isTribal = def.apparel.tags != null && def.apparel.tags.Contains("Neolithic");
				if (isTribal == preferTribal)
				{
					firstBest = def;
					break;
				}

				if (secondBest == null)
				{
					secondBest = def;
				}
			}

			return TryWearFallbackDef(pawn, firstBest ?? secondBest);
		}

		private static bool CanUseGenericFallbackDef(Pawn pawn, ThingDef def, DevelopmentalStage stage, bool requireHead)
		{
			if (!CanWearFallbackDef(pawn, def))
			{
				return false;
			}

			if (!def.apparel.developmentalStageFilter.Has(stage))
			{
				return false;
			}

			List<BodyPartGroupDef> groups = def.apparel.bodyPartGroups;
			if (groups == null || groups.Count == 0)
			{
				return false;
			}

			if (requireHead)
			{
				return groups.Contains(BodyPartGroupDefOf.UpperHead) || groups.Contains(BodyPartGroupDefOf.FullHead);
			}

			return groups.Contains(BodyPartGroupDefOf.Torso) || groups.Contains(BodyPartGroupDefOf.Legs);
		}

		private static bool HasBodyApparel(Pawn pawn)
		{
			List<Apparel> worn = pawn?.apparel?.WornApparel;
			if (worn == null)
			{
				return false;
			}

			for (int i = 0; i < worn.Count; i++)
			{
				List<BodyPartGroupDef> groups = worn[i]?.def?.apparel?.bodyPartGroups;
				if (groups == null)
				{
					continue;
				}

				if (groups.Contains(BodyPartGroupDefOf.Torso) || groups.Contains(BodyPartGroupDefOf.Legs))
				{
					return true;
				}
			}

			return false;
		}

		private static bool HasHeadApparel(Pawn pawn)
		{
			List<Apparel> worn = pawn?.apparel?.WornApparel;
			if (worn == null)
			{
				return false;
			}

			for (int i = 0; i < worn.Count; i++)
			{
				List<BodyPartGroupDef> groups = worn[i]?.def?.apparel?.bodyPartGroups;
				if (groups == null)
				{
					continue;
				}

				if (groups.Contains(BodyPartGroupDefOf.UpperHead) || groups.Contains(BodyPartGroupDefOf.FullHead))
				{
					return true;
				}
			}

			return false;
		}

		private static bool ShouldUseWarmHeadgear(Pawn pawn, int tile)
		{
			float temp = 21f;
			if (pawn?.Spawned == true)
			{
				temp = pawn.AmbientTemperature;
			}
			else if (tile >= 0)
			{
				temp = GenTemperature.GetTemperatureAtTile(tile);
			}

			return temp < WarmHeadgearThreshold;
		}

		private static void TryInjectBabyFood(Pawn toddler, float ageYears)
		{
			if (toddler == null || toddler.inventory == null)
			{
				return;
			}

			// 计算婴儿食品数量基于年龄
			float foodUnits = BabyFoodBaseUnits + (ageYears * BabyFoodUnitsPerToddlerAgeYear);
			foodUnits = Mathf.Clamp(foodUnits, MinBabyFoodUnits, MaxBabyFoodUnits);

			int foodCount = Mathf.RoundToInt(foodUnits);

			if (foodCount <= 0)
			{
				return;
			}

			try
			{
				Thing babyFood = ThingMaker.MakeThing(ThingDefOf.BabyFood);
				if (babyFood != null)
				{
					babyFood.stackCount = foodCount;
					toddler.inventory.innerContainer.TryAdd(babyFood);

					if (Prefs.DevMode)
					{
						Log.Message($"[RimTalk_ToddlersExpansion] 为toddler {toddler.Name}添加了{foodCount}个婴儿食品");
					}
				}
			}
			catch (Exception ex)
			{
				Log.Warning($"[RimTalk_ToddlersExpansion] 注入婴儿食品失败: {ex.Message}");
			}
		}
	}
}
