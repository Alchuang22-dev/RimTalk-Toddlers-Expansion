using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.BioTech;
using RimTalk_ToddlersExpansion.Language;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public sealed class LanguageLearningBootstrapComponent : GameComponent
	{
		private const int BackfillStartDelayTicks = 60;
		private const int BackfillBatchSize = 64;
		private bool _backfillCompleted;
		private bool _backfillQueued;
		private int _nextBackfillTick;
		private int _pendingIndex;
		private int _addedLanguage;
		private int _addedBabbling;
		private readonly List<Pawn> _pendingBackfillPawns = new List<Pawn>(256);

		public LanguageLearningBootstrapComponent(Game game)
		{
		}

		public override void StartedNewGame()
		{
			base.StartedNewGame();
			// New games rely on Toddlers lifecycle hooks for hediff setup.
			_backfillCompleted = true;
			_backfillQueued = false;
			_pendingBackfillPawns.Clear();
			_pendingIndex = 0;
		}

		public override void LoadedGame()
		{
			base.LoadedGame();
			QueueBackfillIfNeeded();
		}

		public override void GameComponentTick()
		{
			base.GameComponentTick();

			if (!_backfillQueued || _backfillCompleted || Find.TickManager == null)
			{
				return;
			}

			int tick = Find.TickManager.TicksGame;
			if (tick < _nextBackfillTick)
			{
				return;
			}

			ProcessBackfillBatch();
			_nextBackfillTick = tick + 1;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref _backfillCompleted, "languageBackfillCompleted");
		}

		private void QueueBackfillIfNeeded()
		{
			if (_backfillCompleted || _backfillQueued)
			{
				return;
			}

			bool canAddLanguage = ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning != null;
			bool canAddBabbling = ToddlersExpansionHediffDefOf.RimTalk_BabyBabbling != null;
			if (!canAddLanguage && !canAddBabbling)
			{
				_backfillCompleted = true;
				return;
			}

			HashSet<int> seen = new HashSet<int>();
			_pendingBackfillPawns.Clear();
			_pendingIndex = 0;
			_addedLanguage = 0;
			_addedBabbling = 0;

			if (Find.Maps != null)
			{
				for (int i = 0; i < Find.Maps.Count; i++)
				{
					Map map = Find.Maps[i];
					List<Pawn> pawns = map?.mapPawns?.AllPawns;
					if (pawns == null)
					{
						continue;
					}

					for (int j = 0; j < pawns.Count; j++)
					{
						Pawn pawn = pawns[j];
						if (pawn != null && seen.Add(pawn.thingIDNumber))
						{
							_pendingBackfillPawns.Add(pawn);
						}
					}
				}
			}

			List<Pawn> worldPawns = Find.WorldPawns?.AllPawnsAlive;
			if (worldPawns != null)
			{
				for (int i = 0; i < worldPawns.Count; i++)
				{
					Pawn pawn = worldPawns[i];
					if (pawn != null && seen.Add(pawn.thingIDNumber))
					{
						_pendingBackfillPawns.Add(pawn);
					}
				}
			}

			if (_pendingBackfillPawns.Count == 0)
			{
				_backfillCompleted = true;
				return;
			}

			_backfillQueued = true;
			_nextBackfillTick = (Find.TickManager?.TicksGame ?? 0) + BackfillStartDelayTicks;
		}

		private void ProcessBackfillBatch()
		{
			bool canAddLanguage = ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning != null;
			bool canAddBabbling = ToddlersExpansionHediffDefOf.RimTalk_BabyBabbling != null;
			if (!canAddLanguage && !canAddBabbling)
			{
				CompleteBackfill();
				return;
			}

			int limit = Mathf.Min(_pendingBackfillPawns.Count, _pendingIndex + BackfillBatchSize);
			for (; _pendingIndex < limit; _pendingIndex++)
			{
				TryEnsurePawn(_pendingBackfillPawns[_pendingIndex], canAddLanguage, canAddBabbling, ref _addedLanguage, ref _addedBabbling);
			}

			if (_pendingIndex >= _pendingBackfillPawns.Count)
			{
				CompleteBackfill();
			}
		}

		private void CompleteBackfill()
		{
			_backfillCompleted = true;
			_backfillQueued = false;
			_pendingBackfillPawns.Clear();
			_pendingIndex = 0;
			if (_addedLanguage > 0 || _addedBabbling > 0)
			{
				Log.Message($"[RimTalk_ToddlersExpansion] One-time language backfill added language hediff to {_addedLanguage} toddler(s), babbling hediff to {_addedBabbling} baby(ies).");
			}
		}

		private static void TryEnsurePawn(Pawn pawn, bool canAddLanguage, bool canAddBabbling, ref int addedLanguage, ref int addedBabbling)
		{
			if (pawn == null || pawn.Dead || pawn.Destroyed || pawn.health?.hediffSet == null)
			{
				return;
			}

			bool isToddler = ToddlersCompatUtility.IsToddler(pawn);
			bool isBabyOnly = BiotechCompatUtility.IsBaby(pawn) && !isToddler;

			if (isBabyOnly)
			{
				RemoveLanguageHediffIfPresent(pawn, canAddLanguage);
				if (canAddBabbling
					&& pawn.health.hediffSet.GetFirstHediffOfDef(ToddlersExpansionHediffDefOf.RimTalk_BabyBabbling) == null)
				{
					Hediff babbling = HediffMaker.MakeHediff(ToddlersExpansionHediffDefOf.RimTalk_BabyBabbling, pawn);
					pawn.health.AddHediff(babbling);
					addedBabbling += 1;
				}
				return;
			}

			RemoveBabblingHediffIfPresent(pawn, canAddBabbling);
			if (canAddLanguage && isToddler)
			{
				bool hadLanguage = pawn.health.hediffSet.GetFirstHediffOfDef(ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning) != null;
				if (LanguageLevelUtility.TrySyncLearningProgress(pawn, createLanguageIfMissing: true))
				{
					bool hasLanguage = pawn.health.hediffSet.GetFirstHediffOfDef(ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning) != null;
					if (!hadLanguage && hasLanguage)
					{
						addedLanguage += 1;
					}
				}
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
