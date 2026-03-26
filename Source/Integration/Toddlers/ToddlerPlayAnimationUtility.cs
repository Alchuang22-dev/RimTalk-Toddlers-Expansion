using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.BioTech;
using UnityEngine;
using Verse;

	namespace RimTalk_ToddlersExpansion.Integration.Toddlers
	{
		public static class ToddlerPlayAnimationUtility
		{
			public static AnimationDef GetRandomSelfPlayAnimation(Pawn pawn)
			{
			if (!CanUseManagedPlayAnimations(pawn))
			{
				ClearManagedNativePlayAnimation(pawn);
				return null;
			}

			return PickRandom(BuildSelfPlayAnimations());
		}

		public static AnimationDef GetRandomMutualPlayAnimation(Pawn pawn)
		{
			if (!CanUseManagedPlayAnimations(pawn))
			{
				ClearManagedNativePlayAnimation(pawn);
				return null;
			}

			return PickRandom(BuildMutualPlayAnimations());
		}

		public static AnimationDef GetSharedMutualPlayAnimation(Pawn pawn, Pawn partner)
		{
			if (!CanUseManagedPlayAnimations(pawn))
			{
				ClearManagedNativePlayAnimation(pawn);
				return null;
			}

			AnimationDef[] defs = BuildMutualPlayAnimations();
			if (defs == null || defs.Length == 0)
			{
				return null;
			}

			int first = Mathf.Min(pawn?.thingIDNumber ?? 0, partner?.thingIDNumber ?? 0);
			int second = Mathf.Max(pawn?.thingIDNumber ?? 0, partner?.thingIDNumber ?? 0);
			int seed = Gen.HashCombineInt(first, second) & int.MaxValue;
			return defs[seed % defs.Length];
		}

		public static void TryApplyAnimation(Pawn pawn, AnimationDef animation)
		{
			if (pawn?.Drawer?.renderer == null || animation == null)
			{
				return;
			}

			if (!CanUseManagedPlayAnimations(pawn))
			{
				ClearManagedNativePlayAnimation(pawn);
				return;
			}

			if (ToddlerCarryingUtility.IsBeingCarried(pawn))
			{
				ClearCurrentAnimation(pawn);
				return;
			}

			if (ShouldDelayPlayAnimationForMovement(pawn))
			{
				ClearManagedNativePlayAnimation(pawn);
				return;
			}

			if (pawn.Drawer.renderer.CurAnimation != animation)
			{
				pawn.Drawer.renderer.SetAnimation(animation);
			}

			YayoAnimation.YayoAnimationCompatUtility.TrackSafeFallbackPawn(pawn);
		}

		public static void ClearAnimation(Pawn pawn, AnimationDef animation)
		{
			if (pawn?.Drawer?.renderer == null || animation == null)
			{
				return;
			}

			if (pawn.Drawer.renderer.CurAnimation == animation)
			{
				pawn.Drawer.renderer.SetAnimation(null);
			}

			YayoAnimation.YayoAnimationCompatUtility.UntrackSafeFallbackPawn(pawn);
		}

		public static void ClearCurrentAnimation(Pawn pawn)
		{
			if (pawn?.Drawer?.renderer == null)
			{
				return;
			}

			if (pawn.Drawer.renderer.CurAnimation != null)
			{
				pawn.Drawer.renderer.SetAnimation(null);
			}

			YayoAnimation.YayoAnimationCompatUtility.UntrackSafeFallbackPawn(pawn);
		}

		public static bool ClearManagedNativePlayAnimation(Pawn pawn)
		{
			AnimationDef current = pawn?.Drawer?.renderer?.CurAnimation;
			if (current == null)
			{
				YayoAnimation.YayoAnimationCompatUtility.UntrackSafeFallbackPawn(pawn);
				return false;
			}

			if (IsManagedPlayAnimation(current))
			{
				pawn.Drawer.renderer.SetAnimation(null);
				YayoAnimation.YayoAnimationCompatUtility.UntrackSafeFallbackPawn(pawn);
				return true;
			}

			YayoAnimation.YayoAnimationCompatUtility.UntrackSafeFallbackPawn(pawn);
			return false;
		}

		public static bool HasManagedPlayAnimation(Pawn pawn)
		{
			return IsManagedPlayAnimation(pawn?.Drawer?.renderer?.CurAnimation);
		}

		public static bool ArePlayAnimationsAllowedForPawn(Pawn pawn)
		{
			if (pawn == null)
			{
				return true;
			}

			if (BiotechCompatUtility.IsBaby(pawn)
				&& !ToddlersCompatUtility.IsToddler(pawn)
				&& !ToddlersExpansionSettings.EnableNewbornPlayAnimations)
			{
				return false;
			}

			return true;
		}

		public static bool ShouldDelayPlayAnimationForMovement(Pawn pawn)
		{
			return pawn?.pather?.MovingNow == true;
		}

		private static bool CanUseManagedPlayAnimations(Pawn pawn)
		{
			return pawn != null
				&& ToddlersCompatUtility.IsToddlerOrBaby(pawn)
				&& ArePlayAnimationsAllowedForPawn(pawn);
		}

		private static bool IsManagedPlayAnimation(AnimationDef animation)
		{
			if (animation == null)
			{
				return false;
			}

			if (animation == ToddlersExpansionAnimationDefOf.RimTalk_ToddlerPlay_Wiggle
				|| animation == ToddlersExpansionAnimationDefOf.RimTalk_ToddlerPlay_Sway
				|| animation == ToddlersExpansionAnimationDefOf.RimTalk_ToddlerPlay_Lay
				|| animation == ToddlersExpansionAnimationDefOf.RimTalk_ToddlerPlay_Crawl)
			{
				return true;
			}

			AnimationDef toddlerWobble = DefDatabase<AnimationDef>.GetNamedSilentFail("ToddlerWobble");
			return animation == toddlerWobble;
		}

		private static AnimationDef[] BuildSelfPlayAnimations()
		{
			AnimationDef toddlersWobble = DefDatabase<AnimationDef>.GetNamedSilentFail("ToddlerWobble");

			return BuildList(
				ToddlersExpansionSettings.EnableNativePlayWiggle ? ToddlersExpansionAnimationDefOf.RimTalk_ToddlerPlay_Wiggle : null,
				ToddlersExpansionSettings.EnableNativePlayLay ? ToddlersExpansionAnimationDefOf.RimTalk_ToddlerPlay_Lay : null,
				ToddlersExpansionSettings.EnableNativePlayCrawl ? ToddlersExpansionAnimationDefOf.RimTalk_ToddlerPlay_Crawl : null,
				ToddlersExpansionSettings.EnableNativePlaySway ? ToddlersExpansionAnimationDefOf.RimTalk_ToddlerPlay_Sway : null,
				ToddlersExpansionSettings.EnableNativePlayToddlerWobble ? toddlersWobble : null);
		}

		private static AnimationDef[] BuildMutualPlayAnimations()
		{
			AnimationDef toddlersWobble = DefDatabase<AnimationDef>.GetNamedSilentFail("ToddlerWobble");

			return BuildList(
				ToddlersExpansionSettings.EnableNativePlayWiggle ? ToddlersExpansionAnimationDefOf.RimTalk_ToddlerPlay_Wiggle : null,
				ToddlersExpansionSettings.EnableNativePlaySway ? ToddlersExpansionAnimationDefOf.RimTalk_ToddlerPlay_Sway : null,
				ToddlersExpansionSettings.EnableNativePlayCrawl ? ToddlersExpansionAnimationDefOf.RimTalk_ToddlerPlay_Crawl : null,
				ToddlersExpansionSettings.EnableNativePlayLay ? ToddlersExpansionAnimationDefOf.RimTalk_ToddlerPlay_Lay : null,
				ToddlersExpansionSettings.EnableNativePlayToddlerWobble ? toddlersWobble : null);
		}

		private static AnimationDef[] BuildList(params AnimationDef[] defs)
		{
			List<AnimationDef> list = new List<AnimationDef>(defs.Length);
			for (int i = 0; i < defs.Length; i++)
			{
				if (defs[i] != null)
				{
					list.Add(defs[i]);
				}
			}

			return list.ToArray();
		}

		private static AnimationDef PickRandom(AnimationDef[] defs)
		{
			if (defs == null || defs.Length == 0)
			{
				return null;
			}

			return defs[Rand.Range(0, defs.Length)];
		}
	}
}
