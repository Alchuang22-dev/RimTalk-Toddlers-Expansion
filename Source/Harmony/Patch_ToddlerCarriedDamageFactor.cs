using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_ToddlerCarriedDamageFactor
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			MethodInfo target = AccessTools.Method(
				typeof(Pawn),
				"PreApplyDamage",
				new[] { typeof(DamageInfo).MakeByRefType(), typeof(bool).MakeByRefType() });

			if (target == null)
			{
				return;
			}

			MethodInfo prefix = AccessTools.Method(typeof(Patch_ToddlerCarriedDamageFactor), nameof(PreApplyDamage_Prefix));
			harmony.Patch(target, prefix: new HarmonyMethod(prefix));
		}

		private static bool PreApplyDamage_Prefix(Pawn __instance, ref DamageInfo dinfo, ref bool absorbed)
		{
			if (__instance == null || absorbed)
			{
				return true;
			}

			if (!ToddlerCarryingUtility.IsBeingCarried(__instance))
			{
				return true;
			}

			if (!ToddlerCarryProtectionUtility.HasCarryProtection(__instance))
			{
				return true;
			}

			Pawn carrier = ToddlerCarryingUtility.GetCarrier(__instance);
			if (carrier == null || carrier == __instance || carrier.Dead || carrier.Destroyed || carrier.health == null)
			{
				return true;
			}

			if (dinfo.Amount <= 0f)
			{
				absorbed = true;
				return false;
			}

			try
			{
				// Redirect carried baby/toddler damage to the carrier.
				carrier.TakeDamage(dinfo);
				absorbed = true;
				return false;
			}
			catch
			{
				// Fall back to vanilla damage flow if redirect fails unexpectedly.
				return true;
			}
		}
	}
}
