# 疯狂割草

游戏类型：2D 割草 / Roguelite

核心玩法：移动走位 + 自动攻击 + 三选一升级 + 宝箱掉落 + Boss 战

## 快速开始

Unity版本：2022.3.61f1c1

打开场景：”Main Menu“，Play按键即可。

## 项目结构

`Scripts/Managers`: `LevelManager`, `EntityManager`, `AbilityManager`, `StatsManager`

`Scripts/Abilities`: `Ability` 及其子类（Projectile/Melee/Throwable/Gun）

`Scripts/Entities`: Monster/Projectile/Collectable/Chest

`Scripts/Data`: Blueprints（ScriptableObject）与掉落表

`Scripts/UI`: DialogBox/AbilitySelectionDialog/PauseMenu/GameOverDialog

`Scripts/Utils`: SpatialHashGrid/FastList/ZPositioner 等

# 一、对象池逻辑

- 所有物品的对象池类结构都相同，对象池逻辑在xxxPool类中，生成和销毁逻辑在EntityManager类中，并且都是通过pool.Get()来生成。
- 调用pool.Release(coin);会执行以下逻辑**触发 `OnReturnedToPool` 回调**：执行 `projectile.gameObject.SetActive(false)`，**将 `projectile` 实例存入对象池的内部缓存容器**，并标记为可复用，下次再调用pool.Get()时，就会优先在**缓存**容器中取出，而不是Instantiate

## 1）各池内对应的物品

- Projectile池，主要是一些直来直去的子弹：火箭筒子弹（Bazooka Projectile）、手里剑（Shuriken）、ak子弹（Player Bullet (Small)）、手榴弹溅射物（Player Bullet (Medium)）。Projectile池身上挂载了几个脚本代表有几中投射物
- Throwable池，以抛物线形式抛出去的物品：燃烧瓶，手榴弹
- Boomerange池，回旋镖池，所用扔出去还会回来的物品：弯刀（Machete）、光剑（Lightsaber）

## 2）使用到对象池数组的物体

projectilePrefab（抛射物）、throwablePrefab（投掷物）、boomerangPrefab（回旋镖）

## 3）怪物池逻辑

怪物容器类（MonstersContainer，MiniBossContainer）在LevelBlueprint类中定义，在EntityManager中会依据怪物容器类的数量来分配“数量+1”的对象池。

## 4）动态对象池逻辑

当一个新能力被选择时，会将该能力物体激活，并调用对应的添加对象池函数（如AddPoolForBoomerang）为该能力新建一个对象管理脚本，并将该脚本组件添加到一个gameobject上，然后调用该脚本的Init函数进行新建对象池，并添加到该种对象池列表中，并返回在对象池列表中的索引，该索引会被字典记录下来。在生成该种对象的时候会调用生成函数（entityManager.SpawnBoomerang），需要传入在对象池列表中的索引等参数。

## 5）对象池中对象复用注意事项

以生成子弹为例子

1、先在对象池中Get到子弹对象，然后调用子弹自身的Setup来重置自身的位置、速度、伤害、击退等等属性，并清空上次残留的拖尾

2、在触发碰撞后，会先禁用子弹的碰撞体，并停止移动协程，防止多次触发碰撞和触发未命中。

3、在 `Setup()` 里 `OnHitDamageable = new UnityEvent<float>()`。Projectile
 这意味着：**每次复用都会重建事件容器**，从而天然避免“上一次 AddListener 没清掉 → 监听叠加 → 伤害统计翻倍”这一类对象池高频 bug。

# 二、关卡逻辑

## 1、关卡中有什么？

有以下东西，见LevelBlueprint类，主要作用就是将所有普通怪统一起来装进一个字典中。

而每一个普通怪类都是包含该种类的预制体和一整组怪。

