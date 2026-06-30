
using UnityEngine;

namespace Vampire
{
    //该类是负责把某个 UpgradeableFloat 加一段数值”的能力卡
    public class FloatUpgradeAbility<T> : Ability where T : UpgradeableFloat
    {
        [SerializeField] protected float[] upgrades;
        public override string Description
        {
            get {
                return GetUpgradeDescription();
            }
        }

        protected override void Use()
        {
            //增加该种类型的可升级字段的计数
            base.Use();
            gameObject.SetActive(true);
            //提取所有T类型的可升级字段，执行升级逻辑，upgrades[level]为该能力在此次升级所提升的值
            abilityManager.UpgradeValue<T, float>(upgrades[level]);
        }

        protected override void Upgrade()
        {
            abilityManager.UpgradeValue<T, float>(upgrades[level]);
        }

        //判断该能力是否还可以升级
        public override bool RequirementsMet()
        {
            return level < upgrades.Length;
        }

        //获得升级描述
        protected string GetUpgradeDescription()
        {
            return DescriptionUtils.GetUpgradeDescription(localizedDescription.GetLocalizedString(), upgrades[level]);
        }
    }
}