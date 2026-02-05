using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public static class ToddlerMentalStateUtility
	{
		private const string CryingDefName = "Crying";
		private const string GigglingDefName = "Giggling";

		public static bool HasBlockingMentalState(Pawn pawn)
		{
			if (pawn?.InMentalState != true)
			{
				return false;
			}

			return !IsNonBlockingBabyMentalState(pawn.MentalStateDef);
		}

		private static bool IsNonBlockingBabyMentalState(MentalStateDef mentalStateDef)
		{
			if (mentalStateDef == null)
			{
				return false;
			}

			string defName = mentalStateDef.defName;
			return defName == CryingDefName || defName == GigglingDefName;
		}
	}
}
