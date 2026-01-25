# 幼儿无聊机制设计文档

## 问题描述

当前幼儿的娱乐需求经常维持在100%，导致幼儿频繁进行"闲逛"动作，消耗性能。

## 解决方案

仿照原版娱乐需求的"无聊"机制，当幼儿重复进行同类型娱乐活动时，降低娱乐需求的增益速度。

---

## 幼儿娱乐活动完整列表

### 一、原版 RimWorld (Biotech DLC)

原版 Biotech 对幼儿的娱乐活动支持有限，主要依赖成人照顾：

| 活动名称 | JobDef | 描述 | 执行者 |
|----------|--------|------|--------|
| 婴儿床内玩耍 | BabyPlayInCrib | 婴儿/幼儿在婴儿床内自娱自乐 | 婴儿/幼儿 |
| 喂养婴儿 | FeedBaby | 成人喂养婴儿 | 成人 |
| 奶瓶喂养 | BottleFeedBaby | 用奶瓶喂养婴儿 | 成人 |
| 母乳喂养 | Breastfeed | 母亲母乳喂养婴儿 | 母亲 |
| 带到安全地 | BringBabyToSafety | 将婴儿带到安全位置 | 成人 |

**注意**：原版 Biotech 的幼儿娱乐系统较为简单，主要通过 `Need_Play` 需求和基础的婴儿床玩耍来满足。

---

### 二、Toddlers 模组 (7种玩耍类型)

Toddlers 模组采用 **ToddlerPlayDef** 系统，提供丰富的幼儿玩耍活动：

| 活动名称 | ToddlerPlayDef | JobDef | 描述 | WorkerClass |
|----------|----------------|--------|------|-------------|
| **地面绘画** | ToddlerFloordrawing | ToddlerFloordrawing | 幼儿在地面上涂鸦绘画 | ToddlerPlayGiver_Floordrawing |
| **仰望天空** | ToddlerSkydreaming | ToddlerSkydreaming | 幼儿躺在地上看天空发呆 | ToddlerPlayGiver_Skydreaming |
| **观察昆虫** | ToddlerBugwatching | ToddlerBugwatching | 幼儿观察地上的昆虫 | ToddlerPlayGiver_Bugwatching |
| **玩玩具** | ToddlerPlayToys | ToddlerPlayToys | 幼儿玩地上的玩具物品 | ToddlerPlayGiver_PlayToys |
| **看电视** | ToddlerWatchTelevision | ToddlerWatchTelevision | 幼儿观看电视节目 | ToddlerPlayGiver_WatchTelevision |
| **凝视火焰** | ToddlerFiregazing | ToddlerFiregazing | 幼儿盯着火焰看（篝火、火炉等） | ToddlerPlayGiver_Firegazing |
| **玩耍装饰** | ToddlerPlayDecor | ToddlerPlayDecor | 幼儿与装饰物互动玩耍 | ToddlerPlayGiver_PlayDecor |

**核心机制**：
- 通过 `JobGiver_ToddlerPlay` 随机选择玩耍类型
- 基于 `selectionWeight` 进行加权随机
- 玩耍需求 < 70% 时优先级 6.0，否则 2.0

---

### 三、RimTalk Toddlers Expansion (本模组，7种活动)

本模组扩展了幼儿的社交和学习活动：

