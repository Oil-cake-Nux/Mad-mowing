using System.Linq;

using System.Collections;

using System.Collections.Generic;

using UnityEngine;



namespace Vampire

{

    /// <summary>

    /// AbilityManager（能力管理器）

    /// 核心职责：

    /// 1) 初始化：把关卡可用的能力 prefab 实例化成 Ability 对象，并分成“已拥有”和“未拥有”两类池子

    /// 2) 升级抽卡：在升级弹窗/宝箱弹窗时，按权重与概率，从“已拥有(可升级)”和“新能力(可获取)”中抽取若干个候选

    /// 3) 可升级值系统：统一管理所有 IUpgradeableValue，并提供 UpgradeValue<T,TValue> 批量升级入口

    ///

    /// 注意：

    /// - 本项目的能力（Ability）是通过 Instantiate(abilityPrefab) 实例化到场景中的，

    ///   并不是纯数据对象；能力实例本身会包含各种 UpgradeableValue 字段和行为逻辑。

    /// - newAbilities / ownedAbilities 是“带权重容器”，用于加权随机抽取候选能力。

    /// </summary>

    public class AbilityManager : MonoBehaviour

    {

        private LevelBlueprint levelBlueprint; // 关卡蓝图（包含当前关卡可用的能力预制体 abilityPrefabs）

        private Character playerCharacter;     // 玩家角色引用（用于读取幸运值、等级、起始能力等）



        // newAbilities：未拥有的能力池（候选“新技能”），带权重

        // ownedAbilities：已拥有的能力池（候选“升级已有技能”），带权重

        private WeightedAbilities newAbilities;

        private WeightedAbilities ownedAbilities;



        // 所有已注册到 AbilityManager 的可升级值（UpgradeableValue）

        // 例如 UpgradeableDamage / UpgradeableWeaponCooldown / UpgradeableAOE 等等

        private FastList<IUpgradeableValue> registeredUpgradeableValues;



        // ======= 以下是“各类 Upgradeable 值”的计数器。总共17中字段 =======

        // 作用：很多 UpgradeAbility（例如 DamageUpgradeAbility）会在 RequirementsMet() 中检查：

        // “当前场上是否存在对应 Upgradeable 类型”，如果为 0 就不进候选池，避免出现无效升级。

        // 这些计数器通常在各 UpgradeableValue.Register(this) / RegisterInUse() 中增减维护。

        public int DamageUpgradeablesCount { get; set; } = 0;    //所以可升级字段中“伤害”字段的数量

        public int KnockbackUpgradeablesCount { get; set; } = 0;    //所以可升级字段中“击退”字段的数量

        public int WeaponCooldownUpgradeablesCount { get; set; } = 0;   //所以可升级字段中“武器冷却”字段的数量

        public int RecoveryCooldownUpgradeablesCount { get; set; } = 0;     //所以可升级字段中“恢复冷却”字段的数量

        public int AOEUpgradeablesCount { get; set; } = 0;       //所以可升级字段中“范围”字段的数量

        public int ProjectileSpeedUpgradeablesCount { get; set; } = 0;  //所以可升级字段中“投射物速度”字段的数量

        public int ProjectileCountUpgradeablesCount { get; set; } = 0;  //所以可升级字段中“投射物数量”字段的数量

        public int RecoveryUpgradeablesCount { get; set; } = 0;         //所以可升级字段中“恢复量”字段的数量

        public int RecoveryChanceUpgradeablesCount { get; set; } = 0;   //所以可升级字段中“恢复概率”字段的数量

        public int BleedDamageUpgradeablesCount { get; set; } = 0;      //所以可升级字段中“流血伤害”字段的数量

        public int BleedRateUpgradeablesCount { get; set; } = 0;        //所以可升级字段中“流血频率”字段的数量

        public int BleedDurationUpgradeablesCount { get; set; } = 0;    //所以可升级字段中“流血持续时长”字段的数量

        public int MovementSpeedUpgradeablesCount { get; set; } = 0;    //所以可升级字段中“移动速度”字段的数量

