using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Core;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public static class ToddlerPlayAnimationUtility
	{
		private static bool _initialized;
		private static AnimationDef[] _selfPlayAnimations;
		private static AnimationDef[] _mutualPlayAnimations;

		public static AnimationDef GetRandomSelfPlayAnimation()
		{
			EnsureInitialized();
			return PickRandom(_selfPlayAnimations);
		}

		public static AnimationDef GetRandomMutualPlayAnimation()
		{
			EnsureInitialized();
			return PickRandom(_mutualPlayAnimations);
		}

		public static void TryApplyAnimation(Pawn pawn, AnimationDef animation)
		{
			if (pawn?.Drawer?.renderer == null || animation == null)
			{
				return;
			}

			if (pawn.Drawer.renderer.CurAnimation != animation)
			{
				pawn.Drawer.renderer.SetAnimation(animation);
			}
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
		}

		private static void EnsureInitialized()
		{
			if (_initialized)
			{
				return;
			}

			_initialized = true;

			_selfPlayAnimations = BuildList(
				ToddlersExpansionAnimationDefOf.RimTalk_ToddlerPlay_Wiggle,
				ToddlersExpansionAnimationDefOf.RimTalk_ToddlerPlay_Lay,
				ToddlersExpansionAnimationDefOf.RimTalk_ToddlerPlay_Crawl,
				ToddlersExpansionAnimationDefOf.RimTalk_ToddlerPlay_Sway);

			_mutualPlayAnimations = BuildList(
				ToddlersExpansionAnimationDefOf.RimTalk_ToddlerPlay_Wiggle,
				ToddlersExpansionAnimationDefOf.RimTalk_ToddlerPlay_Sway,
				ToddlersExpansionAnimationDefOf.RimTalk_ToddlerPlay_Crawl,
				ToddlersExpansionAnimationDefOf.RimTalk_ToddlerPlay_Lay);
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
