using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Vampire
{
    /// <summary>
    /// 可投掷物体基类，处理投掷物的飞行、弹跳、旋转、销毁等逻辑
    /// </summary>
    public class Throwable : MonoBehaviour
    {
        [SerializeField] protected SpriteRenderer throwableSpriteRenderer; // 投掷物本身的精灵渲染器
        [SerializeField] protected SpriteRenderer shadowSpriteRenderer; // 投掷物阴影的精灵渲染器
        [SerializeField] protected float maxDistance; // 最大投掷距离
        [SerializeField] protected int maxBounceCount = 4; // 最大弹跳次数
        [SerializeField] protected float throwHeight = 1; // 投掷时的初始高度（影响抛物线）
        [SerializeField] protected float bounciness = 0.5f; // 弹跳弹性系数（值越小弹跳衰减越快）
        [SerializeField] protected float delayTime = 1; // 投掷物落地/停止弹跳后到爆炸的延迟时间
        [SerializeField] protected float initialRotationSpeed = 2; // 初始旋转速度
        protected float rotationSpeed; // 当前旋转速度（会随弹跳衰减）
        protected LayerMask targetLayer; // 可命中的目标图层
        protected float damage; // 投掷物造成的伤害
        protected float knockback; // 投掷物造成的击退力
        protected EntityManager entityManager; // 实体管理器（用于回收投掷物）
        protected Character playerCharacter; // 玩家角色引用
        protected Collider2D col; // 投掷物的碰撞体
        protected ZPositioner zPositioner; // Z轴定位器（控制渲染层级）
        protected int throwableIndex; // 投掷物在对象池中的索引
        protected float throwTime; // 投掷物从抛出到爆炸的总时间
        public UnityEvent<float> OnHitDamageable { get; private set; } // 命中可伤害目标时的事件（参数为伤害值）
        public float ThrowTime { get => throwTime; } // 投掷总时间的 getter
        public float Range => maxDistance; // 最大投掷距离的 getter

        /// <summary>
        /// 唤醒时初始化组件
        /// </summary>
        protected virtual void Awake()
        {
            col = GetComponent<Collider2D>(); // 获取碰撞体组件
            zPositioner = gameObject.AddComponent<ZPositioner>(); // 添加Z轴定位器组件
        }

        /// <summary>
        /// 初始化投掷物的核心引用
        /// </summary>
        /// <param name="entityManager">实体管理器</param>
        /// <param name="playerCharacter">玩家角色</param>
        public virtual void Init(EntityManager entityManager, Character playerCharacter)
        {
            this.entityManager = entityManager;
            this.playerCharacter = playerCharacter;
            zPositioner.Init(playerCharacter.transform); // 初始化Z轴定位器（以玩家为参考）
        }

        /// <summary>
        /// 设置投掷物的基础参数
        /// </summary>
        /// <param name="throwableIndex">该种类型的投掷物在对象池列表中的索引</param>
        /// <param name="position">初始位置</param>
        /// <param name="damage">伤害值</param>
        /// <param name="knockback">击退力</param>
        /// <param name="timeInAir">空中飞行时间（未使用，预留）</param>
        /// <param name="targetLayer">可命中的目标图层</param>
        public virtual void Setup(int throwableIndex, Vector2 position, float damage, float knockback, float timeInAir, LayerMask targetLayer)
        {
            transform.position = position; // 设置初始位置
            this.throwableIndex = throwableIndex;
            this.damage = damage;
            this.knockback = knockback;
            this.targetLayer = targetLayer;
            col.enabled = true; // 启用碰撞体
            OnHitDamageable = new UnityEvent<float>(); // 初始化命中事件
            throwTime = ComputeThrowTime(); // 计算投掷总时间
        }

        /// <summary>
        /// 执行投掷逻辑
        /// </summary>
        /// <param name="toPosition">投掷目标位置</param>
        public virtual void Throw(Vector2 toPosition)
        {
            Vector2 direction = (toPosition - (Vector2)transform.position); // 计算投掷方向（从当前位置到目标位置）
            rotationSpeed = (direction.x > 0) ? initialRotationSpeed : -initialRotationSpeed; // 根据X方向确定旋转方向
            float throwDistance = direction.magnitude; // 计算原始投掷距离
            if (throwDistance > maxDistance) throwDistance = maxDistance; // 限制最大距离
            direction.Normalize(); // 归一化方向向量（仅保留方向）
            StartCoroutine(ThrowRoutine(direction, throwDistance)); // 启动投掷协程
        }

        /// <summary>
        /// 投掷物飞行与弹跳的协程（核心运动逻辑）
        /// </summary>
        /// <param name="direction">投掷方向</param>
        /// <param name="throwDistance">投掷距离</param>
        //总之就是实时计算出投掷物在画面中的速度linearSpeed，使用该速度改变投掷物的位置并更新运动总距离，当达到最大距离时会落地，播放动画
        public virtual IEnumerator ThrowRoutine(Vector2 direction, float throwDistance)
        {
            float distance = 0; // 当前已飞行距离
            int bounceCount = 0; // 当前弹跳次数
            Vector3 initialPosition = transform.position; // 初始位置记录
            // 初始化Y方向速度（基于投掷高度计算，模拟上抛初速度）
            float vy = Mathf.Sqrt(2 * PhysicsConstants.g * throwHeight);
            // 计算初始线速度（确保能在弹跳后到达目标距离）
            float initialLinearVelocity = InitialVelocityForThrowDistance(throwDistance, vy);
            float linearSpeed = initialLinearVelocity; // 当前线速度（随弹跳衰减）
            float y = 0; // Y方向偏移量（模拟高度）

            // 第一阶段：飞行与弹跳（直到达到最大距离、速度为0或超过最大弹跳次数）
            while (distance < throwDistance && linearSpeed > 0)
            {
                // 计算X/Z方向的线性位置（基于已飞行距离）
                Vector3 linearPosition = initialPosition + distance * (Vector3)direction;

                // 模拟Y方向重力与弹跳
                vy -= PhysicsConstants.g * Time.deltaTime; // 重力加速度影响Y方向速度
                y += vy * Time.deltaTime; // 更新Y方向偏移量
                if (y < 0) // 接触地面（Y偏移量小于0）
                {
                    y = 0; // 重置Y偏移（落地）
                    vy = -vy * bounciness; // 反弹（速度反向并按弹性系数衰减）
                    bounceCount++; // 弹跳次数+1
                    // 线速度随弹跳次数衰减（最后一次弹跳后速度接近0）
                    linearSpeed = (initialLinearVelocity * (maxBounceCount - bounceCount)) / maxBounceCount;
                    rotationSpeed *= bounciness; // 旋转速度随弹跳衰减
                }

                // 更新实际位置（线性位置 + Y方向偏移）
                transform.position = linearPosition + Vector3.up * y;
                zPositioner.ManuallySetZByY(linearPosition.y); // 根据Y值手动设置Z轴（控制渲染层级）

                // 若超过最大弹跳次数则终止循环
                if (bounceCount >= maxBounceCount)
                    break;

                // 旋转投掷物精灵
                throwableSpriteRenderer.transform.RotateAround(
                    throwableSpriteRenderer.transform.position,
                    Vector3.back,
                    Time.deltaTime * 100 * rotationSpeed
                );

                // 更新阴影位置（始终在地面）
                shadowSpriteRenderer.transform.position = linearPosition;
                shadowSpriteRenderer.transform.rotation = Quaternion.identity; // 阴影不旋转

                distance += linearSpeed * Time.deltaTime; // 累积已飞行距离
                yield return null; // 等待下一帧
            }

            // 第二阶段：延迟阶段（落地/停止弹跳后，等待延迟时间再爆炸）
            zPositioner.AutomaticallySetZ(); // 恢复自动Z轴设置
            float t = 0; // 延迟计时器
            // 初始滚动速度（基于初始速度和弹跳次数计算）
            float rollingSpeed = 1.0f / maxBounceCount * initialLinearVelocity;
            while (t < delayTime)
            {
                // 计算当前线性位置
                Vector3 linearPosition = initialPosition + distance * (Vector3)direction;
                transform.position = linearPosition; // 固定在地面
                shadowSpriteRenderer.transform.position = linearPosition; // 阴影同步位置
                shadowSpriteRenderer.transform.rotation = Quaternion.identity;

                t += Time.deltaTime; // 累积延迟时间
                distance += rollingSpeed * Time.deltaTime; // 继续缓慢移动（模拟滚动衰减）
                // 继续旋转（速度逐渐减慢）
                throwableSpriteRenderer.transform.RotateAround(
                    throwableSpriteRenderer.transform.position,
                    Vector3.back,
                    Time.deltaTime * 100 * rotationSpeed
                );
                rollingSpeed *= 0.95f; // 滚动速度衰减
                rotationSpeed *= 0.99f; // 旋转速度衰减
                yield return null; // 等待下一帧
            }

            Explode(); // 延迟结束后爆炸
        }

        /// <summary>
        /// 计算初始线速度（确保投掷物能在弹跳后到达目标距离）
        /// </summary>
        /// <param name="throwDistance">目标投掷距离</param>
        /// <param name="vy">Y方向初始速度</param>
        /// <returns>计算得到的初始线速度</returns>
        private float InitialVelocityForThrowDistance(float throwDistance, float vy)
        {
            // 计算调和序列（用于模拟弹跳过程中的速度衰减总和）
            float H = 1;
            for (int i = 1; i <= maxBounceCount; i++)
            {
                H += Mathf.Pow(bounciness, i) * (1 - ((float)i) / maxBounceCount);
            }
            // 根据重力、目标距离、Y方向速度和调和序列计算初始线速度
            return PhysicsConstants.g * throwDistance / (2 * vy * H);
        }

        /// <summary>
        /// 计算投掷物从抛出到爆炸的总时间
        /// </summary>
        /// <returns>总投掷时间</returns>
        protected float ComputeThrowTime()
        {
            float vy = Mathf.Sqrt(2 * PhysicsConstants.g * throwHeight); // Y方向初始速度
            float t = 0;
            // 累加每次弹跳的时间（考虑弹性衰减）
            for (int i = 1; i <= maxBounceCount; i++)
            {
                t += Mathf.Pow(bounciness, i) * 2 * vy / PhysicsConstants.g;
            }
            return t;
        }

        /// <summary>
        /// 爆炸逻辑（可重写以实现具体效果）
        /// </summary>
        protected virtual void Explode()
        {
            DestroyThrowable(); // 销毁投掷物
        }

        /// <summary>
        /// 销毁投掷物（启动销毁动画）
        /// </summary>
        protected virtual void DestroyThrowable()
        {
            StartCoroutine(DestroyThrowableAnimation());
        }

        /// <summary>
        /// 投掷物销毁动画协程（目前仅做简单隐藏和回收）
        /// </summary>
        protected IEnumerator DestroyThrowableAnimation()
        {
            throwableSpriteRenderer.enabled = false; // 隐藏投掷物精灵
            // destructionParticleSystem.Play(); // （预留）播放销毁粒子效果
            yield return new WaitForSeconds(0.0f); // 等待（可调整为粒子效果持续时间）
            throwableSpriteRenderer.enabled = true; // 恢复显示（为下次复用准备）
            entityManager.DespawnThrowable(throwableIndex, this); // 通知实体管理器回收投掷物
        }
    }
}