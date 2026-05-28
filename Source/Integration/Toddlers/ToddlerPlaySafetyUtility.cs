using RimTalk_ToddlersExpansion.Core;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public static class ToddlerPlaySafetyUtility
	{
		private const float WetWeatherRainRateThreshold = 0.1f;

		public static bool IsSafePlayCell(Pawn pawn, IntVec3 cell)
		{
			if (!ToddlersExpansionSettings.enableToddlerPlaySafetyChecks)
			{
				return true;
			}

			Map map = pawn?.MapHeld;
			if (map == null || !cell.IsValid || !cell.InBounds(map))
			{
				return false;
			}

			if (!pawn.ComfortableTemperatureAtCell(cell, map))
			{
				return false;
			}

			if (WouldGetWetAtCell(cell, map))
			{
				return false;
			}

			return true;
		}

		private static bool WouldGetWetAtCell(IntVec3 cell, Map map)
		{
			if (cell.Roofed(map))
			{
				return false;
			}

			return map.weatherManager?.curWeather != null
				&& map.weatherManager.curWeather.rainRate > WetWeatherRainRateThreshold;
		}
	}
}