        public int ArmorUpgradeablesCount { get; set; } = 0;        //所以可升级字段中“护甲”字段的数量

        public int FireRateUpgradeablesCount { get; set; } = 0;     //所以可升级字段中“开火频率”字段的数量

        public int DurationUpgradeablesCount { get; set; } = 0;     //所以可升级字段中“持续时间”字段的数量

        public int RotationSpeedUpgradeablesCount { get; set; } = 0;    //所以可升级字段中“旋转速度”字段的数量



        /// <summary>

        /// 初始化能力管理器（由 LevelManager.Init 调用）

        ///

        /// 做的事情：

        /// 1) 初始化 registeredUpgradeableValues 容器

        /// 2) 把玩家的“起始能力”实例化出来，Init 后直接 Select（立即生效），放入 ownedAbilities

        /// 3) 把关卡蓝图中的“可用能力库”实例化出来（排除玩家起始能力），放入 newAbilities

        ///

        /// 参数说明：

        /// levelBlueprint：关卡配置（包含 abilityPrefabs）

        /// entityManager：实体管理器（能力初始化时会用它建对象池/刷子弹/刷投掷物等）

        /// playerCharacter：玩家角色（能力需要读取位置/朝向/升级属性等）

        /// abilityManager：这里传自身引用，供 Ability.Init 保存引用（有点冗余但能保证传入的是同一个对象）

        /// </summary>

        public void Init(LevelBlueprint levelBlueprint, EntityManager entityManager, Character playerCharacter, AbilityManager abilityManager)

        {

            this.levelBlueprint = levelBlueprint;

            this.playerCharacter = playerCharacter;



            // 保存所有可升级值（UpgradeableValue）的注册列表

            registeredUpgradeableValues = new FastList<IUpgradeableValue>();



            // =========================

            // 1) 已拥有能力池：加载玩家起始能力

            // =========================

            ownedAbilities = new WeightedAbilities();



            // 玩家角色蓝图里配置的 startingAbilities（一组 Ability prefab）

            //startingAbilities为GameObject数组

            foreach (GameObject abilityPrefab in playerCharacter.Blueprint.startingAbilities)

            {

                // 实例化能力 prefab：挂到 AbilityManager 节点下，便于层级管理

                Ability ability = Instantiate(abilityPrefab, transform).GetComponent<Ability>();



                // 初始化能力：传入 AbilityManager / EntityManager / PlayerCharacter

                // 使能力能够：

                // - 请求 EntityManager 为自己的 projectile/throwable 建池并 Spawn

                // - 在升级时调用 AbilityManager.UpgradeValue<T>()

                // - 读取玩家位置/朝向/属性等

                //作用：将能力中的所用可升级字段传入到registeredUpgradeableValues中

                ability.Init(abilityManager, entityManager, playerCharacter);



                // 直接选择：起始能力会立即生效（相当于玩家开局就拥有）

                // Select() 内部通常会：owned=true；如果未拥有则 Use()；如果已拥有则 Upgrade()

                //执行Select

                ability.Select();



                // 放入“已拥有能力池”（用于未来升级候选）

                ownedAbilities.Add(ability);

            }



            // =========================

            // 2) 未拥有能力池：加载关卡可用的新能力

            // =========================

            newAbilities = new WeightedAbilities();



            foreach (GameObject abilityPrefab in levelBlueprint.abilityPrefabs)

            {

                // 如果该 abilityPrefab 已经是玩家起始能力，就跳过，避免重复出现

                if (playerCharacter.Blueprint.startingAbilities.Contains(abilityPrefab)) continue;



                // 实例化能力 prefab（同样挂到 AbilityManager 节点下）

                Ability ability = Instantiate(abilityPrefab, transform).GetComponent<Ability>();



                // 初始化能力（还未生效，只是进入“新能力池”待抽取）

                ability.Init(abilityManager, entityManager, playerCharacter);



                // 放入“新能力池”（用于三选一时抽取“新技能”）

                newAbilities.Add(ability);

            }

        }



