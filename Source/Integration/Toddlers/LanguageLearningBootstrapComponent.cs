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
		private bool _backfillCompleted;
		private int _addedLanguage;
		private int _addedBabbling;

		public LanguageLearningBootstrapComponent(Game game)
		{
		}

		public override void StartedNewGame()
		{
			base.StartedNewGame();
			// New games rely on lifecycle hooks / spawn-time self-healing.
			_backfillCompleted = true;
		}

		public override void LoadedGame()
		{
			base.LoadedGame();
			BackfillLoadedMapsIfNeeded();
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref _backfillCompleted, "languageBackfillCompleted");
		}

		private void BackfillLoadedMapsIfNeeded()
		{
			if (_backfillCompleted)
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
						EnsurePawnLanguageState(pawns[j], canAddLanguage, canAddBabbling, ref _addedLanguage, ref _addedBabbling);
					}
				}
			}

			CompleteBackfill();
		}

		private void CompleteBackfill()
		{
			_backfillCompleted = true;
			if (_addedLanguage > 0 || _addedBabbling > 0)
			{
				Log.Message($"[RimTalk_ToddlersExpansion] One-time map language backfill added language hediff to {_addedLanguage} toddler(s), babbling hediff to {_addedBabbling} baby(ies).");
			}
		}

		public static void NotifyPawnSpawned(Pawn pawn)
		{
			bool canAddLanguage = ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning != null;
			bool canAddBabbling = ToddlersExpansionHediffDefOf.RimTalk_BabyBabbling != null;
			int addedLanguage = 0;
			int addedBabbling = 0;
			EnsurePawnLanguageState(pawn, canAddLanguage, canAddBabbling, ref addedLanguage, ref addedBabbling);
		}

		private static void EnsurePawnLanguageState(Pawn pawn, bool canAddLanguage, bool canAddBabbling, ref int addedLanguage, ref int addedBabbling)
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
				Hediff existingBabbling = canAddBabbling
					? pawn.health.hediffSet.GetFirstHediffOfDef(ToddlersExpansionHediffDefOf.RimTalk_BabyBabbling)
					: null;
				if (canAddBabbling && existingBabbling == null)
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
				Hediff existingLanguage = pawn.health.hediffSet.GetFirstHediffOfDef(ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning);
				bool hadLanguage = existingLanguage != null;
				if (!hadLanguage
					&& LanguageLevelUtility.TryGetToddlersLanguageInitialProgress(pawn, out float initialProgress)
					&& initialProgress < 1f
					&& LanguageLevelUtility.TryGetOrCreateLanguageHediff(pawn, out Hediff language))
				{
					language.Severity = initialProgress;
					if (language != null)
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