```C#
 [Header("Time")]
 public float levelTime = 600;	//关卡时长
 [Header("Background")]
 public Texture2D backgroundTexture;     //关卡背景贴图
 [Header("Abilities")]
 public GameObject[] abilityPrefabs;     //能力预制体数组
 [Header("Monster Settings")]
 public MonstersContainer[] monsters;    // 普通怪物类数组，每个元素都是一个普通怪物类，普通怪类中包含多个普通怪
 public MiniBossContainer[] miniBosses;  // 小Boss容器数组
 public BossContainer finalBoss;         // 最终Boss容器
 public MonsterSpawnTable monsterSpawnTable; // 怪物生成表（控制何时生成哪种怪物）
 [Header("Chest Settings")]
 public ChestBlueprint chestBlueprint;   // 宝箱蓝图（定义宝箱内容、外观等）
 public float chestSpawnDelay = 30;      // 宝箱生成间隔（默认30秒）
 public float chestSpawnAmount = 2;      // 每次生成的宝箱数量（默认2个）
 [Header("Exp Gem Settings")]
 public int initialExpGemCount = 25; // 初始经验宝石数量（默认25个）
 public GemType initialExpGemType = GemType.White1; // 初始经验宝石类型

 private Dictionary<int, (int, int)> monsterIndexMap;  // 怪物索引映射表
```

## 2、关卡曲线增长逻辑

关卡会随着时间增加而更快的刷新出更高级、更多血量的怪物，实现逻辑如下：

- 刷怪数量增长：定义了几种关键帧类，比如刷怪数量，每个类就代表一个关键帧，使用该类的数组将所有的关键帧装了起来，依据时间来判断此时属于哪两个关键帧区间，然后插值来更新该时刻的刷怪数量。其它的也会以这种方式来动态变化
- 刷怪类型：定义刷怪类型关键帧类，该类中有一个数组，表示不同种怪物的刷怪概率。在刷怪选择时，会生成一个随机数，插值计算出该时刻所有怪物的生成概率，然后逐个累加与随机值比较，直到大于该随机值，则选中该索引对应的怪物并生成。
- 刷怪血量：不同种怪物有不同的血量，所以在随时间增长的过程中，所有怪物的血量也需要增长，同样使用刷怪血量关键帧类，该类中有数组表示不同种怪的血量，然后插值更新出该时刻的怪物血量数组，就得到了怪物的血量。

# 三、物品生成逻辑

## 1）刷怪位置逻辑和怪物存储逻辑

- 有两种刷怪位置策略：

一是角色静止时，调用GetRandomMonsterSpawnPosition从屏幕外的四周随机刷怪

> 上下左右四个方向向量指定方向，以角色位置为起点，往某个方向移动到边框位置，然后再加上固定的生成距离monsterSpawnBufferDistance

二是角色移动时，调用GetRandomMonsterSpawnPositionPlayerVelocity，怪更倾向从玩家前进方向出现，提高压力与追逐感。

GetRandomMonsterSpawnPositionPlayerVelocity函数如何运行的后续再看

- 怪物的存储策略：
  MonstersContainer类为小怪类，及一个该类就表示一种小怪。类里面包含该种小怪的预制体，并用MonsterBlueprint[]存储起来每个小怪，MonsterBlueprint类为怪物蓝本类，包含名字血量、攻击力、攻速等等。因此可以使用MonstersContainer[]来存储所有种类的小怪。
  然后将所有的怪按一下方式存入Dictionary<int, (int, int)> monsterIndexMap中。其中不同的i表示不同的怪物类别，相同i不同的j表示同类别的不同怪。这样更容易知道monsterIndexMap[x],属于那种类别的哪个怪。

  ```c#
  if (monsterIndexMap == null)
  {
      monsterIndexMap = new Dictionary<int, (int, int)>();
      int monsterIndex = 0;
      //将普通怪物容器数组中的所有怪物都装进monsterIndexMap怪物索引映射表
      //存的数据代表，第几种怪的第几个对应的monsters[i].monsterBlueprints[j]中的索引值。同
      for (int i = 0; i < monsters.Length; i++)
      {
          for (int j = 0; j < monsters[i].monsterBlueprints.Length; j++)
              monsterIndexMap[monsterIndex++] = (i, j);
      }
  }
  ```

  

## 2）刷新宝箱逻辑

使用以下逻辑来判定刷新位置，并遍历宝箱列表来判断新位置是否有与已有宝箱位置重叠的，有的话就重新生成。

```c#
//minSpawnDistance为屏幕对角线长度的一半
Vector2 spawnPosition = (Vector2)playerCharacter.transform.position
                      + spawnDirection * (minSpawnDistance + monsterSpawnBufferDistance + Random.Range(0, chestSpawnRange));
```

# 四、怪物相关

一个怪物是由如下结构组成:

