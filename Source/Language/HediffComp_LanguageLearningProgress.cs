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

		private float _progress01;
		private static bool _toddlersChecked;
		private static bool _toddlersActive;

		public float Progress01 => _progress01;

		public override void CompExposeData()
		{
			Scribe_Values.Look(ref _progress01, "progress01", 0f);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
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
			return age >= MinToddlerAgeYears;
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
	}
}
