using Verse;

namespace RimTalk_ToddlersExpansion.Language
{
	public sealed class Hediff_ToddlerLanguageLearning : HediffWithComps
	{
		public float Progress01
		{
			get
			{
				HediffComp_LanguageLearningProgress comp = this.TryGetComp<HediffComp_LanguageLearningProgress>();
				return comp?.Progress01 ?? 0f;
			}
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
				float progress = Progress01;
				if (progress <= 0f)
				{
					return base.TipStringExtra;
				}

				string descriptor = LanguageLevelUtility.GetPromptDescriptor(progress);
				return $"Progress: {progress.ToStringPercent()}\nSpeech: {descriptor}";
			}
		}
	}
}
