using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	internal static class ToddlerPlayAnimationMath
	{
		public static float GetTickFraction(AnimationDef def)
		{
			int duration = def?.durationTicks ?? 60;
			if (duration <= 0)
			{
				duration = 60;
			}

			return (Find.TickManager.TicksGame % duration) / (float)duration;
		}

		public static float TriangleWave(float x)
		{
			x = Mathf.Clamp01(x);
			if (x <= 0.25f)
			{
				return x * 4f;
			}

			if (x <= 0.5f)
			{
				return (0.5f - x) * 4f;
			}

			if (x <= 0.75f)
			{
				return -4f * (x - 0.5f);
			}

			return -4f * (1f - x);
		}
	}

	public abstract class AnimationWorker_ToddlerPlayBase : BaseAnimationWorker
	{
		public override bool Enabled(AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
		{
			Pawn pawn = parms.pawn;
			if (pawn == null || !pawn.Spawned)
			{
				return false;
			}

			if (!def.playWhenDowned && pawn.Downed)
			{
				return false;
			}

			if (IsStandingAnimation() && !IsSuitableForStanding(pawn))
			{
				return false;
			}

			return IsPlayJob(pawn);
		}

		public override void PostDraw(AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms, Matrix4x4 matrix)
		{
		}

		public override Vector3 OffsetAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
		{
			return Vector3.zero;
		}

		public override float AngleAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
		{
			return 0f;
		}

		public override Vector3 ScaleAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
		{
			return Vector3.one;
		}

		public override GraphicStateDef GraphicStateAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
		{
			return null;
		}

		protected virtual bool IsStandingAnimation()
		{
			return false;
		}

		protected static bool IsSuitableForStanding(Pawn pawn)
		{
			if (pawn == null)
			{
				return false;
			}

			if (!pawn.Awake())
			{
				return false;
			}

			if (pawn.pather.Moving)
			{
				return false;
			}

			return true;
		}

		protected static bool IsPlayJob(Pawn pawn)
		{
			JobDef jobDef = pawn?.CurJobDef;
			if (jobDef != ToddlersExpansionJobDefOf.RimTalk_ToddlerSelfPlayJob
				&& jobDef != ToddlersExpansionJobDefOf.RimTalk_ToddlerMutualPlayJob)
			{
				return false;
			}

			string toil = pawn.jobs?.curDriver?.CurToilString;
			if (string.IsNullOrEmpty(toil))
			{
				return true;
			}

			return toil == "ToddlerSelfPlay" || toil == "ToddlerMutualPlay";
		}
	}

	public sealed class AnimationWorker_ToddlerPlayWiggle : AnimationWorker_ToddlerPlayBase
	{
		private const float WiggleAngle = 12f;

		public override float AngleAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
		{
			float x = ToddlerPlayAnimationMath.GetTickFraction(def);
			return WiggleAngle * ToddlerPlayAnimationMath.TriangleWave(x);
		}

		protected override bool IsStandingAnimation()
		{
			return true;
		}
	}

	public sealed class AnimationWorker_ToddlerPlaySway : AnimationWorker_ToddlerPlayBase
	{
		private const float SwayAngle = 8f;

		public override float AngleAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
		{
			float x = ToddlerPlayAnimationMath.GetTickFraction(def);
			return SwayAngle * Mathf.Sin(x * Mathf.PI * 2f);
		}

		protected override bool IsStandingAnimation()
		{
			return true;
		}
	}

	public sealed class AnimationWorker_ToddlerPlayLay : AnimationWorker_ToddlerPlayBase
	{
		private const float LayAngle = 35f;

		public override float AngleAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
		{
			if (parms.facing == Rot4.East)
			{
				return LayAngle;
			}

			if (parms.facing == Rot4.West)
			{
				return -LayAngle;
			}

			if (parms.facing == Rot4.North && parms.flipHead)
			{
				return 180f;
			}

			return 0f;
		}

		protected override bool IsStandingAnimation()
		{
			return false;
		}
	}

	public sealed class AnimationWorker_ToddlerPlayCrawl_Root : AnimationWorker_ToddlerPlayBase
	{
		private const float CrawlAngle = 40f;

		public override float AngleAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
		{
			if (parms.facing == Rot4.East)
			{
				return CrawlAngle;
			}

			if (parms.facing == Rot4.West)
			{
				return -CrawlAngle;
			}

			if (parms.facing == Rot4.North && parms.flipHead)
			{
				return 180f;
			}

			return 0f;
		}

		protected override bool IsStandingAnimation()
		{
			return false;
		}
	}

	public sealed class AnimationWorker_ToddlerPlayCrawl_Head : AnimationWorker_ToddlerPlayBase
	{
		private const float CrawlAngle = 40f;

		public override float AngleAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
		{
			if (parms.facing == Rot4.East)
			{
				return -0.5f * CrawlAngle;
			}

			if (parms.facing == Rot4.West)
			{
				return 0.5f * CrawlAngle;
			}

			if (parms.facing == Rot4.North && parms.flipHead)
			{
				return 180f;
			}

			return 0f;
		}

		protected override bool IsStandingAnimation()
		{
			return false;
		}
	}
}
