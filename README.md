# 疯狂割草

一个使用 Unity 制作的 2D Roguelite 割草生存游戏。玩家只需专注移动与走位，武器会自动攻击；通过拾取经验、选择能力、开启宝箱并击败最终 Boss 完成一局游戏。

![Unity](https://img.shields.io/badge/Unity-2022.3.61f1c1-000000?logo=unity&logoColor=white)
![Language](https://img.shields.io/badge/Language-C%23-512BD4?logo=csharp&logoColor=white)
![Genre](https://img.shields.io/badge/Genre-2D%20Roguelite-E85D75)

## 🎮 游戏演示

![疯狂割草游戏演示](Docs/疯狂割草演示gif.gif)

## ✨ 核心玩法

- **移动与自动战斗**：使用键盘、手柄或触屏摇杆控制角色移动，能力按各自的攻击逻辑自动释放。
- **局内成长**：收集经验宝石提升等级，并从随机候选中选择新能力或强化已有能力。
- **丰富的能力组合**：包含枪械、手里剑、回旋镖、近战斩击、燃烧瓶、手榴弹、重力井及多种属性强化。
- **动态敌潮**：刷怪频率、怪物类型和生命倍率随关卡进度变化，并在指定时间生成 Mini Boss 与最终 Boss。
- **收集与掉落**：支持金币、经验、磁铁、炸弹、恢复物品、宝箱和局内背包。
- **数据驱动配置**：角色、关卡、怪物、宝箱和掉落内容通过 `ScriptableObject` 蓝图配置。

## ⚙️ 技术特点

- 使用 `UnityEngine.Pool.ObjectPool<T>` 管理怪物、投射物、投掷物、回旋镖、经验宝石、金币、宝箱和伤害文字。
- 使用 `SpatialHashGrid` 维护实体空间索引，降低附近目标查询的开销；网格会随玩家移动按需重建。
- 由 `LevelBlueprint` 与 `MonsterSpawnTable` 驱动关卡时长、敌人权重、生成速率和生命成长曲线。
- 使用统一的 `Ability` 继承体系实现武器、被动属性和升级逻辑。
- 支持 Unity Input System、触屏虚拟摇杆、安全区域适配和 Unity Localization。
- 通过无限背景、对象复用和自定义 `FastList` 支撑高实体数量场景。

## 🧰 环境要求

| 项目 | 版本 |
| --- | --- |
| Unity Editor | `2022.3.61f1c1` |
| Unity Input System | `1.14.0` |
| Unity Localization | `1.5.2` |
| TextMesh Pro | `3.0.7` |
| Unity Test Framework | `1.1.33` |

建议通过 Unity Hub 安装与项目完全一致的编辑器版本。首次打开时，Unity Package Manager 会根据 `Packages/manifest.json` 自动恢复依赖。

## 🚀 快速开始

1. 获取项目源码，并通过 Unity Hub 选择 **Add project from disk**。
2. 选择项目根目录，使用 Unity `2022.3.61f1c1` 打开项目。
3. 等待资源导入和 Package Manager 依赖恢复完成。
4. 打开场景 `Assets/Scenes/Game/Main Menu.unity`。
5. 点击 Unity Editor 顶部的 **Play** 按钮运行游戏。

构建设置中已按以下顺序配置游戏场景：

1. `Assets/Scenes/Game/Main Menu.unity`
2. `Assets/Scenes/Game/Level 1.unity`

## 🕹️ 操作方式

| 设备 | 操作 |
| --- | --- |
| 键盘 | `WASD` 或方向键移动 |
| 手柄 | 左摇杆移动 |
| 移动端 | 屏幕虚拟摇杆移动 |

角色能力会自动寻找目标或按角色朝向释放。升级、角色选择、暂停和背包道具通过对应的 UI 控件操作。

## 🧩 核心模块

| 模块 | 职责 |
| --- | --- |
| `LevelManager` | 初始化关卡系统，推进计时，调度普通怪、Boss 和宝箱，处理胜负结算。 |
| `EntityManager` | 统一负责实体生成、回收、对象池索引、活跃实体集合与空间网格。 |
| `AbilityManager` | 管理已拥有/未拥有能力、加权候选抽取、升级条件与可升级数值。 |
| `Character` | 处理移动、生命、经验、等级、受伤、击退和角色属性。 |
| `LevelBlueprint` | 配置关卡时长、背景、能力池、怪物、Boss、宝箱与初始经验宝石。 |
| `MonsterSpawnTable` | 根据关卡进度插值计算刷怪速率、怪物权重和生命倍率。 |
| `SpatialHashGrid` | 为角色、怪物等实体提供插入、移除、更新和邻域查询。 |
