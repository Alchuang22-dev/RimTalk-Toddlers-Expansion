using HarmonyLib;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class HarmonyBootstrap
	{
		private static bool _initialized;

		public static void Init()
		{
			if (_initialized)
			{
				return;
			}

			_initialized = true;
			var harmony = new HarmonyLib.Harmony("cj.rimtalk.toddlers");
			Patch_ToddlersJobsAvailability.Init(harmony);
			Patch_RimTalkContextBuilder.Init(harmony);
			Patch_RimTalkTalkService.Init(harmony);
			Patch_BiotechSharedBedroomThoughts.Init(harmony);
			Patch_PawnGroupMakerUtility.Init(harmony);
		}
	}
}
