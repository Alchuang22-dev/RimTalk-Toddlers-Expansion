# RimAudio

## 项目定位

`RimAudio` 计划仿照 `RimScent` 的实现思路，为 RimWorld 建立一套轻量、数据驱动、可扩展的“环境听觉系统”。

它的目标不是模拟真实声波传播，而是提供一个足够稳定、足够便于 patch、且对玩法有反馈价值的“听觉扫描引擎”：

- pawn 会周期性扫描周围声音来源
- 根据听觉能力和听觉敏感度判断是否“听见”
- 将听觉结果转化为 `ThoughtDef`、状态修正或后续行为钩子
- 允许其他模组通过 XML patch 给 `ThingDef` / `HediffDef` / `WeatherDef` / `GameConditionDef` 挂载声音来源

这套设计与 `RimScent` 的关系可以概括为：

- `RimScent` 是“闻到什么”
- `RimAudio` 是“听到什么”

## 核心设计原则

### 1. 数据驱动优先

和 `RimScent` 一样，`RimAudio` 不应把所有声音来源写死在 C# 里。核心引擎只负责：

- 周期扫描
- 过滤来源
- 计算有效强度
- 应用 thought / memory / 状态

具体什么东西会发声，应该由 Def 上的扩展来描述。

### 2. 轻量扫描，而不是真实物理模拟

我们不追求复杂的声场、混响、频段传播和材质反射。

引擎应采用“半径扫描 + 条件过滤 + 强度衰减”的方式：

- 性能可控
- 行为可预测
- 对 XML modder 友好
- 足以支撑 baby / toddler / colonist 的环境反馈

### 3. 听觉不只用于负面反馈

和嗅觉系统一样，听觉既可以表达厌恶，也可以表达安抚、安心和熟悉感。

例如：

- 枪声、爆炸声、机器轰鸣、尖叫声可产生负面 thought
- 雨声、虫鸣、柔和音乐、摇篮声、熟悉的照料声可产生正面 thought

## 引擎结构

建议核心结构与 `RimScent` 保持高度一致，便于维护和理解。

### 1. Pawn 组件

给 `ThingDef Human` 添加一个类似下面概念的组件：

- `Pawn_AudioTracker`

该组件负责在 `CompTick()` 中周期触发听觉扫描。

建议行为：

- 默认每 `500` tick 扫描一次
- 使用随机 tick offset 分摊性能压力
- 只对已生成、可感知、拥有相关 need 的 pawn 生效

### 2. 听觉 Capacity

新增一个自定义 capacity，例如：

- `RimAudio_Hearing`

它用于表示 pawn 的基础听觉能力。

计算思路可仿照 `RimScent_Smell`：

- 检查耳朵相关 body part
- 根据 body part 效率计算 capacity
- 若没有可用耳部，听觉能力为 0

这会让耳部损伤、仿生耳、特殊种族结构都能自然接入系统。

### 3. 听觉敏感度 Stat

新增一个 stat，例如：

- `RimAudio_HearingSensitivity`

它用于表达 pawn 对声音的主观敏感程度。

这个 stat 适合被以下内容修改：

- trait
- gene
- apparel
- hediff
- implant
- time

例如：

- 听觉迟钝者：降低敏感度
- 听觉过敏者：提高敏感度
- 降噪头盔 / 防噪耳罩：降低敏感度
- 婴儿 / 幼儿：可根据设计提高特定声音敏感度
- 是否在夜间

### 4. 声音来源扩展

新增一个 Def 扩展，例如：

- `ModExtension_Audio`

建议至少包含以下字段：

- `thought`: 听到后施加的 `ThoughtDef`
- `radius`: 该声音的基础传播半径
- `loudness`: 基础响度，用于决定优先级和强弱
- `falloff`: 距离衰减方式
- `requireLineOfSight`: 是否要求无遮挡
- `indoorsOnly`: 是否仅室内有效
- `outdoorsOnly`: 是否仅室外有效
- `minInterval`: 最短触发间隔
- `tags`: 声音分类标签，例如 `machine`, `baby`, `combat`, `weather`, `comfort`

如果希望和 RimScent 一样先做最小版本，也可以先只保留：

