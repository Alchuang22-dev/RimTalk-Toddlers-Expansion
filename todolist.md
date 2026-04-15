CRITICAL 级别（会显著影响帧率）
1. YayoAnimation — 每帧每 Pawn 分配内存
文件: YayoAnimationCompatUtility.cs 第 864-894 行, 213-225 行

BuildEnabledPlayProfiles 每个渲染帧为每个 Pawn 创建 new List<SmallPawnPlayProfile>(16) 并调用 .ToArray()。100 个 Pawn 的地图上每帧产生 100 次堆分配 + GC 压力。同时 CheckAni_Prefix 还有多次未缓存的反射字段读取。

2. GenHostility.HostileTo postfix（刚添加的）
文件: Patch_YoungPawnCombatSafety.cs 第 86-125 行

HostileTo(Thing, Thing) 是 RimWorld 最热方法之一，战斗期间每个目标评估、阵营检查、危险检测都会调用。当前 postfix 每次调用都做 as Pawn + ShouldTreatHostileYoungAsColonist 完整检查。好在有 !__result 和 enableHostileToddlerColonistBehavior 两个早期退出，关闭设置时几乎零开销。

3. Log 前缀拦截所有 mod 日志
文件: Patch_ModLogFiltering.cs 第 38-46 行

对 Log.Message/Log.Warning/Log.Error/Log.WarningOnce/Log.ErrorOnce 全部挂了 prefix。游戏中任何 mod 的每次日志调用都要经过 ShouldSuppressModLogMessage，该方法内部做 4 次 text.Contains() 字符串匹配。

HIGH 级别（多项叠加会有明显影响）
4. 渲染链路上 6+ 个 postfix 叠加
每帧每个 Pawn 都要经过以下 postfix：

Postfix 目标	文件
Pawn.DrawPos	Patch_ToddlerBathRendering, Patch_ToddlerCarrying
PawnRenderer.BodyAngle	Patch_ToddlerBathRendering, Patch_ToddlerCarrying
PawnRenderer.LayingFacing	Patch_ToddlerCarrying
PawnRenderer.GetDrawParms	Patch_ToddlerBathRendering
PawnRenderNode_Hair.GraphicFor	Patch_BabyHairRendering
Pawn.CarriedBy	Patch_ToddlerCarrying
每个都有 early exit，但 6 个 postfix 叠在渲染热路径上，100 个 Pawn 时每帧执行 600+ 次 Harmony 调度。

5. Pawn_JobTracker.StartJob 被 3 个 patch 同时 hook
Patch_LearningGiver_NatureRunning.cs — postfix
Patch_BePlayedWithJobSafety.cs — prefix
Patch_ToddlerJobLogging.cs — postfix
每个 Pawn 每次开始 Job 都要跑三遍 patch 调度。

6. Job.GetCachedDriver prefix
文件: Patch_BePlayedWithJobSafety.cs 第 106 行

每次访问缓存的 JobDriver 都触发，极高频率。

MEDIUM 级别（值得优化但影响有限）
问题	文件	说明
ToddlerCarryDesireUtility.Tick 每帧跑	ToddlerCarryDesireUtility.cs:22	两个 HashSet + buffer copy per tick
LordToil_ChildrenOuting 每 tick 遍历 lord pawns	LordToil_ChildrenOuting.cs:87	每次聚会每 tick 遍历
全地图扫描每 90 tick	YayoAnimationSafeFallbackComponent.cs:~126	map.mapPawns.AllPawnsSpawned 全量遍历
4 个 rendering postfix 每帧做反射	Patch_ToddlerCarrying.cs:137-162	GetCarrier() 字典查找 per pawn per frame
建议优先处理
YayoAnimation BuildEnabledPlayProfiles — 用静态缓存 + dirty flag 替代每帧重建
渲染链路 postfix 合并 — 考虑合并 DrawPos/BodyAngle/GetDrawParms 上的多个 postfix 为更少的 patch 点，用统一的 toddler 快速判断减少重复检查
StartJob 三重 patch — 合并为一个 patch
Log prefix — 用 HarmonyMethod(Priority.First) + 更快的匹配（首字符哈希或前缀匹配替代 Contains）
需要我针对哪一项具体实施优化？