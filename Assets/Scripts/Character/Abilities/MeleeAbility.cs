using UnityEngine;

namespace Vampire
{
    //所有“周期触发的近战攻击技能”的基类
    public abstract class MeleeAbility : Ability
    {
        [Header("Melee Stats")]
        [SerializeField] protected LayerMask targetLayer;
        [SerializeField] protected UpgradeableDamage damage;
        [SerializeField] protected UpgradeableKnockback knockback;
        [SerializeField] protected UpgradeableWeaponCooldown cooldown;
        [SerializeField] protected SpriteRenderer weaponSpriteRenderer;
        protected float timeSinceLastAttack;

        protected override void Use()
        {
            base.Use();
            gameObject.SetActive(true);
            timeSinceLastAttack = cooldown.Value;
        }

        void Update()
        {
            timeSinceLastAttack += Time.deltaTime;
            if (timeSinceLastAttack >= cooldown.Value)
            {
                timeSinceLastAttack = Mathf.Repeat(timeSinceLastAttack, cooldown.Value);
                Attack();
            }
        }

        protected abstract void Attack();
    }
}