- `thought`
- `radius`
- `loudness`

其他字段以后再扩展。

## 听觉扫描逻辑

建议 `UpdateAudio(Pawn pawn)` 的总体流程如下。

### 1. 计算听觉因子

先计算：

- `hearingCapacity = pawn.health.capacities.GetLevel(RimAudio_Hearing)`
- `hearingSensitivity = pawn.GetStatValue(RimAudio_HearingSensitivity)`
- `hearingFactor = hearingCapacity * hearingSensitivity`

当 `hearingFactor <= 0` 时：

- 清理活动中的听觉 thought
- 直接退出

### 2. 收集来源

声音来源建议支持四类，与 `RimScent` 保持一致：

- `ThingDef`
- `HediffDef`
- `WeatherDef`
- `GameConditionDef`

具体来说：

- 地图上的建筑、机器、火焰、乐器、玩具、婴儿床、收音机等来自 `ThingDef`
- pawn 身上的哭闹、咳嗽、打鼾、疼痛呻吟、机械噪声来自 `HediffDef`
- 雨、雷暴、风暴来自 `WeatherDef`
- 空袭、轰炸、异常事件、毒雨警报等来自 `GameConditionDef`

### 3. 空间过滤

可参考 `RimScent` 的扫描方式：

- 以 pawn 为中心扫描固定半径
- 超出地图边界则跳过
- 若启用 `homeOnly`，则只处理 home area 内来源
- 可选地要求 `LineOfSight`

但听觉和嗅觉的差异在于：

- 听觉不一定必须依赖严格视线
- 更适合使用“墙体衰减”而不是“完全听不到”

因此建议分两层实现：

- 第一阶段：最小版本，复用 `LineOfSight` 作为简单遮挡判断
- 第二阶段：改成“无视线也能听见，但强度降低”

### 4. 室内外规则

`RimScent` 已经做了室内外和房间判断，`RimAudio` 也应保留类似概念。

建议规则：

- 室内机器声优先在同房间传播
- 室外风雨声优先影响室外 pawn
- 爆炸、枪声等高响度声音可以跨房间传播

也就是说，声音类型不同，传播限制应不同。

最小版本可以统一采用：

- 普通声源：同房间或同室外区域
- 高响度声源：忽略房间限制，仅按半径和遮挡判断

## Thought 施加逻辑

### 1. 强度与优先级

和 `RimScent` 一样，声音 thought 可以按“强度”决策。

建议强度计算类似：

`finalStrength = loudness * hearingFactor * distanceFactor * occlusionFactor`

其中：

- `loudness`：声源自身定义
- `hearingFactor`：pawn 的听觉能力和敏感度
- `distanceFactor`：距离衰减
- `occlusionFactor`：墙体 / 房间 / 遮挡修正

### 2. 默认只保留最强声音

最小版本建议保持与 `RimScent` 一致：

- 默认模式下只保留当前最强的一个声音 thought
- 开启 `uncappedAudio` 后允许多个声音同时生效

这样行为稳定，也更容易调试。

### 3. 支持堆叠

类似 `allowMoodStacking`：

- 相同 thought 可根据来源数量堆叠
- 但应受 `stackLimit` 限制

例如：

- 附近有多个哭闹婴儿，`crying noise` 可叠层
- 多台机器同时运行时，`machine hum` 可叠层

### 4. 不立刻清空，而依赖持续时间

和 `RimScent` 一样，建议 `ThoughtDef` 使用短持续时间，例如 `0.05 ~ 0.1 days`。

理由：

- 可减少频繁增删 memory 的抖动
- 玩家体验更平滑
- 有利于处理断续声音

## 听觉 Trait / Gene 设计建议

建议预留与嗅觉系统平行的角色特性。

例如：

- `HardOfHearing`
  - 大幅降低 `RimAudio_HearingSensitivity`
- `KeenHearing`
  - 提高 `RimAudio_HearingSensitivity`
- `SoundSensitive`
  - 对负面噪音更敏感
- `AudioSeeking`
  - 对音乐、摇篮声、雨声等正向声音更敏感

如果要进一步做性格化，还可以允许某些 `ModExtension_Audio` 定义“反转偏好”或“指定 trait 喜恶变化”。

