using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.BioTech;
using RimTalk_ToddlersExpansion.Language;
using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public sealed class LanguageLearningBootstrapComponent : GameComponent
	{
		private const int FastRetryIntervalTicks = 600;
		private const int SlowRetryIntervalTicks = 2500;
		private bool _initialPassDone;
		private int _nextRetryTick;

		public LanguageLearningBootstrapComponent(Game game)
		{
		}

		public override void StartedNewGame()
		{
			base.StartedNewGame();
			TryInitialize();
		}

		public override void LoadedGame()
		{
			base.LoadedGame();
			TryInitialize();
		}

		public override void GameComponentTick()
		{
			base.GameComponentTick();

			if (Find.TickManager == null)
			{
				return;
			}

			int tick = Find.TickManager.TicksGame;
			if (tick < _nextRetryTick)
			{
				return;
			}

			TryInitialize();
			_nextRetryTick = tick + (_initialPassDone ? SlowRetryIntervalTicks : FastRetryIntervalTicks);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref _initialPassDone, "initialPassDone");
			Scribe_Values.Look(ref _nextRetryTick, "nextRetryTick");
		}

		private void TryInitialize()
		{
			bool canAddLanguage = ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning != null;
			bool canAddBabbling = ToddlersExpansionHediffDefOf.RimTalk_BabyBabbling != null;
			if (!canAddLanguage && !canAddBabbling)
			{
				return;
			}

			if (Find.Maps == null || Find.Maps.Count == 0)
			{
				return;
			}

			int addedLanguage = 0;
			int addedBabbling = 0;
			HashSet<int> seen = new HashSet<int>();

			for (int i = 0; i < Find.Maps.Count; i++)
			{
				Map map = Find.Maps[i];
				if (map == null)
				{
					continue;
				}

				List<Pawn> pawns = (List<Pawn>)(map.mapPawns?.AllPawnsSpawned);
				if (pawns == null)
				{
					continue;
				}

				for (int j = 0; j < pawns.Count; j++)
				{
					TryEnsurePawn(pawns[j], seen, canAddLanguage, canAddBabbling, ref addedLanguage, ref addedBabbling);
				}
			}

			List<Pawn> worldPawns = Find.WorldPawns?.AllPawnsAlive;
			if (worldPawns != null)
			{
				for (int i = 0; i < worldPawns.Count; i++)
				{
					TryEnsurePawn(worldPawns[i], seen, canAddLanguage, canAddBabbling, ref addedLanguage, ref addedBabbling);
				}
			}

			if (addedLanguage > 0 || addedBabbling > 0)
			{
				Log.Message($"[RimTalk_ToddlersExpansion] Added language learning hediff to {addedLanguage} toddler(s), babbling hediff to {addedBabbling} baby(ies).");
			}

			_initialPassDone = true;
		}

		private static void TryEnsurePawn(Pawn pawn, HashSet<int> seen, bool canAddLanguage, bool canAddBabbling, ref int addedLanguage, ref int addedBabbling)
		{
			if (pawn == null || seen == null)
			{
				return;
			}

			if (!seen.Add(pawn.thingIDNumber))
			{
				return;
			}

			if (pawn.health?.hediffSet == null)
			{
				return;
			}

			bool isToddler = ToddlersCompatUtility.IsToddler(pawn);
			bool isBabyOnly = BiotechCompatUtility.IsBaby(pawn) && !isToddler;

			if (isBabyOnly)
			{
				RemoveLanguageHediffIfPresent(pawn, canAddLanguage);

				if (!canAddBabbling)
				{
					return;
				}

				if (pawn.health.hediffSet.GetFirstHediffOfDef(ToddlersExpansionHediffDefOf.RimTalk_BabyBabbling) != null)
				{
					return;
				}

				Hediff babbling = HediffMaker.MakeHediff(ToddlersExpansionHediffDefOf.RimTalk_BabyBabbling, pawn);
				pawn.health.AddHediff(babbling);
				addedBabbling += 1;
				return;
			}

			RemoveBabblingHediffIfPresent(pawn, canAddBabbling);

			if (!canAddLanguage || !isToddler)
			{
				return;
			}

			if (pawn.health.hediffSet.GetFirstHediffOfDef(ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning) != null)
			{
				return;
			}

			if (LanguageLevelUtility.TryGetOrCreateProgressComp(pawn, out _))
			{
				addedLanguage += 1;
			}
		}

		private static void RemoveLanguageHediffIfPresent(Pawn pawn, bool canAddLanguage)
		{
			if (!canAddLanguage || pawn == null)
			{
				return;
			}

			Hediff existing = pawn.health?.hediffSet?.GetFirstHediffOfDef(ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning);
			if (existing != null)
			{
				pawn.health.RemoveHediff(existing);
			}
		}

		private static void RemoveBabblingHediffIfPresent(Pawn pawn, bool canAddBabbling)
		{
			if (!canAddBabbling || pawn == null)
			{
				return;
			}

			Hediff existing = pawn.health?.hediffSet?.GetFirstHediffOfDef(ToddlersExpansionHediffDefOf.RimTalk_BabyBabbling);
			if (existing != null)
			{
				pawn.health.RemoveHediff(existing);
			}
		}
	}

	public static class LanguageLearningUtility
	{
		public static void RegisterGameComponent()
		{
			if (Current.Game == null)
			{
				return;
			}

			if (Current.Game.GetComponent<LanguageLearningBootstrapComponent>() == null)
			{
				Current.Game.components.Add(new LanguageLearningBootstrapComponent(Current.Game));
			}
		}
	}
}
