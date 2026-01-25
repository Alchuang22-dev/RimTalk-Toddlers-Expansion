using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
    public class MidnightSnackGameComponent : GameComponent
    {
        private const int CheckInterval = 2500; // 每小时检查一次
        private int ticksUntilNextCheck = 0;

        private static MidnightSnackGameComponent instance;

        public static MidnightSnackGameComponent Instance
        {
            get
            {
                if (instance == null && Current.Game != null)
                {
                    instance = Current.Game.GetComponent<MidnightSnackGameComponent>();
                }
                return instance;
            }
        }

        public MidnightSnackGameComponent(Game game)
        {
        }

        public override void GameComponentTick()
        {
            // 确保Current.Game不为null
            if (Current.Game == null)
                return;

            base.GameComponentTick();

            if (Find.TickManager.TicksGame <= ticksUntilNextCheck)
                return;

            ticksUntilNextCheck = Find.TickManager.TicksGame + CheckInterval;

            var map = Find.CurrentMap;
            if (map == null)
                return;

            if (!IsValidTimeForCheck())
                return;

            var eligibleToddlers = GetEligibleToddlers(map);
            foreach (var toddler in eligibleToddlers)
            {
                if (toddler.jobs?.jobQueue == null)
                    continue;

                var jobGiver = new JobGiver_MidnightSnack();
                var thinkResult = jobGiver.TryIssueJobPackage(toddler, default);
                if (thinkResult.Job != null)
                {
                    toddler.jobs.jobQueue.EnqueueFirst(thinkResult.Job);
                }
            }
        }

        private bool IsValidTimeForCheck()
        {
            var map = Find.CurrentMap;
            if (map == null)
                return false;

            int hour = GenLocalDate.HourOfDay(map);
            return hour >= 0 && hour <= 3 || hour >= 12 && hour <= 15;
        }

        private IEnumerable<Pawn> GetEligibleToddlers(Map map)
        {
            return map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer)
                .Where(p => (ToddlersCompatUtility.IsToddler(p) || p.DevelopmentalStage == DevelopmentalStage.Child) &&
                           !p.Downed && !p.Dead && p.Awake() &&
                           p.jobs?.curJob == null);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksUntilNextCheck, "ticksUntilNextCheck");
        }
    }

    public static class MidnightSnackUtility
    {
        public static void RegisterGameComponent()
        {
            // 检查 Current.Game 是否为 null（在游戏初始化期间可能发生）
            if (Current.Game == null)
            {
                Log.Warning("[RimTalk_ToddlersExpansion] Current.Game is null, skipping MidnightSnackGameComponent registration. Will retry when game is available.");
                return;
            }

            if (Current.Game.GetComponent<MidnightSnackGameComponent>() == null)
            {
                Current.Game.components.Add(new MidnightSnackGameComponent(Current.Game));
            }
        }
    }
}
