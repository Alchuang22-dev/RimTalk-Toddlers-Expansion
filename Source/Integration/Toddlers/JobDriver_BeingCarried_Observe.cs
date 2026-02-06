using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public class JobDriver_BeingCarried_Observe : JobDriver_BeingCarriedBase
	{
		private const float JoyGainPerTick = 0.00008f;
		private const float PlayGainPerTick = 0.00008f;
		private const float ComfortGainPerTick = 0.0002f;
		private const float LonelinessReductionPerTick = 0.0002f;

		private static bool _defsInitialized;
		private static HediffDef _lonelyDef;

		protected override string ReportKey => "RimTalk_BeingCarriedObserveBy";

		protected override void TickAlways()
		{
			ApplyComfortUsed(1f);
			if (pawn != null && pawn.IsHashIntervalTick(15))
			{
				ApplyJoyAndPlayGain(pawn, 15);
			}
		}

		protected override void TickEffects(int ticks)
		{
			if (pawn == null)
			{
				return;
			}

			ApplyComfortGain(pawn, ticks);
			TryReduceLoneliness(pawn, ticks);
		}

		private static void ApplyJoyAndPlayGain(Pawn pawn, int ticks)
		{
			if (pawn == null)
			{
				return;
			}

			float joyAmount = JoyGainPerTick * ticks;
			float playAmount = PlayGainPerTick * ticks;

			Need_Play play = pawn.needs?.play;
			if (play != null && playAmount > 0f)
			{
				play.Play(playAmount);
			}

			Need_Joy joy = pawn.needs?.joy;
			if (joy != null && joyAmount > 0f)
			{
				joy.GainJoy(joyAmount, JoyKindDefOf.Meditative);
			}
		}

		private static void ApplyComfortGain(Pawn pawn, int ticks)
		{
			Need_Comfort comfort = pawn?.needs?.comfort;
			if (comfort == null)
			{
				return;
			}

			float nextLevel = comfort.CurLevel + ComfortGainPerTick * ticks;
			comfort.CurLevel = Mathf.Min(1f, nextLevel);
		}

		private static void TryReduceLoneliness(Pawn pawn, int ticks)
		{
			EnsureDefsInitialized();
			if (_lonelyDef == null || pawn?.health?.hediffSet == null)
			{
				return;
			}

			Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(_lonelyDef);
			if (hediff == null || hediff.Severity <= 0f)
			{
				return;
			}

			float amount = LonelinessReductionPerTick * ticks;
			hediff.Severity = Mathf.Max(0f, hediff.Severity - amount);
		}

		private static void EnsureDefsInitialized()
		{
			if (_defsInitialized)
			{
				return;
			}

			_defsInitialized = true;
			_lonelyDef = DefDatabase<HediffDef>.GetNamedSilentFail("ToddlerLonely");
		}
	}
}
