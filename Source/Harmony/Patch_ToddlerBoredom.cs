using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using RimTalk_ToddlersExpansion.Core;

namespace RimTalk_ToddlersExpansion
{
    /// <summary>
    /// Harmony Patch 用于应用幼儿无聊机制
    /// </summary>
    public static class Patch_ToddlerBoredom
    {
        // 缓存 pawn 字段的反射引用
        private static FieldInfo _pawnField;

        /// <summary>
        /// 手动注册 Patch
        /// </summary>
        public static void ApplyPatches(HarmonyLib.Harmony harmony)
        {
            // 缓存反射字段
            _pawnField = AccessTools.Field(typeof(Need), "pawn");

            // Patch JobDriver.Cleanup 方法
            var cleanupMethod = AccessTools.Method(typeof(JobDriver), nameof(JobDriver.Cleanup));
            var cleanupPostfix = AccessTools.Method(typeof(Patch_ToddlerBoredom), nameof(Cleanup_Postfix));
            if (cleanupMethod != null && cleanupPostfix != null)
            {
                harmony.Patch(cleanupMethod, postfix: new HarmonyMethod(cleanupPostfix));
                Log.Message("[RimTalk Toddlers Expansion] Patched JobDriver.Cleanup for boredom tracking");
            }

            // Patch Need_Play.Play 方法（Biotech 中的方法）
            var playMethod = AccessTools.Method(typeof(Need_Play), "Play");
            if (playMethod != null)
            {
                var playPrefix = AccessTools.Method(typeof(Patch_ToddlerBoredom), nameof(Play_Prefix));
                harmony.Patch(playMethod, prefix: new HarmonyMethod(playPrefix));
                Log.Message("[RimTalk Toddlers Expansion] Patched Need_Play.Play for boredom system");
            }
            else
            {
                Log.Message("[RimTalk Toddlers Expansion] Need_Play.Play not found, boredom will only track activities");
            }

            // Patch Need.GetTipString 方法用于显示无聊度
            // 注意：Need_Play 没有重写 GetTipString，所以我们需要 patch 基类 Need.GetTipString
            var getTipStringMethod = AccessTools.Method(typeof(Need), "GetTipString");
            if (getTipStringMethod != null)
            {
                var getTipStringPostfix = AccessTools.Method(typeof(Patch_ToddlerBoredom), nameof(GetTipString_Postfix));
                harmony.Patch(getTipStringMethod, postfix: new HarmonyMethod(getTipStringPostfix));
                Log.Message("[RimTalk Toddlers Expansion] Patched Need.GetTipString for boredom display");
                Log.Message("[RimTalk Toddlers Expansion] Patch priority set to ensure our postfix runs after Toddlers mod");
            }
        }

        /// <summary>
        /// Prefix for Need_Play.Play
        /// 使用反射获取 pawn 字段
        /// </summary>
        public static void Play_Prefix(Need_Play __instance, ref float amount)
        {
            if (!ToddlersExpansionSettings.enableBoredomSystem)
                return;

            // 使用反射获取 pawn 字段
            var pawnField = AccessTools.Field(typeof(Need), "pawn");
            if (pawnField == null)
                return;

            var pawn = pawnField.GetValue(__instance) as Pawn;
            if (pawn == null)
                return;

            if (!IsToddler(pawn))
                return;

            var curJob = pawn.jobs?.curJob;
            if (curJob == null)
                return;

            var component = ToddlerBoredomGameComponent.GetCurrent();
            if (component == null)
                return;

            float multiplier = component.GetBoredomMultiplier(pawn, curJob.def);
            amount *= multiplier;

            if (Prefs.DevMode && multiplier < 1.0f)
            {
                Log.Message($"[RimTalk Boredom] {pawn.LabelShort} play gain reduced: {multiplier:F2}x");
            }
        }

        /// <summary>
        /// Patch JobDriver.Cleanup 方法，在工作完成时记录活动
        /// </summary>
        public static void Cleanup_Postfix(JobDriver __instance, JobCondition condition)
        {
            if (!ToddlersExpansionSettings.enableBoredomSystem)
                return;

            // 只在工作成功完成时记录
            if (condition != JobCondition.Succeeded)
                return;

            var pawn = __instance.pawn;
            if (pawn == null)
                return;

            // 检查是否是幼儿
            if (!IsToddler(pawn))
                return;

            var jobDef = __instance.job?.def;
            if (jobDef == null)
                return;

            // 检查是否是玩耍相关的工作
            var category = ToddlerPlayRegistry.GetCategory(jobDef);
            if (category == ToddlerPlayCategory.None)
                return;

            // 记录活动
            var component = ToddlerBoredomGameComponent.GetCurrent();
            component?.RecordPlay(pawn, jobDef);
        }

