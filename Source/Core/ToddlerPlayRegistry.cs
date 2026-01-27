using System;
using System.Collections.Generic;
using Verse;

namespace RimTalk_ToddlersExpansion.Core
{
    /// <summary>
    /// 娱乐活动注册信息
    /// </summary>
    public class ToddlerPlayRegistration
    {
        public string JobDefName { get; set; }
        public ToddlerPlayCategory Category { get; set; }
        public float BoredomWeight { get; set; } = 1.0f;
        public string ModId { get; set; }

        public ToddlerPlayRegistration() { }

        public ToddlerPlayRegistration(string jobDefName, ToddlerPlayCategory category, float boredomWeight = 1.0f, string modId = null)
        {
            JobDefName = jobDefName;
            Category = category;
            BoredomWeight = boredomWeight;
            ModId = modId;
        }
    }

    /// <summary>
    /// 娱乐活动注册管理�?
    /// 提供可扩展的活动分类注册机制
    /// </summary>
    public static class ToddlerPlayRegistry
    {
        private static Dictionary<string, ToddlerPlayRegistration> _registrations = new Dictionary<string, ToddlerPlayRegistration>();
        private static Dictionary<string, int> _customCategories = new Dictionary<string, int>();
        private static int _nextCustomCategoryId = (int)ToddlerPlayCategory.Custom;
        private static bool _initialized = false;

        /// <summary>
        /// 初始化注册表，注册默认活�?
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // 注册 Toddlers 模组活动
            RegisterToddlersModActivities();

            // 注册 RimTalk Expansion 活动
            RegisterRimTalkActivities();

            Log.Message("[RimTalk Toddlers Expansion] ToddlerPlayRegistry initialized with " + _registrations.Count + " activities.");
        }

        /// <summary>
        /// 注册 Toddlers 模组的活�?
        /// </summary>
        private static void RegisterToddlersModActivities()
        {
            // 地面绘画 - 创造活�?
            Register("ToddlerFloordrawing", ToddlerPlayCategory.Creative, 1.0f, "Toddlers");
            // 仰望天空 - 观察活动
            Register("ToddlerSkydreaming", ToddlerPlayCategory.Observation, 1.0f, "Toddlers");
            // 观察昆虫 - 观察活动
            Register("ToddlerBugwatching", ToddlerPlayCategory.Observation, 1.0f, "Toddlers");
            // 玩玩�?- 玩具玩�?
            Register("ToddlerPlayToys", ToddlerPlayCategory.ToyPlay, 1.0f, "Toddlers");
            // 看电�?- 媒体娱乐
            Register("ToddlerWatchTelevision", ToddlerPlayCategory.Media, 1.0f, "Toddlers");
            // 凝视火焰 - 观察活动
            Register("ToddlerFiregazing", ToddlerPlayCategory.Observation, 1.0f, "Toddlers");
            // 玩耍装�?- 玩具玩�?
            Register("ToddlerPlayDecor", ToddlerPlayCategory.ToyPlay, 1.0f, "Toddlers");
        }