> 分别是挂在了Box碰撞器的Sprite / Hitbox精灵、怪物脚下的椭圆形阴影Shadow、怪物死亡时播放的粒子特效Monster Death Particles、中心位置空物体Center Transform。
>
> 其中腿部碰撞器monsterLegsCollider挂载在自身上、而BoxCollider2D在精灵上
>
> ![image-20251222150345423](D:\Unity Projects\休闲游戏实例\疯狂割草\Image\image-20251222150345423.png)

```c#
protected virtual void Awake()
{
    rb = GetComponent<Rigidbody2D>(); // 获取刚体组件
    monsterLegsCollider = GetComponent<CircleCollider2D>(); // 获取腿部碰撞器
    monsterSpriteAnimator = GetComponentInChildren<SpriteAnimator>(); // 获取子对象中的精灵动画器
    monsterSpriteRenderer = GetComponentInChildren<SpriteRenderer>(); // 获取子对象中的精灵渲染器
    zPositioner = gameObject.AddComponent<ZPositioner>(); // 添加Z轴定位器组件
    monsterHitbox = monsterSpriteRenderer.gameObject.AddComponent<BoxCollider2D>(); // 给精灵添加碰撞盒作为攻击检测用
    monsterHitbox.isTrigger = true; // 设置为触发器（不产生物理碰撞，仅用于检测）
}
```

## 随着关卡的进度，生成怪物相关

随着关卡的进行，有三个怪物特征会发生变化。分别是生成的数量、生成的种类、生成的血量。

- 数量：
  定义了一种类，有两个数据成员，时间进度t、数量spawnRate,用于记录关键帧的数据情况。在数据文件中已经记录了有几个关键帧。GetSpawnRate(float t)函数，传入游戏进度，然后判断游戏进度属于哪两个关键帧区间，然后依据两个关键帧的t得到游戏进度在该区间归一化后的结果。使用该结果和两个关键帧的spawnRate数据进行插值，就得到了该进度下应该的刷怪数量。

- 怪物种类和血量：
  定义了一种类，有两个数据成员，时间进度t、不同怪物的生成概率spawnChances[],用于记录关键帧的数据情况。另一个类是不同怪物的血量healthBuffs[]。同样根据t得到游戏进度所处的怪物种类的关键帧区间。然后以j为索引值遍历两个关键帧的spawnChances[]，并使用 “ cumulative += Mathf.Lerp(spawnChanceKeyframes[i - 1].spawnChances[j], spawnChanceKeyframes[i].spawnChances[j], tLerp); ” 进行插值取值并累加计算，与0-1的随机值进行比较，大于随机值的话就返回该索引 j。

  然后再依据t找到此时所属的血量的关键帧区间，将t归一化后，使用刚刚的怪物索引值j执行以下语句得到血量值 “ Mathf.Lerp(hpMultiplierKeyframes[i - 1].healthBuffs[j], hpMultiplierKeyframes[i].healthBuffs[j], tLerp)；”

# 五、对于可升级的属性

对于可升级的属性（如速度，武器伤害，持续事件等等），每个属性都有一个类并且都继承自UpgradeableValue基类。

# 六、角色类

处理角色的一系列行为：

更新角色朝向lookDirection、应用移动动画、获得经验值并升级逻辑，更行等级显示逻辑、击退逻辑、受伤逻辑、受击动画、死亡动画协程逻辑，恢复生命值逻辑、更新移动速度逻辑

- DeathAnimation协程：在角色死亡时调用，alive=false；在此协程里进行。

  > 内容就是将角色材质切换到死亡材质，摧毁激活的所有能力播放死亡粒子特效、并使用循环调整死亡材质shader中的阈值_Wipe来完成溶解效果，然后触发死亡事件OnDeath，并隐藏精灵。

# 七、能力相关逻辑

能力卡即为可以在升级卡片中选择的、用于提升角色或者武器属性的卡片，每个能力都有一个对应一个能力卡。所有能力都继承自Ability类