| 活动名称 | JobDef | JobDriver | 描述 | JoyKind | 备注 |
|----------|--------|-----------|------|---------|------|
| **独自玩耍** | RimTalk_ToddlerSelfPlayJob | JobDriver_ToddlerSelfPlay | 幼儿独自进行想象游戏 | Meditative | 时长2000ticks |
| **相互玩耍** | RimTalk_ToddlerMutualPlayJob | JobDriver_ToddlerMutualPlay | 两个幼儿一起玩耍 | Social | 双人互动 |
| **相互玩耍(伙伴)** | RimTalk_ToddlerMutualPlayPartnerJob | JobDriver_ToddlerMutualPlayPartner | 被邀请的玩伴角色 | Social | 配合主动方 |
| **玩具玩耍** | RimTalk_ToddlerPlayAtToy | JobDriver_ToddlerPlayAtBuilding | 在专用玩具处玩耍 | Meditative | 通用玩具系统 |
| **观察成人工作** | RimTalk_ToddlerObserveAdultWork | JobDriver_ToddlerObserveAdultWork | 幼儿跟随观察成人工作 | Observation | 学习机制 |
| **成人观看幼儿** | RimTalk_WatchToddlerPlayJob | JobDriver_WatchToddlerPlay | 成人观看幼儿玩耍 | Social | 成人获得joy |
| **午夜偷吃** | RimTalk_MidnightSnack | JobDriver_MidnightSnack | 幼儿半夜偷吃零食 | 无 | 蛀牙系统 |

**专用玩具 (ThingDef)**：

| 玩具名称 | ThingDef | 描述 | JoyKind |
|----------|----------|------|---------|
| 积木堆 | RimTalk_ToyBlockPile | 地面玩具，堆叠积木 | Meditative |
| 摇摇马 | RimTalk_ToyRockingHorse | 骑乘玩具，摇晃玩耍 | Social |
| 益智拼图桌 | RimTalk_ToyPuzzleTable | 益智玩具，拼图游戏 | Meditative |

---

### 四、计划中的未来娱乐活动

以下是计划在未来版本中实现的幼儿娱乐活动：

| 活动名称 | 预计 JobDef | 描述 | 预计类别 | 优先级 |
|----------|-------------|------|----------|--------|
| **听故事** | RimTalk_ToddlerListenStory | 幼儿听成人讲故事 | Passive | 高 |
| **野外探索** | RimTalk_ToddlerExploreWild | 幼儿在野外行走跳跃探索 | Exploration | 中 |
| **玩水** | RimTalk_ToddlerPlayWater | 幼儿在浅水中行走跳跃 | Exploration | 中 |
| **观看动物** | RimTalk_ToddlerWatchAnimal | 幼儿跟随殖民地的动物 | Observation | 中 |
| **多人游戏** | RimTalk_ToddlerGroupPlay | 多个幼儿一起玩耍 | SocialPlay | 高 |

---

## 活动分类体系（可扩展）

### 分类原则

基于活动的**性质**和**JoyKind**进行分类，用于无聊机制的计算。采用**可扩展的注册机制**，允许其他模组或新功能注册新的娱乐类型。

### 核心类别定义

| 类别 ID | 类别名称 | 描述 | 对应 JoyKind | 是否可扩展 |
|---------|----------|------|--------------|------------|
| `SoloPlay` | 独自玩耍 | 幼儿独自进行的活动 | Meditative | ✅ |
| `SocialPlay` | 社交玩耍 | 与其他幼儿或成人互动 | Social | ✅ |
| `ToyPlay` | 玩具玩耍 | 使用玩具进行的活动 | Meditative/Social | ✅ |
| `Observation` | 观察学习 | 观察环境或成人工作 | - | ✅ |
| `Media` | 媒体娱乐 | 看电视等被动娱乐 | Gluttonous | ✅ |
| `Passive` | 被动娱乐 | 听故事等被动接收型活动 | - | ✅ (新增) |
| `Exploration` | 探索活动 | 野外探索、玩水等 | - | ✅ (新增) |
| `Creative` | 创造活动 | 绘画、堆雪人等创造性活动 | - | ✅ (新增) |
| `Custom` | 自定义 | 其他模组注册的自定义类别 | - | ✅ (预留) |

### 活动分类映射

#### Toddlers 模组活动分类

