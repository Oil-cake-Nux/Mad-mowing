using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Vampire
{
    // 怪物类，实现了可受伤害接口和空间哈希网格客户端接口，需要挂载Rigidbody2D组件
    [RequireComponent(typeof(Rigidbody2D))]
    public class Monster : IDamageable, ISpatialHashGridClient
    {
        [SerializeField] protected Material defaultMaterial; // 默认材质
        [SerializeField] protected Material whiteMaterial; // 受击时的白色材质（闪烁效果）
        [SerializeField] protected Material dissolveMaterial; // 溶解材质（暂未使用）
        [SerializeField] protected ParticleSystem deathParticles; // 死亡粒子效果
        [SerializeField] protected GameObject shadow; // 阴影对象
        protected BoxCollider2D monsterHitbox; // 怪物碰撞盒（用于检测攻击）
        protected CircleCollider2D monsterLegsCollider; // 怪物腿部碰撞器（用于空间哈希等）
        protected int monsterIndex; // 怪物在对象池中的索引
        protected MonsterBlueprint monsterBlueprint; // 怪物蓝图（存储配置数据，如血量、移动速度等）
        protected SpriteAnimator monsterSpriteAnimator; // 怪物精灵动画器
        protected SpriteRenderer monsterSpriteRenderer; // 怪物精灵渲染器
        protected ZPositioner zPositioner; // Z轴定位器（控制2D对象的Z轴位置，模拟3D层次感）
        protected float currentHealth;  // 当前血量
        protected EntityManager entityManager;  // 实体管理器（怪物池、生成/销毁实体等）
        protected Character playerCharacter;  // 玩家角色引用
        protected Rigidbody2D rb; // 刚体组件（控制物理运动）
        protected int currWalkSequenceFrame = 0; // 当前行走动画帧索引
        protected bool knockedBack = false; // 是否处于击退状态
        protected Coroutine hitAnimationCoroutine = null; // 受击动画协程
        protected bool alive = true; // 是否存活
        protected Transform centerTransform; // 怪物中心点 Transform（用于定位等）
        public Transform CenterTransform { get => centerTransform; } // 公开获取中心点Transform的属性
        public UnityEvent<Monster> OnKilled { get; } = new UnityEvent<Monster>(); // 怪物被击杀时的事件
        public float HP => currentHealth; // 公开获取当前血量的属性

        // 空间哈希网格客户端接口实现
        public Vector2 Position => transform.position; // 用于空间哈希的位置（当前 Transform 位置）
        public Vector2 Size => monsterLegsCollider.bounds.size; // 用于空间哈希的大小（腿部碰撞器范围）
        public Dictionary<int, int> ListIndexByCellIndex { get; set; } // 空间哈希中单元格索引与列表索引的映射
        public int QueryID { get; set; } = -1; // 空间哈希查询ID

        // 唤醒时初始化组件
        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody2D>(); // 获取刚体组件
            monsterLegsCollider = GetComponent<CircleCollider2D>(); // 获取腿部碰撞器
            monsterSpriteAnimator = GetComponentInChildren<SpriteAnimator>(); // 获取子对象中的精灵动画器
            monsterSpriteRenderer = GetComponentInChildren<SpriteRenderer>(); // 获取子对象中的精灵渲染器
            zPositioner = gameObject.AddComponent<ZPositioner>(); // 添加Z轴定位器组件
            monsterHitbox = monsterSpriteRenderer.gameObject.AddComponent<BoxCollider2D>(); // 给精灵添加碰撞盒作为攻击检测用
            monsterHitbox.isTrigger = true; // 设置为触发器（不产生物理碰撞，仅用于检测）
        }

        // 初始化怪物（设置实体管理器和玩家引用）
        public virtual void Init(EntityManager entityManager, Character playerCharacter)
        {
            this.entityManager = entityManager;
            this.playerCharacter = playerCharacter;
            zPositioner.Init(playerCharacter.transform); // 初始化Z轴定位器（参考玩家位置）
        }

        // 设置怪物初始状态
        public virtual void Setup(int monsterIndex, Vector2 position, MonsterBlueprint monsterBlueprint, float hpBuff = 0)
        {
            this.monsterIndex = monsterIndex; // 设置对象池索引
            this.monsterBlueprint = monsterBlueprint; // 设置怪物蓝图
            rb.position = position; // 设置刚体位置
            transform.position = position; // 设置Transform位置
            currentHealth = monsterBlueprint.hp + hpBuff; // 初始化血量（基础血量+ buff）
            alive = true; // 标记为存活
            entityManager.LivingMonsters.Add(this); // 添加到存活怪物列表
            monsterSpriteAnimator.Init(monsterBlueprint.walkSpriteSequence, monsterBlueprint.walkFrameTime, true); // 初始化行走动画
            monsterSpriteAnimator.StartAnimating(true); // 开始播放动画
            monsterHitbox.enabled = true; // 启用碰撞盒
            monsterHitbox.size = monsterSpriteRenderer.bounds.size; // 设置碰撞盒大小为精灵大小
            monsterHitbox.offset = Vector2.up * monsterHitbox.size.y / 2; // 调整碰撞盒偏移（向上居中）
            monsterLegsCollider.radius = monsterHitbox.size.x / 2.5f; // 设置腿部碰撞器半径
            Transform centerTransform1 = transform.Find("Center Transform");
            if (centerTransform1 == null)
                centerTransform = (new GameObject("Center Transform")).transform; // 创建中心点对象
            else
                centerTransform = centerTransform1;
            centerTransform.SetParent(transform); // 设置中心点对象的父节点为怪物自身
            centerTransform.position = transform.position + (Vector3)monsterHitbox.offset; // 设置中心点位置（基于碰撞盒偏移）
            float spd = Random.Range(monsterBlueprint.movespeed - 0.1f, monsterBlueprint.movespeed + 0.1f); // 随机化移动速度（在基础速度±0.1范围内）
            rb.drag = monsterBlueprint.acceleration / (spd * spd); //根据加速度和速度计算阻力
            rb.velocity = Vector2.zero; //重置速度
            StopAllCoroutines(); //停止所有协程（避免残留动画）
        }

        /// <summary>
        /// 每帧更新（处理朝向），使精灵翻转
        /// </summary>
        protected virtual void Update()
        {
            // 根据玩家位置设置精灵翻转（面向玩家）
            monsterSpriteRenderer.flipX = ((playerCharacter.transform.position.x - rb.position.x) < 0);
        }

        // 物理帧更新（可重写用于实现移动逻辑）
        protected virtual void FixedUpdate()
        {

        }

        /// <summary>
        /// 实现IDamageable接口：处理击退
        /// </summary>
        //有两种击退情况，一种是收到攻击，一种是与玩家产生碰撞
        //只有在受到攻击时会边击退边播放受击动画
        public override void Knockback(Vector2 knockback)
        {
            rb.velocity += knockback * Mathf.Sqrt(rb.drag); // 应用击退力（受阻力影响）
        }

        /// <summary>
        /// 实现IDamageable接口：处理受伤
        /// </summary>
        
        public override void TakeDamage(float damage, Vector2 knockback = default(Vector2))
        {
            if (alive) // 仅当存活时处理伤害
            {
                entityManager.SpawnDamageText(monsterHitbox.transform.position, damage); // 生成伤害文本
                currentHealth -= damage; // 扣血
                if (hitAnimationCoroutine != null) StopCoroutine(hitAnimationCoroutine); // 停止正在播放的受击动画
                if (knockback != default(Vector2)) // 如果有击退
                {
                    rb.velocity += knockback * Mathf.Sqrt(rb.drag); // 应用击退力
                    knockedBack = true; // 标记为击退状态
                }
                if (currentHealth > 0) // 未死亡则播放受击动画
                    hitAnimationCoroutine = StartCoroutine(HitAnimation());
                else // 死亡则触发死亡逻辑
                    StartCoroutine(Killed());
            }
        }

        /// <summary>
        /// 受击动画协程（材质闪烁效果）
        /// </summary>
        protected IEnumerator HitAnimation()
        {
            monsterSpriteRenderer.sharedMaterial = whiteMaterial; // 切换为白色材质
            yield return new WaitForSeconds(0.15f); // 等待0.15秒
            monsterSpriteRenderer.sharedMaterial = defaultMaterial; // 恢复默认材质
            knockedBack = false; // 取消击退状态
        }

        /// <summary>
        /// 死亡处理协程
        /// </summary>
        public virtual IEnumerator Killed(bool killedByPlayer = true)
        {
            alive = false; // 标记为死亡
            monsterHitbox.enabled = false; // 禁用碰撞盒
            entityManager.LivingMonsters.Remove(this); // 从存活列表移除

            // 如果被玩家杀死，掉落战利品
            if (killedByPlayer)
                DropLoot();

            // 播放死亡粒子效果
            if (deathParticles != null)
            {
                deathParticles.Play();
            }

            yield return HitAnimation(); // 播放受击动画（最后一次闪烁）

            // 处理粒子效果显示逻辑
            if (deathParticles != null)
            {
                monsterSpriteRenderer.enabled = false; // 隐藏精灵
                shadow.SetActive(false); // 隐藏阴影
                yield return new WaitForSeconds(deathParticles.main.duration - 0.15f); // 等待粒子播放结束
                monsterSpriteRenderer.enabled = true; // 恢复精灵显示（为下次复用准备）
                shadow.SetActive(true); // 恢复阴影显示
            }

            // 触发死亡事件并清空监听者
            OnKilled.Invoke(this);
            OnKilled.RemoveAllListeners();
            // 调用实体管理器销毁怪物（回收到对象池）
            entityManager.DespawnMonster(monsterIndex, this, true);
        }

        /// <summary>
        /// 掉落战利品
        /// </summary>
        protected virtual void DropLoot()
        {
            // 尝试从宝石战利品表掉落宝石
            if (monsterBlueprint.gemLootTable.TryDropLoot(out GemType gemType))
                entityManager.SpawnExpGem((Vector2)transform.position, gemType);
            // 尝试从金币战利品表掉落金币
            if (monsterBlueprint.coinLootTable.TryDropLoot(out CoinType coinType))
                entityManager.SpawnCoin((Vector2)transform.position, coinType);
        }
    }
}