- 能力分类总结：

  > 基于 `ProjectileAbility` 分支:
  >
  > 1. **ShurikenAbility**：发射手里剑类投射物，具备较快的飞行速度，是灵活的中近程远程攻击手段。
  > 2. **BazookaGunAbility（GunAbility 子节点）**：发射火箭筒类高伤害投射物，附带范围爆炸效果，属于高威力单体 / 小范围远程输出能力。
  > 3. **MachineGunAbility（GunAbility 子节点）**：以高射速连续发射投射物，实现持续的远程火力压制，适合快速清理多个敌人。
  >
  > 基于 `FloatUpgradeAbility<T>`/`IntUpgradeAbility<T>` 分支：
  >
  > 1. `DamageUpgradeAbility`：提升投射物（或关联能力）的伤害数值，强化输出效率。
  > 2. `ProjectileSpeedUpgradeAbility`：加快投射物的飞行速度，提升远程攻击的命中效率。
  > 3. `AOEUpgradeAbility`：扩大范围效果（AOE）的作用区域，增强范围能力的覆盖范围。
  > 4. `IceSkatesAbility`：提升玩家的移动速度，让角色更灵活地规避敌人或调整站位。
  > 5. `KnockbackUpgradeAbility`：增强攻击的击退效果强度，拉开与敌人的距离、提升生存空间。
  > 6. `CooldownUpgradeAbility`：缩短对应能力的冷却时间，让能力可以更频繁地使用。
  > 7. `ProjectileCountUpgradeAbility`：提升单次攻击发射的投射物数量，增加远程攻击的覆盖和总伤害。
  > 8. `ArmorUpgradeAbility`：提升玩家的护甲值，减少受到的伤害，增强生存能力。
  >
  > 基于 `ThrowableAbility` 分支：
  >
  > 1. **MolotovAbility**：投掷燃烧瓶，落地后形成持续燃烧的区域，对区域内敌人造成持续火焰伤害。
  > 2. **GravityWellAbility**：投掷引力井装置，生成引力场吸引周围敌人向中心聚集，方便后续范围攻击集中清理。
  > 3. **GrenadeThrowAbility**：投掷手榴弹，触发范围爆炸，对区域内敌人造成高额爆炸伤害。
  >
  > 基于 `MeleeAbility` 分支：
  >
  > 1. **SlashAbility**：挥砍型近战攻击，具备较广的攻击范围，适合同时攻击多个近距离敌人。
  > 2. **StabAbility**：穿刺型近战攻击，以单体高伤害为核心，适合针对单个高血量敌人进行精准输出。
  > 3. **FixedDirectionAbility**：固定方向的近战攻击，朝当前面向方向发起线性 / 扇形伤害，攻击方向更精准。
  > 4. **DaggerAbility**：匕首类近战攻击，通常具备较快的攻击速度，或附带暴击、流血等单体特效。
  >
  > 其它：
  >
  > 1. **GarlicAbility**：生成以玩家为中心的大蒜范围场（吸血鬼题材中大蒜是敌人弱点），对进入区域的敌人造成持续伤害、减速等负面效果，是被动 / 半自动的范围防御 + 输出能力。
  > 2. **RecoveryAbility**：触发后恢复玩家的生命值，提升角色的续航能力
  > 3. **BookAbility**：斧头环绕，以固定半径绕玩家旋转，形成一个“旋转伤害圈”。。
  > 4. **BommerangAbility**：发射回旋镖类投射物，飞出后沿轨迹返回，对路径上的敌人造成多次伤害，兼顾远程和折返命中特性。
  > 5. **LifestealAbility**：攻击敌人时，按造成伤害的一定比例恢复自身生命值，实现 “攻击回血” 的续航效果。

## 1、关于能力初始时的逻辑

- 刚进入游戏时所有的能力都会在AbilityMnager中实例化为AbilityMnager的儿子，并执行初始化逻辑，只是未拥有的能力不会执行ability.Select();函数，因此处于失活状态。

- 在ability.Init中，首先通过反射获取自身的全部可升级字段，如伤害、范围等等，并将这种字段全部转化为IUpgradeableValue接口类型（因为可升级的字段一定为类，并且一定继承自IUpgradeableValue接口），最后存于自身可升级字段列表List<IUpgradeableValue>中。然后还会遍历自生的所有可升级字段，并将其注册到能力管理器的FastList<IUpgradeableValue>  registeredUpgradeableValues种，registeredUpgradeableValues就是专门记录所有能力的所有可升级字段的。

