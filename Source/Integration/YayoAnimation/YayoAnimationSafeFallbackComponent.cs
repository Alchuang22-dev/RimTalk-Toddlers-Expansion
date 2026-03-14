using Verse;

namespace RimTalk_ToddlersExpansion.Integration.YayoAnimation
{
	public sealed class YayoAnimationSafeFallbackComponent : GameComponent
	{
		private const int SyncIntervalTicks = 15;
		private int _nextSyncTick;

		public YayoAnimationSafeFallbackComponent(Game game)
		{
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
			SyncSpawnedPawns();
		}

		private static void SyncSpawnedPawns()
		{
			if (Find.Maps == null)
			{
				return;
			}

			for (int i = 0; i < Find.Maps.Count; i++)
			{
				Map map = Find.Maps[i];
				var pawns = map?.mapPawns?.AllPawnsSpawned;
				if (pawns == null)
				{
					continue;
				}

				for (int j = 0; j < pawns.Count; j++)
				{
					Pawn pawn = pawns[j];
					if (pawn == null || pawn.Dead || pawn.Destroyed)
					{
						continue;
					}

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