| 活动 | 类别 | 理由 |
|------|------|------|
| ToddlerFloordrawing (地面绘画) | Creative | 创造性活动 |
| ToddlerSkydreaming (仰望天空) | Observation | 被动观察活动 |
| ToddlerBugwatching (观察昆虫) | Observation | 被动观察活动 |
| ToddlerPlayToys (玩玩具) | ToyPlay | 使用玩具 |
| ToddlerWatchTelevision (看电视) | Media | 被动媒体娱乐 |
| ToddlerFiregazing (凝视火焰) | Observation | 被动观察活动 |
| ToddlerPlayDecor (玩耍装饰) | ToyPlay | 与物品互动 |

#### RimTalk Expansion 活动分类

| 活动 | 类别 | 理由 |
|------|------|------|
| RimTalk_ToddlerSelfPlayJob (独自玩耍) | SoloPlay | 独自想象游戏 |
| RimTalk_ToddlerMutualPlayJob (相互玩耍) | SocialPlay | 双人社交互动 |
| RimTalk_ToddlerMutualPlayPartnerJob (玩伴) | SocialPlay | 双人社交互动 |
| RimTalk_ToddlerPlayAtToy (玩具玩耍) | ToyPlay | 使用专用玩具 |
| RimTalk_ToddlerObserveAdultWork (观察成人) | Observation | 观察学习 |
| RimTalk_WatchToddlerPlayJob (成人观看) | SocialPlay | 社交互动 |
| RimTalk_MidnightSnack (午夜偷吃) | 无 | 不计入无聊系统 |

#### 未来活动分类预设

| 活动 | 类别 | 理由 |
|------|------|------|
| RimTalk_ToddlerListenStory (听故事) | Passive | 被动接收型活动 |
| RimTalk_ToddlerExploreWild (野外探索) | Exploration | 探索活动 |
| RimTalk_ToddlerPlayWater (玩水) | Exploration | 探索活动 |
| RimTalk_ToddlerWatchAnimal (观看动物) | Observation | 观察活动 |
| RimTalk_ToddlerGroupPlay (多人游戏) | SocialPlay | 社交互动 |

---

## 可扩展的娱乐类型注册机制

### 设计目标

1. **模块化**：新娱乐活动可以通过简单的注册加入系统
2. **兼容性**：支持其他模组注册自定义娱乐类型
3. **灵活性**：允许将新活动归入现有类别或创建新类别
4. **可配置**：通过 XML 或代码配置活动分类

### 核心接口设计

```csharp
/// <summary>
/// 幼儿玩耍类别枚举（可扩展）
/// </summary>
public enum ToddlerPlayCategory
{
    None = 0,
    SoloPlay = 1,       // 独自玩耍
    SocialPlay = 2,     // 社交玩耍
    ToyPlay = 3,        // 玩具玩耍
    Observation = 4,    // 观察学习
    Media = 5,          // 媒体娱乐
    Passive = 6,        // 被动娱乐（听故事等）
    Exploration = 7,    // 探索活动（野外探索、玩水等）
    Creative = 8,       // 创造活动（绘画、堆雪人等）
    Custom = 100        // 自定义类别起始值
}

/// <summary>
/// 娱乐活动注册信息
/// </summary>
public class ToddlerPlayRegistration
{
    public string JobDefName { get; set; }           // JobDef 名称
    public ToddlerPlayCategory Category { get; set; } // 所属类别
    public float BoredomWeight { get; set; } = 1.0f; // 无聊权重（可选）
    public string ModId { get; set; }                // 来源模组ID（可选）
}

/// <summary>
/// 娱乐活动注册管理器
/// </summary>
public static class ToddlerPlayRegistry
{
    // 已注册的活动映射
    private static Dictionary<string, ToddlerPlayRegistration> _registrations;
    
    // 自定义类别映射（用于其他模组注册新类别）
    private static Dictionary<string, int> _customCategories;
    
    /// <summary>
    /// 注册一个娱乐活动
    /// </summary>
    public static void Register(string jobDefName, ToddlerPlayCategory category, 
                                float boredomWeight = 1.0f, string modId = null);
    
    /// <summary>
    /// 批量注册娱乐活动
    /// </summary>
    public static void RegisterBatch(IEnumerable<ToddlerPlayRegistration> registrations);
    
    /// <summary>
    /// 注册自定义类别
    /// </summary>
    public static int RegisterCustomCategory(string categoryName);
    
    /// <summary>
    /// 获取活动的类别
    /// </summary>
    public static ToddlerPlayCategory GetCategory(JobDef jobDef);
    
    /// <summary>
    /// 获取活动的无聊权重
    /// </summary>
    public static float GetBoredomWeight(JobDef jobDef);
    
    /// <summary>
    /// 检查活动是否已注册
    /// </summary>
    public static bool IsRegistered(string jobDefName);
}
```

