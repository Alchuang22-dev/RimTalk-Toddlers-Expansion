using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_ToddlerCarriedDamageFactor
	{
		private static readonly FieldInfo HediffSetPawnField = AccessTools.Field(typeof(HediffSet), "pawn");

		public static void Init(HarmonyLib.Harmony harmony)
		{
			MethodInfo target = AccessTools.Method(typeof(HediffSet), nameof(HediffSet.FactorForDamage), new[] { typeof(DamageInfo) });
			if (target == null)
			{
				return;
			}

			MethodInfo postfix = AccessTools.Method(typeof(Patch_ToddlerCarriedDamageFactor), nameof(FactorForDamage_Postfix));
			harmony.Patch(target, postfix: new HarmonyMethod(postfix));
		}

		private static void FactorForDamage_Postfix(HediffSet __instance, ref float __result)
		{
			if (__instance == null || __result <= 0f)
			{
				return;
			}

			Pawn pawn = HediffSetPawnField?.GetValue(__instance) as Pawn;
			if (pawn == null || !ToddlerCarryingUtility.IsBeingCarried(pawn))
			{
				return;
			}

			if (!ToddlerCarryProtectionUtility.HasCarryProtection(pawn))
			{
				return;
			}

			__result *= ToddlerCarryProtectionUtility.CarriedDamageFactor;
		}
	}
}