        /// <summary>
        /// 注册 RimTalk Expansion 的活�?
        /// </summary>
        private static void RegisterRimTalkActivities()
        {
            // 独自玩�?- 独自玩�?
            Register("RimTalk_ToddlerSelfPlayJob", ToddlerPlayCategory.SoloPlay, 1.0f, "RimTalk_ToddlersExpansion");
            // 相互玩�?- 社交玩�?
            Register("RimTalk_ToddlerMutualPlayJob", ToddlerPlayCategory.SocialPlay, 1.0f, "RimTalk_ToddlersExpansion");
            // 相互玩�?伙伴) - 社交玩�?
            Register("RimTalk_ToddlerMutualPlayPartnerJob", ToddlerPlayCategory.SocialPlay, 1.0f, "RimTalk_ToddlersExpansion");
            // 玩具玩�?- 玩具玩�?
            Register("RimTalk_ToddlerPlayAtToy", ToddlerPlayCategory.ToyPlay, 1.0f, "RimTalk_ToddlersExpansion");
            // 观察成人工作 - 观察学习
            Register("RimTalk_ToddlerObserveAdultWork", ToddlerPlayCategory.Observation, 1.0f, "RimTalk_ToddlersExpansion");
            // 成人观看幼儿 - 社交玩�?
            Register("RimTalk_WatchToddlerPlayJob", ToddlerPlayCategory.SocialPlay, 1.0f, "RimTalk_ToddlersExpansion");
            // 午夜偷吃 - 不计入无聊系�?
            Register("RimTalk_MidnightSnack", ToddlerPlayCategory.None, 0f, "RimTalk_ToddlersExpansion");
            Register("RimTalk_BeingCarried_Observe", ToddlerPlayCategory.Exploration, 0.8f, "RimTalk_ToddlersExpansion");
            Register("RimTalk_BeingCarried_Idle", ToddlerPlayCategory.Passive, 0.3f, "RimTalk_ToddlersExpansion");
            Register("RimTalk_BeingCarried", ToddlerPlayCategory.Passive, 0.3f, "RimTalk_ToddlersExpansion");

            // 预注册未来活�?
            Register("RimTalk_ToddlerListenStory", ToddlerPlayCategory.Passive, 0.8f, "RimTalk_ToddlersExpansion");
            Register("RimTalk_ToddlerExploreWild", ToddlerPlayCategory.Exploration, 1.2f, "RimTalk_ToddlersExpansion");
            Register("RimTalk_ToddlerPlayWater", ToddlerPlayCategory.Exploration, 1.2f, "RimTalk_ToddlersExpansion");
            Register("RimTalk_ToddlerWatchAnimal", ToddlerPlayCategory.Observation, 1.0f, "RimTalk_ToddlersExpansion");
            Register("RimTalk_ToddlerGroupPlay", ToddlerPlayCategory.SocialPlay, 1.0f, "RimTalk_ToddlersExpansion");
        }

        /// <summary>
        /// 注册一个娱乐活�?
        /// </summary>
        /// <param name="jobDefName">JobDef 名称</param>
        /// <param name="category">所属类�?/param>
        /// <param name="boredomWeight">无聊权重（默�?.0�?/param>
        /// <param name="modId">来源模组ID（可选）</param>
        public static void Register(string jobDefName, ToddlerPlayCategory category, float boredomWeight = 1.0f, string modId = null)
        {
            if (string.IsNullOrEmpty(jobDefName))
            {
                Log.Warning("[RimTalk Toddlers Expansion] Attempted to register activity with null or empty jobDefName");
                return;
            }

            var registration = new ToddlerPlayRegistration(jobDefName, category, boredomWeight, modId);

            if (_registrations.ContainsKey(jobDefName))
            {
                _registrations[jobDefName] = registration;
                if (Prefs.DevMode)
                {
                    Log.Message($"[RimTalk Toddlers Expansion] Updated registration for {jobDefName} -> {category}");
                }
            }
            else
            {
                _registrations.Add(jobDefName, registration);
                if (Prefs.DevMode)
                {
                    Log.Message($"[RimTalk Toddlers Expansion] Registered {jobDefName} -> {category}");
                }
            }
        }

        /// <summary>
        /// 批量注册娱乐活动
        /// </summary>
        public static void RegisterBatch(IEnumerable<ToddlerPlayRegistration> registrations)
        {
            foreach (var reg in registrations)
            {
                Register(reg.JobDefName, reg.Category, reg.BoredomWeight, reg.ModId);
            }
        }

        /// <summary>
        /// 注册自定义类�?
        /// </summary>
        /// <param name="categoryName">类别名称</param>
        /// <returns>分配的类别ID</returns>
        public static int RegisterCustomCategory(string categoryName)
        {
            if (_customCategories.ContainsKey(categoryName))
            {
                return _customCategories[categoryName];
            }

            int categoryId = _nextCustomCategoryId++;
            _customCategories.Add(categoryName, categoryId);

            Log.Message($"[RimTalk Toddlers Expansion] Registered custom category '{categoryName}' with ID {categoryId}");

            return categoryId;
        }

        /// <summary>
        /// 获取活动的类�?
        /// </summary>
        public static ToddlerPlayCategory GetCategory(JobDef jobDef)
        {
            if (jobDef == null) return ToddlerPlayCategory.None;
            return GetCategory(jobDef.defName);
        }