        /// <summary>

        /// 注册一个可升级值到 AbilityManager

        ///

        /// 典型调用者：

        /// - 玩家 Character.Init 或某个 Ability.Use() 内，会把自己的 UpgradeableValue（如 damage/cooldown）注册进来

        ///

        /// 参数 inUse：

        /// - false：仅注册存在（可能该值对应的系统还未启用）

        /// - true ：表示该升级值当前已经在使用中，会触发 RegisterInUse()（通常用于增加某类 UpgradeablesCount）

        /// </summary>

        //这里传入的upgradeableValue值是AbilityInit时角色所有的初始可升级字段。

        public void RegisterUpgradeableValue(IUpgradeableValue upgradeableValue, bool inUse = false)

        {

            // 让 upgradeableValue 知道“自己归哪个 AbilityManager 管”

            // Register(this) 内部一般会把自己加入某个统计计数器（例如 DamageUpgradeablesCount++）

            upgradeableValue.Register(this);



            // 保存到统一列表，UpgradeValue<T> 会从这里筛选并批量升级

            registeredUpgradeableValues.Add(upgradeableValue);



            // 如果该升级值已经“启用/生效”，则额外调用 RegisterInUse

            // 常见用途：某些 upgradeable 只在某个技能真正拥有/激活时才算“可升级对象”

            if (inUse) upgradeableValue.RegisterInUse();

        }



        /// <summary>

        /// 升级一类可升级值（批量升级）

        ///

        /// 例如：

        /// DamageUpgradeAbility 选中后会调用 UpgradeValue<UpgradeableDamage,float>(+2)

        /// 然后所有注册过的 UpgradeableDamage 都会统一 +2

        ///

        /// T：可升级值类型（实现 IUpgradeableValue）

        /// TValue：升级值类型（float 或 int）

        /// </summary>

        public void UpgradeValue<T, TValue>(TValue value) where T : IUpgradeableValue

        {

            // 从 registeredUpgradeableValues 中筛选出所有类型为 T 的可升级值

            // 注意：这里写法是先 OfType<T>() -> ToArray()，再强转为 UpgradeableValue<TValue>[]

            // 本质是：把所有 T 类型实例拿出来逐个调用 Upgrade(value)

            UpgradeableValue<TValue>[] upgradeableValues = registeredUpgradeableValues.OfType<T>().ToArray() as UpgradeableValue<TValue>[];



            foreach (UpgradeableValue<TValue> upgradeableValue in upgradeableValues)

            {

                upgradeableValue.Upgrade(value);

            }

        }



        /// <summary>

        /// 选择要展示给玩家的候选能力列表（升级弹窗 / 能力箱子弹窗用）

        ///

        /// 大致策略：

        /// 1) 先从 ownedAbilities / newAbilities 中各自提取“RequirementsMet() 为真”的可用能力

        ///    （提取时会从原池移除，避免重复抽同一个）

        /// 2) 候选数量默认为 3 个；若触发 FourthChance() 则为 4 个

        /// 3) 最多尝试抽 2 个“已拥有能力”（用于升级），但是否真的抽要看 OwnedChance()

        /// 4) 剩余位置优先用“新能力”填满；新能力不足时再用已拥有能力补齐

        /// 5) 抽完后，把未被抽走的“可用能力”放回各自池子（保持池子完整）

        /// </summary>

        public List<Ability> SelectAbilities()

