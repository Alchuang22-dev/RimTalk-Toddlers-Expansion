using RimTalk_ToddlersExpansion.Integration.Toddlers;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Language
{
	public sealed class Hediff_ToddlerLanguageLearning : HediffWithComps
	{
		private const int UpdateIntervalTicks = 2500;

		public override bool ShouldRemove => pawn == null
			|| Severity >= 1f
			|| !ToddlersCompatUtility.IsToddler(pawn);

		public float Progress01 => Mathf.Clamp01(Severity);

		public override void TickInterval(int delta)
		{
			base.TickInterval(delta);
			if (pawn == null || !pawn.IsHashIntervalTick(UpdateIntervalTicks, delta))
			{
				return;
			}

			float agingRateFactor = pawn.ageTracker?.BiologicalTicksPerTick ?? 1f;
			float factor = UpdateIntervalTicks * agingRateFactor;
			Severity += LanguageLevelUtility.GetLearningPerBioTick(pawn) * factor * (1f / LanguageLevelUtility.GetToddlersManipulationLearningFactor());
		}

		public override string SeverityLabel
		{
			get
			{
				if (Severity <= 0f)
				{
					return null;
				}

				return Severity.ToStringPercent();
			}
		}

		public override string TipStringExtra
		{
			get
			{
				if (Severity <= 0f)
				{
					return base.TipStringExtra;
				}

				string descriptor = LanguageLevelUtility.GetPromptDescriptor(Severity);
				return $"Progress: {Severity.ToStringPercent()}\nSpeech: {descriptor}";
			}
		}
	}
}
