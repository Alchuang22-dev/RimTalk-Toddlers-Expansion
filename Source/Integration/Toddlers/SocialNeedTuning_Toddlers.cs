using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Language;
using RimWorld;
using UnityEngine;
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
		private const int HighPlayDecisionWindowTicks = 180;
		private const float HighPlayContinueChanceAtThreshold = 0.82f;
		private const float HighPlayContinueChanceAtFull = 0.97f;
		private const int HighPlayDebugLogIntervalTicks = 1200;

		private static readonly Dictionary<int, int> LastTickByPawn = new Dictionary<int, int>(64);
		private static readonly Dictionary<int, int> LastHighPlayLogByPawn = new Dictionary<int, int>(64);

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
			float level = GetPlayOrJoyLevel(pawn);
			if (level < PlayNeedFullThreshold)
			{
				return false;
			}

			// Even when full, keep optional activities some of the time to avoid excessive idle wandering.
			return !ShouldDoOptionalActivity(pawn, PlayNeedFullThreshold);
		}

		public static bool ShouldDoOptionalActivity(Pawn pawn, float threshold)
		{
			if (pawn == null)
			{
				return false;
			}

			float level = GetPlayOrJoyLevel(pawn);
			if (level < 0f || level < threshold)
			{
				return true;
			}

			int now = Find.TickManager?.TicksGame ?? 0;
			int window = now / HighPlayDecisionWindowTicks;
			int seed = Gen.HashCombineInt(pawn.thingIDNumber, window);
			float clampedLevel = Mathf.Clamp01(level);
			float t = Mathf.InverseLerp(threshold, 1f, clampedLevel);
			float chance = Mathf.Lerp(HighPlayContinueChanceAtThreshold, HighPlayContinueChanceAtFull, t);
			bool shouldContinue = Rand.ChanceSeeded(chance, seed);
			LogHighPlayDecision(pawn, level, threshold, chance, shouldContinue, window);
			return shouldContinue;
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

			float adjustedAmount = Patch_ToddlerBoredom.AdjustPlayGainForToddler(pawn, amount);
			Need_Play play = pawn.needs?.play;
			if (play != null)
			{
				play.Play(adjustedAmount);
				return;
			}

			Need_Joy joy = pawn.needs?.joy;
			if (joy != null && joyKind != null)
			{
				joy.GainJoy(adjustedAmount, joyKind);
			}
		}

		private static void ApplyLanguageGain(Pawn pawn, float amount)
		{
			if (pawn == null || amount <= 0f)
			{
				return;
			}

			// In Toddlers mode, creation/sync is driven by Toddlers learning lifecycle patches.
			// Never create or branch by current Toddlers learning hediff state from social gain logic.
			if (ToddlersCompatUtility.IsToddlersActive)
			{
				return;
			}

			// Fallback path when Toddlers is not active.
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

		private static float GetPlayOrJoyLevel(Pawn pawn)
		{
			Need_Play play = pawn?.needs?.play;
			if (play != null)
			{
				return play.CurLevelPercentage;
			}

			Need_Joy joy = pawn?.needs?.joy;
			return joy?.CurLevelPercentage ?? -1f;
		}

		private static void LogHighPlayDecision(Pawn pawn, float level, float threshold, float chance, bool shouldContinue, int window)
		{
			if (shouldContinue || !Prefs.DevMode || pawn == null)
			{
				return;
			}

			int now = Find.TickManager?.TicksGame ?? 0;
			int key = pawn.thingIDNumber;
			if (LastHighPlayLogByPawn.TryGetValue(key, out int lastLogTick) && now - lastLogTick < HighPlayDebugLogIntervalTicks)
			{
				return;
			}

			LastHighPlayLogByPawn[key] = now;
			Log.Message($"[RimTalk_ToddlersExpansion] High-play gate blocked optional activity: pawn={pawn.LabelShort} level={level:F3} threshold={threshold:F2} chance={chance:F2} window={window}");
		}
	}
}
