using System;
using System.Collections.Generic;
using Verse;
using RimTalk_ToddlersExpansion.Core;

namespace RimTalk_ToddlersExpansion
{
    /// <summary>
    /// 幼儿厌倦追踪器
    /// 追踪每个幼儿的活动历史，计算厌倦值（0%-100%）
    /// </summary>
    public class ToddlerBoredomTracker : IExposable
    {
        /// <summary>
        /// 关联的 Pawn
        /// </summary>
        public Pawn Pawn;

        /// <summary>
        /// 每个类别的厌倦值（0.0 - 1.0）
        /// </summary>
        private Dictionary<ToddlerPlayCategory, float> _boredomValues = new Dictionary<ToddlerPlayCategory, float>();

        public ToddlerBoredomTracker()
        {
        }

        public ToddlerBoredomTracker(Pawn pawn)
        {
            Pawn = pawn;
        }

        /// <summary>
        /// 获取某类别的厌倦值 (0.0 - 1.0)
        /// </summary>
        /// <param name="category">活动类别</param>
        /// <returns>厌倦值，0.0 表示完全不厌倦，1.0 表示非常厌倦</returns>
        public float GetBoredomValue(ToddlerPlayCategory category)
        {
            if (category == ToddlerPlayCategory.None)
                return 0.0f;

            if (!_boredomValues.TryGetValue(category, out float value))
                return 0.0f;

            return value;
        }

        /// <summary>
        /// 获取某类别的厌倦倍率 (0.3 - 1.0)
        /// </summary>
        public float GetBoredomMultiplier(ToddlerPlayCategory category)
        {
            if (category == ToddlerPlayCategory.None)
                return 1.0f;

            float boredomValue = GetBoredomValue(category);
            float multiplier = 1.0f - boredomValue;

            // 确保不低于最小值
            return Math.Max(multiplier, 0.3f);
        }

        /// <summary>
        /// 获取某 JobDef 的厌倦倍率
        /// </summary>
        public float GetBoredomMultiplier(JobDef jobDef)
        {
            if (jobDef == null) return 1.0f;

            var category = ToddlerPlayRegistry.GetCategory(jobDef);
            return GetBoredomMultiplier(category);
        }

        /// <summary>
        /// 获取某 JobDef 的厌倦值
        /// </summary>
        public float GetBoredomValue(JobDef jobDef)
        {
            if (jobDef == null) return 0.0f;

            var category = ToddlerPlayRegistry.GetCategory(jobDef);
            return GetBoredomValue(category);
        }

        /// <summary>
        /// 获取某类别是否达到厌倦封顶
        /// </summary>
        public bool IsAtCap(ToddlerPlayCategory category)
        {
            return GetBoredomValue(category) >= ToddlersExpansionSettings.boredomMaxCap;
        }

        /// <summary>
        /// 记录一次玩耍活动
        /// </summary>
        /// <param name="category">活动类别</param>
        /// <param name="weight">权重（默认1.0）</param>
        public void RecordPlay(ToddlerPlayCategory category, float weight = 1.0f)
        {
            if (category == ToddlerPlayCategory.None)
                return;

            // 增加厌倦值，每次固定增加5%
            if (_boredomValues.ContainsKey(category))
            {
                _boredomValues[category] = Math.Min(_boredomValues[category] + ToddlersExpansionSettings.boredomIncreasePerActivity,
                                                    ToddlersExpansionSettings.boredomMaxCap);
            }
            else
            {
                _boredomValues[category] = Math.Min(ToddlersExpansionSettings.boredomIncreasePerActivity,
                                                    ToddlersExpansionSettings.boredomMaxCap);
            }

            if (Prefs.DevMode)
            {
                Log.Message($"[RimTalk Boredom] {Pawn?.LabelShort ?? "Unknown"} played {category}, boredom now: {_boredomValues[category]:F2}, multiplier: {GetBoredomMultiplier(category):F2}");
            }
        }

        /// <summary>
        /// 记录一次玩耍活动（通过 JobDef）
        /// </summary>
        public void RecordPlay(JobDef jobDef)
        {
            if (jobDef == null) return;

            var category = ToddlerPlayRegistry.GetCategory(jobDef);

            RecordPlay(category, 1.0f);
        }

        /// <summary>
        /// 每 tick 更新衰减（现在改为每日更新）
        /// </summary>
        public void Tick()
        {
            // 每日更新由 GameComponentTick 统一处理
        }

        /// <summary>
        /// 每日厌倦度衰减
        /// </summary>
        public void DailyTick()
        {
            var categoriesToUpdate = new List<ToddlerPlayCategory>(_boredomValues.Keys);
            foreach (var category in categoriesToUpdate)
            {
                if (_boredomValues[category] > 0f)
                {
                    _boredomValues[category] = Math.Max(0f, _boredomValues[category] - ToddlersExpansionSettings.boredomDailyRecoveryRate);

                    // 如果值为0，可以移除
                    if (_boredomValues[category] == 0f)
                    {
                        _boredomValues.Remove(category);
                    }
                }
            }
        }

        /// <summary>
        /// 获取所有类别的厌倦状态（用于 UI 显示）
        /// </summary>
        /// <returns>类别和对应厌倦值的字典</returns>
        public Dictionary<ToddlerPlayCategory, float> GetAllBoredomValues()
        {
            var result = new Dictionary<ToddlerPlayCategory, float>();

            // 遍历所有可能的类别
            foreach (ToddlerPlayCategory category in Enum.GetValues(typeof(ToddlerPlayCategory)))
            {
                if (category == ToddlerPlayCategory.None || category == ToddlerPlayCategory.Custom)
                    continue;

                result[category] = GetBoredomValue(category);
            }

            return result;
        }

        /// <summary>
        /// 检查是否有任何厌倦状态
        /// </summary>
        public bool HasAnyBoredom()
        {
            return _boredomValues.Count > 0;
        }

        /// <summary>
        /// 获取调试状态
        /// </summary>
        public string GetDebugStatus()
        {
            var status = new System.Text.StringBuilder();
            status.AppendLine($"Boredom Status for {Pawn?.LabelShort ?? "Unknown"}:");

            foreach (var kvp in _boredomValues)
            {
                status.AppendLine($"  {kvp.Key}: boredom={kvp.Value:F2}, multiplier={GetBoredomMultiplier(kvp.Key):F2}");
            }

            return status.ToString();
        }

        /// <summary>
        /// 重置所有厌倦状态
        /// </summary>
        public void Reset()
        {
            _boredomValues.Clear();
        }

        /// <summary>
        /// 保存/加载数据
        /// </summary>
        public void ExposeData()
        {
            Scribe_References.Look(ref Pawn, "pawn");

            // 保存/加载厌倦值字典
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                var keys = new List<int>();
                var values = new List<float>();
                foreach (var kvp in _boredomValues)
                {
                    keys.Add((int)kvp.Key);
                    values.Add(kvp.Value);
                }
                Scribe_Collections.Look(ref keys, "boredomKeys", LookMode.Value);
                Scribe_Collections.Look(ref values, "boredomValues", LookMode.Value);
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                var keys = new List<int>();
                var values = new List<float>();
                Scribe_Collections.Look(ref keys, "boredomKeys", LookMode.Value);
                Scribe_Collections.Look(ref values, "boredomValues", LookMode.Value);

                _boredomValues = new Dictionary<ToddlerPlayCategory, float>();
                if (keys != null && values != null)
                {
                    for (int i = 0; i < Math.Min(keys.Count, values.Count); i++)
                    {
                        _boredomValues[(ToddlerPlayCategory)keys[i]] = values[i];
                    }
                }
            }
        }
    }
}