        /// <summary>
        /// 获取活动的类别（通过名称�?
        /// </summary>
        public static ToddlerPlayCategory GetCategory(string jobDefName)
        {
            if (string.IsNullOrEmpty(jobDefName)) return ToddlerPlayCategory.None;

            // 确保已初始化
            if (!_initialized) Initialize();

            // 查找已注册的活动
            if (_registrations.TryGetValue(jobDefName, out var registration))
            {
                return registration.Category;
            }

            // 尝试自动检�?
            if (ToddlersExpansionSettings.enableAutoDetection)
            {
                var jobDef = DefDatabase<JobDef>.GetNamedSilentFail(jobDefName);
                if (jobDef != null)
                {
                    return AutoDetectCategory(jobDef);
                }
            }

            return ToddlerPlayCategory.None;
        }

        /// <summary>
        /// 获取活动的无聊权�?
        /// </summary>
        public static float GetBoredomWeight(JobDef jobDef)
        {
            if (jobDef == null) return 0f;
            return GetBoredomWeight(jobDef.defName);
        }

        /// <summary>
        /// 获取活动的无聊权重（通过名称�?
        /// </summary>
        public static float GetBoredomWeight(string jobDefName)
        {
            if (string.IsNullOrEmpty(jobDefName)) return 0f;

            // 确保已初始化
            if (!_initialized) Initialize();

            if (_registrations.TryGetValue(jobDefName, out var registration))
            {
                return registration.BoredomWeight;
            }

            return 1.0f; // 默认权重
        }

        /// <summary>
        /// 检查活动是否已注册
        /// </summary>
        public static bool IsRegistered(string jobDefName)
        {
            if (!_initialized) Initialize();
            return _registrations.ContainsKey(jobDefName);
        }

        /// <summary>
        /// 获取所有已注册的活�?
        /// </summary>
        public static IEnumerable<ToddlerPlayRegistration> GetAllRegistrations()
        {
            if (!_initialized) Initialize();
            return _registrations.Values;
        }

        /// <summary>
        /// 自动检测未注册活动的类�?
        /// </summary>
        public static ToddlerPlayCategory AutoDetectCategory(JobDef jobDef)
        {
            if (jobDef == null) return ToddlerPlayCategory.None;

            // 1. 检�?JoyKind
            if (jobDef.joyKind != null)
            {
                switch (jobDef.joyKind.defName)
                {
                    case "Meditative":
                        return ToddlerPlayCategory.SoloPlay;
                    case "Social":
                        return ToddlerPlayCategory.SocialPlay;
                    case "Gluttonous":
                        return ToddlerPlayCategory.Media;
                }
            }

            // 2. 检�?JobDef 名称模式
            string name = jobDef.defName.ToLower();

            if (name.Contains("watch") || name.Contains("observe") || name.Contains("gaze"))
                return ToddlerPlayCategory.Observation;

            if (name.Contains("toy") || name.Contains("decor"))
                return ToddlerPlayCategory.ToyPlay;

            if (name.Contains("mutual") || name.Contains("social") || name.Contains("group"))
                return ToddlerPlayCategory.SocialPlay;

            if (name.Contains("explore") || name.Contains("water") || name.Contains("wild"))
                return ToddlerPlayCategory.Exploration;

            if (name.Contains("draw") || name.Contains("build") || name.Contains("create") || name.Contains("floor"))
                return ToddlerPlayCategory.Creative;

            if (name.Contains("listen") || name.Contains("story"))
                return ToddlerPlayCategory.Passive;

            if (name.Contains("television") || name.Contains("tv") || name.Contains("screen"))
                return ToddlerPlayCategory.Media;

            if (name.Contains("self") || name.Contains("solo") || name.Contains("alone"))
                return ToddlerPlayCategory.SoloPlay;

            // 3. 默认返回 None
            return ToddlerPlayCategory.None;
        }

        /// <summary>
        /// 重置注册表（用于测试�?
        /// </summary>
        public static void Reset()
        {
            _registrations.Clear();
            _customCategories.Clear();
            _nextCustomCategoryId = (int)ToddlerPlayCategory.Custom;
            _initialized = false;
        }
    }
}
