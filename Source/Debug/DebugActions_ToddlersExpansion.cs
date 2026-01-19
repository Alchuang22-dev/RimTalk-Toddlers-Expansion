using System.Collections.Generic;
using LudeonTK;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimTalk_ToddlersExpansion.Language;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Debug
{
	public static class DebugActions_ToddlersExpansion
	{
		[DebugAction("RimTalk Toddlers", "Add language hediff", allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void AddLanguageHediff()
		{
			ShowPawnMenu(pawn =>
			{
				if (!ToddlersCompatUtility.IsToddler(pawn))
				{
					return;
				}

				LanguageLevelUtility.TryGetOrCreateProgressComp(pawn, out _);
			});
		}

		[DebugAction("RimTalk Toddlers", "Add language progress +10%", allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void AddLanguageProgress()
		{
			ShowPawnMenu(pawn =>
			{
				if (!ToddlersCompatUtility.IsToddler(pawn))
				{
					return;
				}

				if (LanguageLevelUtility.TryGetOrCreateProgressComp(pawn, out HediffComp_LanguageLearningProgress comp))
				{
					comp.AddProgress(0.1f);
				}
			});
		}

		[DebugAction("RimTalk Toddlers", "Start toddler self-play", allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void StartSelfPlay()
		{
			WorkGiver_ToddlerSelfPlay giver = new WorkGiver_ToddlerSelfPlay();
			ShowPawnMenu(pawn =>
			{
				if (!ToddlersCompatUtility.IsToddler(pawn))
				{
					return;
				}

				Job job = giver.TryGiveJob(pawn);
				if (job != null)
				{
					pawn.jobs.TryTakeOrderedJob(job);
				}
			});
		}

		[DebugAction("RimTalk Toddlers", "Start toddler mutual play", allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void StartMutualPlay()
		{
			WorkGiver_ToddlerMutualPlay giver = new WorkGiver_ToddlerMutualPlay();
			ShowPawnMenu(pawn =>
			{
				if (!ToddlersCompatUtility.IsToddler(pawn))
				{
					return;
				}

				Job job = giver.TryGiveJob(pawn);
				if (job != null)
				{
					pawn.jobs.TryTakeOrderedJob(job);
				}
			});
		}

		private static void ShowPawnMenu(System.Action<Pawn> action)
		{
			Map map = Find.CurrentMap;
			if (map == null)
			{
				return;
			}

			List<DebugMenuOption> options = new List<DebugMenuOption>();
			foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
			{
				Pawn local = pawn;
				options.Add(new DebugMenuOption(local.LabelShort, DebugMenuOptionMode.Action, () => action(local)));
			}

			Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
		}
	}
}
