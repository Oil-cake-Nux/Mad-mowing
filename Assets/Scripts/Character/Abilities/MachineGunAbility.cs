using System.Collections;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 机枪技能（MachineGunAbility）
    /// ------------------------------------------------------------
    /// 这是 GunAbility 的一个具体实现：让“枪本体”绕着玩家做圆周旋转，
    /// 并按冷却与射速机制自动发射子弹（Projectile）。
    ///
    /// 关键特点：
    /// 1) 枪的位置 = 玩家中心点 + 圆周方向 * 半径（gunRadius）
    /// 2) 枪的朝向 = 当前圆周方向（gunDirection）
    /// 3) 在“换弹/冷却期间”会额外做一圈旋转动画（reloadRotation），增强表现
    /// 4) 子弹生成/回收走 EntityManager 的对象池体系（SpawnProjectile + Despawn）
    /// </summary>
    public class MachineGunAbility : GunAbility
    {
        [Header("Machine Gun Stats")]

        /// <summary>
        /// 机枪的可视化对象（通常是一个带 Sprite/模型的子物体）
        /// 注意：这里控制的是枪的“显示位置/旋转”，不直接等于子弹出生点。
        /// </summary>
        [SerializeField] protected GameObject machineGun;

        /// <summary>
        /// 子弹的发射点（枪口位置）。
        /// 通常是 machineGun 下的一个子 Transform，用来精确控制子弹出生位置。
        /// </summary>
        [SerializeField] protected Transform launchTransform;

        /// <summary>
        /// 旋转速度（可升级值）。
        /// rotationSpeed.Value 越大，枪绕玩家旋转越快。
        /// </summary>
        [SerializeField] protected UpgradeableRotationSpeed rotationSpeed;

        /// <summary>
        /// 枪绕玩家旋转的半径（圆周半径）。
        /// 半径越大，枪离玩家越远。
        /// </summary>
        [SerializeField] protected float gunRadius;

        /// <summary>
        /// 当前枪的“指向方向”（单位向量，XY 平面）。
        /// 用于：
        /// 1) 决定枪摆放在玩家周围的哪个方向（位置 = center + dir * radius）
        /// 2) 决定子弹发射方向（projectile.Launch(gunDirection)）
        ///
        /// 默认值 Vector2.right 表示初始朝右。
        /// </summary>
        protected Vector3 gunDirection = Vector2.right;

        /// <summary>
        /// 每帧更新：
        /// 1) 先调用 base.Update() 让 GunAbility/ProjectileAbility 完成通用的计时/攻击逻辑
        /// 2) 计算换弹（冷却）期间的额外旋转动画量 reloadRotation
        /// 3) 计算当前圆周角 theta，使枪绕玩家旋转
        /// 4) 更新枪的位置与自转
        /// </summary>
        protected override void Update()
        {
            // 一定要先调用父类 Update：
            // - 父类通常会更新 timeSinceLastAttack
            // - 满足条件时会触发 Attack() -> LaunchProjectile()
            // 所以这里的旋转表现是“附加表现层”，不应该破坏父类攻击节奏。
            base.Update();

            // -----------------------------
            // 1) “换弹/冷却”时的额外旋转动画
            // -----------------------------
            // Rotate the gun if it is reloading
            float reloadRotation = 0;

            // t 表示“距离上次攻击过去了多少”的归一化比例：
            // timeSinceLastAttack / cooldown.Value
            // - 如果 timeSinceLastAttack = 0：刚刚开火，t=0
            // - 如果 timeSinceLastAttack = cooldown：冷却结束，t=1
            float t = timeSinceLastAttack / cooldown.Value;

            // 当 0<t<1 说明处于冷却/装填期间：
            // 我们让枪额外旋转一整圈（360 度），并随着 t 从 0 -> 1 逐渐增加
            // 这样就会出现“冷却期间枪在自转”的视觉反馈。
            if (t > 0 && t < 1)
            {
                reloadRotation = t * 360;
            }

            // -----------------------------
            // 2) 枪绕玩家做圆周旋转（持续旋转）
            // -----------------------------
            // theta 是弧度角：时间 * 旋转速度
            // 注意：Time.time 是游戏运行到现在的总时间（秒），不是 deltaTime。
            // 因此这是一个持续的、与时间同步的平滑旋转。
            float theta = Time.time * rotationSpeed.Value;

            // 根据角度 theta 计算单位圆上的方向向量 (cos, sin)
            // 这就是枪相对于玩家中心点的“圆周方向”。
            gunDirection = new Vector3(Mathf.Cos(theta), Mathf.Sin(theta), 0);

            // 枪的位置：玩家中心点 + 圆周方向 * 半径
            machineGun.transform.position = playerCharacter.CenterTransform.position + gunDirection * gunRadius;

            // 枪的朝向（Z 轴旋转）：
            // Mathf.Rad2Deg * theta：把弧度角转成角度
            // - reloadRotation：冷却期间额外反向旋转，使动画更有“装填感”
            //无上子弹动画版本。
            machineGun.transform.rotation = Quaternion.Euler(0, 0, Mathf.Rad2Deg * theta );
            //有上子弹动画版本
            //machineGun.transform.rotation = Quaternion.Euler(0, 0, Mathf.Rad2Deg * theta - reloadRotation);
        }

        /// <summary>
        /// 实际发射子弹的逻辑（覆写 GunAbility/ProjectileAbility 的发射方式）
        ///
        /// 注意：
        /// - 子弹出生位置用 launchTransform.position（枪口）
        /// - 子弹方向用 gunDirection（枪当前绕圈方向），而不是玩家 LookDirection
        /// </summary>
        protected override void LaunchProjectile()
        {
            // 通过 EntityManager 从对象池生成/取出一个 Projectile
            // 参数含义：
            // projectileIndex：该 projectilePrefab 对应的池编号（在 Use() 时 AddPoolForProjectile 得到）
            // launchTransform.position：出生点（枪口）
            // damage/knockback/speed：可升级数值（Upgradeable）
            // monsterLayer：子弹命中判定的目标层（通常是怪物层）
            Projectile projectile = entityManager.SpawnProjectile(
                projectileIndex,
                launchTransform.position,
                damage.Value,
                knockback.Value,
                speed.Value,
                monsterLayer
            );

            // 当子弹命中 damageable 时，把造成的伤害值计入玩家统计（StatsManager）
            // 这条链路在 Character.Init 中把 OnDealDamage 绑定到 StatsManager.IncreaseDamageDealt
            projectile.OnHitDamageable.AddListener(playerCharacter.OnDealDamage.Invoke);

            // 发射：方向使用“枪当前绕圈方向”
            projectile.Launch(gunDirection);
        }
    }
}
