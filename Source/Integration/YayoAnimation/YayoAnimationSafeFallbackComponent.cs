using System.Collections.Generic;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.YayoAnimation
{
	public sealed class YayoAnimationSafeFallbackComponent : GameComponent
	{
		private const int SyncIntervalTicks = 15;
		private const int CurrentMapScanIntervalTicks = 90;
		private const int FullRescanIntervalTicks = 600;
		private int _nextSyncTick;
		private int _nextCurrentMapScanTick;
		private int _nextFullRescanTick;
		private readonly List<Pawn> _trackedPawnsBuffer = new List<Pawn>(64);

		public YayoAnimationSafeFallbackComponent(Game game)
		{
		}

		public override void StartedNewGame()
		{
			base.StartedNewGame();
			_nextSyncTick = 0;
			_nextCurrentMapScanTick = 0;
			_nextFullRescanTick = 0;
			YayoAnimationCompatUtility.ClearTrackedSafeFallbackPawns();
		}

		public override void LoadedGame()
		{
			base.LoadedGame();
			_nextSyncTick = 0;
			_nextCurrentMapScanTick = 0;
			_nextFullRescanTick = 0;
			YayoAnimationCompatUtility.ClearTrackedSafeFallbackPawns();
		}

		public override void GameComponentTick()
		{
			base.GameComponentTick();

			if (!YayoAnimationCompatUtility.IsYayoAnimationLoaded || Find.TickManager == null)
			{
				return;
			}

			int now = Find.TickManager.TicksGame;
			if (now < _nextSyncTick)
			{
				return;
			}

			_nextSyncTick = now + SyncIntervalTicks;
			SyncTrackedPawnsOnCurrentMap();

			if (now >= _nextCurrentMapScanTick)
			{
				_nextCurrentMapScanTick = now + CurrentMapScanIntervalTicks;
				ScanCurrentMapCandidates();
			}

			if (now >= _nextFullRescanTick)
			{
				_nextFullRescanTick = now + FullRescanIntervalTicks;
				RescanAllMapsForCandidates();
			}
		}

		private void SyncTrackedPawnsOnCurrentMap()
		{
			Map currentMap = Find.CurrentMap;
			if (currentMap == null)
			{
				return;
			}

			YayoAnimationCompatUtility.CopyTrackedSafeFallbackPawnsTo(_trackedPawnsBuffer);
			for (int i = 0; i < _trackedPawnsBuffer.Count; i++)
			{
				Pawn pawn = _trackedPawnsBuffer[i];
				if (pawn == null || pawn.Map != currentMap)
				{
					continue;
				}

				if (!YayoAnimationCompatUtility.ShouldKeepSafeFallbackPawnTracked(pawn))
				{
					YayoAnimationCompatUtility.UntrackSafeFallbackPawn(pawn);
					continue;
				}

				YayoAnimationCompatUtility.SyncSafeNativePlayAnimation(pawn);
			}

			_trackedPawnsBuffer.Clear();
		}

		private static void ScanCurrentMapCandidates()
		{
			Map currentMap = Find.CurrentMap;
			if (currentMap == null)
			{
				return;
			}

			ScanMapCandidates(currentMap, syncImmediately: true);
		}

		private static void RescanAllMapsForCandidates()
		{
			if (Find.Maps == null)
			{
				return;
			}

			Map currentMap = Find.CurrentMap;
			for (int i = 0; i < Find.Maps.Count; i++)
			{
				Map map = Find.Maps[i];
				ScanMapCandidates(map, syncImmediately: map == currentMap);
			}
		}

		private static void ScanMapCandidates(Map map, bool syncImmediately)
		{
			var pawns = map?.mapPawns?.AllPawnsSpawned;
			if (pawns == null)
			{
				return;
			}

			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				if (!YayoAnimationCompatUtility.IsRelevantSmallPawnCandidate(pawn))
				{
					continue;
				}

				if (!YayoAnimationCompatUtility.ShouldKeepSafeFallbackPawnTracked(pawn))
				{
					continue;
				}

				YayoAnimationCompatUtility.TrackSafeFallbackPawn(pawn);
				if (syncImmediately)
				{
					YayoAnimationCompatUtility.SyncSafeNativePlayAnimation(pawn);
				}
			}
		}
	}

	public static class YayoAnimationSafeFallbackUtility
	{
		public static void RegisterGameComponent()
		{
			if (Current.Game == null)
			{
				return;
			}

			if (Current.Game.GetComponent<YayoAnimationSafeFallbackComponent>() == null)
			{
				Current.Game.components.Add(new YayoAnimationSafeFallbackComponent(Current.Game));
			}
		}
	}
}