- 能力的初始化“ability.Init(abilityManager, entityManager, playerCharacter);”往往配合着能力的选择“ability.Select();”，但是为什么能力的选择不并入能力初始化中？因为只有在ability.Select()中，会判断该能力是否已拥有，如是则会调用升级逻辑，否则执行可升级值计数逻辑，并设置能力为已拥有。

  注：FastList为自定义列表类，其中使用字典记录了列表中元素的索引值，以实现更加方便的查找和删除

## 2、关于抽取到伤害+10%等类似卡片时的处理逻辑

在AbilityManager类中，会将角色所有能力的所用可升级字段都存储到“FastList<IUpgradeableValue> registeredUpgradeableValues”中，因为这些可升级字段都继承自IUpgradeableValue接口。然后使用“UpgradeableValue<TValue>[] upgradeableValues = registeredUpgradeableValues.OfType<T>().ToArray() as UpgradeableValue<TValue>[];”遍历registeredUpgradeableValues中的所有可升级伤害类UpgradeableDamage，然后执行Update（）就可以了。

## 3、升级时，可选的能力列表中的能力是如何选出来的

首先是能力的分类：我自定义了一个用于装Ability的WeightedAbilities列表类，并带有所有能力的总权重，作用相当于是带权重的列表。然后使用该类定义两个newAbilities、ownedAbilities分别代表未拥有的能力池和已拥有的能力池。先提取出满足条件的能力，并计算候选能力卡片的总数。尽可能的从可升级能力池（availableOwnedAbilities）中选出两个，剩余的卡片从可获取的新能力池（availableNewAbilities）中抽取（中间涉及角色的luck值，不说太细）。从池中抽取能力的概率是与能力的稀有度有关的，这部分由PullAbility负责，稀有度越高，越难被抽中。

```c#
public List<Ability> SelectAbilities()
{
    //升级串口显示的可选择的能力
    List<Ability> selectedAbilities = new List<Ability>();

    // 从已拥有池中提取“可升级”的能力（RequirementsMet()）
    WeightedAbilities availableOwnedAbilities = ExtractAvailableAbilities(ownedAbilities);

    // 从新能力池中提取“可获取”的能力（RequirementsMet()）
    WeightedAbilities availableNewAbilities = ExtractAvailableAbilities(newAbilities);

    // 候选数：基本 3 个；按 FourthChance 概率可能额外 +1 变成 4 个
    int selectedAbilitiesCount = 3 + (ResolveChance(FourthChance) ? 1 : 0);

    // 先尝试抽最多 2 个“已拥有能力”（给升级用）
    // 如果 availableOwnedAbilities 少于 2，就只能抽那么多
    int ownedAbilitiesCount = availableOwnedAbilities.Count < 2 ? availableOwnedAbilities.Count : 2;

    for (int i = 0; i < ownedAbilitiesCount; i++)
    {
        // 每次抽已拥有能力都要按 OwnedChance 决定是否抽到（不是必抽）
        // Luck 越高，OwnedChance 越高，则升级已有能力的候选更常出现
        if (ResolveChance(OwnedChance))
            //按权重从可升级能力列表availableOwnedAbilities中抽取一个
            selectedAbilities.Add(PullAbility(availableOwnedAbilities));
    }

    // 剩余位置用“新能力”填充（优先给玩家新技能）
    int availableAbilitiesCount = selectedAbilitiesCount - selectedAbilities.Count;

    // 防止新能力不足导致越界：最多只能抽 availableNewAbilities.Count 个
    if (availableAbilitiesCount > availableNewAbilities.Count) availableAbilitiesCount = availableNewAbilities.Count;

    for (int i = 0; i < availableAbilitiesCount; i++)
    {
        selectedAbilities.Add(PullAbility(availableNewAbilities));
    }

    // 若候选还没满（例如新能力不足），用已拥有能力补齐
    // i - selectedAbilities.Count < availableOwnedAbilities.Count 用于防越界
    for (int i = selectedAbilities.Count; i < selectedAbilitiesCount && i - selectedAbilities.Count < availableOwnedAbilities.Count; i++)
    {
        selectedAbilities.Add(PullAbility(availableOwnedAbilities));
    }

    // 把“这次没被抽中但仍然可用”的能力放回原池子
    // 注意：availableNewAbilities/availableOwnedAbilities 此时已经被 PullAbility 取走了一部分
    // 剩下的就是“可用但未被选中”的能力
    foreach (Ability ability in availableNewAbilities)
        newAbilities.Add(ability);

    foreach (Ability ability in availableOwnedAbilities)
        ownedAbilities.Add(ability);

    return selectedAbilities;
}
```

