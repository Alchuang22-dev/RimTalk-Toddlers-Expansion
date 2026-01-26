# 幼儿背负系统 (Toddler Carrying System)

## 概述

幼儿背负系统允许成年人"抱着"幼儿移动，使得商队、访客等场景中的幼儿能够跟随成年人。这解决了幼儿作为独立pawn存在时的移动问题。

### 效果预览
- 幼儿会显示在成年人的胸前/侧面
- 幼儿的Job会显示为"被 XXX 抱着"
- 成年人的Job会显示为"做某事 - 抱着 XXX"

## 核心组件

### 1. ToddlerCarryingUtility (公共API)

主要公共方法：

```csharp
// 让载体背起幼儿
bool TryMountToddler(Pawn carrier, Pawn toddler);

// 让幼儿从载体身上下来
bool DismountToddler(Pawn toddler);

// 获取背着指定幼儿的载体
Pawn GetCarrier(Pawn toddler);

// 获取载体背着的所有幼儿
List<Pawn> GetCarriedToddlers(Pawn carrier);

// 检查幼儿是否正在被背着
bool IsBeingCarried(Pawn toddler);

// 检查pawn是否正在背着幼儿
bool IsCarryingToddler(Pawn carrier);

// 自动为群组分配背负关系
void AutoAssignCarryingForGroup(List<Pawn> pawns);

// 清除所有与指定pawn相关的背负关系
void ClearAllCarryingRelations(Pawn pawn);
```

### 2. ToddlerCarryingTracker (内部追踪器)

追踪所有背负关系的静态类，提供：
- 背负关系的注册/取消注册
- 获取载体和被背幼儿的映射
- 无效条目清理

### 3. Patch_ToddlerCarrying (Harmony补丁)

处理渲染和位置同步：
- `DrawPos` - 修改被背幼儿的渲染位置，使其显示在载体胸前
- `Pawn.Tick` - 每tick同步被背幼儿的位置
- `Pawn.DeSpawn/Kill` - 在pawn移除/死亡时清理背负关系

### 4. ToddlerCarryingGameComponent

定期清理无效的背负关系（每10秒）。

## 视觉效果

被背的幼儿会渲染在载体的胸前位置：
- 面向北：幼儿在背后（略低）
- 面向南：幼儿在胸前
- 面向东/西：幼儿在侧面

偏移量可在 `ToddlerCarryingUtility.CarryOffsets` 中调整。

## 使用场景

### 商队生成
在 `TravelingPawnInjectionUtility.TryInjectToddlerOrChildPawns` 中，当添加幼儿后自动调用 `AutoAssignCarryingForGroup`。

### Lord创建
在 `Patch_TravelingLord.Lord_Postfix` 中，对商队、访客等Lord自动分配背负关系。

### 手动使用
```csharp
// 让carrier背着toddler
if (ToddlerCarryingUtility.TryMountToddler(carrier, toddler))
{
    // 成功
}

// 让toddler下来
ToddlerCarryingUtility.DismountToddler(toddler);
```

## 调试命令

在开发者模式下，"RimTalk Toddlers" 分类中提供以下命令：
- **Show carrying status** - 显示当前所有背负关系
- **Force mount selected toddler** - 强制让最近的成年人背起选中的幼儿
- **Force dismount selected toddler** - 强制让选中的幼儿下来
- **Clear all carrying relations** - 清除所有背负关系
- **Test auto-assign carrying for visitors** - 测试自动分配功能

## 限制

1. 每个载体最多背1个幼儿（可通过 `GetMaxCarryCapacity` 调整）
2. 只有幼儿和婴儿可以被背
3. 载体必须是能移动的成年人类
4. 背负关系不持久化（游戏保存后不保留）

## 扩展

### 增加背负容量
修改 `ToddlerCarryingUtility.GetMaxCarryCapacity` 方法。

### 调整渲染偏移
修改 `ToddlerCarryingUtility.CarryOffsets` 字典。

### 添加新的自动分配触发点
调用 `ToddlerCarryingUtility.AutoAssignCarryingForGroup(pawns)` 即可。