        {

            //升级串口显示的可选择的能力

            List<Ability> selectedAbilities = new List<Ability>();



            // 从已拥有池中提取“可升级”的能力（RequirementsMet()）
            //既还未达到最大等级的能力
            WeightedAbilities availableOwnedAbilities = ExtractAvailableAbilities(ownedAbilities);



            // 从新能力池中提取“可获取”的能力（RequirementsMet()）

            WeightedAbilities availableNewAbilities = ExtractAvailableAbilities(newAbilities);



            // 候选数：基本 3 个；按 FourthChance 概率可能额外 +1 变成 4 个
            //判断某随机值是否小于(1 - 1 / playerCharacter.Luck)，如果小于则加1，最高为4个能力
            int selectedAbilitiesCount = 3 + (ResolveChance(FourthChance) ? 1 : 0);



            // 先尝试抽最多 2 个“已拥有能力”（给升级用）

            // 如果 availableOwnedAbilities 少于 2，就只能抽那么多

            int ownedAbilitiesCount = availableOwnedAbilities.Count < 2 ? availableOwnedAbilities.Count : 2;



            for (int i = 0; i < ownedAbilitiesCount; i++)

            {

                // 每次抽已拥有能力都要按 OwnedChance 决定是否抽到（不是必抽）

                // Luck越高，OwnedChance的返回值越高，则升级已有能力的候选更常出现

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



        /// <summary>

        /// 将未选择的候选能力放回对应池子

        /// 通常在升级弹窗关闭时调用：

        /// - 玩家选了其中一个，其他没选的要放回去，保证下次还能抽到

        /// </summary>

        public void ReturnAbilities(List<Ability> abilities)

        {

            foreach (Ability ability in abilities)

            {

                // Owned=true 表示该能力属于已拥有池（用于升级）

                if (ability.Owned)

                    ownedAbilities.Add(ability);

                else

                    newAbilities.Add(ability);

            }

        }



        /// <summary>

        /// 销毁所有已拥有的能力实例（通常用于退出关卡/重新开始前清理）

        /// 注意：这里只销毁 ownedAbilities 里的对象（即当前激活/拥有的能力）

        /// </summary>

        public void DestroyActiveAbilities()

        {

            foreach (Ability ability in ownedAbilities)

            {

                Destroy(ability.gameObject);

            }

        }



        /// <summary>

        /// 检查是否存在“可用能力”

        /// 用途：例如宝箱打开时，如果没有任何能力可拿/可升级，则宝箱可能退化为普通掉落箱

        /// </summary>

        public bool HasAvailableAbilities()

        {

            // 已拥有能力：如果还能继续升级（RequirementsMet），则说明有可用能力

            foreach (Ability ability in ownedAbilities)

            {

                if (ability.RequirementsMet())

                    return true;

            }



            // 新能力：如果满足获取条件（通常是未满级、或前置满足），则可用

            foreach (Ability ability in newAbilities)

            {

                if (ability.RequirementsMet())

                    return true;

            }



            return false;

        }



        /// <summary>

        /// 从某个能力池中提取“满足 RequirementsMet() 的可用能力”，并从原池移除。

        /// 这样做的原因：

        /// - 便于本次抽卡时只在可用能力里抽

        /// - 同时避免在 PullAbility 时“抽到不可用能力”

        /// - 从原池移除可避免同一个能力在本次抽卡过程中被重复抽到

        /// </summary>

        //作用：传入自定义带权重的技能列表类，将其中可升级的剪切出来，并返回剪切出来的新的技能列表

        private WeightedAbilities ExtractAvailableAbilities(WeightedAbilities abilities)

        {

            WeightedAbilities availableAbilities = new WeightedAbilities();



            // 遍历池子，把满足条件的能力拎出来
            foreach (Ability ability in abilities)

            {

                //能力有最大等级，达到最大等级的能力不可再被选出来进行升级，因此通过ability.RequirementsMet()来做判断

                //如果当前能力等级小于最大等级限制，就可以

                if (ability.RequirementsMet())

                    availableAbilities.Add(ability);

            }



            // 从原池移除这些“可用能力”（避免重复处理）

            foreach (Ability ability in availableAbilities)

            {

                abilities.Remove(ability);

            }



            return availableAbilities;

        }



        /// <summary>

        /// 按权重从 WeightedAbilities 中抽取一个能力（并从该容器移除）

        /// 实现方式：轮盘赌（Roulette Wheel Selection）

        /// - 每个能力有 DropWeight（权重）

        /// - 容器维护总权重 Weight

        /// - 在 [0, Weight) 之间取随机数，落在哪个区间就抽中哪个能力

        /// </summary>

        //作用：依据能力的稀有度从abilities中挑选出一个能力

        private Ability PullAbility(WeightedAbilities abilities)

        {

            float rand = Random.Range(0f, abilities.Weight);

            float cumulative = 0;



            foreach (Ability ability in abilities)

            {

                cumulative += ability.DropWeight;



                if (rand < cumulative)

                {

                    // 抽中后从可用池移除，保证本次不重复抽到

                    abilities.Remove(ability);

                    return ability;

                }

            }



            // 理论上不应该走到这里（除非权重/列表异常）

            Debug.LogError("Failed to pull ability!");

            return null;

        }



        /// <summary>

        /// 已拥有能力（用于升级）出现在候选里的概率

        /// 公式：1 + 0.3f * x - 1/玩家幸运值

        /// x 取决于玩家等级奇偶：

        /// - 偶数等级：x=2（更倾向给升级选项）

        /// - 奇数等级：x=1

        ///

        /// Luck 越高，(1/Luck) 越小 → 概率越高

        /// 直觉：幸运高 → 更容易刷到“升级已有技能”

        /// </summary>

        private float OwnedChance()

        {

            float x = playerCharacter.CurrentLevel % 2 == 0 ? 2 : 1;

            return 1 + 0.3f * x - 1 / playerCharacter.Luck;

        }



        /// <summary>

        /// 第四个候选能力出现的概率

        /// 公式：1 - 1/玩家幸运值

        /// Luck 越高 → 1/Luck 越小 → 概率越接近 1

        /// 直觉：幸运高 → 更容易出现 4 选 1

        /// </summary>

        private float FourthChance()

        {

            return 1 - 1 / playerCharacter.Luck;

        }



        /// <summary>

        /// 解析概率函数：根据传入的 chanceFunction 计算概率，并用随机数判断是否触发

        /// </summary>

        //System.Func<float> chanceFunction限定参数只能是一个返回值为float且无参数的函数。

        private bool ResolveChance(System.Func<float> chanceFunction)

        {

            return Random.Range(0.0f, 1.0f) < chanceFunction();

        }



        /// <summary>

        /// WeightedAbilities：带权重的能力容器（内部类）

        /// 功能：

        /// - 维护一个 Ability 列表

        /// - 维护总权重 weight（等于所有 ability.DropWeight 之和）

        /// - 支持 Add/Remove 时同步更新 weight

        /// - 实现 IEnumerable 以支持 foreach

        /// </summary>

        //此类实例化后支持foreach，newAbilities就是其定义的对象，虽然是个类，但也可以，应为该类继承自IEnumerable<Ability>，并实现了最后两段，就可以了。

        private class WeightedAbilities : IEnumerable<Ability>

        {

            private FastList<Ability> abilities; // 能力列表（FastList：项目自定义的高效列表）

            private float weight;                // 总权重（用于 PullAbility 轮盘赌）

            public float Weight { get => weight; set => weight = value; }

            public int Count { get => abilities.Count; }



            public WeightedAbilities()

            {

                abilities = new FastList<Ability>();

                weight = 0;

            }



            /// <summary>

            /// 添加能力并累加权重

            /// 注意：DropWeight 越大，越容易被 PullAbility 抽中

            /// </summary>

            public void Add(Ability ability)

            {

                abilities.Add(ability);

                weight += ability.DropWeight;

            }



            /// <summary>

            /// 移除能力并减去权重

            /// 注意：PullAbility 抽中能力后会 Remove，确保本次抽卡不重复

            /// </summary>

            public void Remove(Ability ability)

            {

                weight -= ability.DropWeight;

                abilities.Remove(ability);

            }



            // 支持 foreach 遍历，遍历的返回值就为ability。

            //实现IEnumerable<Ability>接口中的函数

            public IEnumerator<Ability> GetEnumerator()

            {

                foreach (Ability ability in abilities)

                {

                    yield return ability;

                }

            }



            IEnumerator IEnumerable.GetEnumerator()

            {

                return GetEnumerator();

            }

        }

    }

}

