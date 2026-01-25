using System;
using System.Reflection;
using RimTalk_ToddlersExpansion.Core;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Language
{
	public sealed class HediffComp_LanguageLearningProgress : HediffComp
	{
		private const int UpdateIntervalTicks = 2500;
		private const float DefaultYearsToFluent = 2f;
		private const float TicksPerDay = 60000f;
		private const float DaysPerYear = 60f;
		private const float MinToddlerAgeYears = 1f;
		private const string ToddlersUtilityTypeName = "Toddlers.ToddlerUtility";
		private const float DefaultStage2Min = 0.34f;
		private const float DefaultStage3Min = 0.68f;

		private float _progress01;
		private static bool _toddlersChecked;
		private static bool _toddlersActive;
		private static bool _toddlerAgeChecked;
		private static Func<Pawn, float> _toddlerMinAge;
		private static bool _stageThresholdsInitialized;
		private static float _stage2Min = DefaultStage2Min;
		private static float _stage3Min = DefaultStage3Min;

		public float Progress01 => _progress01;

		public override void CompPostPostAdd(DamageInfo? dinfo)
		{
			base.CompPostPostAdd(dinfo);
			EnsureStageThresholds();
			InitializeProgressFromExisting();
			UpdateSeverity();
		}

		public override void CompExposeData()
		{
			Scribe_Values.Look(ref _progress01, "progress01", 0f);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				EnsureStageThresholds();
				InitializeProgressFromExisting();
				UpdateSeverity();
			}
		}

		public override void CompPostTickInterval(ref float severityAdjustment, int delta)
		{
			if (Pawn == null || !Pawn.Spawned)
			{
				return;
			}

			if (!Pawn.IsHashIntervalTick(UpdateIntervalTicks, delta))
			{
				return;
			}

			InitializeProgressFromExisting();
			if (!ShouldUseProgress())
			{
				UpdateSeverity();
				return;
			}

			float agingRate = Pawn.ageTracker?.BiologicalTicksPerTick ?? 1f;

			// 使用 Toddlers 模组的学习速度计算（如果可用），否则使用默认计算
			float progressPerInterval;
			if (IsToddlersActive())
			{
				// 使用与 Toddlers 模组兼容的方式计算进度
				// 基于幼儿阶段总时长计算每tick的学习进度
				float ticksAsToddler = GetToddlerStageInTicks(Pawn);
				float learningPerTick = GetLearningPerBioTickCompatible(ticksAsToddler);
				progressPerInterval = learningPerTick * UpdateIntervalTicks * agingRate;
			}
			else
			{
				// 使用 RimTalk 自己的默认计算
				float passivePerTick = 1f / (DefaultYearsToFluent * DaysPerYear * TicksPerDay);
				progressPerInterval = passivePerTick * UpdateIntervalTicks * agingRate;
			}

			// 应用学习因子（受 ToddlersExpansionSettings 控制，如果 Toddlers 模组激活则使用其设置）
			float learningFactor = GetLearningFactor();
			AddProgress(progressPerInterval / learningFactor);

			EnsureStageThresholds();
			UpdateSeverity();
		}


		public void SetProgress01(float value)
		{
			_progress01 = Mathf.Clamp01(value);
			UpdateSeverity();
		}

		public void AddProgress(float value)
		{
			if (value <= 0f)
			{
				return;
			}

			_progress01 = Mathf.Clamp01(_progress01 + value);
			UpdateSeverity();
		}

		private void UpdateSeverity()
		{
			if (parent == null)
			{
				return;
			}

			EnsureStageThresholds();
			float severity = ShouldUseProgress() ? Mathf.Clamp01(_progress01) : 0f;
			if (!Mathf.Approximately(parent.Severity, severity))
			{
				parent.Severity = severity;
			}
		}

		private bool ShouldUseProgress()
		{
			float age = Pawn?.ageTracker?.AgeBiologicalYearsFloat ?? 0f;
			float minAge = GetToddlerMinAgeYears(Pawn);
			return age >= minAge;
		}

		private void InitializeProgressFromExisting()
		{
			if (_progress01 > 0f)
			{
				return;
			}

			float existingSeverity = parent?.Severity ?? 0f;
			HediffDef source = FindSelfCareHediff();
			if (source == null || Pawn?.health?.hediffSet == null)
			{
				if (existingSeverity > 0f)
				{
					_progress01 = Mathf.Clamp01(existingSeverity);
				}

				return;
			}

			Hediff hediff = Pawn.health.hediffSet.GetFirstHediffOfDef(source);
			float sourceSeverity = hediff?.Severity ?? 0f;
			float best = existingSeverity;
			if (sourceSeverity > best)
			{
				best = sourceSeverity;
			}

			if (best > 0f)
			{
				_progress01 = Mathf.Clamp01(best);
			}
		}

		private static float GetToddlerMinAgeYears(Pawn pawn)
		{
			if (!IsToddlersActive())
			{
				return MinToddlerAgeYears;
			}

			EnsureToddlerAgeMethod();
			if (_toddlerMinAge == null)
			{
				return MinToddlerAgeYears;
			}

			try
			{
				float value = _toddlerMinAge(pawn);
				return value > 0f ? value : MinToddlerAgeYears;
			}
			catch (Exception)
			{
				return MinToddlerAgeYears;
			}
		}

		private static void EnsureToddlerAgeMethod()
		{
			if (_toddlerAgeChecked)
			{
				return;
			}

			_toddlerAgeChecked = true;
			Type utilityType = GenTypes.GetTypeInAnyAssembly(ToddlersUtilityTypeName);
			if (utilityType == null)
			{
				return;
			}

			MethodInfo minAgeMethod = utilityType.GetMethod("ToddlerMinAge", BindingFlags.Public | BindingFlags.Static);
			if (minAgeMethod != null)
			{
				_toddlerMinAge = (Func<Pawn, float>)Delegate.CreateDelegate(typeof(Func<Pawn, float>), minAgeMethod);
			}
		}

		private static bool IsToddlersActive()
		{
			if (_toddlersChecked)
			{
				return _toddlersActive;
			}

			_toddlersChecked = true;
			_toddlersActive = GenTypes.GetTypeInAnyAssembly(ToddlersUtilityTypeName) != null;
			return _toddlersActive;
		}

		private void EnsureStageThresholds()
		{
			if (_stageThresholdsInitialized)
			{
				return;
			}

			_stageThresholdsInitialized = true;
			_stage2Min = DefaultStage2Min;
			_stage3Min = DefaultStage3Min;

			HediffDef source = FindSelfCareHediff();
			if (source != null && source.stages != null && source.stages.Count >= 3)
			{
				float stage2 = source.stages[1].minSeverity;
				float stage3 = source.stages[2].minSeverity;
				if (stage2 > 0f && stage2 < 1f && stage3 > stage2 && stage3 < 1f)
				{
					_stage2Min = stage2;
					_stage3Min = stage3;
				}
			}

			HediffDef def = parent?.def;
			if (def?.stages == null || def.stages.Count < 3)
			{
				return;
			}

			def.stages[1].minSeverity = _stage2Min;
			def.stages[2].minSeverity = _stage3Min;
		}

		private static HediffDef FindSelfCareHediff()
		{
			if (!IsToddlersActive())
			{
				return null;
			}

			string[] candidates =
			{
				"LearningSelfCare",
				"LearningToSelfCare",
				"ToddlerLearningSelfCare",
				"ToddlerLearningToSelfCare",
				"LearningManipulation",
				"ToddlerLearningManipulation"
			};

			for (int i = 0; i < candidates.Length; i++)
			{
				HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(candidates[i]);
				if (def?.stages != null && def.stages.Count >= 3)
				{
					return def;
				}
			}

			foreach (HediffDef def in DefDatabase<HediffDef>.AllDefsListForReading)
			{
				if (def == null || def.stages == null || def.stages.Count < 3)
				{
					continue;
				}

				string defName = def.defName ?? string.Empty;
				if (defName.IndexOf("SelfCare", StringComparison.OrdinalIgnoreCase) < 0)
				{
					if (defName.IndexOf("Manipulation", StringComparison.OrdinalIgnoreCase) < 0)
					{
						continue;
					}
				}

				if (defName.IndexOf("Learning", StringComparison.OrdinalIgnoreCase) < 0)
				{
					continue;
				}

				return def;
			}

			return null;
		}

		// 计算幼儿阶段的总ticks（兼容版）
		private static float GetToddlerStageInTicks(Pawn pawn)
		{
			if (pawn?.ageTracker == null)
			{
				return DefaultYearsToFluent * DaysPerYear * TicksPerDay; // 默认2年
			}

			float minAge = GetToddlerMinAgeYears(pawn);
			float maxAge = minAge + DefaultYearsToFluent; // 默认幼儿阶段为2年

			return (maxAge - minAge) * DaysPerYear * TicksPerDay;
		}

		// 兼容Toddlers模组的学习进度计算
		private static float GetLearningPerBioTickCompatible(float ticksAsToddler)
		{
			if (ticksAsToddler <= 0f)
			{
				return 1f / (DefaultYearsToFluent * DaysPerYear * TicksPerDay);
			}
			return 1f / ticksAsToddler;
		}

		// 获取学习因子（使用 Toddlers 模组设置如果可用）
		private static float GetLearningFactor()
		{
			// 如果 Toddlers 模组激活，尝试使用其设置（通过反射）
			if (IsToddlersActive())
			{
				try
				{
					Type settingsType = GenTypes.GetTypeInAnyAssembly("Toddlers.Toddlers_Settings");
					if (settingsType != null)
					{
						var field = settingsType.GetField("learningFactor_Manipulation",
							BindingFlags.Public | BindingFlags.Static);
						if (field != null)
						{
							object value = field.GetValue(null);
							if (value is float floatValue && floatValue > 0f)
							{
								return floatValue;
							}
						}
					}
				}
				catch (Exception)
				{
					// 如果反射失败，回退到 RimTalk 自己的设置
				}
			}

			// 使用 RimTalk 自己的设置
			return ToddlersExpansionSettings.learningFactor_Talking;
		}
	}
}
