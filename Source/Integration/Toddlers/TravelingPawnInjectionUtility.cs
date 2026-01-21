using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public static class TravelingPawnInjectionUtility
	{
		private const int MinBatchCount = 1;
		private const int MaxBatchCount = 5;
		private const float ExtraBatchChance = 0.5f;
		private const float ChildBiasWhenAlreadyPresent = 0.7f;
		private const float WalkingToddlerSeverity = 0.6f;

		private static bool _walkHediffChecked;
		private static HediffDef _learningToWalkDef;

		public static void TryInjectToddlerOrChildPawns(PawnGroupMakerParms parms, ref IEnumerable<Pawn> pawns)
		{
			if (!IsEligibleGroup(parms) || pawns == null)
			{
				return;
			}

			List<Pawn> pawnList = pawns as List<Pawn> ?? pawns.ToList();
			if (pawnList.Count == 0 || !HasAdultLeader(pawnList))
			{
				return;
			}

			PawnKindDef baseKind = GetBaseKind(pawnList, parms);
			if (baseKind == null)
			{
				return;
			}

			Pawn samplePawn = GetSamplePawn(pawnList);
			bool hasChildAlready = pawnList.Any(IsChildPawn);
			float childChance = hasChildAlready ? ChildBiasWhenAlreadyPresent : 0.5f;
			int addCount = RollStackedCount();
			if (addCount <= 0)
			{
				return;
			}

			int added = 0;
			bool addedToddler = false;
			for (int i = 0; i < addCount; i++)
			{
				bool wantChild = !ToddlersCompatUtility.IsToddlersActive || Rand.Value < childChance;
				if (!TryGetTargetAgeYears(baseKind, samplePawn, wantChild, out float ageYears))
				{
					continue;
				}

				Pawn pawn = GeneratePawn(baseKind, parms, ageYears);
				if (pawn != null)
				{
					pawnList.Add(pawn);
					added++;
					if (!wantChild && ToddlersCompatUtility.IsToddler(pawn))
					{
						addedToddler = true;
					}
				}
			}

			if (added > 0)
			{
				if (addedToddler)
				{
					EnsureWalkDef();
					EnsureWalkingToddlers(pawnList);
				}

				pawns = pawnList;
				if (Prefs.DevMode)
				{
					string kind = parms?.groupKind?.defName ?? "UnknownGroup";
					string faction = parms?.faction?.Name ?? "NoFaction";
					Log.Message($"[RimTalk_ToddlersExpansion] Added {added} toddler/child pawns to {kind} group ({faction}).");
				}
			}
		}

		private static bool IsEligibleGroup(PawnGroupMakerParms parms)
		{
			if (parms == null || parms.inhabitants)
			{
				return false;
			}

			if (parms.faction == null || parms.faction == Faction.OfPlayer)
			{
				return false;
			}

			if (parms.raidStrategy != null)
			{
				return false;
			}

			if (parms.faction.HostileTo(Faction.OfPlayer))
			{
				return false;
			}

			PawnGroupKindDef kind = parms.groupKind;
			if (kind == null)
			{
				return false;
			}

			if (kind == PawnGroupKindDefOf.Combat || kind == PawnGroupKindDefOf.Settlement)
			{
				return false;
			}

			return kind == PawnGroupKindDefOf.Trader
				|| kind == PawnGroupKindDefOf.Peaceful
				|| kind.defName == "Visitor"
				|| kind.defName == "Traveler";
		}

		private static int RollStackedCount()
		{
			int count = Rand.RangeInclusive(MinBatchCount, MaxBatchCount);
			while (Rand.Chance(ExtraBatchChance))
			{
				count += Rand.RangeInclusive(MinBatchCount, MaxBatchCount);
			}

			return count;
		}

		private static bool HasAdultLeader(List<Pawn> pawns)
		{
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				if (pawn?.RaceProps?.Humanlike != true)
				{
					continue;
				}

				if (pawn.DevelopmentalStage.Newborn() || pawn.DevelopmentalStage.Baby())
				{
					continue;
				}

				if (ToddlersCompatUtility.IsToddler(pawn) || pawn.DevelopmentalStage == DevelopmentalStage.Child)
				{
					continue;
				}

				return true;
			}

			return false;
		}

		private static PawnKindDef GetBaseKind(List<Pawn> pawns, PawnGroupMakerParms parms)
		{
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				if (pawn?.RaceProps?.Humanlike == true && pawn.kindDef != null)
				{
					return pawn.kindDef;
				}
			}

			return parms?.faction?.def?.basicMemberKind;
		}

		private static Pawn GetSamplePawn(List<Pawn> pawns)
		{
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				if (pawn?.RaceProps?.Humanlike == true)
				{
					return pawn;
				}
			}

			return pawns.Count > 0 ? pawns[0] : null;
		}

		private static bool TryGetTargetAgeYears(PawnKindDef kind, Pawn samplePawn, bool wantChild, out float ageYears)
		{
			ageYears = 0f;
			if (kind?.RaceProps == null)
			{
				return false;
			}

			if (wantChild)
			{
				if (!ModsConfig.BiotechActive)
				{
					return false;
				}

				if (!TryGetChildAgeRange(kind, out float minAge, out float maxAge))
				{
					return false;
				}

				ageYears = Rand.Range(minAge, maxAge);
				return true;
			}

			if (!ToddlersCompatUtility.IsToddlersActive)
			{
				return false;
			}

			float minToddler = ToddlersCompatUtility.GetToddlerMinAgeYears(samplePawn);
			float maxToddler = ToddlersCompatUtility.GetToddlerEndAgeYears(samplePawn);
			if (maxToddler <= minToddler)
			{
				return false;
			}

			ageYears = Rand.Range(minToddler, maxToddler);
			return true;
		}

		private static bool TryGetChildAgeRange(PawnKindDef kind, out float minAge, out float maxAge)
		{
			minAge = 0f;
			maxAge = 0f;

			List<LifeStageAge> ages = kind?.RaceProps?.lifeStageAges;
			if (ages == null || ages.Count == 0)
			{
				return false;
			}

			for (int i = 0; i < ages.Count; i++)
			{
				LifeStageAge stageAge = ages[i];
				if (stageAge?.def?.developmentalStage != DevelopmentalStage.Child)
				{
					continue;
				}

				minAge = stageAge.minAge;
				maxAge = i + 1 < ages.Count ? ages[i + 1].minAge - 0.01f : minAge + 1f;
				if (maxAge <= minAge)
				{
					maxAge = minAge + 0.5f;
				}

				return true;
			}

			return false;
		}

		private static Pawn GeneratePawn(PawnKindDef kind, PawnGroupMakerParms parms, float ageYears)
		{
			try
			{
				PawnGenerationRequest request = new PawnGenerationRequest(
					kind,
					parms.faction,
					PawnGenerationContext.NonPlayer,
					tile: parms.tile,
					forceGenerateNewPawn: true,
					fixedBiologicalAge: ageYears,
					fixedChronologicalAge: ageYears);

				return PawnGenerator.GeneratePawn(request);
			}
			catch
			{
				return null;
			}
		}

		private static bool IsChildPawn(Pawn pawn)
		{
			return pawn != null && pawn.DevelopmentalStage == DevelopmentalStage.Child;
		}

		private static void EnsureWalkDef()
		{
			if (_walkHediffChecked)
			{
				return;
			}

			_walkHediffChecked = true;
			_learningToWalkDef = DefDatabase<HediffDef>.GetNamedSilentFail("LearningToWalk");
		}

		private static void EnsureWalkingToddlers(List<Pawn> pawns)
		{
			if (_learningToWalkDef == null || pawns == null)
			{
				return;
			}

			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				if (!ToddlersCompatUtility.IsToddler(pawn))
				{
					continue;
				}

				Hediff hediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(_learningToWalkDef);
				if (hediff == null && pawn.health != null)
				{
					hediff = pawn.health.AddHediff(_learningToWalkDef);
				}

				if (hediff != null && hediff.Severity < WalkingToddlerSeverity)
				{
					hediff.Severity = WalkingToddlerSeverity;
				}
			}
		}
	}
}
