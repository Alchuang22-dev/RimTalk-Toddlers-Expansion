using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	/// <summary>
	/// 处理幼儿/儿童 pawn 生成相关的工具方法，包括：
	/// - PawnKindDef 的动态创建和管理
	/// - 服装后备处理
	/// - 婴儿食品注入
	/// </summary>
	public static class ToddlerPawnGenerationUtility
	{
		#region Constants
		
		private const float WarmHeadgearThreshold = 15f;
		private const float BabyFoodBaseUnits = 5f;
		private const float BabyFoodUnitsPerToddlerAgeYear = 2f;
		private const float MinBabyFoodUnits = 3f;
		private const float MaxBabyFoodUnits = 15f;

		private static readonly string[] BabyBodyFallback_Industrial = { "Apparel_BabyOnesie", "Apparel_BabyTribal" };
		private static readonly string[] BabyBodyFallback_Tribal = { "Apparel_BabyTribal", "Apparel_BabyOnesie" };
		private static readonly string[] BabyHeadFallback_Cold = { "Apparel_BabyTuque", "Apparel_BabyShadecone" };
		private static readonly string[] BabyHeadFallback_Hot = { "Apparel_BabyShadecone", "Apparel_BabyTuque" };

		private static readonly string[] ChildBodyFallback_Industrial = { "Apparel_KidRomper", "Apparel_KidTribal" };
		private static readonly string[] ChildBodyFallback_Tribal = { "Apparel_KidTribal", "Apparel_KidRomper" };
		private static readonly string[] ChildHeadFallback_Cold = { "Apparel_Tuque", "Apparel_Shadecone" };
		private static readonly string[] ChildHeadFallback_Hot = { "Apparel_Shadecone", "Apparel_Tuque" };

		#endregion

		#region PawnKindDef Management

		// 缓存动态创建的 PawnKindDef（按种族）
		private static readonly Dictionary<ThingDef, PawnKindDef> _raceKindDefCache = new Dictionary<ThingDef, PawnKindDef>();

		/// <summary>
		/// 获取或创建一个使用指定种族的 PawnKindDef（无技能/工作要求）
		/// 优先查找现有的无技能要求的 kindDef，如果没有则动态创建
		/// </summary>
		public static PawnKindDef GetOrCreateKindDefForRace(ThingDef raceDef, PawnKindDef fallbackKindDef = null)
		{
			if (raceDef == null)
			{
				return fallbackKindDef ?? PawnKindDefOf.Villager;
			}

			// 检查缓存
			if (_raceKindDefCache.TryGetValue(raceDef, out PawnKindDef cachedKind))
			{
				return cachedKind;
			}

			// 尝试找一个现有的 kindDef 使用目标种族且没有技能/工作要求
			PawnKindDef existingKind = FindExistingKindDefForRace(raceDef);
			if (existingKind != null)
			{
				_raceKindDefCache[raceDef] = existingKind;
				return existingKind;
			}

			// 动态创建一个新的 PawnKindDef
			PawnKindDef newKindDef = CreateMinimalKindDefForRace(raceDef);
			_raceKindDefCache[raceDef] = newKindDef;

			if (Prefs.DevMode)
			{
				Log.Message($"[RimTalk_ToddlersExpansion] Created temporary PawnKindDef for race {raceDef.defName}");
			}

			return newKindDef;
		}

		/// <summary>
		/// 尝试找一个现有的 PawnKindDef 使用目标种族且没有技能/工作要求
		/// </summary>
		private static PawnKindDef FindExistingKindDefForRace(ThingDef raceDef)
		{
			List<PawnKindDef> allKindDefs = DefDatabase<PawnKindDef>.AllDefsListForReading;
			for (int i = 0; i < allKindDefs.Count; i++)
			{
				PawnKindDef kind = allKindDefs[i];
				if (kind.race != raceDef)
				{
					continue;
				}

				// 检查是否有工作要求
				if (kind.requiredWorkTags != WorkTags.None)
				{
					continue;
				}

				// 检查是否有技能要求
				if (HasSkillRequirements(kind))
				{
					continue;
				}

				// 找到一个合适的 kindDef
				return kind;
			}

			return null;
		}

		/// <summary>
		/// 检查 PawnKindDef 是否有技能要求
		/// </summary>
		private static bool HasSkillRequirements(PawnKindDef kind)
		{
			if (kind.skills == null || kind.skills.Count == 0)
			{
				return false;
			}

			for (int j = 0; j < kind.skills.Count; j++)
			{
				if (kind.skills[j].Range.min > 0)
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// 为指定种族创建一个最简单的 PawnKindDef（无技能/工作要求）
		/// </summary>
		private static PawnKindDef CreateMinimalKindDefForRace(ThingDef raceDef)
		{
			return new PawnKindDef
			{
				defName = $"RimTalkTemp_{raceDef.defName}",
				label = "toddler",
				race = raceDef,
				combatPower = 1f,
				isFighter = false,
				canMeleeAttack = false,
				appearsRandomlyInCombatGroups = false,
				weaponMoney = FloatRange.Zero,
				initialResistanceRange = new FloatRange(0f, 1f),
				initialWillRange = new FloatRange(0f, 0f),
				skills = null,
				requiredWorkTags = WorkTags.None
			};
		}

		#endregion

		#region Apparel Management

		/// <summary>
		/// 确保幼儿有基本服装
		/// </summary>
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
				TryEquipFallbackBody(pawn, ToddlersCompatUtility.ToddlerApparelSetting.BabyApparel, preferTribal);
			}

			if (needsHead && !TryEquipFallbackHead(pawn, effectiveSetting, preferWarmHeadgear))
			{
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

		#endregion

		#region Baby Food

		/// <summary>
		/// 为幼儿注入婴儿食品
		/// </summary>
		public static void TryInjectBabyFood(Pawn toddler, float ageYears)
		{
			if (toddler == null || toddler.inventory == null)
			{
				return;
			}

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

		#endregion
	}
}