## 4、抽取到新武器能力卡片和抽取到数值提升类卡片时逻辑的区别

- 第一次抽取到新武器时：比如回旋镖武器

  > 调用Use（）函数，然后激活该预制体，使用“boomerangIndex = entityManager.AddPoolForBoomerang(boomerangPrefab);”为该武器所发射的子弹预制体动态创建对象池，并记录该类型的子弹的对象池在对象池列表中的索引boomerangIndex，该索引会在发射该子弹时被用到。

- 第一次抽到新能力时：比如伤害增加类能力

  > 增加属性的能力会继承自一个泛型类FloatUpgradeAbility<T>或IntUpgradeAbility<T>，抽到后会执行“abilityManager.UpgradeValue<T, float>(upgrades[level]);”来提取所有T类型的可升级字段，执行升级逻辑，upgrades[level]为该能力在此次升级所提升的值。此时的T为“UpgradeableDamage”

# 八、地图生成逻辑

## 1、动态更新的哈希网格

1）作用：空间哈希网格管理器，用于高效管理游戏世界中的实体位置，支持快速查询附近实体
如下所示为哈希网格的初始化：指定范围、指定范围内网格维度、查询ID计数器，并初始化dimensions.x * dimensions.y个网格，每个网格即为一个ISpatialHashGridClient型列表，列表用于存储网格内的物体。
而对于每个继承自ISpatialHashGridClient的物体都会有一个字典属性“Dictionary<int, int> ListIndexByCellIndex { get; set; }”用于记录自身碰撞体积所占用的网格的索引和列表索引。这个数据是通过自身坐标和网格边缘维度计算出来的。

```c#
//一个列表表示一个网格，长*宽就为总网格数量，也就是该数组的容量
protected List<ISpatialHashGridClient>[] cells;
/// <summary>网格边界，[0]为左下角，[1]为右上角</summary>
protected Vector2[] bounds;
/// <summary>网格的维度（x方向格子数，y方向格子数）</summary>
protected Vector2Int dimensions;
/// <summary>查询ID计数器，用于生成唯一查询标识</summary>
protected int queryIds;

public SpatialHashGrid(Vector2[] bounds, Vector2Int dimensions)
{
    this.bounds = bounds;	//Vector2[]型，网格边界（左下角和右上角坐标）
    this.dimensions = dimensions;	//Vector2Int，网格维度（x和y方向的格子数量）
    this.queryIds = 0;
    // 初始化单元格数组，每个单元格创建一个实体列表
    this.cells = new List<ISpatialHashGridClient>[dimensions.x * dimensions.y];
    //挨个对网格进行初始化
    for (int i = 0; i < cells.Length; i++)
        cells[i] = new List<ISpatialHashGridClient>();
}
```

2）网格的动态更新
在EntityManager的Update中，每当玩家靠近网格边缘就会执行网格重建。
因为网格的维度和尺寸是固定大小的，所以每次重建就像刚初始化那样，并更新每个物体的字典值ListIndexByCellIndex 。

```c#
void Update()
{
    // 如果玩家接近网格边缘，则重建网格（使网格中心跟随玩家移动）
    // 目的：保持附近查询有效，避免玩家跑出网格覆盖范围导致查找错误/性能问题
    if (grid.CloseToEdge(playerCharacter))
    {
        grid.Rebuild(playerCharacter.transform.position);
    }
}
```



# 九、子弹相关

1. 子弹是什么组成的：
   分为发射子弹的枪械和子弹本身，枪械类会调用子弹类中的Launch函数来发射子弹。子弹本身是一个白色的圆形精灵，加了描边和拖尾会有游戏中的效果。
2. 子弹的移动是调用协程实现的
3. 抛物线子弹的轨迹：
   在抛物线类子弹中，抛物的实现方式是计算玩家指向目标的方向，如果方向的x大于零，则顺时针旋转方向与Y轴夹角的度数后得到新方向，然后插值的方式将该方向逐渐像最终方向靠近，在其运动距离达到最大值时停止并释放火焰或爆炸。
4. 武器发射子弹逻辑：
   武器分为连发和单发，都是在冷却时间到达后调用Attack（）来完成发射的，发射的数量就是调用LaunchProjectile()的次数，在LaunchProjectile()中调用协程，先播放瞄准动画，然后通过对象池生成子弹实体，调用子弹实体的Launch（）函数来向指定方向移动。
