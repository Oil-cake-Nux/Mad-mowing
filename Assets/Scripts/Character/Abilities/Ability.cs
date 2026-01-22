using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace Vampire
{

    public abstract class Ability : MonoBehaviour
    {
        [Header("Ability Details")]
        [SerializeField] protected Sprite image; // 能力图标
        [SerializeField] protected LocalizedString localizedName; // 本地化的能力名称（支持多语言）
        [SerializeField] protected LocalizedString localizedDescription; // 本地化的能力描述
        [SerializeField] protected Rarity rarity = Rarity.Common; // 能力稀有度（影响出现概率）

        protected AbilityManager abilityManager; // 能力管理器引用
        protected EntityManager entityManager; // 实体管理器引用（管理游戏中的实体，如角色、敌人等）
        protected Character playerCharacter; // 玩家角色引用
        protected List<IUpgradeableValue> upgradeableValues; // 该能力中所有可升级的值（如伤害、冷却时间等）
        protected int level = 0; // 当前等级（初始为0，选择后升为1）
        protected int maxLevel; // 最大等级（）
        protected bool owned = false; // 是否已拥有该能力

        // 公共属性（供外部访问）
        public int Level { get => level; } // 获取当前等级
        public bool Owned { get => owned; } // 获取是否拥有
        public Sprite Image { get => image; } // 获取能力图标
        public string Name { get => localizedName.GetLocalizedString(); } // 获取本地化名称
        public float DropWeight { get => (float)rarity; } // 掉落权重（由稀有度决定，用于随机选择）
        public virtual string Description
        {
            get
            {
                if (!owned)
                    // 未拥有时显示基础描述
                    return localizedDescription.GetLocalizedString();
                else
                    // 已拥有时显示升级后的描述
                    return GetUpgradeDescriptions();
            }
        }

        /// <summary>
        /// 初始化能力
        /// </summary>
        /// <param name="abilityManager">能力管理器</param>
        /// <param name="entityManager">实体管理器</param>
        /// <param name="playerCharacter">玩家角色</param>
        public virtual void Init(AbilityManager abilityManager, EntityManager entityManager, Character playerCharacter)
        {
            this.abilityManager = abilityManager;
            this.entityManager = entityManager;
            this.playerCharacter = playerCharacter;

            // 通过反射获取当前能力中所有实现了IUpgradeableValue接口的字段（可升级值）
            //通过以下脚本来获取该能力中所有可升级的字段值，并且转为IUpgradeableValue接口类型（因为可升级的字段一定为类，并且一定继承自IUpgradeableValue接口）
            upgradeableValues = this.GetType()  // 获取当前实例的类型（比如某个继承自Ability的具体能力类）
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)  // 获取该类型的所有字段（包括公共、私有、实例字段）
            .Where(fi => typeof(IUpgradeableValue).IsAssignableFrom(fi.FieldType))  // 筛选出“字段类型是IUpgradeableValue接口或其实现类”的字段
            .Select(fi => fi.GetValue(this) as IUpgradeableValue)  // 将字段的值转换为IUpgradeableValue接口实例
            .ToList();  // 转为列表

            //以上代码的等价写法
            // 获取类型信息
            //Type currentType = this.GetType();
            //// 获取所有字段
            //FieldInfo[] allFields = currentType.GetFields(
            //    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public
            //);

            //upgradeableValues = new List<IUpgradeableValue>();

            //// 遍历所有字段
            //foreach (FieldInfo field in allFields)
            //{
            //    // 检查字段类型是否实现了 IUpgradeableValue
            //    if (typeof(IUpgradeableValue).IsAssignableFrom(field.FieldType))
            //    {
            //        // 获取字段值
            //        object fieldValue = field.GetValue(this);
            //        // 转换为接口类型
            //        IUpgradeableValue upgradeableValue = fieldValue as IUpgradeableValue;

            //        if (upgradeableValue != null)
            //        {
            //            upgradeableValues.Add(upgradeableValue);
            //        }
            //    }
            //}

            // 将可升级值注册到能力管理器
            upgradeableValues.ForEach(x => abilityManager.RegisterUpgradeableValue(x));
            //以上语句等同于
            //foreach (IUpgradeableValue x in upgradeableValues)
            //{
            //    abilityManager.RegisterUpgradeableValue(x);
            //}
            // 计算最大等级：最大升级次数 + 1（初始等级）
            if (upgradeableValues.Count > 0)
                maxLevel = upgradeableValues.Max(x => x.UpgradeCount) + 1;
        }

        /// <summary>
        /// 选择能力（首次选择为"获取"，后续选择为"升级"）
        /// </summary>
        public virtual void Select()
        {
            if (!owned)
            {
                // 未拥有时，标记为已拥有并触发"使用"逻辑
                owned = true;
                Use();
            }
            else
            {
                // 已拥有时，触发"升级"逻辑
                Upgrade();
            }
            // 无论获取还是升级，等级+1
            level++;
        }

        /// <summary>
        /// 首次获取能力时的逻辑（默认注册可升级值为"已启用"）
        /// </summary>
        protected virtual void Use()
        {
            //RegisterInUse的作用是增加该种类型的可升级值的计数
            upgradeableValues.ForEach(x => x.RegisterInUse());
        }

        /// <summary>
        /// 升级能力时的逻辑（默认升级所有可升级值）
        /// </summary>
        protected virtual void Upgrade()
        {
            upgradeableValues.ForEach(x => x.Upgrade());
        }

        /// <summary>
        /// 检查是否满足升级条件（默认：当前等级 < 最大等级）
        /// </summary>
        public virtual bool RequirementsMet()
        {
            return level < maxLevel;
        }

        /// <summary>
        /// 获取所有可升级值的升级描述（组合成完整描述），例如伤害+10%
        /// </summary>
        protected string GetUpgradeDescriptions()
        {
            string description = "";
            upgradeableValues.ForEach(x => description += x.GetUpgradeDescription());
            return description;
        }

        /// <summary>
        /// 能力稀有度（影响掉落权重，数值越高越容易出现）
        /// </summary>
        public enum Rarity
        {
            Common = 50,      // 普通
            Uncommon = 25,    //  uncommon
            Rare = 15,        // 稀有
            Legendary = 9,    // 传奇
            Exotic = 1        // 异域（最稀有）
        }
    }
}