### XML 配置支持

允许通过 XML 文件配置活动分类，便于其他模组扩展：

```xml
<!-- Defs/ToddlerPlayCategoryDefs/PlayCategories_RimTalk.xml -->
<Defs>
    <RimTalk.ToddlerPlayCategoryDef>
        <defName>RimTalk_PlayCategories</defName>
        <registrations>
            <!-- 现有活动 -->
            <li>
                <jobDefName>RimTalk_ToddlerSelfPlayJob</jobDefName>
                <category>SoloPlay</category>
                <boredomWeight>1.0</boredomWeight>
            </li>
            <li>
                <jobDefName>RimTalk_ToddlerMutualPlayJob</jobDefName>
                <category>SocialPlay</category>
                <boredomWeight>1.0</boredomWeight>
            </li>
            <!-- 未来活动 -->
            <li>
                <jobDefName>RimTalk_ToddlerListenStory</jobDefName>
                <category>Passive</category>
                <boredomWeight>0.8</boredomWeight>
            </li>
            <li>
                <jobDefName>RimTalk_ToddlerExploreWild</jobDefName>
                <category>Exploration</category>
                <boredomWeight>1.2</boredomWeight>
            </li>
        </registrations>
    </RimTalk.ToddlerPlayCategoryDef>
</Defs>
```

### 代码注册示例

其他模组可以通过代码注册新的娱乐活动：

```csharp
// 在模组初始化时注册
[StaticConstructorOnStartup]
public static class MyModPlayRegistration
{
    static MyModPlayRegistration()
    {
        // 注册单个活动到现有类别
        ToddlerPlayRegistry.Register(
            jobDefName: "MyMod_ToddlerNewActivity",
            category: ToddlerPlayCategory.SocialPlay,
            boredomWeight: 1.0f,
            modId: "MyMod"
        );
        
        // 注册自定义类别
        int customCategoryId = ToddlerPlayRegistry.RegisterCustomCategory("MyMod_SpecialPlay");
        
        // 注册活动到自定义类别
        ToddlerPlayRegistry.Register(
            jobDefName: "MyMod_ToddlerSpecialActivity",
            category: (ToddlerPlayCategory)customCategoryId,
            boredomWeight: 0.5f,
            modId: "MyMod"
        );
    }
}
```

### 自动检测机制

对于未注册的活动，系统提供自动检测回退机制：

```csharp
public static ToddlerPlayCategory AutoDetectCategory(JobDef jobDef)
{
    // 1. 检查是否有 CompToddlerToy 目标
    if (HasToddlerToyTarget(jobDef))
        return ToddlerPlayCategory.ToyPlay;
    
    // 2. 检查 JoyKind
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
    
    // 3. 检查 JobDef 名称模式
    string name = jobDef.defName.ToLower();
    if (name.Contains("watch") || name.Contains("observe"))
        return ToddlerPlayCategory.Observation;
    if (name.Contains("play") && name.Contains("toy"))
        return ToddlerPlayCategory.ToyPlay;
    if (name.Contains("mutual") || name.Contains("social") || name.Contains("group"))
        return ToddlerPlayCategory.SocialPlay;
    if (name.Contains("explore") || name.Contains("water"))
        return ToddlerPlayCategory.Exploration;
    if (name.Contains("draw") || name.Contains("build") || name.Contains("create"))
        return ToddlerPlayCategory.Creative;
    if (name.Contains("listen") || name.Contains("story"))
        return ToddlerPlayCategory.Passive;
    
    // 4. 默认返回 None
    return ToddlerPlayCategory.None;
}
```

