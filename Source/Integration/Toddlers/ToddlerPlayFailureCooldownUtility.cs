using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	internal static class ToddlerPlayFailureCooldownUtility
	{
		private const int FailureCooldownTicks = 600;
		private const int SelfPlayFailureCooldownKey = 1934182756;
		private const int MutualPlayFailureCooldownKey = 1934182757;

		public static bool IsSelfPlayOnCooldown(Pawn pawn)
		{
			return IsOnCooldown(pawn, SelfPlayFailureCooldownKey);
		}

		public static void StartSelfPlayCooldown(Pawn pawn)
		{
			StartCooldown(pawn, SelfPlayFailureCooldownKey);
		}

		public static bool IsMutualPlayOnCooldown(Pawn pawn)
		{
			return IsOnCooldown(pawn, MutualPlayFailureCooldownKey);
		}

		public static void StartMutualPlayCooldown(Pawn pawn)
		{
			StartCooldown(pawn, MutualPlayFailureCooldownKey);
		}

		private static bool IsOnCooldown(Pawn pawn, int key)
		{
			if (pawn?.mindState?.thinkData == null)
			{
				return false;
			}

			int currentTick = Find.TickManager?.TicksGame ?? 0;
			return pawn.mindState.thinkData.TryGetValue(key, out int nextAllowedTick)
				&& currentTick < nextAllowedTick;
		}

		private static void StartCooldown(Pawn pawn, int key)
		{
			if (pawn?.mindState?.thinkData == null)
			{
				return;
			}

			int currentTick = Find.TickManager?.TicksGame ?? 0;
			pawn.mindState.thinkData[key] = currentTick + FailureCooldownTicks;
		}
	}
}
