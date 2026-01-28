using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	internal sealed class ToddlerOutingMapComponent : MapComponent
	{
		private const int SessionCheckIntervalTicks = 400;
		private const int SessionDurationTicks = 3200;
		private const int SessionCooldownTicks = 2000;
		private const int TalkIntervalTicks = 1200;
		private const int MinParticipantsValue = 2;
		private const int MaxParticipantsValue = 6;
		private const int LateArrivalTicksValue = 2400;
		private const int OrderCooldownTicks = 200;
		private const int ReturnJobExpiryTicks = 800;
		private const int IdleJobExpiryMinTicks = 180;
		private const int IdleJobExpiryMaxTicks = 360;
		private const float ParticipantSearchRadius = 30f;
		private const float GatherSpotSearchRadius = 6f;
		private const float LostDistanceValue = 12f;
		private const float ArrivedDistanceValue = 4f;

		private int _nextSessionTick;
		private int _nextStartTick;
		private List<ToddlerOutingSession> _sessions = new List<ToddlerOutingSession>();

		public int MinParticipants => MinParticipantsValue;
		public float LostDistance => LostDistanceValue;
		public float ArrivedDistance => ArrivedDistanceValue;
		public int LateArrivalTicks => LateArrivalTicksValue;

		public ToddlerOutingMapComponent(Map map) : base(map)
		{
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref _nextSessionTick, "nextSessionTick");
			Scribe_Values.Look(ref _nextStartTick, "nextStartTick");
			Scribe_Collections.Look(ref _sessions, "outingSessions", LookMode.Deep);

			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				if (_sessions == null)
				{
					_sessions = new List<ToddlerOutingSession>();
				}

				for (int i = _sessions.Count - 1; i >= 0; i--)
				{
					ToddlerOutingSession session = _sessions[i];
					if (session == null)
					{
						_sessions.RemoveAt(i);
						continue;
					}

					session.PostLoadInit();
				}
			}
		}

		public override void MapComponentTick()
		{
			base.MapComponentTick();
			if (map == null || Find.TickManager == null)
			{
				return;
			}

			int tick = Find.TickManager.TicksGame;
			if (tick < _nextSessionTick)
			{
				return;
			}

			_nextSessionTick = tick + SessionCheckIntervalTicks;
			for (int i = _sessions.Count - 1; i >= 0; i--)
			{
				ToddlerOutingSession session = _sessions[i];
				if (session == null)
				{
					_sessions.RemoveAt(i);
					continue;
				}

				session.Tick(map, tick, this);
				if (session.IsEnded)
				{
					EndSession(session);
					_sessions.RemoveAt(i);
				}
			}
		}

		public bool TryStartOuting(Pawn initiator, out ToddlerOutingSession session)
		{
			session = null;
			if (!CanInitiateOuting(initiator))
			{
				return false;
			}

			if (TryGetParticipant(initiator, out ToddlerOutingSession existing, out _))
			{
				if (existing != null && !existing.IsEnded)
				{
					session = existing;
					return true;
				}
			}

			int tick = Find.TickManager.TicksGame;
			if (tick < _nextStartTick)
			{
				return false;
			}

			if (TryJoinExistingSession(initiator, tick, out session))
			{
				_nextStartTick = tick + SessionCooldownTicks;
				return true;
			}

			if (HasActiveSession())
			{
				return false;
			}

			if (!TryFindOutingSpot(initiator, out IntVec3 spot))
			{
				return false;
			}

			List<Pawn> participants = CollectParticipants(initiator);
			if (participants.Count < MinParticipantsValue)
			{
				return false;
			}

			session = new ToddlerOutingSession(spot, tick, SessionDurationTicks, TalkIntervalTicks);
			session.InitializeParticipants(participants, tick);
			_sessions.Add(session);
			_nextStartTick = tick + SessionCooldownTicks;

			IssueOutingJobs(session, initiator, includeInitiator: false);
			return true;
		}

		public Job CreateOutingJob(Pawn pawn, ToddlerOutingSession session)
		{
			if (pawn == null || session == null)
			{
				return null;
			}

			Job job = JobMaker.MakeJob(ToddlersExpansionJobDefOf.RimTalk_ToddlerOuting, session.Spot);
			job.ignoreJoyTimeAssignment = true;
			job.expiryInterval = Rand.Range(1800, 3200);
			return job;
		}

		public bool TryGetSessionForPawn(Pawn pawn, out ToddlerOutingSession session)
		{
			session = null;
			if (pawn == null)
			{
				return false;
			}

			for (int i = 0; i < _sessions.Count; i++)
			{
				ToddlerOutingSession entry = _sessions[i];
				if (entry != null && entry.TryGetParticipant(pawn, out _))
				{
					session = entry;
					return true;
				}
			}

			return false;
		}

		public bool TryGetParticipant(Pawn pawn, out ToddlerOutingSession session, out ToddlerOutingParticipant participant)
		{
			session = null;
			participant = null;
			if (pawn == null)
			{
				return false;
			}

			for (int i = 0; i < _sessions.Count; i++)
			{
				ToddlerOutingSession entry = _sessions[i];
				if (entry != null && entry.TryGetParticipant(pawn, out participant))
				{
					session = entry;
					return true;
				}
			}

			return false;
		}

		internal void TryIssueOutingJob(Pawn pawn, ToddlerOutingSession session, ToddlerOutingParticipant participant, int tick)
		{
			if (pawn == null || session == null || pawn.jobs == null)
			{
				return;
			}

			if (!CanAssignJob(pawn))
			{
				return;
			}

			if (pawn.CurJobDef == JobDefOf.Goto && pawn.CurJob?.targetA.Cell == session.Spot)
			{
				return;
			}

			if (participant != null && tick - participant.LastOrderTick < OrderCooldownTicks)
			{
				return;
			}

			Job job = CreateOutingJob(pawn, session);
			if (job == null)
			{
				return;
			}

			pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
			if (participant != null)
			{
				participant.LastOrderTick = tick;
			}
		}

		internal void TryIssueReturnToSpot(Pawn pawn, IntVec3 spot, ToddlerOutingParticipant participant, int tick)
		{
			if (pawn == null || pawn.jobs == null)
			{
				return;
			}

			if (!CanAssignJob(pawn))
			{
				return;
			}

			if (pawn.CurJobDef == JobDefOf.Goto && pawn.CurJob?.targetA.Cell == spot)
			{
				return;
			}

			if (participant != null && tick - participant.LastOrderTick < OrderCooldownTicks)
			{
				return;
			}

			if (!spot.IsValid || !spot.InBounds(map))
			{
				return;
			}

			if (!pawn.CanReach(spot, PathEndMode.OnCell, Danger.Some))
			{
				return;
			}

			Job job = JobMaker.MakeJob(JobDefOf.Goto, spot);
			job.expiryInterval = ReturnJobExpiryTicks;
			pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
			if (participant != null)
			{
				participant.LastOrderTick = tick;
			}
		}

		private static bool CanAssignJob(Pawn pawn)
		{
			if (pawn.Downed || pawn.Drafted || pawn.InMentalState)
			{
				return false;
			}

			if (pawn.IsPrisonerOfColony || pawn.IsPrisoner)
			{
				return false;
			}

			if (!pawn.Awake())
			{
				return false;
			}

			if (pawn.jobs?.curJob?.playerForced ?? false)
			{
				return false;
			}

			return true;
		}

		private void EndSession(ToddlerOutingSession session)
		{
			if (session?.Participants == null)
			{
				return;
			}

			for (int i = 0; i < session.Participants.Count; i++)
			{
				ToddlerOutingParticipant participant = session.Participants[i];
				if (participant?.Pawn == null || participant.Pawn.jobs == null)
				{
					continue;
				}

				if (participant.Pawn.jobs.curJob?.playerForced ?? false)
				{
					continue;
				}

				QueueReturnJobs(participant.Pawn, participant.ReturnCell);
			}
		}

		private void QueueReturnJobs(Pawn pawn, IntVec3 returnCell)
		{
			if (pawn == null || pawn.jobs?.jobQueue == null)
			{
				return;
			}

			if (pawn.Downed || pawn.Drafted || pawn.InMentalState)
			{
				return;
			}

			if (pawn.jobs.curJob?.playerForced ?? false)
			{
				return;
			}

			if (returnCell.IsValid && returnCell.InBounds(map) && returnCell.Standable(map))
			{
				if (pawn.CanReach(returnCell, PathEndMode.OnCell, Danger.Some))
				{
					Job goBack = JobMaker.MakeJob(JobDefOf.Goto, returnCell);
					goBack.expiryInterval = ReturnJobExpiryTicks;
					pawn.jobs.jobQueue.EnqueueLast(goBack);
				}
			}

			Job idle = JobMaker.MakeJob(JobDefOf.Wait);
			idle.expiryInterval = Rand.Range(IdleJobExpiryMinTicks, IdleJobExpiryMaxTicks);
			pawn.jobs.jobQueue.EnqueueLast(idle);
		}

		private void IssueOutingJobs(ToddlerOutingSession session, Pawn initiator, bool includeInitiator)
		{
			if (session?.Participants == null)
			{
				return;
			}

			int tick = Find.TickManager.TicksGame;
			for (int i = 0; i < session.Participants.Count; i++)
			{
				ToddlerOutingParticipant participant = session.Participants[i];
				Pawn pawn = participant?.Pawn;
				if (pawn == null)
				{
					continue;
				}

				if (!includeInitiator && pawn == initiator)
				{
					continue;
				}

				TryIssueOutingJob(pawn, session, participant, tick);
			}
		}

		private bool CanInitiateOuting(Pawn pawn)
		{
			if (pawn == null || pawn.Map != map)
			{
				return false;
			}

			if (pawn.Downed || pawn.Drafted || pawn.InMentalState)
			{
				return false;
			}

			if (!pawn.Awake())
			{
				return false;
			}

			if (PawnUtility.WillSoonHaveBasicNeed(pawn, 0f))
			{
				return false;
			}

			if (pawn.jobs?.curJob?.playerForced ?? false)
			{
				return false;
			}

			return true;
		}

		private bool TryJoinExistingSession(Pawn pawn, int tick, out ToddlerOutingSession session)
		{
			session = null;
			for (int i = 0; i < _sessions.Count; i++)
			{
				ToddlerOutingSession entry = _sessions[i];
				if (entry == null || entry.IsEnded || entry.Participants == null)
				{
					continue;
				}

				if (entry.Participants.Count >= MaxParticipantsValue)
				{
					continue;
				}

				if (!pawn.Position.InHorDistOf(entry.Spot, ParticipantSearchRadius))
				{
					continue;
				}

				if (!IsEligibleParticipant(pawn, pawn))
				{
					continue;
				}

				if (entry.AddParticipant(pawn, tick))
				{
					session = entry;
					IssueOutingJobs(entry, pawn, includeInitiator: false);
					return true;
				}
			}

			return false;
		}

		private bool HasActiveSession()
		{
			for (int i = 0; i < _sessions.Count; i++)
			{
				ToddlerOutingSession session = _sessions[i];
				if (session != null && !session.IsEnded)
				{
					return true;
				}
			}

			return false;
		}

		private bool TryFindOutingSpot(Pawn pawn, out IntVec3 spot)
		{
			spot = IntVec3.Invalid;
			if (pawn == null || map == null)
			{
				return false;
			}

			if (TryFindGatherSpot(pawn, out spot))
			{
				return true;
			}

			if (NatureRunningUtility.TryFindNatureInterestTarget(pawn, out LocalTargetInfo interestTarget))
			{
				spot = interestTarget.Cell;
				return spot.IsValid;
			}

			return CellFinder.TryFindRandomCellNear(pawn.Position, map, 14, cell => IsValidOutingCell(cell, pawn), out spot);
		}

		private bool TryFindGatherSpot(Pawn pawn, out IntVec3 spot)
		{
			spot = IntVec3.Invalid;
			List<CompGatherSpot> spots = map?.gatherSpotLister?.activeSpots;
			if (spots == null || spots.Count == 0)
			{
				return false;
			}

			foreach (CompGatherSpot gatherSpot in spots.InRandomOrder())
			{
				if (gatherSpot == null || !gatherSpot.Active)
				{
					continue;
				}

				IntVec3 root = gatherSpot.parent.Position;
				if (CellFinder.TryFindRandomCellNear(root, map, (int)GatherSpotSearchRadius, cell => IsValidOutingCell(cell, pawn), out spot))
				{
					return true;
				}
			}

			return false;
		}

		private static bool IsValidOutingCell(IntVec3 cell, Pawn pawn)
		{
			if (pawn?.Map == null)
			{
				return false;
			}

			Map map = pawn.Map;
			if (!cell.InBounds(map) || !cell.Standable(map))
			{
				return false;
			}

			if (cell.IsForbidden(pawn))
			{
				return false;
			}

			if (!pawn.CanReach(cell, PathEndMode.OnCell, Danger.Some))
			{
				return false;
			}

			return true;
		}

		private List<Pawn> CollectParticipants(Pawn initiator)
		{
			List<Pawn> result = new List<Pawn> { initiator };
			List<Pawn> pawns = map.mapPawns.SpawnedPawnsInFaction(initiator.Faction);
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				if (pawn == null || pawn == initiator)
				{
					continue;
				}

				if (!pawn.Position.InHorDistOf(initiator.Position, ParticipantSearchRadius))
				{
					continue;
				}

				if (!IsEligibleParticipant(pawn, initiator))
				{
					continue;
				}

				result.Add(pawn);
				if (result.Count >= MaxParticipantsValue)
				{
					break;
				}
			}

			return result;
		}

		private bool IsEligibleParticipant(Pawn pawn, Pawn initiator)
		{
			if (pawn == null || pawn.Map != map)
			{
				return false;
			}

			if (pawn.Downed || pawn.Drafted || pawn.InMentalState)
			{
				return false;
			}

			if (!pawn.Awake())
			{
				return false;
			}

			if (pawn.health?.capacities == null || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving))
			{
				return false;
			}

			bool isChild = pawn.DevelopmentalStage.Child();
			bool isToddler = ToddlersCompatUtility.IsToddler(pawn);
			if (!isChild && !isToddler)
			{
				return false;
			}

			if (isToddler && ToddlerCarryingUtility.IsBeingCarried(pawn))
			{
				return false;
			}

			if (PawnUtility.WillSoonHaveBasicNeed(pawn, 0f))
			{
				return false;
			}

			if (pawn.jobs?.curJob?.playerForced ?? false)
			{
				return false;
			}

			if (pawn.timetable != null)
			{
				TimeAssignmentDef assignment = pawn.timetable.CurrentAssignment;
				if (assignment == TimeAssignmentDefOf.Sleep || assignment == TimeAssignmentDefOf.Work)
				{
					return false;
				}
			}

			if (initiator != null && pawn.relations != null)
			{
				int opinion = pawn.relations.OpinionOf(initiator);
				if (opinion < -20)
				{
					return false;
				}
			}

			JobDef currentJob = pawn.CurJobDef;
			if (currentJob != null
				&& currentJob != JobDefOf.Wait
				&& currentJob != JobDefOf.Wait_Wander
				&& currentJob != JobDefOf.Wait_MaintainPosture)
			{
				return false;
			}

			return true;
		}
	}
}