5. 关于子弹的发射方向：手里剑的发射方向是角色的面朝向，机枪的发射方向是枪管的朝向、大炮的朝向是目标的方向

# 十、拾取物与背包相关

金币、经验、炸弹、磁铁、血瓶、红药水总共六个继承自Collectable的可拾取物。

## 1、可拾取物的拾取

可拾取物的拾取方式有两种，一种是磁铁吸引、一种是碰撞拾取。每中可拾取物都有一个对应的数据文件（CollectableType），来记载背包中该拾取物的槽位数量，0个即为不可以装入背包。

1）在可拾取物生成时会调用Setup（）函数，将自身添加到entityManager.MagneticCollectables的可拾取物列表中。

2）在玩家触发磁铁效果时会遍历可拾取物列表并且执行Collect方法来飞向玩家，当玩家与可拾取物进行碰撞时，也会触发该效果，在该方法中还会通过该种类型的背包槽位数量来判断是否装入背包。

注：通过设置拾取状态为true并禁用碰撞体（beingCollected = true;col.enabled = false;）来避免重复拾取。

## 2、背包逻辑

- 初始化逻辑

  > 1）Inventory ButtonsUI中挂载了Inventory类，并已通过该类关联了四个道具按钮UI，也就是四个库存槽（InventorySlot类），装入了InventorySlot[]数组中。初始化时会遍历该数组，然后初始化数组中的库存槽，并使用字典Dictionary<CollectableType, InventorySlot> inventorySlotByType来记录道具类型对应的库存槽。
  >
  > 2）InventorySlot库存槽类之所以可以装道具那是因为在该类中创建了一个可库存物列表List<Collectable>，初始化库存槽就是初始化该列表并隐藏道具UI中的负责数量显示的子UI，并设置按钮照片为半透明。

- 功能逻辑

  > 

# 十一、层级管理

Z轴定位器ZPositioner、图层LayerMask。

# 问题

## 1、如何将全部小怪统计起来？

在LevelBlueprint关卡配置类中有如下代码：其中i表示有多少种小怪，j表示每种小怪的数量

```c#
//将普通怪物容器数组中的所有怪物都装进monsterIndexMap怪物索引映射表
for (int i = 0; i < monsters.Length; i++)
{
    for (int j = 0; j < monsters[i].monsterBlueprints.Length; j++)
        monsterIndexMap[monsterIndex++] = (i, j);
}
```

## 2、无限地图如何实现的？

- 在角色物体下有名为Infinite Background的2D平面子物体，该物体上挂载了InfiniteBackground脚本，就是通过该脚本实现的无限地图

- 对InfiniteBackground脚本逻辑的解释：该脚本会与地图的shader关联，在update中，计算玩家当前位置与上一次重置背景位置的偏移向量toReset，当该偏移量大于阈值时，启动背景重置协程ResetBackground。在该协程中，设置shader中的_Resetting为1，来通知shader开始重置地图。

- shader中的逻辑：重置地图和初次新建地图流程基本一致

  > 第一步就是根据计算出来的toReset来更新本像素的UV坐标
  >
  > 第二步：以0.005*uv的方式算出噪声值，并依据噪声值得出两个差值为1的偏移值，分别使用两个偏移值与UV相加进行采样。
  >
  > 第三步：将两个采样值进行插值混合，然后将该混合值再与老地图的颜色值进行插值，得到最终结果。

## 3、执行炸弹效果时如何判断怪物是否在屏幕范围内？

有一个函数，专门判断某transform是否在屏幕范围内，就是将屏幕左下角坐标和右上角坐标转为世界空间下的坐标。
然后判断transform.position是否同时满足大于左下角坐标的x，y并且小于右上角坐标的x，y。就可以了

## 4、如何实现已有的能力会出现强化卡片，未拥有的能力出现初始卡片的？



## 5、在升级弹窗的UI中，能力卡片的描述内容是如何修改的？



# 6、游戏顺序

1、点击开始后会调用DialogBox.open来激活英雄选择页面。

## 2、选角色卡阶段

