using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Language;
using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public static class SocialNeedTuning_Toddlers
	{
		private const float SelfPlayGainPerTick = 0.00014f;
		private const float MutualPlayGainPerTick = 0.0002f;
		private const float WatchPlayJoyGainPerTick = 0.00012f;
		private const float PlayNeedFullThreshold = 0.95f;
		private const float PlayedWithMeThreshold = 0.6f;
		private const float LanguageGainSelfPerTick = 0.00005f;
		private const float LanguageGainMutualPerTick = 0.00008f;
		private const float LanguageGainWatchPerTick = 0.00003f;

		private static readonly Dictionary<int, int> LastTickByPawn = new Dictionary<int, int>(64);

		public static void ApplySelfPlayTickEffects(Pawn toddler, int delta)
		{
			if (!ShouldApplyTick(toddler))
			{
				return;
			}

			ApplyPlayGain(toddler, SelfPlayGainPerTick * delta, JoyKindDefOf.Meditative);
			ApplyLanguageGain(toddler, LanguageGainSelfPerTick * delta);
		}

		public static void ApplyMutualPlayTickEffects(Pawn toddler, Pawn partner, int delta)
		{
			if (!ShouldApplyTick(toddler))
			{
				return;
			}

			ApplyPlayGain(toddler, MutualPlayGainPerTick * delta, JoyKindDefOf.Social);
			ApplyLanguageGain(toddler, LanguageGainMutualPerTick * delta);

			if (partner != null && partner.CurJob?.def != ToddlersExpansionJobDefOf.RimTalk_ToddlerMutualPlayJob)
			{
				ApplyPlayGain(partner, MutualPlayGainPerTick * delta * 0.5f, JoyKindDefOf.Social);
				ApplyLanguageGain(partner, LanguageGainMutualPerTick * delta * 0.5f);
			}
		}

		public static void ApplyWatchPlayTickEffects(Pawn watcher, Pawn toddler, int delta)
		{
			if (!ShouldApplyTick(watcher))
			{
				return;
			}

			Need_Joy joy = watcher?.needs?.joy;
			if (joy != null)
			{
				joy.GainJoy(WatchPlayJoyGainPerTick * delta, JoyKindDefOf.Social);
			}

			ApplyLanguageGain(toddler, LanguageGainWatchPerTick * delta);
		}

		public static bool IsPlayNeedSatisfied(Pawn pawn)
		{
			Need_Play play = pawn?.needs?.play;
			if (play != null)
			{
				return play.CurLevelPercentage >= PlayNeedFullThreshold;
			}

			Need_Joy joy = pawn?.needs?.joy;
			return joy != null && joy.CurLevelPercentage >= PlayNeedFullThreshold;
		}

		public static bool ShouldGainPlayedWithMeThought(float initialPlay, float currentPlay)
		{
			return initialPlay >= 0f && currentPlay - initialPlay >= PlayedWithMeThreshold;
		}

		public static void TryGainPlayedWithMeThought(Pawn toddler, Pawn other)
		{
			if (toddler?.needs?.mood?.thoughts?.memories == null || !ModsConfig.BiotechActive)
			{
				return;
			}

			if (ThoughtDefOf.PlayedWithMe != null)
			{
				toddler.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.PlayedWithMe, other);
			}
		}

		private static void ApplyPlayGain(Pawn pawn, float amount, JoyKindDef joyKind)
		{
			if (pawn == null || amount <= 0f)
			{
				return;
			}

			Need_Play play = pawn.needs?.play;
			if (play != null)
			{
				play.Play(amount);
				return;
			}

			Need_Joy joy = pawn.needs?.joy;
			if (joy != null && joyKind != null)
			{
				joy.GainJoy(amount, joyKind);
			}
		}

		private static void ApplyLanguageGain(Pawn pawn, float amount)
		{
			if (pawn == null || amount <= 0f)
			{
				return;
			}

			if (LanguageLevelUtility.TryGetOrCreateProgressComp(pawn, out HediffComp_LanguageLearningProgress comp))
			{
				comp.AddProgress(amount);
			}
		}

		private static bool ShouldApplyTick(Pawn pawn)
		{
			if (pawn == null)
			{
				return false;
			}

			int tick = Find.TickManager.TicksGame;
			int key = pawn.thingIDNumber;
			if (LastTickByPawn.TryGetValue(key, out int lastTick) && lastTick == tick)
			{
				return false;
			}

			LastTickByPawn[key] = tick;
			return true;
		}
	}
}