## 与婴儿 / 幼儿系统的结合点

这是 `RimAudio` 相比 `RimScent` 更有特色的部分。

### 1. 婴儿是声音来源

婴儿和幼儿本身可以通过 `HediffDef` 或状态组件提供声音来源，例如：

- 哭闹
- 咯咯笑
- 咳嗽
- 打鼾
- 吐奶后的不适哼声

这些来源适合挂在：

- `HediffDef`
- 或将来专门的 `CompAudioEmitter`

### 2. 婴儿也是高敏感听众

婴儿和幼儿不只是发声源，也应是重要的“听众”：

- 对爆炸、枪声、火灾、机器巨响更敏感
- 对照料者声音、摇篮、白噪音、轻柔音乐更容易获得安抚
- 对夜间突发噪音可能产生惊醒、烦躁、哭闹链式反应

这部分可以不一开始就写死进引擎，而是通过：

- `ThoughtDef`
- `trait`
- `stat offset`
- 自定义 hediff / need

逐步实现。

### 3. 可进一步扩展为行为驱动

后续如果要做得更深入，听觉系统还能作为 AI 行为的触发器：

- 婴儿听到母亲或照料者靠近时停止哭闹
- 幼儿听到枪声后进入害怕状态
- colonist 听到婴儿哭声后提高照料 job 优先级
- 动物或敌对 pawn 根据巨大噪音做出反应

这部分属于第二阶段，不建议一开始混入基础引擎。

## 最小可实现版本

如果以 `RimScent` 为模板，`RimAudio` 的第一版建议只做以下内容：

### C# 层

- `Pawn_AudioTracker`
- `PawnCapacityWorker_Hearing`
- `ModExtension_Audio`
- `RimAudioSettings`
- `RimAudioMod`

### Def 层

- `PawnCapacityDef`: `RimAudio_Hearing`
- `StatDef`: `RimAudio_HearingSensitivity`
- 一批基础 `ThoughtDef`

### Patch 层

- 给 `ThingDef Human` 添加 `Pawn_AudioTracker`
- 给基础世界对象挂 `ModExtension_Audio`
- 给若干 hediff / weather / game condition 挂 `ModExtension_Audio`

### 建议第一批声音来源

- `Fire` -> 火焰噪声
- 发电机 -> 机器轰鸣
- 雨 / 雷暴 -> 环境声
- 爆炸相关 hediff / filth / condition -> 巨响 / 余震声
- 婴儿哭闹 hediff -> 哭声
- 婴儿床 / 安抚装置 -> 白噪音 / 摇篮声

## 第一版建议设置项

建议直接对齐 `RimScent` 的设置风格：

- `audioTickInterval`
- `audioRadius`
- `uncappedAudio`
- `allowMoodStacking`
- `homeOnly`
- `colonistsCanHear`
- `prisonersCanHear`
- `slavesCanHear`
- `friendlyFactionsCanHear`
- `enemyFactionsCanHear`

这样用户理解成本最低。

## 设计结论

`RimAudio` 最适合走一条和 `RimScent` 高度平行的路线：

- 用 `Pawn_AudioTracker` 做周期扫描
- 用 `Hearing capacity + Hearing sensitivity` 做感知强度
- 用 `ModExtension_Audio` 声明来源
- 用 `ThoughtDef` 承载情绪反馈
- 用 XML patch 扩展兼容性

这条路线的优点是：

- 结构简单
- 容易快速落地
- 非常适合做 baby / toddler 扩展
- 后续可以逐步演化成更复杂的行为系统

在此基础上，后续你可以再决定是否加入更复杂的内容：

- 墙体衰减
- 房间隔音
- 持续声源与瞬时声源的区分
- 婴幼儿专属声音标签
- 听觉触发 AI 反应

## 听觉内容

1. 环境声源

- 天气类
    - 雨/雾雨：安宁的雨声（+1）
    - 暴雨：令人烦躁的雨声（-1）
    - 雷暴/旱天雷：电闪雷鸣（-3）
- 地形类
    - 河流：流水（+1）
    - 浅海/深海水：波浪声（+1）
