using RimTalk_ToddlersExpansion.Integration.Toddlers;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Language
{
	public sealed class HediffComp_LanguageLearningProgress : HediffComp
	{
		private const int UpdateIntervalTicks = 2500;

		private float _progress01;

		public float Progress01 => _progress01;

		public override void CompPostPostAdd(DamageInfo? dinfo)
		{
			base.CompPostPostAdd(dinfo);
			InitializeProgressFromSeverity();
			UpdateSeverity();
		}

		public override void CompExposeData()
		{
			Scribe_Values.Look(ref _progress01, "progress01", 0f);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				InitializeProgressFromSeverity();
				UpdateSeverity();
			}
		}

		public override void CompPostTickInterval(ref float severityAdjustment, int delta)
		{
			if (Pawn == null)
			{
				return;
			}

			if (!Pawn.IsHashIntervalTick(UpdateIntervalTicks, delta))
			{
				return;
			}

			if (!ShouldTickForPawn())
			{
				UpdateSeverity();
				return;
			}

			float agingRateFactor = Pawn.ageTracker?.BiologicalTicksPerTick ?? 1f;
			float factor = UpdateIntervalTicks * agingRateFactor;
			float learningPerBioTick = LanguageLevelUtility.GetLearningPerBioTick(Pawn);
			float learningFactor = LanguageLevelUtility.GetToddlersManipulationLearningFactor();

			AddProgress(learningPerBioTick * factor * (1f / learningFactor));
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

		private void InitializeProgressFromSeverity()
		{
			if (_progress01 > 0f)
			{
				return;
			}

			float existingSeverity = parent?.Severity ?? 0f;
			if (existingSeverity > 0f)
			{
				_progress01 = Mathf.Clamp01(existingSeverity);
			}
		}

		private bool ShouldTickForPawn()
		{
			if (ToddlersCompatUtility.IsToddlersActive)
			{
				return ToddlersCompatUtility.IsToddler(Pawn);
			}

			float age = Pawn?.ageTracker?.AgeBiologicalYearsFloat ?? 0f;
			return age >= 1f;
		}

		private void UpdateSeverity()
		{
			if (parent == null)
			{
				return;
			}

			float severity = Mathf.Clamp01(_progress01);
			if (!Mathf.Approximately(parent.Severity, severity))
			{
				parent.Severity = severity;
			}
		}
	}
}
