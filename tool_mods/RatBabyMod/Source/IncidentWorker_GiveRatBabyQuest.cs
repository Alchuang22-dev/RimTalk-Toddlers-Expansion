using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RatBabyMod
{
	public class IncidentWorker_GiveRatBabyQuest : IncidentWorker
	{
		protected override bool CanFireNowSub(IncidentParms parms)
		{
			if (!base.CanFireNowSub(parms))
			{
				return false;
			}

			if (!ModsConfig.BiotechActive || !Find.Storyteller.difficulty.ChildrenAllowed)
			{
				return false;
			}

			if (!RatBabyResolver.TryResolve(out _))
			{
				return false;
			}

			QuestScriptDef questDef = def.questScriptDef ?? parms.questScriptDef ?? RatBabyDefOf.RatBaby_RatkinOrphanQuest;
			if (questDef == null)
			{
				return false;
			}

			return (questDef.CanRun(parms.points, parms.target) || questDef.rootSelectionWeight <= 0f)
				&& CanQuestOccurOnTile(parms.target.Tile, questDef)
				&& PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists_NoSuspended.Any();
		}

		protected override bool TryExecuteWorker(IncidentParms parms)
		{
			QuestScriptDef questDef = def.questScriptDef ?? parms.questScriptDef ?? RatBabyDefOf.RatBaby_RatkinOrphanQuest;
			if (questDef == null)
			{
				return false;
			}

			Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(questDef, parms.points);
			if (!quest.hidden && questDef.sendAvailableLetter)
			{
				QuestUtility.SendLetterQuestAvailable(quest);
			}

			return true;
		}

		private static bool CanQuestOccurOnTile(PlanetTile tile, QuestScriptDef quest)
		{
			if (!tile.Valid)
			{
				return true;
			}

			if (quest != null)
			{
				PlanetLayerDef layerDef = tile.LayerDef;
				if ((!quest.layerWhitelist.NullOrEmpty() && !quest.layerWhitelist.Contains(layerDef))
					|| (!quest.layerBlacklist.NullOrEmpty() && quest.layerBlacklist.Contains(layerDef))
					|| (!quest.canOccurOnAllPlanetLayers
						&& layerDef.onlyAllowWhitelistedIncidents
						&& (quest.layerWhitelist.NullOrEmpty() || !quest.layerWhitelist.Contains(layerDef))))
				{
					return false;
				}
			}

			return !tile.LayerDef.onlyAllowWhitelistedQuests;
		}
	}
}
