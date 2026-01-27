using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public class ToddlerCarryingGameComponent : GameComponent
	{
		private const int CleanupInterval = 600;
		private const int CarriedJobInterval = 120;

		private int _tickCounter;

		public ToddlerCarryingGameComponent(Game game)
		{
		}

		public override void GameComponentTick()
		{
			_tickCounter++;
			if (_tickCounter >= CleanupInterval)
			{
				_tickCounter = 0;
				ToddlerCarryingTracker.CleanupInvalidEntries();
			}

			ToddlerCarryDesireUtility.Tick();

			if (_tickCounter % CarriedJobInterval == 0)
			{
				CarriedToddlerStateUtility.UpdateCarriedJobs();
			}
		}

		public override void StartedNewGame()
		{
			ToddlerCarryingTracker.ClearAll();
		}

		public override void LoadedGame()
		{
			ToddlerCarryingTracker.ClearAll();
		}

		public override void ExposeData()
		{
		}
	}
}
