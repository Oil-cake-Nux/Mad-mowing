using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Vampire
{
    /// <summary>
    /// 可升级数值的接口，定义了可升级数值的核心行为
    /// </summary>
    public interface IUpgradeableValue
    {
        int UpgradeCount { get; }   // 获取可升级的总次数（升级数组的长度）

        void Register(AbilityManager abilityManager);  //注册到能力管理器
        //RegisterInUse的作用是增加该种类型的可升级值的计数
        void RegisterInUse();   //标记为正在使用（当该数值被选中时调用）
        void Upgrade();  //执行升级逻辑（例如：数值提升10%等）
        string GetUpgradeDescription(); // 获取升级描述（例如："伤害提升10%"）
    }

    /// <summary>
    /// 可升级数值的泛型抽象基类，实现IUpgradeableValue接口
    /// 封装了可升级数值的通用逻辑，子类需实现具体升级细节
    /// </summary>
    public abstract class UpgradeableValue<T> : IUpgradeableValue
    {
        [SerializeField] protected T value; // 基础数值
        [SerializeField] protected T[] upgrades; // 升级数组，存储每次升级的增量/倍率
        protected AbilityManager abilityManager; // 能力管理器引用
        protected int level = 0; // 当前升级等级（从0开始，记录已升级次数）

        protected virtual string UpgradeName { get; set; } // 升级名称（本地化字符串）
        public virtual T Value { get => value; set => this.value = value; } // 当前生效的数值（可能经过升级计算）
        public int UpgradeCount { get => upgrades.Length; } // 可升级总次数（升级数组的长度）
        public UnityEvent OnChanged { get; } = new UnityEvent(); // 数值变化时触发的事件

        /// <summary>
        /// 注册到能力管理器
        /// </summary>
        public void Register(AbilityManager abilityManager) { this.abilityManager = abilityManager; }

        /// <summary>
        /// 标记为正在使用（子类需实现具体逻辑，如增加管理器中对应类型的计数）
        /// </summary>
        public abstract void RegisterInUse();

        /// <summary>
        /// 执行升级（如果未达到最大等级，则使用升级数组中对应等级的增量进行升级）
        /// </summary>
        public virtual void Upgrade()
        {
            if (level < upgrades.Length)
                Upgrade(upgrades[level++]);
        }

        /// <summary>
        /// 具体的升级逻辑（子类需实现，根据类型处理数值变化）
        /// </summary>
        public abstract void Upgrade(T upgrade);

        /// <summary>
        /// 获取升级描述（子类需实现，返回本地化的描述文本）
        /// </summary>
        public abstract string GetUpgradeDescription();
    }

    ////////////////////////////////////////////////////////////////////////////////
    /// 浮点型可升级数值
    ////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// 浮点型可升级数值的抽象基类，继承自UpgradeableValue<float>
    /// 实现了浮点型数值的通用升级逻辑（倍率相乘）
    /// </summary>
    public abstract class UpgradeableFloat : UpgradeableValue<float>
    {
        /// <summary>
        /// 浮点型数值的升级逻辑：基础数值乘以（1+升级倍率）
        /// 例如：升级倍率为0.1时，数值变为原来的1.1倍（提升10%）
        /// </summar>
        public override void Upgrade(float upgrade)
        {
            value *= (1 + upgrade);
            OnChanged.Invoke(); // 触发数值变化事件
        }

        /// <summary>
        /// 获取浮点型升级的描述（如果未达到最大等级且升级倍率不为0，则返回本地化描述）
        /// </summary>
        /// <returns>升级描述字符串</returns>
        public override string GetUpgradeDescription()
        {
            if (level >= upgrades.Length || upgrades[level] == 0) return "";
            return DescriptionUtils.GetUpgradeDescription(UpgradeName, upgrades[level]);
        }
    }

    /// <summary>
    /// 可升级的伤害值（浮点型）
    /// </summary>
    [System.Serializable]
    public class UpgradeableDamage : UpgradeableFloat
    {
        protected override string UpgradeName => LocalizationSettings.StringDatabase.GetLocalizedString("Upgradeable Values", "Damage"); // 本地化的"伤害"名称
        public override void RegisterInUse() { abilityManager.DamageUpgradeablesCount++; } // 注册使用时，增加能力管理器中伤害可升级数值量的计数
    }

    /// <summary>
    /// 可升级的伤害频率（浮点型）
    /// </summary>
    [System.Serializable]
    public class UpgradeableDamageRate : UpgradeableFloat
    {
        protected override string UpgradeName => LocalizationSettings.StringDatabase.GetLocalizedString("Upgradeable Values", "Damage Rate"); // 本地化的"伤害频率"名称
        public override void RegisterInUse() { abilityManager.FireRateUpgradeablesCount++; } // 注册使用时，增加能力管理器中伤害频率可升级数值的计数
    }

    /// <summary>
    /// 可升级的武器冷却时间（浮点型）
    /// </summary>
    [System.Serializable]
    public class UpgradeableWeaponCooldown : UpgradeableFloat
    {
        protected override string UpgradeName => LocalizationSettings.StringDatabase.GetLocalizedString("Upgradeable Values", "Weapon Cooldown"); // 本地化的"武器冷却时间"名称
        public override void RegisterInUse() { abilityManager.WeaponCooldownUpgradeablesCount++; } // 注册使用时，增加能力管理器中武器冷却可升级数值的计数
    }

    /// <summary>
    /// 可升级的恢复冷却时间（浮点型）
    /// </summary>
    [System.Serializable]
    public class UpgradeableRecoveryCooldown : UpgradeableFloat
    {
        protected override string UpgradeName => LocalizationSettings.StringDatabase.GetLocalizedString("Upgradeable Values", "Recovery Cooldown"); // 本地化的"恢复冷却时间"名称
        public override void RegisterInUse() { abilityManager.RecoveryCooldownUpgradeablesCount++; } // 注册使用时，增加能力管理器中恢复冷却可升级数值的计数
    }

    /// <summary>
    /// 可升级的持续时间（浮点型）
    /// </summary>
    [System.Serializable]
    public class UpgradeableDuration : UpgradeableFloat
    {
        protected override string UpgradeName => LocalizationSettings.StringDatabase.GetLocalizedString("Upgradeable Values", "Duration"); // 本地化的"持续时间"名称
        public override void RegisterInUse() { abilityManager.DurationUpgradeablesCount++; } // 注册使用时，增加能力管理器中持续时间可升级数值的计数
    }

    /// <summary>
    /// 可升级的范围（AOE，浮点型）
    /// </summary>
    [System.Serializable]
    public class UpgradeableAOE : UpgradeableFloat
    {
        protected override string UpgradeName => LocalizationSettings.StringDatabase.GetLocalizedString("Upgradeable Values", "AOE"); // 本地化的"范围"名称
        public override void RegisterInUse() { abilityManager.AOEUpgradeablesCount++; } // 注册使用时，增加能力管理器中范围可升级数值的计数
    }

    /// <summary>
    /// 可升级的击退效果（浮点型）
    /// </summary>
    [System.Serializable]
    public class UpgradeableKnockback : UpgradeableFloat
    {
        protected override string UpgradeName => LocalizationSettings.StringDatabase.GetLocalizedString("Upgradeable Values", "Knockback"); // 本地化的"击退"名称
        public override void RegisterInUse() { abilityManager.KnockbackUpgradeablesCount++; } // 注册使用时，增加能力管理器中击退可升级数值的计数
    }

    /// <summary>
    /// 可升级的 projectile 速度（浮点型）
    /// </summary>
    [System.Serializable]
    public class UpgradeableProjectileSpeed : UpgradeableFloat
    {
        protected override string UpgradeName => LocalizationSettings.StringDatabase.GetLocalizedString("Upgradeable Values", "Projectile Speed"); // 本地化的"投射物速度"名称
        public override void RegisterInUse() { abilityManager.ProjectileSpeedUpgradeablesCount++; } // 注册使用时，增加能力管理器中投射物速度可升级数值的计数
    }

    /// <summary>
    /// 可升级的恢复概率（浮点型）
    /// </summary>
    [System.Serializable]
    public class UpgradeableRecoveryChance : UpgradeableFloat
    {
        protected override string UpgradeName => LocalizationSettings.StringDatabase.GetLocalizedString("Upgradeable Values", "Recovery Chance"); // 本地化的"恢复概率"名称
        public override void RegisterInUse() { abilityManager.RecoveryChanceUpgradeablesCount++; } // 注册使用时，增加能力管理器中恢复概率可升级数值的计数
    }

    /// <summary>
    /// 可升级的流血伤害（浮点型）
    /// </summary>
    [System.Serializable]
    public class UpgradeableBleedDamage : UpgradeableFloat
    {
        protected override string UpgradeName => LocalizationSettings.StringDatabase.GetLocalizedString("Upgradeable Values", "Bleed Damage"); // 本地化的"流血伤害"名称
        public override void RegisterInUse() { abilityManager.BleedDamageUpgradeablesCount++; } // 注册使用时，增加能力管理器中流血伤害可升级数值的计数
    }

    /// <summary>
    /// 可升级的流血频率（浮点型）
    /// </summary>
    [System.Serializable]
    public class UpgradeableBleedRate : UpgradeableFloat
    {
        protected override string UpgradeName => LocalizationSettings.StringDatabase.GetLocalizedString("Upgradeable Values", "Bleed Rate"); // 本地化的"流血频率"名称
        public override void RegisterInUse() { abilityManager.BleedRateUpgradeablesCount++; } // 注册使用时，增加能力管理器中流血频率可升级数值的计数
    }

    /// <summary>
    /// 可升级的流血持续时间（浮点型）
    /// </summary>
    [System.Serializable]
    public class UpgradeableBleedDuration : UpgradeableFloat
    {
        protected override string UpgradeName => LocalizationSettings.StringDatabase.GetLocalizedString("Upgradeable Values", "Bleed Duration"); // 本地化的"流血持续时间"名称
        public override void RegisterInUse() { abilityManager.BleedDurationUpgradeablesCount++; } // 注册使用时，增加能力管理器中流血持续时间可升级数值的计数
    }

    /// <summary>
    /// 可升级的移动速度（浮点型）
    /// </summary>
    [System.Serializable]
    public class UpgradeableMovementSpeed : UpgradeableFloat
    {
        protected override string UpgradeName => LocalizationSettings.StringDatabase.GetLocalizedString("Upgradeable Values", "Movement Speed"); // 本地化的"移动速度"名称
        public override void RegisterInUse() { abilityManager.MovementSpeedUpgradeablesCount++; } // 注册使用时，增加能力管理器中移动速度可升级数值的计数
    }

    /// <summary>
    /// 可升级的旋转速度（浮点型）
    /// </summary>
    [System.Serializable]
    public class UpgradeableRotationSpeed : UpgradeableFloat
    {
        protected override string UpgradeName => LocalizationSettings.StringDatabase.GetLocalizedString("Upgradeable Values", "Rotation Speed"); // 本地化的"旋转速度"名称
        public override void RegisterInUse() { abilityManager.RotationSpeedUpgradeablesCount++; } // 注册使用时，增加能力管理器中旋转速度可升级数值的计数
    }

    ////////////////////////////////////////////////////////////////////////////////
    /// 整型可升级数值
    ////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// 整型可升级数值的抽象基类，继承自UpgradeableValue<int>
    /// 实现了整型数值的通用升级逻辑（增量相加）
    /// </summary>
    public abstract class UpgradeableInt : UpgradeableValue<int>
    {
        /// <summary>
        /// 整型数值的升级逻辑：基础数值加上升级增量
        /// 例如：升级增量为2时，数值直接增加2
        /// </summary>
        /// <param name="upgrade">本次升级的增量</param>
        public override void Upgrade(int upgrade)
        {
            value += upgrade;
            OnChanged.Invoke(); // 触发数值变化事件
        }

        /// <summary>
        /// 获取整型升级的描述（如果未达到最大等级且升级增量不为0，则返回本地化描述）
        /// </summary>
        /// <returns>升级描述字符串</returns>
        public override string GetUpgradeDescription()
        {
            if (level >= upgrades.Length || upgrades[level] == 0) return "";
            return DescriptionUtils.GetUpgradeDescription(UpgradeName, upgrades[level]);
        }
    }

    /// <summary>
    /// 可升级的 projectile 数量（整型）
    /// 特殊逻辑：最终数量 = 基础数量 × 每单位数量（projectilesPer）
    /// </summary>
    [System.Serializable]
    public class UpgradeableProjectileCount : UpgradeableInt
    {
        [SerializeField] protected int projectilesPer = 1; // 每单位基础数量对应的实际数量（例如：1单位=2发，则projectilesPer=2）
        public override int Value { get => projectilesPer * value; } // 最终生效的数量（基础数量×每单位数量）
        protected override string UpgradeName => LocalizationSettings.StringDatabase.GetLocalizedString("Upgradeable Values", "Projectile Count"); // 本地化的"投射物数量"名称
        public override void RegisterInUse() { abilityManager.ProjectileCountUpgradeablesCount++; } // 注册使用时，增加能力管理器中投射物数量可升级数值的计数

        /// <summary>
        /// 获取投射物数量的升级描述（考虑每单位数量的倍率）
        /// </summary>
        /// <returns>升级描述字符串</returns>
        public override string GetUpgradeDescription()
        {
            if (level >= upgrades.Length || upgrades[level] == 0) return "";
            return DescriptionUtils.GetUpgradeDescription(UpgradeName, upgrades[level] * projectilesPer); // 描述中显示实际增加的数量（升级增量×每单位数量）
        }
    }

    /// <summary>
    /// 可升级的恢复量（整型）
    /// </summary>
    [System.Serializable]
    public class UpgradeableRecovery : UpgradeableInt
    {
        protected override string UpgradeName => LocalizationSettings.StringDatabase.GetLocalizedString("Upgradeable Values", "Recovery"); // 本地化的"恢复量"名称
        public override void RegisterInUse() { abilityManager.RecoveryUpgradeablesCount++; } // 注册使用时，增加能力管理器中恢复量可升级数值的计数
    }

    /// <summary>
    /// 可升级的护甲值（整型）
    /// </summary>
    [System.Serializable]
    public class UpgradeableArmor : UpgradeableInt
    {
        protected override string UpgradeName => LocalizationSettings.StringDatabase.GetLocalizedString("Upgradeable Values", "Armor"); // 本地化的"护甲"名称
        public override void RegisterInUse() { abilityManager.ArmorUpgradeablesCount++; } // 注册使用时，增加能力管理器中护甲可升级数值的计数
    }
}