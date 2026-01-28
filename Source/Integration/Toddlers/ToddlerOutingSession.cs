using System.Collections.Generic;
using System.Linq;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.RimTalk;
using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	internal enum ToddlerOutingActivity
	{
		Play,
		Observe,
		Chat
	}

	internal sealed class ToddlerOutingParticipant : IExposable
	{
		public Pawn Pawn;
		public IntVec3 ReturnCell = IntVec3.Invalid;
		public ToddlerOutingActivity Activity = ToddlerOutingActivity.Play;
		public int JoinTick;
		public int LastOrderTick;
		public bool HasArrived;

		public void ExposeData()
		{
			Scribe_References.Look(ref Pawn, "pawn");
			Scribe_Values.Look(ref ReturnCell, "returnCell");
			Scribe_Values.Look(ref Activity, "activity", ToddlerOutingActivity.Play);
			Scribe_Values.Look(ref JoinTick, "joinTick");
		}
	}

	internal sealed class ToddlerOutingSession : IExposable
	{
		private int _talkIntervalTicks = 1200;

		public IntVec3 Spot = IntVec3.Invalid;
		public int StartTick;
		public int DurationTicks;
		public int NextTalkTick;
		public bool IsEnded;
		public List<ToddlerOutingParticipant> Participants = new List<ToddlerOutingParticipant>();

		public ToddlerOutingSession()
		{
		}

		public ToddlerOutingSession(IntVec3 spot, int startTick, int durationTicks, int talkIntervalTicks)
		{
			Spot = spot;
			StartTick = startTick;
			DurationTicks = durationTicks;
			_talkIntervalTicks = talkIntervalTicks;
			NextTalkTick = startTick + Rand.RangeInclusive(talkIntervalTicks / 2, talkIntervalTicks);
		}

		public void ExposeData()
		{
			Scribe_Values.Look(ref Spot, "spot");
			Scribe_Values.Look(ref StartTick, "startTick");
			Scribe_Values.Look(ref DurationTicks, "durationTicks");
			Scribe_Values.Look(ref NextTalkTick, "nextTalkTick");
			Scribe_Values.Look(ref IsEnded, "isEnded");
			Scribe_Values.Look(ref _talkIntervalTicks, "talkIntervalTicks", 1200);
			Scribe_Collections.Look(ref Participants, "participants", LookMode.Deep);
		}

		public void PostLoadInit()
		{
			if (Participants == null)
			{
				Participants = new List<ToddlerOutingParticipant>();
			}
		}

		public bool TryGetParticipant(Pawn pawn, out ToddlerOutingParticipant participant)
		{
			participant = null;
			if (pawn == null || Participants == null)
			{
				return false;
			}

			for (int i = 0; i < Participants.Count; i++)
			{
				ToddlerOutingParticipant entry = Participants[i];
				if (entry?.Pawn == pawn)
				{
					participant = entry;
					return true;
				}
			}

			return false;
		}

		public void InitializeParticipants(List<Pawn> pawns, int tick)
		{
			Participants.Clear();
			if (pawns == null)
			{
				return;
			}

			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				if (pawn == null)
				{
					continue;
				}

				Participants.Add(new ToddlerOutingParticipant
				{
					Pawn = pawn,
					ReturnCell = pawn.Position,
					Activity = ChooseActivity(pawn),
					JoinTick = tick
				});
			}
		}

		public bool AddParticipant(Pawn pawn, int tick)
		{
			if (pawn == null)
			{
				return false;
			}

			if (TryGetParticipant(pawn, out _))
			{
				return false;
			}

			Participants.Add(new ToddlerOutingParticipant
			{
				Pawn = pawn,
				ReturnCell = pawn.Position,
				Activity = ChooseActivity(pawn),
				JoinTick = tick
			});

			return true;
		}

		public void Tick(Map map, int tick, ToddlerOutingMapComponent owner)
		{
			if (IsEnded)
			{
				return;
			}

			if (map == null || !Spot.IsValid || !Spot.InBounds(map))
			{
				IsEnded = true;
				return;
			}

			if (!Spot.Standable(map))
			{
				IsEnded = true;
				return;
			}

			if (tick >= StartTick + DurationTicks)
			{
				IsEnded = true;
				return;
			}

			if (!IsWeatherAcceptable(map))
			{
				IsEnded = true;
				return;
			}

			for (int i = Participants.Count - 1; i >= 0; i--)
			{
				ToddlerOutingParticipant participant = Participants[i];
				Pawn pawn = participant?.Pawn;
				if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map != map)
				{
					Participants.RemoveAt(i);
					continue;
				}

				if (ShouldRemoveParticipant(pawn))
				{
					Participants.RemoveAt(i);
					continue;
				}

				if (!participant.HasArrived && pawn.Position.InHorDistOf(Spot, owner.ArrivedDistance))
				{
					participant.HasArrived = true;
				}

				if (!participant.HasArrived && tick - participant.JoinTick > owner.LateArrivalTicks)
				{
					Participants.RemoveAt(i);
					continue;
				}

				if (pawn.Position.DistanceTo(Spot) > owner.LostDistance)
				{
					owner.TryIssueReturnToSpot(pawn, Spot, participant, tick);
					continue;
				}

				if (pawn.CurJobDef != ToddlersExpansionJobDefOf.RimTalk_ToddlerOuting)
				{
					owner.TryIssueOutingJob(pawn, this, participant, tick);
				}
			}

			if (Participants.Count < owner.MinParticipants)
			{
				IsEnded = true;
				return;
			}

			if (tick >= NextTalkTick)
			{
				TriggerRimTalk();
				NextTalkTick = tick + _talkIntervalTicks;
			}
		}

		private static ToddlerOutingActivity ChooseActivity(Pawn pawn)
		{
			if (ToddlersCompatUtility.IsToddler(pawn))
			{
				return ToddlerOutingActivity.Play;
			}

			float roll = Rand.Value;
			if (roll < 0.45f)
			{
				return ToddlerOutingActivity.Chat;
			}

			if (roll < 0.8f)
			{
				return ToddlerOutingActivity.Play;
			}

			return ToddlerOutingActivity.Observe;
		}

		private bool ShouldRemoveParticipant(Pawn pawn)
		{
			if (pawn.Downed || pawn.Drafted || pawn.InMentalState)
			{
				return true;
			}

			if (!pawn.Awake())
			{
				return true;
			}

			if (pawn.jobs?.curJob?.playerForced ?? false)
			{
				return true;
			}

			if (PawnUtility.WillSoonHaveBasicNeed(pawn, -0.05f))
			{
				return true;
			}

			if (pawn.DevelopmentalStage.Child() && pawn.needs?.learning != null && LearningUtility.LearningSatisfied(pawn))
			{
				return true;
			}

			JobDef currentJob = pawn.CurJobDef;
			if (currentJob != null
				&& currentJob != ToddlersExpansionJobDefOf.RimTalk_ToddlerOuting
				&& currentJob != JobDefOf.Wait
				&& currentJob != JobDefOf.Wait_Wander
				&& currentJob != JobDefOf.Wait_MaintainPosture)
			{
				if (currentJob != JobDefOf.Goto || pawn.CurJob?.targetA.Cell != Spot)
				{
					return true;
				}
			}

			return false;
		}

		private bool IsWeatherAcceptable(Map map)
		{
			if (map == null)
			{
				return false;
			}

			Room room = Spot.GetRoom(map);
			if (room != null && !room.PsychologicallyOutdoors)
			{
				return true;
			}

			Pawn pawn = Participants.FirstOrDefault(p => p?.Pawn != null)?.Pawn;
			return pawn == null || JoyUtility.EnjoyableOutsideNow(pawn);
		}

		private void TriggerRimTalk()
		{
			if (!RimTalkCompatUtility.IsRimTalkActive || Participants.Count < 2)
			{
				return;
			}

			Pawn first = Participants.Select(p => p.Pawn).Where(p => p != null && p.Spawned && !p.Dead).InRandomOrder().FirstOrDefault();
			if (first == null)
			{
				return;
			}

			Pawn second = Participants.Select(p => p.Pawn).Where(p => p != null && p != first && p.Spawned && !p.Dead).InRandomOrder().FirstOrDefault();
			if (second == null)
			{
				return;
			}

			if (first.Position.DistanceToSquared(second.Position) > 144f)
			{
				return;
			}

			string prompt = $"Toddler outing: {first.LabelShort} and {second.LabelShort} are chatting during a group outing.";
			RimTalkCompatUtility.TryQueueTalk(first, second, prompt, "Event");
		}
	}
}
