using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public sealed class ToddlerAgeSettingsGameComponent : GameComponent
	{
		public ToddlerAgeSettingsGameComponent(Game game)
		{
		}

		public override void StartedNewGame()
		{
			base.StartedNewGame();
			ToddlerAgeSettingsUtility.RefreshExistingPawnLifeStagesForConfiguredAge();
		}

		public override void LoadedGame()
		{
			base.LoadedGame();
			ToddlerAgeSettingsUtility.RefreshExistingPawnLifeStagesForConfiguredAge();
		}

		public static void RegisterGameComponent()
		{
			if (Current.Game == null)
			{
				return;
			}

			if (Current.Game.GetComponent<ToddlerAgeSettingsGameComponent>() == null)
			{
				Current.Game.components.Add(new ToddlerAgeSettingsGameComponent(Current.Game));
			}

			if (Current.ProgramState == ProgramState.Playing)
			{
				ToddlerAgeSettingsUtility.RefreshExistingPawnLifeStagesForConfiguredAge();
			}
		}
	}
}
