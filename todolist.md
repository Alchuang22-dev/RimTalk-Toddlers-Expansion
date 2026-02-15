RimTalk Expand Toddlers 

- 添加更多玩具
- 婴幼儿的噩梦/梦游精神状态
- 完善婴幼儿访客机制
- 婴幼儿的“偷吃糖果”等有趣的行为和精神状态
- 捉迷藏活动
- 哭泣应对
- 搞破坏
- 抱着幼儿时被攻击应对
- 给路过小孩送衣服



严重: Pawn.Tick 热路径里存在“每 pawn 每 tick 分配 List”的模式，极易造成 GC 抖动。
Patch_ToddlerCarrying.cs (line 39)、Patch_ToddlerCarrying.cs (line 165)、Patch_ToddlerCarrying.cs (line 203)
ToddlerCarryingUtility.cs (line 201)、ToddlerCarryingUtility.cs (line 211)、ToddlerCarryingUtility.cs (line 176)
ToddlerCarryingTracker.cs (line 105)、ToddlerCarryingTracker.cs (line 109)、ToddlerCarryingTracker.cs (line 117)
问题点：IsCarryingToddler -> GetCarriedToddlerCount -> GetCarriedToddlers 在非 carrier 也会 new 空列表。

严重: 渲染热路径中重复反射查找/调用（每帧可能多次）。
Patch_BabyHairRendering.cs (line 37)、Patch_BabyHairRendering.cs (line 72)、Patch_BabyHairRendering.cs (line 75)
Patch_ToddlerBathRendering.cs (line 55)、Patch_ToddlerBathRendering.cs (line 123)、Patch_ToddlerBathRendering.cs (line 209)、Patch_ToddlerBathRendering.cs (line 218)
Patch_ToddlerCarrying.cs (line 447)、Patch_ToddlerCarrying.cs (line 457)
问题点：AccessTools.Method/Field 和 MethodInfo.Invoke 在 draw 路径重复执行。

高: 无聊系统清理逻辑疑似退化为“几乎每 tick 清理一次”。
ToddlerBoredomGameComponent.cs (line 87)、ToddlerBoredomGameComponent.cs (line 99)、ToddlerBoredomGameComponent.cs (line 101)、ToddlerBoredomGameComponent.cs (line 108)
问题点：小时清理和日更新共用 _lastDailyTick，超过 1 小时后条件长期为真。

高: 夜宵 JobGiver 同一轮判定里重复全图食物扫描，且每个候选都做可达/预留检查。
JobGiver_MidnightSnack.cs (line 25)、JobGiver_MidnightSnack.cs (line 51)、JobGiver_MidnightSnack.cs (line 106)、JobGiver_MidnightSnack.cs (line 114)、JobGiver_MidnightSnack.cs (line 147)、JobGiver_MidnightSnack.cs (line 150)
MidnightSnackGameComponent.cs (line 52)、MidnightSnackGameComponent.cs (line 58)

中: 多个 ThinkNode/JobGiver 在 CanDo 和 TryGiveJob 双重重复搜索（伙伴、玩具、位置、成人）。
ToddlerPlayGivers_RimTalk.cs (line 36)、ToddlerPlayGivers_RimTalk.cs (line 51)、ToddlerPlayGivers_RimTalk.cs (line 112)、ToddlerPlayGivers_RimTalk.cs (line 128)、ToddlerPlayGivers_RimTalk.cs (line 244)、ToddlerPlayGivers_RimTalk.cs (line 254)、ToddlerPlayGivers_RimTalk.cs (line 374)、ToddlerPlayGivers_RimTalk.cs (line 384)

中: 自洗澡清洁值获取每次先做反射字段/属性搜索，再用缓存，顺序反了。
ToddlerSelfBathGameComponent.cs (line 735)、ToddlerSelfBathGameComponent.cs (line 748)、ToddlerSelfBathGameComponent.cs (line 778)、ToddlerSelfBathGameComponent.cs (line 785)、ToddlerSelfBathGameComponent.cs (line 800)

中: 语言学习 bootstrap 在初始完成后仍每 2500 tick 扫描全部地图 pawn + 世界 pawn。
LanguageLearningBootstrapComponent.cs (line 33)、LanguageLearningBootstrapComponent.cs (line 49)、LanguageLearningBootstrapComponent.cs (line 77)、LanguageLearningBootstrapComponent.cs (line 97)

低: 潜在重试循环无上限（概率循环）。
TravelingPawnInjectionUtility.cs (line 251)、TravelingPawnInjectionUtility.cs (line 254)
while (Rand.Chance(...)) 理论上可长尾。

低（条件触发）: 高频路径中的异常日志可能在异常持续时刷屏。
Patch_ExitMapDuty.cs (line 40)、Patch_ExitMapDuty.cs (line 64)
Patch_TravelingLord.cs (line 291)、Patch_TravelingLord.cs (line 309)、Patch_TravelingLord.cs (line 318)、Patch_TravelingLord.cs (line 333)

补充结论（对应你提的四类）

高频扫描：确实存在，最重的是 Pawn.Tick 级别和若干 ThinkNode 的重复搜索。
重试循环：发现 1 处无上限概率循环（低概率长尾）。
高频日志：绝大多数 Log.Message 已受 Prefs.DevMode 保护；但异常日志在热路径里有刷屏风险。
高频读写：未发现文件 I/O 热路径，主要是内存扫描/反射/路径计算问题。
假设/不确定项

这是静态审查，未结合运行时 profiler 采样。
ToddlerBoredomGameComponent 是否始终注册需你这边再确认（若未注册，第 3 条影响会小很多）。