---

## 无聊机制实现

### 核心概念

1. **活动历史追踪**：记录幼儿最近进行的娱乐活动类别
2. **无聊倍率计算**：根据重复次数计算娱乐增益倍率
3. **衰减机制**：随时间推移，无聊程度逐渐恢复
4. **权重调整**：不同活动可以有不同的无聊权重

### 数据结构

```csharp
public class ToddlerBoredomTracker : IExposable
{
    // 每个类别的最近使用时间和次数
    private Dictionary<ToddlerPlayCategory, int> recentPlayCounts;
    private Dictionary<ToddlerPlayCategory, int> lastPlayTick;
    
    // 获取某类别的无聊倍率 (0.0 - 1.0)
    public float GetBoredomMultiplier(ToddlerPlayCategory category);
    
    // 记录一次玩耍活动（考虑权重）
    public void RecordPlay(ToddlerPlayCategory category, float weight = 1.0f);
    
    // 每 tick 更新衰减
    public void Tick();
    
    // 保存/加载
    public void ExposeData();
}
```

### 无聊倍率计算

```
基础倍率 = 1.0 - (重复次数 * 0.15)
最终倍率 = Max(基础倍率, 最小倍率)
最小倍率 = 0.3 (即使非常无聊也有30%效果)
```

| 重复次数 | 倍率 |
|----------|------|
| 0 | 1.0 |
| 1 | 0.85 |
| 2 | 0.70 |
| 3 | 0.55 |
| 4 | 0.40 |
| 5+ | 0.30 |

### 衰减机制

- 每 2500 ticks (约1小时游戏时间) 减少1次重复计数
- 不同类别独立计算
- 睡眠时衰减速度加倍

---

## 实现步骤

### 1. 创建类别枚举和注册系统

文件：`Source/Core/ToddlerPlayCategory.cs`
文件：`Source/Core/ToddlerPlayRegistry.cs`

### 2. 创建无聊追踪器

文件：`Source/Core/ToddlerBoredomTracker.cs`

### 3. 创建 GameComponent 管理追踪器

文件：`Source/Core/ToddlerBoredomGameComponent.cs`

### 4. 创建 XML Def 类型

文件：`Source/Core/ToddlerPlayCategoryDef.cs`

### 5. Harmony Patch 修改娱乐增益

Patch `Need_Play.GainJoy` 或相关方法，应用无聊倍率

### 6. 在 JobDriver 中记录活动

在各个 JobDriver 的完成回调中调用 `RecordPlay()`

### 7. 创建默认配置 XML

文件：`Defs/ToddlerPlayCategoryDefs/PlayCategories_RimTalk.xml`

---

## 配置选项

在 `ToddlersExpansionSettings` 中添加：

```csharp
public bool enableBoredomSystem = true;
public float boredomMultiplierMin = 0.3f;
public float boredomDecayRate = 0.15f;
public int boredomDecayIntervalTicks = 2500;
public bool enableAutoDetection = true;  // 自动检测未注册活动的类别
```

---

## 附录：活动来源汇总表

| 来源 | 活动数量 | 主要类型 |
|------|----------|----------|
| 原版 Biotech | 5 | 基础照顾 |
| Toddlers 模组 | 7 | 独立玩耍 |
| RimTalk Expansion (现有) | 7 | 社交/学习 |
| RimTalk Expansion (计划) | 8 | 探索/创造 |
| **总计** | **27** | - |

---

## 附录：类别无聊独立性说明

不同类别之间的无聊是**独立计算**的，这意味着：

- 幼儿连续进行3次"独自玩耍"后，独自玩耍的效率降低到55%
- 但此时进行"社交玩耍"仍然是100%效率
- 这鼓励幼儿进行多样化的活动

---

**文档版本**: 3.0
**最后更新**: 2026-01-24
**状态**: 待实现
