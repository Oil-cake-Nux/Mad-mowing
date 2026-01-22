using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Vampire
{
    /// <summary>
    /// 投射物类，处理子弹、箭矢等投射物的发射、移动、碰撞和销毁逻辑
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        [SerializeField] protected SpriteRenderer projectileSpriteRenderer; // 投射物的精灵渲染器（用于显示投射物外观）
        [SerializeField] protected float maxDistance; // 最大飞行距离（超过此距离后自动销毁）
        [SerializeField] protected float rotationSpeed = 0; // 旋转速度（控制投射物飞行时的旋转快慢）
        [SerializeField] protected float airResistance = 0; // 空气阻力（使投射物速度随时间衰减）
        [SerializeField] protected ParticleSystem destructionParticleSystem; // 销毁时的粒子效果（如爆炸、消散特效）
        protected float despawnTime = 1;  // 离屏后自动销毁的时间（单位：秒）
        protected LayerMask targetLayer; // 目标碰撞层（用于区分可碰撞的目标层级）
        protected float speed; // 飞行速度
        protected float damage; // 造成的伤害值
        protected float knockback; // 击退力度（与方向结合使用）
        protected EntityManager entityManager; // 实体管理器（用于投射物的回收与管理）
        protected Character playerCharacter; // 玩家角色引用（用于定位等依赖）
        protected Collider2D col; // 投射物的碰撞体（用于检测碰撞）
        protected ZPositioner zPositioner; // Z轴定位器（控制2D场景中投射物的显示层级）
        protected Coroutine moveCoroutine; // 移动协程（用于控制投射物的持续移动）
        protected int projectileIndex; // 投射物在对象池中的索引（用于回收时定位）
        protected Vector2 direction; // 飞行方向（二维向量）
        protected TrailRenderer trailRenderer = null; // 拖尾渲染器（显示飞行轨迹，如子弹尾迹）
        public UnityEvent<float> OnHitDamageable { get; private set; } // 命中可伤害对象时的事件（参数为伤害值）

        /// <summary>
        /// 组件唤醒时调用，初始化基础组件
        /// </summary>
        protected virtual void Awake()
        {
            col = GetComponent<Collider2D>(); // 获取自身碰撞体组件
            zPositioner = gameObject.AddComponent<ZPositioner>(); // 添加Z轴定位器组件
            TryGetComponent<TrailRenderer>(out trailRenderer); // 尝试获取拖尾渲染器（若存在）
        }

        /// <summary>
        /// 初始化投射物，设置依赖引用
        /// </summary>
        public virtual void Init(EntityManager entityManager, Character playerCharacter)
        {
            this.entityManager = entityManager;
            this.playerCharacter = playerCharacter;
            zPositioner.Init(playerCharacter.transform); // 初始化Z轴定位器（以玩家为参考）
        }

        /// <summary>
        /// 配置投射物的核心参数（发射前调用）
        /// </summary>
        public virtual void Setup(int projectileIndex, Vector2 position, float damage, float knockback, float speed, LayerMask targetLayer)
        {
            transform.position = position; // 设置初始位置
            trailRenderer?.Clear(); // 清空拖尾（避免残留上一次的轨迹）
            this.projectileIndex = projectileIndex;
            this.damage = damage;
            this.knockback = knockback;
            this.speed = speed;
            this.targetLayer = targetLayer;
            col.enabled = true; // 启用碰撞体（准备检测碰撞）
            OnHitDamageable = new UnityEvent<float>(); // 初始化命中事件
        }

        /// <summary>
        /// 发射投射物（设置方向并启动移动逻辑）
        /// </summary>
        public virtual void Launch(Vector2 direction)
        {
            this.direction = direction.normalized; // 归一化方向向量（确保速度均匀）
            moveCoroutine = StartCoroutine(Move()); // 启动移动协程
        }

        /// <summary>
        /// 投射物移动逻辑（协程，每帧更新位置）
        /// </summary>
        public virtual IEnumerator Move()
        {
            float distanceTravelled = 0; // 已飞行距离
            float timeOffScreen = 0; // 离屏时间
            // 循环条件：未超过最大距离、离屏时间未超限、速度未减为0
            while (distanceTravelled < maxDistance && timeOffScreen < despawnTime && speed > 0)
            {
                float step = speed * Time.deltaTime; // 每帧移动距离（速度*时间）
                transform.position += step * (Vector3)direction; // 更新位置
                distanceTravelled += step; // 累计飞行距离
                // 绕自身旋转（Z轴，旋转速度*时间）
                //相对与某点的某轴旋转
                transform.RotateAround(transform.position, Vector3.back, Time.deltaTime * 100 * rotationSpeed);
                // 注释部分：检测是否离屏，离屏则累计时间（实际逻辑暂未启用）
                // if (entityManager.TransformOnScreen(transform, Vector2.one))
                //     timeOffScreen = 0;
                // else
                //     timeOffScreen += Time.deltaTime;
                speed -= airResistance * Time.deltaTime; // 应用空气阻力（减速）
                yield return null; // 等待下一帧
            }
            HitNothing(); // 满足终止条件时，触发"未命中目标"逻辑
        }

        /// <summary>
        /// 命中可伤害对象时的处理
        /// </summary>
        protected virtual void HitDamageable(IDamageable damageable)
        {
            damageable.TakeDamage(damage, knockback * direction); // 调用目标的受伤害方法（传递伤害和击退力）
            //OnDealDamage事件监听的是statsManager.IncreaseDamageDealt事件，触发后会更新总造成伤害值
            OnHitDamageable.Invoke(damage); // 触发命中事件（外部可监听此事件）
            DestroyProjectile(); // 销毁投射物
        }

        /// <summary>
        /// 未命中任何目标时的处理
        /// </summary>
        protected virtual void HitNothing()
        {
            DestroyProjectile(); // 销毁投射物
        }

        /// <summary>
        /// 开始投射物销毁流程
        /// </summary>
        protected virtual void DestroyProjectile()
        {
            StartCoroutine(DestroyProjectileAnimation()); // 启动销毁动画协程
        }

        /// <summary>
        /// 投射物销毁动画（隐藏精灵、播放粒子效果、回收对象）
        /// </summary>
        protected IEnumerator DestroyProjectileAnimation()
        {
            projectileSpriteRenderer.gameObject.SetActive(false); // 隐藏投射物精灵
            destructionParticleSystem.Play(); // 播放销毁粒子效果
            // 等待粒子效果播放完成（根据粒子系统的持续时间）
            yield return new WaitForSeconds(destructionParticleSystem.main.duration);
            projectileSpriteRenderer.gameObject.SetActive(true); // 恢复精灵显示（为下次复用做准备）
            entityManager.DespawnProjectile(projectileIndex, this); // 通知实体管理器回收投射物（对象池机制）
        }

        /// <summary>
        /// 碰撞检测逻辑（判断碰撞对象是否为目标层）
        /// </summary>
        protected void CollisionCheck(Collider2D collider)
        {
            // 检查碰撞对象的层级是否在目标层中
            if ((targetLayer & (1 << collider.gameObject.layer)) != 0)
            {
                col.enabled = false; // 禁用碰撞体（避免重复碰撞）
                StopCoroutine(moveCoroutine); // 停止移动协程
                // 尝试获取碰撞对象的父级中的IDamageable组件
                if (collider.transform.parent.TryGetComponent<IDamageable>(out IDamageable damageable))
                {
                    HitDamageable(collider.gameObject.GetComponentInParent<IDamageable>()); // 命中可伤害对象
                }
                else
                {
                    HitNothing(); // 未命中可伤害对象
                }
            }
        }

        /// <summary>
        /// 2D触发器碰撞回调（进入碰撞时调用）
        /// </summary>
        protected virtual void OnTriggerEnter2D(Collider2D collider)
        {
            CollisionCheck(collider); // 执行碰撞检测
        }

        // 注释部分：2D碰撞器碰撞回调（未启用，可能用于非触发器碰撞）
        // void OnColliderEnter2D(Collider2D collider)
        // {
        //     CollisionCheck(collider);
        // }
    }
}