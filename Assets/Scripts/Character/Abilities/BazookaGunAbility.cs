using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 火箭筒武器（BazookaGunAbility）
    /// ------------------------------------------------------------
    /// 继承自 GunAbility（枪械类通用能力），属于“定时攻击 + 连发/射速控制”的一类武器。
    ///
    /// 与普通 GunAbility 子类不同点：
    /// 1) LaunchProjectile() 不是直接生成子弹，而是启动一个协程动画 LaunchProjecileAnimation()
    ///    - 先寻敌/锁定方向（或随机方向）
    ///    - 做一个“瞄准旋转”的过渡动画（EaseOutBack）
    ///    - 最后再生成 ExplosiveProjectile（爆炸弹）并发射
    /// 2) 子弹类型为 ExplosiveProjectile（爆炸范围伤害），并调用 SetupExplosion 配置爆炸参数
    /// 3) bazookaGun 本体有一个轻微的上下浮动（hoverOffset + sin 波动），用来增强表现
    /// </summary>
    public class BazookaGunAbility : GunAbility
    {
        [Header("Bazooka Gun Stats")]

        /// <summary>
        /// 火箭筒可视化物体（枪模型/精灵所在 GameObject）
        /// 用于控制火箭筒显示的位置与旋转（瞄准动画）。
        /// </summary>
        [SerializeField] protected GameObject bazookaGun;

        /// <summary>
        /// 发射点（枪口 Transform）
        /// 爆炸弹从这里出生。
        /// </summary>
        [SerializeField] protected Transform launchTransform;

        /// <summary>
        /// 发射粒子特效（开火火焰/烟雾等）
        /// 在真正生成并发射 projectile 后播放。
        /// </summary>
        [SerializeField] protected ParticleSystem launchParticles;

        /// <summary>
        /// 爆炸范围（UpgradeableAOE 可升级值）
        /// 用于 SetupExplosion(damage, aoe, knockback) 的 aoe 参数。
        /// </summary>
        [SerializeField] protected UpgradeableAOE explosionAOE;

        /// <summary>
        /// 火箭筒相对玩家中心点的悬停偏移（例如拿在手上/肩上）
        /// 实际显示位置 = 玩家中心 + currHoverOffset
        /// </summary>
        [SerializeField] protected Vector2 hoverOffset;

        /// <summary>
        /// 锁定目标的搜索半径
        /// 在 LaunchProjecileAnimation() 中会从 bazookaGun 当前位置出发，
        /// 找半径内最近的 ISpatialHashGridClient（通常就是怪物）。
        /// </summary>
        [SerializeField] protected float targetRadius = 5;

        /// <summary>
        /// 当前悬停偏移（在 hoverOffset 基础上加上 sin 的上下浮动）
        /// </summary>
        protected Vector2 currHoverOffset;

        /// <summary>
        /// 枪的方向（单位向量）
        /// 注意：这个变量在当前版本基本没用于 Update() 的旋转（注释掉了），
        /// 发射方向最终由协程里计算得到的 launchDirection 决定。
        /// </summary>
        protected Vector3 gunDirection = Vector2.right;

        /// <summary>
        /// 当前枪的角度（度数），用于控制 bazookaGun 的 z 轴旋转
        /// 在协程中会从 initialTheta 逐渐插值到 targetTheta（瞄准目标）。
        /// </summary>
        protected float theta = 0;

        /// <summary>
        /// 每帧更新：
        /// 1) base.Update()：让 GunAbility/ProjectileAbility 完成通用计时与攻击触发
        /// 2) 计算 reloadRotation（目前未实际用于旋转，因为旋转逻辑被注释）
        /// 3) 让 bazookaGun 在玩家周围做轻微“上下漂浮”，并保持跟随玩家
        /// </summary>
        protected override void Update()
        {
            // 父类 Update 负责：
            // - timeSinceLastAttack 计时
            // - 满足条件时触发 Attack() / LaunchProjectile()
            base.Update();

            // -----------------------------
            // 1) 冷却/装填期间的旋转量（视觉用）
            // -----------------------------
            // Rotate the gun if it is reloading
            float reloadRotation = 0;

            // t = 当前冷却进度（0~1）
            // timeSinceLastAttack / cooldown.Value
            float t = timeSinceLastAttack / cooldown.Value;

            // 当 0<t<1 表示正在冷却中，这里把它映射成 0~360 度的旋转量
            // 但注意：后面实际应用旋转的代码（bazookaGun.transform.rotation...）目前被注释掉了
            if (t > 0 && t < 1)
            {
                reloadRotation = t * 360;
            }

            // -----------------------------
            // 2) 火箭筒悬停/漂浮表现 + 跟随玩家
            // -----------------------------
            // gunDirection = new Vector3(Mathf.Cos(theta), Mathf.Sin(theta), 0);

            // 在 hoverOffset 基础上增加一个上下浮动：
            // Vector2.up * sin(时间*5) * 0.1
            // - 频率：5
            // - 振幅：0.1
            currHoverOffset = hoverOffset + Vector2.up * Mathf.Sin(Time.time * 5) * 0.1f;

            // bazookaGun 始终跟随玩家中心点，并加上漂浮偏移
            bazookaGun.transform.position = (Vector2)playerCharacter.CenterTransform.position + currHoverOffset;

            // bazookaGun.transform.rotation = Quaternion.Euler(0, 0, theta - reloadRotation);
        }

        /// <summary>
        /// 发射逻辑入口（覆写父类的 LaunchProjectile）
        /// 这里不直接生成子弹，而是启动一个“瞄准/旋转/延迟后发射”的协程动画。
        /// </summary>
        protected override void LaunchProjectile()
        {
            StartCoroutine(LaunchProjecileAnimation());
        }

        /// <summary>
        /// 发射协程：包含“寻找目标 → 瞄准旋转动画 → 生成爆炸弹并发射 → 播放开火粒子”
        ///
        /// 细节流程：
        /// 1) 在 targetRadius 半径内找最近目标（ISpatialHashGridClient），可能为空
        /// 2) 计算 launchDirection（目标方向或随机方向）
        /// 3) 计算目标角 targetTheta（相对于 Vector2.right 的 SignedAngle）
        /// 4) 用一个短时间 tMax 做“EaseOutBack”的角度插值（从 initialTheta -> targetTheta）
        ///    - 若 targetEntity 不为空：插值过程中不断更新目标方向（跟踪移动目标）
        /// 5) 再进行一段“锁定保持”：
        ///    - 有目标：再持续 tMax 时间，每帧强制对准目标
        ///    - 无目标：等待 tMax
        /// 6) 最终生成 ExplosiveProjectile，设置爆炸参数，发射并播放粒子
        /// </summary>
        protected IEnumerator LaunchProjecileAnimation()
        {
            // 通过 SpatialHashGrid 查询：从 bazookaGun 当前的位置，找指定半径内最近的实体
            // 返回类型 ISpatialHashGridClient（怪物/玩家等可在格子系统里被查询的实体）
            ISpatialHashGridClient targetEntity = entityManager.Grid.FindClosestInRadius(bazookaGun.transform.position, targetRadius);

            // 计算初始发射方向：
            // - 如果没有目标：随机方向（insideUnitCircle）并归一化
            // - 如果有目标：指向目标的方向向量（目标位置 - bazookaGun位置）并归一化
            Vector2 launchDirection = targetEntity == null
                ? Random.insideUnitCircle.normalized
                : (targetEntity.Position - (Vector2)bazookaGun.transform.position).normalized;

            // 目标角度：把方向向量转换为相对 Vector2.right 的有符号角度（度）
            float targetTheta = Vector2.SignedAngle(Vector2.right, launchDirection);

            // 起始角度：当前 theta（上一次/当前枪身的角度）
            float initialTheta = theta;

            // -----------------------------
            // 1) 瞄准插值动画（从 initialTheta → targetTheta）
            // -----------------------------
            float t = 0;

            // tMax：动画时长
            // 这里用 (1/firerate) * 0.45
            // 解释：firerate 越高（越快），这个瞄准动画越短；0.45 是一个表现系数
            float tMax = 1 / firerate.Value * 0.45f;

            while (t < tMax)
            {
                // 当前进度比例（0~1），虽然此变量 tScaled 在现版本没被使用（保留/可用于调试）
                float tScaled = t / tMax;

                // 如果有目标：动画过程中持续更新目标方向，让枪能“跟踪移动目标”
                if (targetEntity != null)
                {
                    launchDirection = (targetEntity.Position - (Vector2)bazookaGun.transform.position).normalized;
                    targetTheta = Vector2.SignedAngle(Vector2.right, launchDirection);
                }

                // 将 theta 从 initialTheta 插值到 targetTheta
                // 注意这里传入 EasingUtils.EaseOutBack(t) —— 参数是 t（秒）而不是归一化 tScaled
                // 这取决于你们 EasingUtils 的实现方式（该项目里确实有 EasingUtils.cs）
                theta = Mathf.Lerp(initialTheta, targetTheta, EasingUtils.EaseOutBack(t));

                // 应用旋转到火箭筒显示物体（绕 Z 轴）
                bazookaGun.transform.rotation = Quaternion.Euler(0, 0, theta);

                //bazookaGun.transform.rotation = Quaternion.Lerp(Quaternion.Euler(0, 0, initialTheta), Quaternion.Euler(0, 0, targetTheta), EasingUtils.EaseOutBack(t));

                // 时间推进
                t += Time.deltaTime;
                yield return null;
            }

            // -----------------------------
            // 2) 锁定保持（进一步稳定瞄准）
            // -----------------------------
            if (targetEntity != null)
            {
                // 有目标：再持续 tMax 时间，每帧强制把枪对准目标
                t = 0;
                while (t < tMax)
                {
                    float tScaled = t / tMax;

                    launchDirection = (targetEntity.Position - (Vector2)bazookaGun.transform.position).normalized;
                    targetTheta = Vector2.SignedAngle(Vector2.right, launchDirection);

                    // 这里直接把 bazookaGun 的旋转设置到目标角度（不再插值）
                    bazookaGun.transform.rotation = Quaternion.Euler(0, 0, targetTheta);

                    //bazookaGun.transform.rotation = Quaternion.Lerp(Quaternion.Euler(0, 0, initialTheta), Quaternion.Euler(0, 0, targetTheta), EasingUtils.EaseOutBack(t));

                    t += Time.deltaTime;
                    yield return null;
                }
            }
            else
            {
                // 没目标：等待 tMax（等价于“瞄准动作耗时”）
                yield return new WaitForSeconds(tMax);
            }

            // 最终锁定：把 theta 设置为 targetTheta，并确保枪身旋转到最终角度
            theta = targetTheta;
            bazookaGun.transform.rotation = Quaternion.Euler(0, 0, theta);

            // -----------------------------
            // 3) 生成爆炸投射物并发射
            // -----------------------------
            // 注意：这里强转为 ExplosiveProjectile，说明该枪对应的 projectilePrefab 必须是 ExplosiveProjectile
            ExplosiveProjectile projectile = (ExplosiveProjectile)entityManager.SpawnProjectile(
                projectileIndex,
                launchTransform.position,
                damage.Value,
                knockback.Value,
                speed.Value,
                monsterLayer
            );

            // 配置爆炸参数：
            // - 爆炸伤害：damage.Value
            // - 爆炸范围：explosionAOE.Value（可升级）
            // - 爆炸击退：knockback.Value
            projectile.SetupExplosion(damage.Value, explosionAOE.Value, knockback.Value);

            // 命中统计：把子弹对敌人造成的伤害记入 Character.OnDealDamage
            projectile.OnHitDamageable.AddListener(playerCharacter.OnDealDamage.Invoke);

            // 发射方向：朝最终锁定方向（如果有目标则对准目标，否则随机）
            projectile.Launch(launchDirection);

            // 播放开火粒子特效
            launchParticles.Play();
        }
    }
}
