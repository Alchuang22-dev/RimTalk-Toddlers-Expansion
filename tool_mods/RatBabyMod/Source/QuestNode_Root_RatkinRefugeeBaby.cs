using System;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace RatBabyMod
{
	public class QuestNode_Root_RatkinRefugeeBaby : QuestNode_Root_WandererJoin
	{
		private const float MinBabyAgeYears = 0.01f;
		private const float MaxBabyAgeYears = 0.99f;
		private const string DeadMotherFlag = "ratBabyHasDeadMother";
		private const string InjuredMotherFlag = "ratBabyHasInjuredMother";
		private const string MouseGuardianFlag = "ratBabyHasMouseGuardian";

		protected override bool TestRunInt(Slate slate)
		{
			return ModsConfig.BiotechActive
				&& Find.Storyteller.difficulty.ChildrenAllowed
				&& RatBabyResolver.TryResolve(out _);
		}

		protected override void RunInt()
		{
			if (!RatBabyResolver.TryResolve(out _))
			{
				return;
			}

			base.RunInt();
		}

		public override Pawn GeneratePawn()
		{
			if (!RatBabyResolver.TryResolve(out RatBabyResolver.ResolvedDefs resolved))
			{
				return null;
			}

			PawnGenerationRequest request = new PawnGenerationRequest(
				resolved.BabyKindDef,
				resolved.Faction,
				tile: new PlanetTile?((PlanetTile)(-1)),
				forceGenerateNewPawn: true,
				allowDowned: true,
				forceNoIdeo: true,
				forceRecruitable: true)
			{
				AllowedDevelopmentalStages = DevelopmentalStage.Baby,
				KindDef = resolved.BabyKindDef
			};

			Pawn pawn = PawnGenerator.GeneratePawn(request);
			SetBabyAgeUnderOneYear(pawn);
			pawn.SetFaction(null);
			return pawn;
		}

		protected override void AddSpawnPawnQuestParts(Quest quest, Map map, Pawn pawn)
		{
			if (pawn == null || !RatBabyResolver.TryResolve(out RatBabyResolver.ResolvedDefs resolved))
			{
				return;
			}

			IntVec3 babyCell = FindSpawnCell(map);
			IntVec3 nearbyCell = FindNearbySpawnCell(map, babyCell);
			int branch = Rand.RangeInclusive(0, 2);

			QuestGen.slate.Set(DeadMotherFlag, false);
			QuestGen.slate.Set(InjuredMotherFlag, false);
			QuestGen.slate.Set(MouseGuardianFlag, false);

			switch (branch)
			{
				case 0:
					SpawnDeadMother(map, pawn, resolved, nearbyCell);
					QuestGen.slate.Set(DeadMotherFlag, true);
					break;
				case 1:
					SpawnInjuredMother(map, pawn, resolved, nearbyCell);
					QuestGen.slate.Set(InjuredMotherFlag, true);
					break;
				default:
					SpawnMouseGuardian(map, resolved, nearbyCell);
					QuestGen.slate.Set(MouseGuardianFlag, true);
					break;
			}

			GenSpawn.Spawn(pawn, babyCell, map);
		}

		[Obsolete]
		public override void SendLetter(Quest quest, Pawn pawn)
		{
			TaggedString title = "RatBaby_LetterLabel".Translate();
			TaggedString text = "RatBaby_LetterBase".Translate(pawn.Named("PAWN")).AdjustedFor(pawn);

			if (QuestGen.slate.Get(DeadMotherFlag, false))
			{
				text = "RatBaby_LetterDeadMother".Translate(pawn.Named("PAWN")).AdjustedFor(pawn);
			}
			else if (QuestGen.slate.Get(InjuredMotherFlag, false))
			{
				text = "RatBaby_LetterInjuredMother".Translate(pawn.Named("PAWN")).AdjustedFor(pawn);
			}
			else if (QuestGen.slate.Get(MouseGuardianFlag, false))
			{
				text = "RatBaby_LetterMouseGuardian".Translate(pawn.Named("PAWN")).AdjustedFor(pawn);
			}

			QuestNode_Root_WandererJoin_WalkIn.AppendCharityInfoToLetter("JoinerCharityInfo".Translate((NamedArgument)pawn), ref text);
			PawnRelationUtility.TryAppendRelationsWithColonistsInfo(ref text, ref title, pawn);
			Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.NeutralEvent, new TargetInfo(pawn));
		}

		private static IntVec3 FindSpawnCell(Map map)
		{
			if (CellFinder.TryFindRandomEdgeCellWith(
				cell => map.reachability.CanReachColony(cell) && !cell.Fogged(map),
				map,
				CellFinder.EdgeRoadChance_Neutral,
				out IntVec3 result))
			{
				return result;
			}

			return CellFinder.RandomClosewalkCellNear(map.Center, map, 8);
		}

		private static IntVec3 FindNearbySpawnCell(Map map, IntVec3 center)
		{
			if (CellFinder.TryFindRandomCellNear(
				center,
				map,
				2,
				cell => cell.Standable(map) && map.reachability.CanReachColony(cell) && !cell.Fogged(map),
				out IntVec3 result))
			{
				return result;
			}

			return center;
		}

		private static void SpawnDeadMother(Map map, Pawn baby, RatBabyResolver.ResolvedDefs resolved, IntVec3 cell)
		{
			Pawn mother = GenerateMother(resolved, allowDead: true);
			if (mother == null)
			{
				return;
			}

			baby.relations.AddDirectRelation(PawnRelationDefOf.Parent, mother);
			HealthUtility.DamageUntilDead(mother);
			TryApplyCareHediffs(mother, severe: true);
			if (mother.Corpse != null)
			{
				GenSpawn.Spawn(mother.Corpse, cell, map);
			}
		}

		private static void SpawnInjuredMother(Map map, Pawn baby, RatBabyResolver.ResolvedDefs resolved, IntVec3 cell)
		{
			Pawn mother = GenerateMother(resolved, allowDead: false);
			if (mother == null)
			{
				return;
			}

			baby.relations.AddDirectRelation(PawnRelationDefOf.Parent, mother);
			HealthUtility.DamageUntilDowned(mother);
			TryApplyCareHediffs(mother, severe: false);
			if (mother.needs?.food != null)
			{
				mother.needs.food.CurLevelPercentage = 0.08f;
			}
			if (mother.needs?.rest != null)
			{
				mother.needs.rest.CurLevelPercentage = Rand.Range(0.02f, 0.12f);
			}

			if (mother.guest != null)
			{
				mother.guest.Recruitable = true;
			}

			mother.mindState.WillJoinColonyIfRescued = true;
			GenSpawn.Spawn(mother, cell, map);
		}

		private static void SpawnMouseGuardian(Map map, RatBabyResolver.ResolvedDefs resolved, IntVec3 cell)
		{
			Pawn mouse = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
				resolved.AnimalKindDef,
				tile: new PlanetTile?((PlanetTile)(-1)),
				forceGenerateNewPawn: true));

			if (mouse == null)
			{
				return;
			}

			if (mouse.RaceProps?.Animal == true && mouse.training != null)
			{
				mouse.training.Train(TrainableDefOf.Obedience, null, complete: true);
				mouse.training.Train(TrainableDefOf.Release, null, complete: true);
			}

			GenSpawn.Spawn(mouse, cell, map);
		}

		private static Pawn GenerateMother(RatBabyResolver.ResolvedDefs resolved, bool allowDead)
		{
			PawnGenerationRequest request = new PawnGenerationRequest(
				resolved.MotherKindDef,
				resolved.Faction,
				tile: new PlanetTile?((PlanetTile)(-1)),
				forceGenerateNewPawn: true,
				allowDead: allowDead)
			{
				KindDef = resolved.MotherKindDef,
				FixedGender = Gender.Female
			};

			return PawnGenerator.GeneratePawn(request);
		}

		private static void TryApplyCareHediffs(Pawn pawn, bool severe)
		{
			HediffDef lactating = DefDatabase<HediffDef>.GetNamedSilentFail("Lactating");
			HediffDef malnutrition = DefDatabase<HediffDef>.GetNamedSilentFail("Malnutrition");
			HediffDef bloodLoss = HediffDefOf.BloodLoss;

			if (lactating != null)
			{
				HealthUtility.AdjustSeverity(pawn, lactating, Rand.Range(0.12f, 0.35f));
			}

			if (bloodLoss != null)
			{
				HealthUtility.AdjustSeverity(pawn, bloodLoss, severe ? 0.9f : Rand.Range(0.35f, 0.6f));
			}

			if (malnutrition != null)
			{
				HealthUtility.AdjustSeverity(pawn, malnutrition, severe ? 0.5f : Rand.Range(0.15f, 0.3f));
			}

			if (!severe)
			{
				HediffDef infection = DefDatabase<HediffDef>.GetNamedSilentFail("Infection");
				if (infection != null)
				{
					HealthUtility.AdjustSeverity(pawn, infection, Rand.Range(0.12f, 0.22f));
				}
			}
		}

		private static void SetBabyAgeUnderOneYear(Pawn pawn)
		{
			if (pawn?.ageTracker == null)
			{
				return;
			}

			float ageYears = Rand.Range(MinBabyAgeYears, MaxBabyAgeYears);
			long ageTicks = (long)(ageYears * GenDate.TicksPerYear);
			pawn.ageTracker.AgeBiologicalTicks = ageTicks;
			pawn.ageTracker.AgeChronologicalTicks = ageTicks;
		}
	}
}
