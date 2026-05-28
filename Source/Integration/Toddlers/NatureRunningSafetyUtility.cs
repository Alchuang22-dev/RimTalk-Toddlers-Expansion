using System;
using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public static class NatureRunningSafetyUtility
	{
		private const float UnsafeRainRateThreshold = 0.1f;

		public static bool IsSafeToStartOrContinue(Pawn pawn)
		{
			if (!ToddlersExpansionSettings.enableNatureRunningSafetyChecks)
			{
				return true;
			}

			Map map = pawn?.MapHeld;
			if (map == null)
			{
				return false;
			}

			if (HasUnsafeWeather(map))
			{
				return false;
			}

			return IsComfortableCell(pawn, pawn.PositionHeld, map);
		}

		public static bool IsSafeNatureRunningTarget(Pawn pawn, IntVec3 cell, Map map = null)
		{
			if (!ToddlersExpansionSettings.enableNatureRunningSafetyChecks)
			{
				return true;
			}

			map ??= pawn?.MapHeld;
			if (pawn == null || map == null || !cell.IsValid || !cell.InBounds(map))
			{
				return false;
			}

			if (HasUnsafeWeather(map))
			{
				return false;
			}

			return IsComfortableCell(pawn, cell, map);
		}

		public static bool HasUnsafeWeather(Map map)
		{
			if (map?.weatherManager == null)
			{
				return false;
			}

			if (map.weatherManager.RainRate > UnsafeRainRateThreshold)
			{
				return true;
			}

			WeatherDef weather = map.weatherManager.curWeather;
			if (WeatherHasLightning(weather))
			{
				return true;
			}

			if (map.gameConditionManager?.ConditionIsActive(GameConditionDefOf.Flashstorm) == true)
			{
				return true;
			}

			return false;
		}

		private static bool IsComfortableCell(Pawn pawn, IntVec3 cell, Map map)
		{
			return cell.IsValid && cell.InBounds(map) && pawn.ComfortableTemperatureAtCell(cell, map);
		}

		private static bool WeatherHasLightning(WeatherDef weather)
		{
			if (weather == null)
			{
				return false;
			}

			string defName = weather.defName ?? string.Empty;
			if (defName.IndexOf("Thunder", StringComparison.OrdinalIgnoreCase) >= 0
				|| defName.IndexOf("Lightning", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}

			if (weather.eventMakers == null)
			{
				return false;
			}

			for (int i = 0; i < weather.eventMakers.Count; i++)
			{
				Type eventClass = weather.eventMakers[i]?.eventClass;
				if (eventClass == null)
				{
					continue;
				}

				if (typeof(WeatherEvent_LightningFlash).IsAssignableFrom(eventClass)
					|| eventClass.Name.IndexOf("Lightning", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}
			}

			return false;
		}
	}
}