- 动物类
    - 苏醒的鸟类：小鸟啾啾（+3）
    - 苏醒的牲畜（畜栏动物）类：牲畜嘶鸣（-1）
    - 苏醒的野生动物：动物行走（+1）
    - 苏醒的捕食者：威胁的吼声（-3）
    - 宠物（待细化）：喵喵叫（+3）
- 植物类
    - 密集森林：树叶沙沙（+1）
- 特殊类
    - 火焰：火焰噼啪（-1）

2. 人造声源

- 娱乐类
    - 收音机/通电的电视机/有人操作的乐器：音乐（+3）
- 工作设施类
    - 正在工作的工作台：吵闹的机床（-1）
    - 正在工作的发电机：吵闹的发电机（-3）
    - 制冷机：电流声（-1）
    - 活动的机械体：机器人声（-1）

3. Pawn声源

- 密集的人群：人声鼎沸（trait决定）

4. 事件声源

- 战斗类
    - 主动触发，开枪：枪声（-3）
    - 主动触发，开炮：炮声（-3）
    - 受伤的敌人：刺耳哀嚎（-3）
    - 爆炸：爆炸（-5）
- 瞬时事件类
    - 主动触发，运输舱/穿梭机起飞：引擎声（-3）

5. 婴儿特有

- 咯咯笑的婴儿：其他宝宝在笑（+3）
- 哭泣的婴儿：其他宝宝在哭（-3）
- 父母：熟悉的声音（+5）
- 自己的床：宝宝的摇篮（+3）
- 玩具：玩具音乐（+3）

## 已确认的扫描实现规则

以下规则基于对 `reference/RimWorld` / `reference/Verse` 的确认，以及 `RimAudio` 作为 gameplay hearing system 的实现取舍。
其中一部分直接复用原版已有状态，另一部分是对原版对象做低复杂度抽象，不追求还原真实声学播放链路。

### 1. 天气 / 雷暴 / 雨

- 扫描对象：
  - `map.weatherManager`
  - `map.GameConditionManager`
- 判定条件：
  - 当前地图存在有效天气状态
  - 或当前地图存在会产生环境声的 `GameCondition`
- 读取方式：
  - 雨、雷暴、风暴等直接读取 `map.weatherManager.curWeather` 或相关 weather 状态
  - 特殊环境事件直接读取 `map.GameConditionManager.ActiveConditions` 或 `GetActiveCondition(...)`
- 说明：
  - 这是最简单、最稳定的一类持续环境声源
  - 第一版直接把天气 / 雷暴 / 雨视为全图级或室外优先级声源即可
  - 如后续需要，可再细分室内外衰减、屋顶遮挡和天气强度

### 2. 电视

- 扫描对象：`TubeTelevision` / `FlatscreenTelevision` / `MegascreenTelevision`
- 判定条件：
  - 建筑存在于扫描半径内
  - 有电力组件且 `CompPowerTrader.PowerOn == true`
  - 与听者同房间，或满足听觉系统放宽后的高响度跨房间规则
- 说明：
  - 原版电视本身有通电状态
  - 原版电视音效通过 `effectWatching -> WatchingTelevision -> Television_Ambience` 播放
  - 第一版 `RimAudio` 不需要真的追踪 effecter，只要把“通电的电视”视为稳定声源即可

### 3. 收音机 / 非插电娱乐设施

- 扫描对象：娱乐建筑或玩具类建筑
- 判定条件：
  - 建筑存在于扫描半径内
  - 若无电力需求，则默认视为可发声
  - 若有房间限制，则按距离 + 房间过滤
- 说明：
  - 这里的“收音机”在 `RimAudio` 中按设计语义处理，不要求 vanilla 必须存在一个同名建筑
  - 可作为 `ThingDef + ModExtension_Audio` 的泛化娱乐声源类别

### 4. 制冷机

- 扫描对象：`Building_Cooler`
- 判定条件：
  - 建筑存在于扫描半径内
  - `compPowerTrader.PowerOn == true`
- 说明：
  - 第一版不细分高功率 / 低功率工作声
  - 通电即视为持续低强度机器声
  - 如后续需要，可再根据 `operatingAtHighPower` 细分强弱

