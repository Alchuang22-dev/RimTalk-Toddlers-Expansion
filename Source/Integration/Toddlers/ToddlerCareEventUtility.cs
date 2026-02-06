using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public static class ToddlerCareEventUtility
	{
		private const int CheckIntervalTicks = 250;
		private const int EventCooldownTicks = 30000;
		private const int FightDurationTicks = 300;

		private const float NeglectStartSeverity = 0.0f;
		private const float NeglectFullSeverity = 0.8f;

		private const float SelfPlayBaseChancePerTick = 1.2e-6f;
		private const float MutualPlayBaseChancePerTick = 2.0e-6f;

		private const float SelfPlayCrawlingFactor = 0.6f;
		private const float SelfPlayWalkingFactor = 1.0f;
		private const float SelfPlayWobblyFactor = 1.3f;

		private const float MutualPlayCrawlingFactor = 0.6f;
		private const float MutualPlayWobblyFactor = 1.0f;
		private const float MutualPlayWalkingFactor = 1.3f;

		private const int SelfPlayDamageMin = 2;
		private const int SelfPlayDamageMax = 5;

		private static readonly System.Collections.Generic.Dictionary<int, int> LastEventTickByPawn =
			new System.Collections.Generic.Dictionary<int, int>(64);

		private static bool _defsInitialized;
		private static HediffDef _lonelyDef;
		private static HediffDef _learningToWalkDef;

		public static bool TryTriggerSelfPlayMishap(Pawn toddler, int delta)
		{
			if (!ShouldCheck(toddler, delta))
			{
				return false;
			}

			float neglect = GetNeglectFactor(toddler);
			if (neglect <= 0f)
			{
				return false;
			}

			float stageFactor = GetSelfPlayStageFactor(toddler);
			float chance = SelfPlayBaseChancePerTick * stageFactor * neglect * delta;
			if (!Rand.Chance(chance))
			{
				return false;
			}

			TriggerTumble(toddler);
			return true;
		}

		public static bool TryTriggerMutualPlayMishap(Pawn toddler, Pawn partner, int delta)
		{
			if (!ShouldCheck(toddler, delta))
			{
				return false;
			}

			if (partner == null || partner.Downed || !partner.Spawned)
			{
				return false;
			}

			float neglect = GetNeglectFactor(toddler);
			if (neglect <= 0f)
			{
				return false;
			}

			float stageFactor = GetMutualPlayStageFactor(toddler);
			float chance = MutualPlayBaseChancePerTick * stageFactor * neglect * delta;
			if (!Rand.Chance(chance))
			{
				return false;
			}

			TriggerFight(toddler, partner);
			return true;
		}

		private static bool ShouldCheck(Pawn toddler, int delta)
		{
			if (toddler == null || toddler.Dead || toddler.Downed || !toddler.Spawned)
			{
				return false;
			}

			if (!toddler.IsColonistPlayerControlled)
			{
				return false;
			}

			if (!ToddlersCompatUtility.IsToddler(toddler))
			{
				return false;
			}

			if (!toddler.IsHashIntervalTick(CheckIntervalTicks, delta))
			{
				return false;
			}

			int now = Find.TickManager.TicksGame;
			int key = toddler.thingIDNumber;
			if (LastEventTickByPawn.TryGetValue(key, out int lastTick) && now - lastTick < EventCooldownTicks)
			{
				return false;
			}

			return true;
		}

		private static float GetNeglectFactor(Pawn toddler)
		{
			EnsureDefsInitialized();
			if (_lonelyDef == null || toddler?.health?.hediffSet == null)
			{
				return 0f;
			}

			Hediff hediff = toddler.health.hediffSet.GetFirstHediffOfDef(_lonelyDef);
			if (hediff == null)
			{
				hediff = toddler.health.AddHediff(_lonelyDef);
			}

			float severity = hediff?.Severity ?? 0f;
			if (severity <= NeglectStartSeverity)
			{
				return 0f;
			}

			float factor = Mathf.InverseLerp(NeglectStartSeverity, NeglectFullSeverity, severity);
			return Mathf.Clamp01(factor);
		}

		private static float GetSelfPlayStageFactor(Pawn toddler)
		{
			return GetWalkingStage(toddler) switch
			{
				WalkingStage.Crawling => SelfPlayCrawlingFactor,
				WalkingStage.Wobbly => SelfPlayWobblyFactor,
				_ => SelfPlayWalkingFactor,
			};
		}

		private static float GetMutualPlayStageFactor(Pawn toddler)
		{
			return GetWalkingStage(toddler) switch
			{
				WalkingStage.Crawling => MutualPlayCrawlingFactor,
				WalkingStage.Wobbly => MutualPlayWobblyFactor,
				_ => MutualPlayWalkingFactor,
			};
		}

		private static WalkingStage GetWalkingStage(Pawn toddler)
		{
			EnsureDefsInitialized();
			if (_learningToWalkDef == null || toddler?.health?.hediffSet == null)
			{
				return WalkingStage.Walking;
			}

			Hediff hediff = toddler.health.hediffSet.GetFirstHediffOfDef(_learningToWalkDef);
			if (hediff == null)
			{
				return WalkingStage.Walking;
			}

			return hediff.Severity < 0.5f ? WalkingStage.Crawling : WalkingStage.Wobbly;
		}

		/// <summary>
		/// 触发幼儿摔倒事件（供调试使用）- 对选中的幼儿
		/// </summary>
		[LudeonTK.DebugAction("RimTalk Toddlers", "Trigger Tumble (Selected)", allowedGameStates = LudeonTK.AllowedGameStates.PlayingOnMap)]
		public static void DebugTriggerTumbleSelected()
		{
			Pawn toddler = Find.Selector.SingleSelectedThing as Pawn;
			if (toddler == null)
			{
				Messages.Message("Please select a toddler first.", MessageTypeDefOf.RejectInput);
				return;
			}
			
			if (!ToddlersCompatUtility.IsToddler(toddler))
			{
				Messages.Message($"{toddler.LabelShort} is not a toddler.", MessageTypeDefOf.RejectInput);
				return;
			}
			
			TriggerTumble(toddler);
			Messages.Message($"Triggered tumble for {toddler.LabelShort}", MessageTypeDefOf.NeutralEvent);
		}

		/// <summary>
		/// 触发幼儿打架事件（供调试使用）- 对选中的幼儿
		/// </summary>
		[LudeonTK.DebugAction("RimTalk Toddlers", "Trigger Scuffle (Selected)", allowedGameStates = LudeonTK.AllowedGameStates.PlayingOnMap)]
		public static void DebugTriggerScuffleSelected()
		{
			Pawn toddler = Find.Selector.SingleSelectedThing as Pawn;
			if (toddler == null)
			{
				Messages.Message("Please select a toddler first.", MessageTypeDefOf.RejectInput);
				return;
			}
			
			if (!ToddlersCompatUtility.IsToddler(toddler))
			{
				Messages.Message($"{toddler.LabelShort} is not a toddler (Stage: {toddler.DevelopmentalStage}).", MessageTypeDefOf.RejectInput);
				return;
			}
			
			// 找一个附近的幼儿作为打架对象
			var allPawns = toddler.Map?.mapPawns?.SpawnedPawnsInFaction(toddler.Faction)?.ToList();
			var partner = allPawns?.FirstOrDefault(p => p != toddler && ToddlersCompatUtility.IsToddler(p) && !p.Dead && !p.Downed);
			
			if (partner == null)
			{
				Messages.Message("No other toddler found to scuffle with.", MessageTypeDefOf.RejectInput);
				return;
			}
			
			DebugTriggerFight(toddler, partner);
		}
		
		private static void DebugTriggerFight(Pawn toddler, Pawn partner)
		{
			int now = Find.TickManager.TicksGame;
			LastEventTickByPawn[toddler.thingIDNumber] = now;

			toddler.jobs?.EndCurrentJob(JobCondition.InterruptForced);
			partner.jobs?.EndCurrentJob(JobCondition.InterruptForced);

			Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, partner);
			job.expiryInterval = FightDurationTicks;
			job.checkOverrideOnExpire = true;
			
			toddler.jobs?.TryTakeOrderedJob(job);

			string label = "RimTalk_ToddlersExpansion_ToddlerScuffleLabel".Translate();
			string text = "RimTalk_ToddlersExpansion_ToddlerScuffleText".Translate(toddler.Named("TODDLER"), partner.Named("PARTNER"));
			Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.NegativeEvent, toddler);
			
			Messages.Message($"Triggered scuffle between {toddler.LabelShort} and {partner.LabelShort}", MessageTypeDefOf.NeutralEvent);
		}

		private static void TriggerTumble(Pawn toddler)
		{
			int now = Find.TickManager.TicksGame;
			LastEventTickByPawn[toddler.thingIDNumber] = now;

			float amount = Rand.RangeInclusive(SelfPlayDamageMin, SelfPlayDamageMax);
			DamageInfo damage = new DamageInfo(DamageDefOf.Blunt, amount, instigator: null);
			toddler.TakeDamage(damage);

			string label = "RimTalk_ToddlersExpansion_ToddlerTumbleLabel".Translate();
			string text = "RimTalk_ToddlersExpansion_ToddlerTumbleText".Translate(toddler.Named("TODDLER"));
			Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.NegativeEvent, toddler);
		}

		private static void TriggerFight(Pawn toddler, Pawn partner)
		{
			if (toddler.WorkTagIsDisabled(WorkTags.Violent))
			{
				return;
			}

			int now = Find.TickManager.TicksGame;
			LastEventTickByPawn[toddler.thingIDNumber] = now;

			toddler.jobs?.EndCurrentJob(JobCondition.InterruptForced);
			partner.jobs?.EndCurrentJob(JobCondition.InterruptForced);

			Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, partner);
			job.expiryInterval = FightDurationTicks;
			job.checkOverrideOnExpire = true;
			toddler.jobs?.StartJob(job, JobCondition.InterruptForced);

			string label = "RimTalk_ToddlersExpansion_ToddlerScuffleLabel".Translate();
			string text = "RimTalk_ToddlersExpansion_ToddlerScuffleText".Translate(toddler.Named("TODDLER"), partner.Named("PARTNER"));
			Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.NegativeEvent, toddler);
		}

		private static void EnsureDefsInitialized()
		{
			if (_defsInitialized)
			{
				return;
			}

			_defsInitialized = true;
			_lonelyDef = DefDatabase<HediffDef>.GetNamedSilentFail("ToddlerLonely");
			_learningToWalkDef = DefDatabase<HediffDef>.GetNamedSilentFail("LearningToWalk");
		}

		private enum WalkingStage
		{
			Crawling,
			Wobbly,
			Walking
		}
	}
}
