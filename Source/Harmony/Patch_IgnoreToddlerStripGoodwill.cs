using HarmonyLib;
using RimWorld;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	/// <summary>
	/// Prevents baby/toddler strip flows from being treated as "member stripped" goodwill hits.
	/// Some apparel workflows can route through Pawn.Strip, which reports a hostile diplomatic event.
	/// </summary>
	public static class Patch_IgnoreToddlerStripGoodwill
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			var stripMethod = AccessTools.Method(typeof(Pawn), nameof(Pawn.Strip), new[] { typeof(bool) });
			if (stripMethod != null)
			{
				harmony.Patch(stripMethod, prefix: new HarmonyMethod(typeof(Patch_IgnoreToddlerStripGoodwill), nameof(Strip_Prefix)));
			}

			var notifyMemberStripped = AccessTools.Method(typeof(Faction), nameof(Faction.Notify_MemberStripped), new[] { typeof(Pawn), typeof(Faction) });
			if (notifyMemberStripped != null)
			{
				harmony.Patch(
					notifyMemberStripped,
					prefix: new HarmonyMethod(typeof(Patch_IgnoreToddlerStripGoodwill), nameof(Notify_MemberStripped_Prefix)));
			}
		}

		private static void Strip_Prefix(Pawn __instance, ref bool notifyFaction)
		{
			if (!notifyFaction || __instance == null)
			{
				return;
			}

			// Babies/toddlers are frequently dressed via assisted flows; avoid goodwill penalties here.
			if (!ToddlersCompatUtility.IsToddlerOrBaby(__instance))
			{
				return;
			}

			notifyFaction = false;
		}

		private static bool Notify_MemberStripped_Prefix(Pawn member, Faction violator)
		{
			if (member == null || violator != Faction.OfPlayer)
			{
				return true;
			}

			// Some dress/strip workflows from other mods can call this directly.
			// Ignore goodwill loss when the "victim" is a baby/toddler.
			return !ToddlersCompatUtility.IsToddlerOrBaby(member);
		}
	}
}
