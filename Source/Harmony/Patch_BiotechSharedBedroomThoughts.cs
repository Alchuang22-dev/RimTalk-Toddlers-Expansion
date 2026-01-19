using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.BioTech;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_BiotechSharedBedroomThoughts
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			MethodInfo sharedBed = AccessTools.Method(typeof(ThoughtWorker_SharedBed), "CurrentStateInternal");
			if (sharedBed != null)
			{
				MethodInfo postfix = AccessTools.Method(typeof(Patch_BiotechSharedBedroomThoughts), nameof(SharedBed_Postfix));
				harmony.Patch(sharedBed, postfix: new HarmonyMethod(postfix));
			}

			MethodInfo applyBedThoughts = AccessTools.Method(typeof(Toils_LayDown), "ApplyBedThoughts");
			if (applyBedThoughts != null)
			{
				MethodInfo postfix = AccessTools.Method(typeof(Patch_BiotechSharedBedroomThoughts), nameof(ApplyBedThoughts_Postfix));
				harmony.Patch(applyBedThoughts, postfix: new HarmonyMethod(postfix));
			}
		}

		private static void SharedBed_Postfix(Pawn p, ref ThoughtState __result)
		{
			if (!__result.Active || p == null)
			{
				return;
			}

			Room room = p.ownership?.OwnedBed?.GetRoom() ?? p.GetRoom();
			if (BedroomThoughtsPatchHelper.ShouldReplaceWithMyBabyThought(p, room))
			{
				__result = ThoughtState.Inactive;
			}
		}

		private static void ApplyBedThoughts_Postfix(Pawn actor, Building_Bed bed)
		{
			if (actor?.needs?.mood?.thoughts?.memories == null)
			{
				return;
			}

			Room room = bed?.GetRoom() ?? actor.GetRoom();
			if (BedroomThoughtsPatchHelper.ShouldReplaceWithMyBabyThought(actor, room))
			{
				actor.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.SleptInBarracks);
			}

			ToddlerSleepThoughtUtility.ApplySleepThoughts(actor, bed);
		}
	}
}