### 5. 动物

- 扫描对象：附近 animal pawn
- 判定条件：
  - `pawn.RaceProps.Animal == true`
  - `pawn.Awake() == true`
  - 位于扫描半径内
- 可选细分标签：
  - `predator`
  - `petness`
  - `packAnimal`
- 说明：
  - 第一版不区分原版是否真的正在播放叫声
  - 只把“清醒动物存在”抽象为环境动物声源

### 6. 机械体

- 扫描对象：附近 mech pawn
- 判定条件：
  - `pawn.RaceProps.IsMechanoid == true`
  - `pawn.Awake() == true`
  - 位于扫描半径内
- 说明：
  - 第一版直接将“清醒机械体”视为稳定机械噪声来源

### 7. 瞬时声源

- 处理方式：不做高频地图轮询，改为主动推送
- 适用对象：
  - 枪声
  - 近战打击声
  - 爆炸声
  - 起飞 / 引擎脉冲声
- 实现建议：
  - 在相关事件或 Harmony hook 触发时，向附近 pawn 主动分发一次短时 hearing event
  - 事件对象只保留极短寿命或直接当场结算
- 说明：
  - 这样比持续扫战斗现场更省性能
  - 也更符合 one-shot 声源特性

### 8. 敌人受伤

- 扫描对象：附近敌对 pawn
- 判定条件：
  - pawn 位于扫描半径内
  - 对听者或玩家阵营 hostile
  - pawn 当前受伤，或最近短时间内受伤
- 第一版建议：
  - 允许简化为“受伤中的敌对 pawn 视为会发出痛呼”
- 说明：
  - 这是一种 gameplay 抽象，不要求原版存在持续呻吟的显式状态

### 9. 婴儿建筑

- 扫描对象：
  - 婴儿床 / 婴儿专用床
  - 玩具箱
  - 其他安抚或白噪音设施
- 判定条件：
  - 建筑存在于扫描半径内
- 说明：
  - `Building_Bed.ForHumanBabies` 可用于识别婴儿床
  - `ThingDefOf.ToyBox` 可直接作为婴儿娱乐声源
  - 第一版允许“扫到即算发声”，后续再加开关或使用状态

### 10. 婴儿的父母

- 扫描对象：婴儿的父母 pawn
- 判定条件：
  - 父母位于扫描半径内
  - 可选要求：同房间，或至少无遮挡 / 低遮挡
- 说明：
  - 第一版把“父母在附近”抽象为熟悉的人声 / 安抚声源
  - 不要求父母必须正在社交、说话或执行 childcare job

### 11. 咯咯笑 / 哭泣

- 结论：可直接复用原版 Biotech childcare 相关 `ThoughtDef`
- 已确认可复用的原版定义：
  - `CryingBaby`
  - `MyCryingBaby`
  - `GigglingBaby`
  - `MyGigglingBaby`
- 来源：`reference/RimWorld/Data/Biotech/Defs/ThoughtDefs/Thoughts_Memory_Childcare.xml`
- 实现建议：
  - 当婴儿被判定为“哭泣声源”时，对附近 pawn 按关系施加 `CryingBaby` 或 `MyCryingBaby`
  - 当婴儿被判定为“咯咯笑声源”时，对附近 pawn 按关系施加 `GigglingBaby` 或 `MyGigglingBaby`
- 说明：
  - 这组 `ThoughtDef` 本来就是“听到婴儿声音”的 mood memory，和 `RimAudio` 的目标完全一致

## 第一版推荐落地口径

为保证实现速度和性能，第一版 `RimAudio` 建议统一采用以下三类口径：

- 持续建筑声源：
  - 建筑存在 + 满足 `PowerOn` / `IsBeingPlayed` / 自定义 active 条件
- 持续 pawn 声源：
  - pawn 存在 + `Awake()` + 符合物种 / 阵营 / 关系过滤
- 瞬时事件声源：
  - 不轮询，改为事件主动广播到附近 pawn

这样可以把绝大多数声源压缩到低复杂度的统一扫描框架中，后续再逐步加入墙体衰减、房间隔音、短时缓存和更细的强度分级。
