using System;
using System.Collections.Generic;
using Verse;

namespace RimTalk_ToddlersExpansion.Core
{
    /// <summary>
    /// 用于 XML 配置的活动分类 Def
    /// 允许其他模组通过 XML 注册活动分类
    /// </summary>
    public class ToddlerPlayCategoryDef : Def
    {
        /// <summary>
        /// 活动类别
        /// </summary>
        public ToddlerPlayCategory category = ToddlerPlayCategory.None;

        /// <summary>
        /// 关联的 JobDef 名称列表
        /// </summary>
        public List<string> jobDefNames = new List<string>();

        /// <summary>
        /// 无聊权重（默认1.0）
        /// </summary>
        public float boredomWeight = 1.0f;

        /// <summary>
        /// 在 Def 加载完成后注册活动
        /// </summary>
        public override void ResolveReferences()
        {
            base.ResolveReferences();

            if (jobDefNames == null || jobDefNames.Count == 0)
                return;

            foreach (var jobDefName in jobDefNames)
            {
                if (string.IsNullOrEmpty(jobDefName))
                {
                    Log.Warning($"[RimTalk Toddlers Expansion] ToddlerPlayCategoryDef '{defName}' has an entry with null or empty jobDefName");
                    continue;
                }

                ToddlerPlayRegistry.Register(
                    jobDefName,
                    category,
                    boredomWeight,
                    defName // 使用 Def 名称作为模组 ID
                );
            }

            Log.Message($"[RimTalk Toddlers Expansion] Loaded {jobDefNames.Count} play activities for category '{category}' from '{defName}'");
        }
    }
}
