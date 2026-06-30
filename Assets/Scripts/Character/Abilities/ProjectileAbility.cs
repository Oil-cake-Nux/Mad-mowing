using System.Collections;
using UnityEngine;

namespace Vampire
{
    // 发射投射物的器械类
    public class ProjectileAbility : Ability
    {
        [Header("投射物属性")]
        [SerializeField] protected GameObject projectilePrefab; // 投射物预制体
        [SerializeField] protected LayerMask monsterLayer; // 怪物所在的图层（用于投射物检测碰撞）
        [SerializeField] protected UpgradeableDamage damage; // 可升级的伤害值
        [SerializeField] protected UpgradeableProjectileSpeed speed; // 可升级的投射物速度
        [SerializeField] protected UpgradeableKnockback knockback; // 可升级的击退力度
        [SerializeField] protected UpgradeableWeaponCooldown cooldown; // 可升级的冷却时间
        protected float timeSinceLastAttack; // 记录上次攻击到现在的时间
        protected int projectileIndex; // 投射物在对象池中的索引

        protected override void Use()
        {

            // 遍历upgradeableValues可升级值列表，统计不同类字段的数量
            base.Use();
            // 激活物体，初始化冷却时间，为投射物创建对象池并记录索引
            gameObject.SetActive(true);
            timeSinceLastAttack = cooldown.Value;
            //调用AddPoolForProjectile函数会返回该种投射物在在投射物池列表中的索引值
            projectileIndex = entityManager.AddPoolForProjectile(projectilePrefab);
        }

        protected virtual void Update()
        {
            // 累加时间，当达到冷却时间时触发攻击
            timeSinceLastAttack += Time.deltaTime;
            if (timeSinceLastAttack >= cooldown.Value)
            {
                // 重置时间（保留余数，避免时间累积误差）
                timeSinceLastAttack = Mathf.Repeat(timeSinceLastAttack, cooldown.Value);
                // 执行攻击
                Attack();
            }
        }

        protected virtual void Attack()
        {
            // 发射投射物
            LaunchProjectile();
        }

        //
        protected virtual void LaunchProjectile()
        {
            // 从对象池生成投射物（参数：索引、生成位置、伤害、击退、速度、目标图层）
            Projectile projectile = entityManager.SpawnProjectile(projectileIndex, playerCharacter.CenterTransform.position, damage.Value, knockback.Value, speed.Value, monsterLayer);
            // 监听投射物击中目标的事件，触发玩家的"造成伤害"事件
            //OnDealDamage事件监听的是statsManager.IncreaseDamageDealt事件，触发后会更新总造成伤害值
            projectile.OnHitDamageable.AddListener(playerCharacter.OnDealDamage.Invoke);
            // 沿玩家朝向发射投射物
            projectile.Launch(playerCharacter.LookDirection);
        }
    }
}