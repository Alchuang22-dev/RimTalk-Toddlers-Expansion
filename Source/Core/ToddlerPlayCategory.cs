using System;

namespace RimTalk_ToddlersExpansion.Core
{
    /// <summary>
    /// 幼儿玩耍类别枚举
    /// 用于无聊机制的分类计算
    /// </summary>
    public enum ToddlerPlayCategory
    {
        /// <summary>
        /// 无类别（不计入无聊系统）
        /// </summary>
        None = 0,

        /// <summary>
        /// 独自玩耍 - 幼儿独自进行的活动
        /// 对应 JoyKind: Meditative
        /// </summary>
        SoloPlay = 1,

        /// <summary>
        /// 社交玩耍 - 与其他幼儿或成人互动
        /// 对应 JoyKind: Social
        /// </summary>
        SocialPlay = 2,

        /// <summary>
        /// 玩具玩耍 - 使用玩具进行的活动
        /// 对应 JoyKind: Meditative/Social
        /// </summary>
        ToyPlay = 3,

        /// <summary>
        /// 观察学习 - 观察环境或成人工作
        /// </summary>
        Observation = 4,

        /// <summary>
        /// 媒体娱乐 - 看电视等被动娱乐
        /// 对应 JoyKind: Gluttonous
        /// </summary>
        Media = 5,

        /// <summary>
        /// 被动娱乐 - 听故事等被动接收型活动
        /// </summary>
        Passive = 6,

        /// <summary>
        /// 探索活动 - 野外探索、玩水等
        /// </summary>
        Exploration = 7,

        /// <summary>
        /// 创造活动 - 绘画、堆雪人等创造性活动
        /// </summary>
        Creative = 8,

        /// <summary>
        /// 自定义类别起始值
        /// 其他模组可以从此值开始注册自定义类别
        /// </summary>
        Custom = 100
    }
}