        /// <summary>
        /// 检查 Pawn 是否是幼儿
        /// </summary>
        private static bool IsToddler(Pawn pawn)
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
        /// Postfix for Need.GetTipString
        /// 在玩乐需求悬浮窗中显示完整信息，包括无聊度和游戏类型
        /// 这会替换 Toddlers 模组原有的浮窗显示
        /// </summary>
        [HarmonyPriority(Priority.Last)]  // 确保在 Toddlers 模组之后运行
        public static string GetTipString_Postfix(string __result, Need __instance)
        {
            // 只对 Need_Play 实例生效
            // 只对 Need_Play 实例生效
            if (!(__instance is Need_Play needPlay))
            {
                if (Prefs.DevMode && __instance.def.defName == "Play")
                {
                    Log.Message($"[RimTalk Boredom Debug] Instance is not Need_Play, type: {__instance.GetType().Name}");
                }
                return __result;
            }

            // 获取 pawn
            if (_pawnField == null)
            {
                Log.Error("[RimTalk Boredom] _pawnField is null!");
                return __result;
            }

            var pawn = _pawnField.GetValue(__instance) as Pawn;
            if (pawn == null)
            {
                if (Prefs.DevMode)
                    Log.Message("[RimTalk Boredom Debug] Pawn is null");
                return __result;
            }

            // 检查是否是幼儿
            if (!IsToddler(pawn))
            {
                if (Prefs.DevMode)
                    Log.Message($"[RimTalk Boredom Debug] {pawn.LabelShort} is not a toddler");
                return __result;
            }

            // if (Prefs.DevMode)
            // {
            //     Log.Message($"[RimTalk Boredom Debug] Processing tooltip for toddler: {pawn.LabelShort}, original length: {__result.Length}");
            // }

            // 构建完整的浮窗内容
            var sb = new StringBuilder();

            // 标题：玩乐: 94%
            string header = (needPlay.LabelCap + ": " + needPlay.CurLevelPercentage.ToStringPercent()).Colorize(ColoredText.TipSectionTitleColor);
            sb.AppendLine(header);

            // 正文：从翻译键获取
            sb.AppendLine("NeedTipStringPlay".Translate());

            // 孤独值（从 Toddlers 模组获取）
            string lonelyReport = "Loneliness".Translate() + ": " + GetLoneliness(pawn).ToStringPercent();
            sb.AppendLine();
            sb.AppendLine(lonelyReport);

            // 如果启用了无聊系统，添加无聊度信息
            if (ToddlersExpansionSettings.enableBoredomSystem)
            {
                var component = ToddlerBoredomGameComponent.GetCurrent();
                if (component != null)
                {
                    var tracker = component.GetTracker(pawn);
                    if (tracker != null)
                    {
                        sb.AppendLine();
                        sb.AppendLine("RimTalk_Boredom_Header".Translate());

                        var boredomValues = tracker.GetAllBoredomValues();
                        foreach (var kvp in boredomValues)
                        {
                            string categoryName = GetCategoryTranslationKey(kvp.Key).Translate();
                            string boredomPercent = kvp.Value.ToStringPercent();

                            // 添加无聊标签（当厌倦度 > 70%）
                            string boredomLabel = "";
                            if (kvp.Value > 0.7f)
                                boredomLabel = " (" + "RimTalk_Boredom_Label_Boring".Translate() + ")";

                            sb.AppendLine($"  - {categoryName}: {boredomPercent}{boredomLabel}");
                        }

                        // 添加设置说明
                        sb.AppendLine();
                        string recoveryRate = ToddlersExpansionSettings.boredomDailyRecoveryRate.ToStringPercent();
                        sb.AppendLine("RimTalk_Boredom_RecoveryInfo".Translate(recoveryRate));

                        // 添加建议使用的游戏类型数量
                        int playTypeCount = boredomValues.Count;
                        sb.AppendLine();
                        sb.AppendLine("RimTalk_Boredom_PlayTypeSuggestion".Translate(playTypeCount));

                        // 列出所有游戏类型
                        sb.AppendLine();
                        foreach (var kvp in boredomValues)
                        {
                            string categoryName = GetCategoryTranslationKey(kvp.Key).Translate();
                            sb.AppendLine($"- {categoryName}");
                        }
                    }
                }
            }

            string finalResult = sb.ToString();

            // if (Prefs.DevMode)
            // {
            //     Log.Message($"[RimTalk Boredom Debug] Tooltip replaced for {pawn.LabelShort}, new length: {finalResult.Length}");
            // }

            return finalResult;
        }

        /// <summary>
        /// 从 Toddlers 模组获取孤独值
        /// </summary>
        private static float GetLoneliness(Pawn pawn)
        {
            var hediffSet = pawn?.health?.hediffSet;
            if (hediffSet == null) return 0f;

            // 获取 ToddlerLonely hediff（使用反射来获取 Toddlers 模组的定义）
            var hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail("ToddlerLonely");
            if (hediffDef == null) return 0f;

            var hediff = hediffSet.GetFirstHediffOfDef(hediffDef);
            return hediff?.Severity ?? 0f;
        }

        /// <summary>
        /// 获取类别的翻译键
        /// </summary>
        private static string GetCategoryTranslationKey(ToddlerPlayCategory category)
        {
            switch (category)
            {
                case ToddlerPlayCategory.SoloPlay:
                    return "RimTalk_Boredom_Category_SoloPlay";
                case ToddlerPlayCategory.SocialPlay:
                    return "RimTalk_Boredom_Category_SocialPlay";
                case ToddlerPlayCategory.ToyPlay:
                    return "RimTalk_Boredom_Category_ToyPlay";
                case ToddlerPlayCategory.Observation:
                    return "RimTalk_Boredom_Category_Observation";
                case ToddlerPlayCategory.Media:
                    return "RimTalk_Boredom_Category_Media";
                case ToddlerPlayCategory.Passive:
                    return "RimTalk_Boredom_Category_Passive";
                case ToddlerPlayCategory.Exploration:
                    return "RimTalk_Boredom_Category_Exploration";
                case ToddlerPlayCategory.Creative:
                    return "RimTalk_Boredom_Category_Creative";
                default:
                    return "RimTalk_Boredom_Category_Unknown";
            }
        }
    }
}