1）角色卡UI的父亲挂载了CharacterSelector脚本，该脚本定义了CharacterBlueprint[]，通过手动拖动赋值将三个角色蓝本数据文件赋值给这个数组，然后在父亲下实例化三个角色卡预制体克隆体，并获取克隆体身上的CharacterCard脚本，然后使用CharacterCard.init()按照CharacterBlueprint[]中的数据文件对角色卡进行初始化，并执行自定义的自动更新UI布局方法。

2）在点击选择后，就会将该被选择的角色蓝图数据传给一个单例全局类进行存储，然后加载游戏场景。

3）在LevelManager物体中有如下图已经挂载好的各种脚本，然后会在LevelManager脚本的start中执行Init函数，该初始化函数包含各种管理类的初始化和角色的初始化,如下是初始化脚本。

> - 初始化实体总管：
>   初始化三种FastList自定义列表，分别存储活怪、可磁吸拾取物、宝箱;
>   初始化怪物对象池数组MonsterPool[]  monsterPools;并初始化数组中的全部怪物池，将全部怪物对象池挂载在一个怪物池空物体上；
>   初始化三种动态创建的对象池列表，并使用字典来记录不同类型物体在对象池中的索引；
>   初始化固定的一次性对象池：经验宝石/金币/宝箱/伤害飘字；
>   初始化空间哈希网格。
> - 初始化能力系统：
>   初始化可升级值注册列表；
>   初始化已拥有能力和未拥有能力列表，并实例化所用能力，但只激活角色的初始能力（使用Select（）函数进行激活）；
>   注：执行Select（）的内部逻辑，首先如果该能力未拥有时，则owned = true，标记为已拥有，并统计该能力中的所有可升级字段，然后增加对应的可升级值计数”abilityManager.DamageUpgradeablesCount++;“。然后激活该能力等等其它逻辑。如果已经拥有该能力，则执行升级逻辑
> - 初始化升级弹窗：对abilityManager、entityManager、playerCharacter进行赋值。
> - 初始化玩家角色：对abilityManager、entityManager、statsManager进行赋值
>   注册造成伤害事件、初始化协程队列；
>   初始化生命值和经验条；
>   初始化动画spriteAnimator.Init；
>   初始化可升级移动速度和护甲。
> - 初始化无线背景：
>   设置背景shader中的主纹理
>   设置shader中的”_Shockwave“、”_ResetOffset“、”_Resetting“等值
> - 初始化背包系统

```c#
public void Init(LevelBlueprint levelBlueprint)
{
    // 记录关卡配置，并重置关卡时间
    this.levelBlueprint = levelBlueprint;
    levelTime = 0f;

    // 1) 初始化实体总管（对象池/网格/掉落等）
    //    后续刷怪、刷宝箱、刷宝石都依赖 entityManager
    entityManager.Init(this.levelBlueprint, playerCharacter, inventory, statsManager, infiniteBackground, abilitySelectionDialog);

    // 2) 初始化能力系统（构建技能池：起始技能 + 新技能）
    abilityManager.Init(this.levelBlueprint, entityManager, playerCharacter, abilityManager);

    // 3) 初始化升级弹窗（它需要 abilityManager 生成候选，需要 entityManager 生成保底宝箱等）
    abilitySelectionDialog.Init(abilityManager, entityManager, playerCharacter);

    // 4) 初始化玩家角色（血条/经验条、可升级属性注册、动画等）
    playerCharacter.Init(entityManager, abilityManager, statsManager);

    // 5) 绑定玩家死亡事件：玩家死了 → 关卡失败结算
    playerCharacter.OnDeath.AddListener(GameOver);

    // 6) 开局在玩家周围生成初始经验宝石（让玩家尽快升级进入循环）
    entityManager.SpawnGemsAroundPlayer(this.levelBlueprint.initialExpGemCount,
                                       this.levelBlueprint.initialExpGemType);

    // 7) 开局生成一个宝箱（引导玩家开箱/升级/掉落）
    entityManager.SpawnChest(levelBlueprint.chestBlueprint);

    // 8) 初始化无限背景（贴图 + 跟随玩家）
    infiniteBackground.Init(this.levelBlueprint.backgroundTexture, playerCharacter.transform);

    // 9) 初始化背包系统（建立 CollectableType→Slot 映射等）
    inventory.Init();
}
```

![image-20251225220202318](D:\Unity Projects\休闲游戏实例\疯狂割草\Image\image-20251225220202318.png)



