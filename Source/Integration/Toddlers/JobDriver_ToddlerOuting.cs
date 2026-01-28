using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public sealed class JobDriver_ToddlerOuting : JobDriver
	{
		private const TargetIndex SpotInd = TargetIndex.A;
		private const int MinPlayTicks = 900;
		private const int MaxPlayTicks = 1800;
		private const int InteractionCooldownTicks = 250;

		private AnimationDef _playAnimation;
		private ToddlerOutingActivity _activity = ToddlerOutingActivity.Play;
		private int _lastInteractionTick;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return pawn.Reserve(job.GetTarget(SpotInd), job, 1, -1, null, errorOnFailed);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOn(() => pawn == null || pawn.Map == null);
			this.FailOn(() => pawn.Downed || pawn.Drafted || pawn.InMentalState);
			this.FailOn(() => !TryGetParticipant(out _, out _));
			this.AddEndCondition(() =>
			{
				if (!pawn.DevelopmentalStage.Child())
				{
					return JobCondition.Ongoing;
				}

				return PawnUtility.WillSoonHaveBasicNeed(pawn, -0.05f) ? JobCondition.Incompletable : JobCondition.Ongoing;
			});

			yield return Toils_Goto.GotoCell(SpotInd, PathEndMode.OnCell);

			Toil play = ToilMaker.MakeToil("ToddlerOutingPlay");
			play.initAction = () =>
			{
				if (TryGetParticipant(out _, out ToddlerOutingParticipant participant))
				{
					_activity = participant.Activity;
				}

				if (ToddlersCompatUtility.IsToddler(pawn))
				{
					_playAnimation = ToddlerPlayAnimationUtility.GetRandomSelfPlayAnimation();
					ToddlerPlayAnimationUtility.TryApplyAnimation(pawn, _playAnimation);
				}
			};
			play.tickIntervalAction = delta =>
			{
				if (!TryGetParticipant(out _, out ToddlerOutingParticipant participant))
				{
					EndJobWith(JobCondition.Incompletable);
					return;
				}

				if (pawn.needs?.learning != null)
				{
					if (LearningUtility.LearningTickCheckEnd(pawn, delta))
					{
						return;
					}
				}

				ApplyOutingTick(participant, delta);
			};
			play.handlingFacing = true;
			play.defaultCompleteMode = ToilCompleteMode.Delay;
			play.defaultDuration = job.def.joyDuration > 0 ? job.def.joyDuration : Rand.Range(MinPlayTicks, MaxPlayTicks);
			play.AddFinishAction(() => ToddlerPlayAnimationUtility.ClearAnimation(pawn, _playAnimation));

			yield return play;
		}

		private void ApplyOutingTick(ToddlerOutingParticipant participant, int delta)
		{
			if (participant == null)
			{
				return;
			}

			if (_playAnimation != null && pawn.Drawer?.renderer?.CurAnimation != _playAnimation)
			{
				ToddlerPlayAnimationUtility.TryApplyAnimation(pawn, _playAnimation);
			}

			Pawn partner = FindPartner();
			if (partner != null)
			{
				pawn.rotationTracker.FaceTarget(partner);
			}

			if (ToddlersCompatUtility.IsToddler(pawn))
			{
				if (partner != null)
				{
					SocialNeedTuning_Toddlers.ApplyMutualPlayTickEffects(pawn, partner, delta);
				}
				else
				{
					SocialNeedTuning_Toddlers.ApplySelfPlayTickEffects(pawn, delta);
				}

				ToddlerPlayEffectUtility.TryTriggerGigglingEffect(pawn);
			}
			else
			{
				Need_Joy joy = pawn.needs?.joy;
				if (joy != null)
				{
					joy.GainJoy(0.00012f * delta, JoyKindDefOf.Social);
				}
			}

			if (_activity == ToddlerOutingActivity.Chat && partner != null)
			{
				TryInteractWithPartner(partner);
			}
		}

		private Pawn FindPartner()
		{
			if (!TryGetParticipant(out ToddlerOutingSession session, out _))
			{
				return null;
			}

			Pawn best = null;
			float bestDistance = float.MaxValue;
			for (int i = 0; i < session.Participants.Count; i++)
			{
				Pawn other = session.Participants[i]?.Pawn;
				if (other == null || other == pawn || !other.Spawned || other.Dead)
				{
					continue;
				}

				float distance = pawn.Position.DistanceTo(other.Position);
				if (distance < bestDistance)
				{
					bestDistance = distance;
					best = other;
				}
			}

			return best;
		}

		private void TryInteractWithPartner(Pawn partner)
		{
			if (partner == null || pawn.interactions == null)
			{
				return;
			}

			int tick = Find.TickManager.TicksGame;
			if (tick - _lastInteractionTick < InteractionCooldownTicks)
			{
				return;
			}

			if (pawn.Position.DistanceToSquared(partner.Position) > 64f)
			{
				return;
			}

			_lastInteractionTick = tick;
			pawn.interactions.TryInteractWith(partner, null);
		}

		private bool TryGetParticipant(out ToddlerOutingSession session, out ToddlerOutingParticipant participant)
		{
			session = null;
			participant = null;
			Map map = pawn.Map;
			if (map == null)
			{
				return false;
			}

			ToddlerOutingMapComponent component = map.GetComponent<ToddlerOutingMapComponent>();
			if (component == null)
			{
				return false;
			}

			return component.TryGetParticipant(pawn, out session, out participant);
		}
	}
}
