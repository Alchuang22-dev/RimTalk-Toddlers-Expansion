using System;
using System.Reflection;
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

		public override void CompExposeData()
		{
			Scribe_Values.Look(ref _progress01, "progress01", 0f);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				EnsureStageThresholds();
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

			float agingRate = Pawn.ageTracker?.BiologicalTicksPerTick ?? 1f;
			float passivePerTick = 1f / (DefaultYearsToFluent * DaysPerYear * TicksPerDay);
			AddProgress(passivePerTick * UpdateIntervalTicks * agingRate);
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
			float severity = ShouldUseToddlerStages() ? _progress01 : 0f;
			if (!Mathf.Approximately(parent.Severity, severity))
			{
				parent.Severity = severity;
			}
		}

		private bool ShouldUseToddlerStages()
		{
			if (!IsToddlersActive())
			{
				return false;
			}

			float age = Pawn?.ageTracker?.AgeBiologicalYearsFloat ?? 0f;
			float minAge = GetToddlerMinAgeYears(Pawn);
			return age >= minAge;
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
				"ToddlerLearningToSelfCare"
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
					continue;
				}

				if (defName.IndexOf("Learning", StringComparison.OrdinalIgnoreCase) < 0)
				{
					continue;
				}

				return def;
			}

			return null;
		}
	}
}
