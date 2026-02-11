using System.Collections.Generic;
using LudeonTK;
using RimWorld;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers.HAR
{
	public sealed class HarNurseryMoodGameComponent : GameComponent
	{
		private const string LogPrefix = "[RimTalk_ToddlersExpansion][HAR Nursery]";
		private const int ScanIntervalTicks = GenDate.TicksPerDay;
		private const int LowTierThreshold = 5;
		private const int HighTierThreshold = 10;

		private int _nextScanTick;

		private static readonly HarRaceWhitelistUtility.MiliraAlignedRaceGroup[] SupportedGroups =
		{
			HarRaceWhitelistUtility.MiliraAlignedRaceGroup.Ratkin,
			HarRaceWhitelistUtility.MiliraAlignedRaceGroup.Kiiro,
			HarRaceWhitelistUtility.MiliraAlignedRaceGroup.MoeLotl,
			HarRaceWhitelistUtility.MiliraAlignedRaceGroup.Milira,
			HarRaceWhitelistUtility.MiliraAlignedRaceGroup.Bunny,
			HarRaceWhitelistUtility.MiliraAlignedRaceGroup.Cinder
		};

		private static readonly Dictionary<HarRaceWhitelistUtility.MiliraAlignedRaceGroup, string> LowTierThoughtDefNames =
			new Dictionary<HarRaceWhitelistUtility.MiliraAlignedRaceGroup, string>
			{
				{ HarRaceWhitelistUtility.MiliraAlignedRaceGroup.Ratkin, "RimTalk_ManyRatkinBabies" },
				{ HarRaceWhitelistUtility.MiliraAlignedRaceGroup.Kiiro, "RimTalk_ManyKiiroBabies" },
				{ HarRaceWhitelistUtility.MiliraAlignedRaceGroup.MoeLotl, "RimTalk_ManyMoeLotlBabies" },
				{ HarRaceWhitelistUtility.MiliraAlignedRaceGroup.Milira, "RimTalk_ManyMiliraBabies" },
				{ HarRaceWhitelistUtility.MiliraAlignedRaceGroup.Bunny, "RimTalk_ManyBunnyBabies" },
				{ HarRaceWhitelistUtility.MiliraAlignedRaceGroup.Cinder, "RimTalk_ManyCinderBabies" }
			};

		private static readonly Dictionary<HarRaceWhitelistUtility.MiliraAlignedRaceGroup, string> HighTierThoughtDefNames =
			new Dictionary<HarRaceWhitelistUtility.MiliraAlignedRaceGroup, string>
			{
				{ HarRaceWhitelistUtility.MiliraAlignedRaceGroup.Ratkin, "RimTalk_LiveInRatkinNursery" },
				{ HarRaceWhitelistUtility.MiliraAlignedRaceGroup.Kiiro, "RimTalk_LiveInKiiroNursery" },
				{ HarRaceWhitelistUtility.MiliraAlignedRaceGroup.MoeLotl, "RimTalk_LiveInMoeLotlNursery" },
				{ HarRaceWhitelistUtility.MiliraAlignedRaceGroup.Milira, "RimTalk_LiveInMiliraNursery" },
				{ HarRaceWhitelistUtility.MiliraAlignedRaceGroup.Bunny, "RimTalk_LiveInBunnyNursery" },
				{ HarRaceWhitelistUtility.MiliraAlignedRaceGroup.Cinder, "RimTalk_LiveInCinderNursery" }
			};

		public HarNurseryMoodGameComponent(Game game)
		{
		}

		public override void StartedNewGame()
		{
			base.StartedNewGame();
			_nextScanTick = 0;
		}

		public override void LoadedGame()
		{
			base.LoadedGame();
			_nextScanTick = 0;
		}

		public override void GameComponentTick()
		{
			base.GameComponentTick();

			if (Find.TickManager == null || Find.Maps == null || Find.Maps.Count == 0)
			{
				return;
			}

			int currentTick = Find.TickManager.TicksGame;
			if (currentTick < _nextScanTick)
			{
				return;
			}

			_nextScanTick = currentTick + ScanIntervalTicks;
			ScanAndApplyNurseryThoughts();
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref _nextScanTick, "harNurseryMoodNextScanTick");
		}

		[DebugAction("RimTalk Toddlers", "Force Scan HAR Nursery Thoughts", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void DebugForceScanHarNurseryThoughts()
		{
			if (Current.Game == null || Find.TickManager == null)
			{
				Messages.Message("[RimTalk Toddlers] Game not ready for HAR nursery thought scan.", MessageTypeDefOf.RejectInput);
				if (Prefs.DevMode)
				{
					Log.Warning($"{LogPrefix} Debug scan aborted: game or tick manager not ready.");
				}
				return;
			}

			HarNurseryMoodGameComponent component = Current.Game.GetComponent<HarNurseryMoodGameComponent>();
			if (Prefs.DevMode)
			{
				Log.Message($"{LogPrefix} Debug scan triggered at tick={Find.TickManager.TicksGame}, maps={Find.Maps?.Count ?? 0}.");
			}

			ScanAndApplyNurseryThoughts(debugLog: Prefs.DevMode);

			if (component != null)
			{
				component._nextScanTick = Find.TickManager.TicksGame + ScanIntervalTicks;
				if (Prefs.DevMode)
				{
					Log.Message($"{LogPrefix} Next auto scan tick set to {component._nextScanTick}.");
				}
			}
			else if (Prefs.DevMode)
			{
				Log.Warning($"{LogPrefix} Debug scan: component instance is null.");
			}

			Messages.Message("[RimTalk Toddlers] HAR nursery thoughts scanned.", MessageTypeDefOf.NeutralEvent);
		}

		private static void ScanAndApplyNurseryThoughts(bool debugLog = false)
		{
			Dictionary<HarRaceWhitelistUtility.MiliraAlignedRaceGroup, List<Pawn>> infantsByGroup =
				new Dictionary<HarRaceWhitelistUtility.MiliraAlignedRaceGroup, List<Pawn>>();

			for (int i = 0; i < Find.Maps.Count; i++)
			{
				Map map = Find.Maps[i];
				if (map == null)
				{
					continue;
				}

				List<Pawn> colonists = map.mapPawns?.SpawnedPawnsInFaction(Faction.OfPlayer);
				if (colonists == null || colonists.Count == 0)
				{
					continue;
				}

				for (int j = 0; j < colonists.Count; j++)
				{
					Pawn pawn = colonists[j];
					if (!TryGetTrackedRaceGroup(pawn, out HarRaceWhitelistUtility.MiliraAlignedRaceGroup group))
					{
						continue;
					}

					if (!infantsByGroup.TryGetValue(group, out List<Pawn> list))
					{
						list = new List<Pawn>();
						infantsByGroup[group] = list;
					}

					list.Add(pawn);
				}
			}

			for (int i = 0; i < SupportedGroups.Length; i++)
			{
				HarRaceWhitelistUtility.MiliraAlignedRaceGroup group = SupportedGroups[i];
				int count = infantsByGroup.TryGetValue(group, out List<Pawn> infants) ? infants.Count : 0;
				ThoughtDef lowTierThought = ResolveThoughtDef(LowTierThoughtDefNames, group);
				ThoughtDef highTierThought = ResolveThoughtDef(HighTierThoughtDefNames, group);
				ThoughtDef thought = ResolveThoughtForCount(group, count);
				int withMoodMemory = 0;
				int gainedMemory = 0;
				int blockedByCanGetThought = 0;
				int blockedDetailsPrinted = 0;

				if (infants == null || infants.Count == 0)
				{
					if (debugLog)
					{
						Log.Message($"{LogPrefix} group={group}, tracked=0, thought=None");
					}
					continue;
				}

				for (int j = 0; j < infants.Count; j++)
				{
					if (ApplyThought(
						infants[j],
						lowTierThought,
						highTierThought,
						thought,
						out bool hasMoodMemory,
						out bool canGetThought,
						out string canGetThoughtFailReason))
					{
						gainedMemory += 1;
					}

					if (hasMoodMemory)
					{
						withMoodMemory += 1;
					}

					if (!canGetThought)
					{
						blockedByCanGetThought += 1;
						if (debugLog && blockedDetailsPrinted < 3)
						{
							blockedDetailsPrinted += 1;
							Log.Message(
								$"{LogPrefix} blocked pawn={infants[j]?.LabelShort ?? "null"}, stage={infants[j]?.DevelopmentalStage.ToString() ?? "null"}, reason={canGetThoughtFailReason}");
						}
					}
				}

				if (debugLog)
				{
					string thoughtName = thought?.defName ?? "None";
					Log.Message(
						$"{LogPrefix} group={group}, tracked={count}, thought={thoughtName}, withMood={withMoodMemory}, blockedCanGet={blockedByCanGetThought}, gained={gainedMemory}");
				}
			}
		}

		private static bool TryGetTrackedRaceGroup(Pawn pawn, out HarRaceWhitelistUtility.MiliraAlignedRaceGroup group)
		{
			group = HarRaceWhitelistUtility.MiliraAlignedRaceGroup.Common;
			if (pawn == null || pawn.Dead || pawn.Destroyed || pawn.Faction != Faction.OfPlayer)
			{
				return false;
			}

			bool isNewborn = pawn.DevelopmentalStage.Newborn() || pawn.DevelopmentalStage.Baby();
			bool isToddler = ToddlersCompatUtility.IsToddler(pawn);
			if (!isNewborn && !isToddler)
			{
				return false;
			}

			group = HarRaceWhitelistUtility.GetMiliraAlignedRaceGroup(pawn);
			return group != HarRaceWhitelistUtility.MiliraAlignedRaceGroup.Common;
		}

		private static ThoughtDef ResolveThoughtForCount(HarRaceWhitelistUtility.MiliraAlignedRaceGroup group, int count)
		{
			if (count >= HighTierThreshold)
			{
				return ResolveThoughtDef(HighTierThoughtDefNames, group);
			}

			if (count >= LowTierThreshold)
			{
				return ResolveThoughtDef(LowTierThoughtDefNames, group);
			}

			return null;
		}

		private static ThoughtDef ResolveThoughtDef(
			Dictionary<HarRaceWhitelistUtility.MiliraAlignedRaceGroup, string> defNames,
			HarRaceWhitelistUtility.MiliraAlignedRaceGroup group)
		{
			if (!defNames.TryGetValue(group, out string defName) || defName.NullOrEmpty())
			{
				return null;
			}

			return DefDatabase<ThoughtDef>.GetNamedSilentFail(defName);
		}

		private static bool ApplyThought(
			Pawn pawn,
			ThoughtDef lowTierThought,
			ThoughtDef highTierThought,
			ThoughtDef selectedThought,
			out bool hasMoodMemory,
			out bool canGetThought,
			out string canGetThoughtFailReason)
		{
			MemoryThoughtHandler memories = pawn?.needs?.mood?.thoughts?.memories;
			if (memories == null)
			{
				hasMoodMemory = false;
				canGetThought = false;
				canGetThoughtFailReason = "null memories handler";
				return false;
			}

			hasMoodMemory = true;
			canGetThought = true;
			canGetThoughtFailReason = string.Empty;

			if (lowTierThought != null)
			{
				memories.RemoveMemoriesOfDef(lowTierThought);
			}

			if (highTierThought != null)
			{
				memories.RemoveMemoriesOfDef(highTierThought);
			}

			if (selectedThought != null)
			{
				if (!ThoughtUtility.CanGetThought(pawn, selectedThought))
				{
					canGetThought = false;
					canGetThoughtFailReason = GetCanGetThoughtFailureReason(pawn, selectedThought);
					return false;
				}

				memories.TryGainMemory(selectedThought);
				return memories.GetFirstMemoryOfDef(selectedThought) != null;
			}

			return false;
		}

		private static string GetCanGetThoughtFailureReason(Pawn pawn, ThoughtDef thought)
		{
			if (pawn == null || thought == null)
			{
				return "pawn or thought is null";
			}

			if (!thought.developmentalStageFilter.Has(pawn.DevelopmentalStage))
			{
				return $"developmentalStageFilter mismatch (pawn={pawn.DevelopmentalStage}, filter={thought.developmentalStageFilter})";
			}

			if (thought.gender != Gender.None && pawn.gender != thought.gender && !thought.IsSocial)
			{
				return $"gender mismatch (pawn={pawn.gender}, required={thought.gender})";
			}

			if (thought.doNotApplyToQuestLodgers && pawn.IsQuestLodger())
			{
				return "quest lodger blocked by doNotApplyToQuestLodgers";
			}

			if (!thought.validWhileDespawned && !pawn.Spawned && !thought.IsMemory)
			{
				return "despawned pawn blocked";
			}

			if (pawn.story?.traits != null && pawn.story.traits.IsThoughtDisallowed(thought))
			{
				return "trait disallows thought";
			}

			return "blocked by ThoughtUtility.CanGetThought (other condition)";
		}
	}

	public static class HarNurseryMoodUtility
	{
		public static void RegisterGameComponent()
		{
			if (Current.Game == null)
			{
				return;
			}

			if (Current.Game.GetComponent<HarNurseryMoodGameComponent>() == null)
			{
				Current.Game.components.Add(new HarNurseryMoodGameComponent(Current.Game));
			}
		}
	}
}
