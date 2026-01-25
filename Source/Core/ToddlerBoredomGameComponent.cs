using System;
using System.Collections.Generic;
using Verse;
using RimWorld;
using RimTalk_ToddlersExpansion.Core;

namespace RimTalk_ToddlersExpansion
{
    /// <summary>
    /// GameComponent 管理所有幼儿的无聊追踪器
    /// </summary>
    public class ToddlerBoredomGameComponent : GameComponent
    {
        /// <summary>
        /// 所有幼儿的无聊追踪器
        /// </summary>
        private Dictionary<int, ToddlerBoredomTracker> _trackers = new Dictionary<int, ToddlerBoredomTracker>();

        /// <summary>
        /// 用于序列化的追踪器列表
        /// </summary>
        private List<ToddlerBoredomTracker> _trackerList = new List<ToddlerBoredomTracker>();

        /// <summary>
        /// 上次每日更新的 tick
        /// </summary>
        private int _lastDailyTick = 0;

        public ToddlerBoredomGameComponent(Game game) : base()
        {
            // 初始化注册表
            ToddlerPlayRegistry.Initialize();
        }

        /// <summary>
        /// 获取或创建 Pawn 的无聊追踪器
        /// </summary>
        public ToddlerBoredomTracker GetTracker(Pawn pawn)
        {
            if (pawn == null) return null;

            int pawnId = pawn.thingIDNumber;

            if (_trackers.TryGetValue(pawnId, out var tracker))
            {
                return tracker;
            }

            // 创建新的追踪器
            tracker = new ToddlerBoredomTracker(pawn);
            _trackers[pawnId] = tracker;

            return tracker;
        }

        /// <summary>
        /// 检查 Pawn 是否是幼儿
        /// </summary>
        private bool IsToddler(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed)
                return false;

            // 检查发育阶段
            if (pawn.DevelopmentalStage != DevelopmentalStage.Baby && 
                pawn.DevelopmentalStage != DevelopmentalStage.Child)
                return false;

            // 检查年龄（幼儿通常是1-3岁）
            float age = pawn.ageTracker?.AgeBiologicalYearsFloat ?? 0f;
            return age >= 1f && age < 4f;
        }

        /// <summary>
        /// 每 tick 更新
        /// </summary>
        public override void GameComponentTick()
        {
            base.GameComponentTick();

            if (!ToddlersExpansionSettings.enableBoredomSystem)
                return;

            int currentTick = Find.TickManager?.TicksGame ?? 0;

            // 检查是否需要每日更新（每天一次）
            if (currentTick - _lastDailyTick >= GenDate.TicksPerDay)
            {
                _lastDailyTick = currentTick;

                // 每日厌倦度衰减
                foreach (var tracker in _trackers.Values)
                {
                    tracker.DailyTick();
                }
            }

            // 每小时的清理间隔
            if (currentTick - _lastDailyTick > GenDate.TicksPerHour)
            {
                CleanupInvalidTrackers();
            }
        }

        /// <summary>
        /// 清理无效的追踪器（Pawn 已死亡或不再是幼儿）
        /// </summary>
        private void CleanupInvalidTrackers()
        {
            var toRemove = new List<int>();

            foreach (var kvp in _trackers)
            {
                var pawn = kvp.Value.Pawn;
                if (pawn == null || pawn.Dead || pawn.Destroyed || !IsToddler(pawn))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var id in toRemove)
            {
                _trackers.Remove(id);
            }

            if (toRemove.Count > 0 && Prefs.DevMode)
            {
                Log.Message($"[RimTalk Boredom] Cleaned up {toRemove.Count} invalid trackers");
            }
        }

        /// <summary>
        /// 记录玩耍活动
        /// </summary>
        public void RecordPlay(Pawn pawn, JobDef jobDef)
        {
            if (!ToddlersExpansionSettings.enableBoredomSystem)
                return;

            if (pawn == null || jobDef == null)
                return;

            if (!IsToddler(pawn))
                return;

            var tracker = GetTracker(pawn);
            tracker?.RecordPlay(jobDef);
        }

        /// <summary>
        /// 记录玩耍活动（通过类别）
        /// </summary>
        public void RecordPlay(Pawn pawn, ToddlerPlayCategory category, float weight = 1.0f)
        {
            if (!ToddlersExpansionSettings.enableBoredomSystem)
                return;

            if (pawn == null)
                return;

            if (!IsToddler(pawn))
                return;

            var tracker = GetTracker(pawn);
            tracker?.RecordPlay(category, weight);
        }

        /// <summary>
        /// 获取无聊倍率
        /// </summary>
        public float GetBoredomMultiplier(Pawn pawn, JobDef jobDef)
        {
            if (!ToddlersExpansionSettings.enableBoredomSystem)
                return 1.0f;

            if (pawn == null || jobDef == null)
                return 1.0f;

            if (!IsToddler(pawn))
                return 1.0f;

            var tracker = GetTracker(pawn);
            return tracker?.GetBoredomMultiplier(jobDef) ?? 1.0f;
        }

        /// <summary>
        /// 获取无聊倍率（通过类别）
        /// </summary>
        public float GetBoredomMultiplier(Pawn pawn, ToddlerPlayCategory category)
        {
            if (!ToddlersExpansionSettings.enableBoredomSystem)
                return 1.0f;

            if (pawn == null)
                return 1.0f;

            if (!IsToddler(pawn))
                return 1.0f;

            var tracker = GetTracker(pawn);
            return tracker?.GetBoredomMultiplier(category) ?? 1.0f;
        }

        /// <summary>
        /// 获取调试状态
        /// </summary>
        public string GetDebugStatus(Pawn pawn)
        {
            if (pawn == null)
                return "Pawn is null";

            var tracker = GetTracker(pawn);
            return tracker?.GetDebugStatus() ?? "No tracker found";
        }

        /// <summary>
        /// 保存/加载数据
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                _trackerList = new List<ToddlerBoredomTracker>(_trackers.Values);
            }

            Scribe_Collections.Look(ref _trackerList, "trackers", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _trackers = new Dictionary<int, ToddlerBoredomTracker>();
                if (_trackerList != null)
                {
                    foreach (var tracker in _trackerList)
                    {
                        if (tracker?.Pawn != null)
                        {
                            _trackers[tracker.Pawn.thingIDNumber] = tracker;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 获取当前实例
        /// </summary>
        public static ToddlerBoredomGameComponent GetCurrent()
        {
            if (Verse.Current.Game == null) return null;
            return Verse.Current.Game.GetComponent<ToddlerBoredomGameComponent>();
        }
    }
}
