using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_ToddlersRemoveClothes
	{
		private const float OutdoorTemperatureBlockThreshold = 0f;
		private const float SummerExtraAttemptMtbMultiplier = 2f;
		private static MentalStateDef _removeClothesDef;

		public static void Init(HarmonyLib.Harmony harmony)
		{
			Type workerType = AccessTools.TypeByName("Toddlers.MentalStateWorker_RemoveClothes");
			if (workerType != null)
			{
				MethodInfo stateCanOccur = AccessTools.Method(workerType, "StateCanOccur", new[] { typeof(Pawn) });
				if (stateCanOccur != null)
				{
					harmony.Patch(stateCanOccur, postfix: new HarmonyMethod(typeof(Patch_ToddlersRemoveClothes), nameof(StateCanOccur_Postfix)));
				}
			}

			MethodInfo tickInterval = AccessTools.Method(typeof(Hediff), nameof(Hediff.TickInterval), new[] { typeof(int) });
			if (tickInterval != null)
			{
				harmony.Patch(tickInterval, postfix: new HarmonyMethod(typeof(Patch_ToddlersRemoveClothes), nameof(Hediff_TickInterval_Postfix)));
			}
		}

		private static void StateCanOccur_Postfix(Pawn pawn, ref bool __result)
		{
			if (!__result || pawn == null)
			{
				return;
			}

			if (GetOutdoorTemperature(pawn) < OutdoorTemperatureBlockThreshold)
			{
				__result = false;
			}
		}

		private static void Hediff_TickInterval_Postfix(Hediff __instance, int delta)
		{
			Pawn pawn = __instance?.pawn;
			if (pawn == null || delta <= 0 || !pawn.Spawned || pawn.InMentalState || !pawn.Awake())
			{
				return;
			}

			if (!pawn.IsHashIntervalTick(60, delta))
			{
				return;
			}

			if (!IsSummer(pawn.MapHeld) || GetOutdoorTemperature(pawn) < OutdoorTemperatureBlockThreshold)
			{
				return;
			}

			HediffStage curStage = __instance.CurStage;
			if (curStage?.mentalStateGivers == null)
			{
				return;
			}

			MentalStateDef removeClothes = GetRemoveClothesDef();
			if (removeClothes == null)
			{
				return;
			}

			for (int i = 0; i < curStage.mentalStateGivers.Count; i++)
			{
				MentalStateGiver giver = curStage.mentalStateGivers[i];
				if (giver?.mentalState != removeClothes || giver.mtbDays <= 0f)
				{
					continue;
				}

				float extraMtbDays = giver.mtbDays * SummerExtraAttemptMtbMultiplier;
				if (!Rand.MTBEventOccurs(extraMtbDays, 60000f, 60f))
				{
					continue;
				}

				pawn.mindState?.mentalStateHandler?.TryStartMentalState(removeClothes, "MentalStateReason_Hediff".Translate(__instance.Label));
				if (pawn.InMentalState)
				{
					return;
				}
			}
		}

		private static MentalStateDef GetRemoveClothesDef()
		{
			if (_removeClothesDef == null)
			{
				_removeClothesDef = DefDatabase<MentalStateDef>.GetNamedSilentFail("RemoveClothes");
			}

			return _removeClothesDef;
		}

		private static bool IsSummer(Map map)
		{
			if (map == null)
			{
				return false;
			}

			Season season = GenLocalDate.Season(map);
			return season == Season.Summer || season == Season.PermanentSummer;
		}

		private static float GetOutdoorTemperature(Pawn pawn)
		{
			if (pawn?.MapHeld != null)
			{
				return pawn.MapHeld.mapTemperature.OutdoorTemp;
			}

			if (pawn?.Tile >= 0)
			{
				return GenTemperature.GetTemperatureAtTile(pawn.Tile);
			}

			return float.MaxValue;
		}
	}
}
