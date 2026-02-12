using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_BabyCryMoodOnly
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			MethodInfo target = AccessTools.Method(typeof(MentalState_BabyCry), "AuraEffect", new[] { typeof(Thing), typeof(Pawn) });
			if (target != null)
			{
				harmony.Patch(target, prefix: new HarmonyMethod(typeof(Patch_BabyCryMoodOnly), nameof(AuraEffect_Prefix)));
			}
		}

		private static bool AuraEffect_Prefix(Thing source, Pawn hearer)
		{
			if (!ToddlersExpansionSettings.babyCryAffectsMoodOnly)
			{
				return true;
			}

			if (hearer == null)
			{
				return false;
			}

			hearer.HearClamor(source, ClamorDefOf.BabyCry);
			if (!(source is Pawn pawn) || hearer.needs?.mood == null)
			{
				return false;
			}

			if (hearer == pawn.GetMother() || hearer == pawn.GetFather())
			{
				hearer.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.MyCryingBaby, pawn);
			}
			else
			{
				hearer.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.CryingBaby, pawn);
			}

			return false;
		}
	}
}
