using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;

namespace Vampire
{
    /// <summary>
    /// 角色类，继承可受伤害接口和空间哈希网格客户端接口，实现角色核心逻辑
    /// </summary>
    public class Character : IDamageable, ISpatialHashGridClient
    {
        [Header("依赖组件")]
        [SerializeField] protected Transform centerTransform; // 角色中心点 Transform
        [SerializeField] protected Transform lookIndicator; // 朝向指示器
        [SerializeField] protected float lookIndicatorRadius; // 朝向指示器距离
        [SerializeField] protected TextMeshProUGUI levelText; // 等级文本
        [SerializeField] protected AbilitySelectionDialog abilitySelectionDialog; // 能力选择对话框
        [SerializeField] protected PointBar healthBar;  // 血量条
        [SerializeField] protected PointBar expBar;  // 经验条
        [SerializeField] protected Collider2D collectableCollider; // 收集物碰撞体
        [SerializeField] protected Collider2D meleeHitboxCollider; // 近战攻击碰撞体
        [SerializeField] protected ParticleSystem dustParticles; // 灰尘粒子效果
        [SerializeField] protected Material defaultMaterial, hitMaterial, deathMaterial; // 材质（默认、受击、死亡）
        [SerializeField] protected ParticleSystem deathParticles; // 死亡粒子效果

        protected CharacterBlueprint characterBlueprint; // 角色蓝图（存储初始属性）
        protected UpgradeableMovementSpeed movementSpeed; // 可升级的移动速度
        protected UpgradeableArmor armor; // 可升级的护甲
        protected bool alive = true; // 是否存活
        protected int currentLevel = 1; // 当前等级
        protected float currentExp = 0; // 当前经验
        protected float nextLevelExp = 5; // 下一级所需经验
        protected float expToNextLevel = 5; // 升级所需经验的增量
        protected float currentHealth; // 当前生命值
        protected SpriteRenderer spriteRenderer; // 精灵渲染器
        protected SpriteAnimator spriteAnimator; // 精灵动画器
        protected AbilityManager abilityManager; // 能力管理器
        protected EntityManager entityManager; // 实体管理器
        protected StatsManager statsManager; // 统计管理器
        protected Rigidbody2D rb; // 刚体组件
        protected ZPositioner zPositioner; // Z轴定位器（控制渲染层级）
        protected Vector2 lookDirection = Vector2.right; // 朝向方向
        protected CoroutineQueue coroutineQueue; // 协程队列（有序执行异步逻辑）
        protected Coroutine hitAnimationCoroutine = null; // 受击动画协程
        protected Vector2 moveDirection; // 移动方向

        /// <summary>角色朝向（设置时忽略零向量）</summary>
        public Vector2 LookDirection
        {
            get { return lookDirection; }
            set
            {
                if (value != Vector2.zero)
                    lookDirection = value;
            }
        }

        public Transform CenterTransform { get => centerTransform; }
        public Collider2D CollectableCollider { get => collectableCollider; }
        public float Luck { get => characterBlueprint.luck; } // 幸运值（来自蓝图）
        public int CurrentLevel { get => currentLevel; }
        /// <summary>造成伤害时触发的事件（参数：伤害值）</summary>
        public UnityEvent<float> OnDealDamage { get; } = new UnityEvent<float>();
        /// <summary>角色死亡时触发的事件</summary>
        public UnityEvent OnDeath { get; } = new UnityEvent();
        public CharacterBlueprint Blueprint { get => characterBlueprint; }
        public Vector2 Velocity { get => rb.velocity; }

        // 实现ISpatialHashGridClient接口
        public Vector2 Position => transform.position; // 角色位置（用于网格定位）
        public Vector2 Size => meleeHitboxCollider.bounds.size; // 碰撞体大小（用于网格范围计算）
        public Dictionary<int, int> ListIndexByCellIndex { get; set; } // 网格索引记录
        public int QueryID { get; set; } = -1; // 查询标识

        /// <summary>
        /// 唤醒时初始化基础组件
        /// </summary>
        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            zPositioner = gameObject.AddComponent<ZPositioner>();
            spriteAnimator = GetComponentInChildren<SpriteAnimator>();
            spriteRenderer = spriteAnimator.GetComponent<SpriteRenderer>();
            characterBlueprint = CrossSceneData.CharacterBlueprint; // 从跨场景数据获取角色蓝图
        }

        /// <summary>
        /// 初始化角色（外部调用）
        /// </summary>
        public virtual void Init(EntityManager entityManager, AbilityManager abilityManager, StatsManager statsManager)
        {
            this.entityManager = entityManager;
            this.abilityManager = abilityManager;
            this.statsManager = statsManager;
            // 注册事件：造成伤害时更新统计
            OnDealDamage.AddListener(statsManager.IncreaseDamageDealt);
            // 初始化协程队列
            coroutineQueue = new CoroutineQueue(this);
            coroutineQueue.StartLoop();
            // 初始化生命值和经验条
            currentHealth = characterBlueprint.hp;
            //三个参数分别为当前值，最小值，最大值
            healthBar.Setup(currentHealth, 0, characterBlueprint.hp);
            expBar.Setup(currentExp, 0, nextLevelExp);
            currentLevel = 1;
            UpdateLevelDisplay();
            // 初始化动画
            spriteAnimator.Init(characterBlueprint.walkSpriteSequence, characterBlueprint.walkFrameTime, false);
            // 初始化可升级移动速度
            movementSpeed = new UpgradeableMovementSpeed();
            movementSpeed.Value = characterBlueprint.movespeed;
            abilityManager.RegisterUpgradeableValue(movementSpeed, true);
            UpdateMoveSpeed();
            // 初始化可升级护甲
            armor = new UpgradeableArmor();
            armor.Value = characterBlueprint.armor;
            abilityManager.RegisterUpgradeableValue(armor, true);
            zPositioner.Init(transform);
        }

        /// <summary>
        /// 每帧更新视觉相关逻辑
        /// </summary>
        protected virtual void Update()
        {
            //角色怪物左右移动都会翻转角色贴图
            // 更新朝向指示器位置
            lookIndicator.transform.localPosition = lookDirection * lookIndicatorRadius;
            // 根据朝向翻转精灵
            spriteRenderer.flipX = lookDirection.x < 0;
        }

        /// <summary>
        /// 物理帧更新移动逻辑
        /// </summary>
        protected virtual void FixedUpdate()
        {
            // 根据移动方向更新朝向
            if (moveDirection != Vector2.zero)
                lookDirection = moveDirection;
            else
                StopWalkAnimation(); // 停止移动时停止动画
            // 存活时应用移动加速度
            if (alive)
                rb.velocity += moveDirection * characterBlueprint.acceleration * Time.deltaTime;
        }

        /// <summary>
        /// 获得经验值（加入协程队列执行）
        /// </summary>
        public void GainExp(float exp)
        {
            if (alive)
                //协程队列（CoroutineQueue）的作用：管理协程的执行顺序，让多个协程按加入队列的顺序依次执行（前一个协程完成后，再执行下一个），避免并发冲突。
                //GainExpCoroutine（处理经验获取和升级的协程）会被加入队列，等待队列中前面的协程执行完毕后再运行。
                coroutineQueue.EnqueueCoroutine(GainExpCoroutine(exp));
        }

        /// <summary>
        /// 处理经验值获取的协程（支持多级升级）
        /// </summary>
        private IEnumerator GainExpCoroutine(float exp)
        {
            if (alive)
            {
                // 循环升级是为了处理一次获得的经验值足够提升多个等级的场景
                while (currentExp + exp >= nextLevelExp)
                {
                    float expDiff = nextLevelExp - currentExp; // 升级所需的剩余经验
                    currentExp += expDiff;
                    exp -= expDiff;
                    expBar.Setup(currentExp, 0, nextLevelExp); // 临时显示为满
                    // 等待升级完成
                    yield return LevelUpCoroutine();
                    // 更新下一级所需经验
                    float prevLevelExp = nextLevelExp;
                    //expToNextLevel的默认值为5，每次增加10、13、16等等
                    expToNextLevel += characterBlueprint.LevelToExpIncrease(currentLevel);
                    nextLevelExp += expToNextLevel;
                    expBar.Setup(currentExp, prevLevelExp, nextLevelExp);
                }
                // 经验值会一直积累，不会每次升级而清零，添加剩余经验
                currentExp += exp;
                expBar.AddPoints(exp);
            }
        }

        /// <summary>
        /// 处理升级逻辑的协程
        /// </summary>
        private IEnumerator LevelUpCoroutine()
        {
            if (alive)
            {
                currentLevel++; // 提升等级
                UpdateLevelDisplay(); // 更新等级显示
                // 打开能力选择对话框，执行后会将MenuOpen = true
                abilitySelectionDialog.Open();
                // 等待对话框关闭，关闭对话框后会设置MenuOpen = false
                while (abilitySelectionDialog.MenuOpen)
                {
                    yield return null;
                }
            }
        }

        /// <summary>
        /// 更新等级文本显示
        /// </summary>
        private void UpdateLevelDisplay()
        {
            levelText.text = "LV " + currentLevel;
        }

        /// <summary>
        /// 实现击退逻辑（IDamageable接口）
        /// </summary>
        public override void Knockback(Vector2 knockback)
        {
            //rb.drag为阻力，他的存在是避免击退效果过于强烈（否则角色可能飞出去太远）
            rb.velocity += knockback * Mathf.Sqrt(rb.drag);
        }

        /// <summary>
        /// 实现受伤害逻辑（IDamageable接口），输入伤害值和击退方向向量，来进行扣血、更新血条和应用击退
        /// 并播放受击动画，判断是否死亡
        /// </summary>
        public override void TakeDamage(float damage, Vector2 knockback = default(Vector2))
        {
            if (alive)
            {
                // 应用护甲减免（最低1点伤害）
                if (armor.Value >= damage)
                    damage = damage < 1 ? damage : 1;
                else
                    damage -= armor.Value;
                // 扣除生命值并更新血条
                healthBar.SubtractPoints(damage);
                currentHealth -= damage;
                // 应用击退
                rb.velocity += knockback * Mathf.Sqrt(rb.drag);
                // 更新受到的伤害统计
                statsManager.IncreaseDamageTaken(damage);
                // 判断是否死亡
                if (currentHealth <= 0)
                {
                    StartCoroutine(DeathAnimation());
                }
                else
                {
                    // 播放受击动画
                    //这样写的原因是有一种连续受击的表现效果
                    if (hitAnimationCoroutine != null) StopCoroutine(hitAnimationCoroutine);
                    hitAnimationCoroutine = StartCoroutine(HitAnimation());
                }
            }
        }

        /// <summary>
        /// 受击动画协程（临时切换材质）
        /// </summary>
        private IEnumerator HitAnimation()
        {
            spriteRenderer.sharedMaterial = hitMaterial;
            yield return new WaitForSeconds(0.15f);
            spriteRenderer.sharedMaterial = defaultMaterial;
        }

        /// <summary>
        /// 死亡动画协程
        /// 
        /// </summary>
        private IEnumerator DeathAnimation()
        {
            alive = false;
            spriteRenderer.sharedMaterial = deathMaterial;

            // 销毁激活的能力
            abilityManager.DestroyActiveAbilities();
            StopWalkAnimation();
            deathParticles.Play();

            float height = spriteRenderer.bounds.size.y;
            float t = 0;
            // 播放死亡溶解动画
            while (t < 1)
            {
                spriteRenderer.sharedMaterial = deathMaterial;
                deathParticles.transform.position = transform.position + Vector3.up * height * (1 - t);
                deathMaterial.SetFloat("_Wipe", t); // 控制溶解效果
                t += Time.deltaTime;
                yield return null;
            }
            deathMaterial.SetFloat("_Wipe", 1.0f);

            yield return new WaitForSeconds(0.5f);

            // 触发死亡事件并隐藏精灵
            OnDeath.Invoke();
            spriteRenderer.enabled = false;
        }

        /// <summary>
        /// 恢复生命值
        /// </summary>
        public void GainHealth(float health)
        {
            healthBar.AddPoints(health);
            currentHealth += health;
            // 限制生命值不超过最大值
            if (currentHealth > characterBlueprint.hp)
                currentHealth = characterBlueprint.hp;
        }

        /// <summary>
        /// 通过输入设置朝向（InputSystem回调）
        /// </summary>
        public void SetLookDirecton(InputAction.CallbackContext context)
        {
            //LookDirection属性修改lookDirection值
            LookDirection = context.ReadValue<Vector2>();
        }

        /// <summary>
        /// 更新移动速度（通过刚体阻力控制最大速度）
        /// </summary>
        public void UpdateMoveSpeed()
        {
            rb.drag = characterBlueprint.acceleration / (movementSpeed.Value * movementSpeed.Value);
        }

        /// <summary>
        /// 设置移动方向
        /// </summary>
        public void Move(Vector2 moveDirection)
        {
            this.moveDirection = moveDirection;
        }

        /// <summary>
        /// 开始行走动画
        /// </summary>
        public void StartWalkAnimation()
        {
            if (alive)
                spriteAnimator.StartAnimating();
        }

        /// <summary>
        /// 停止行走动画
        /// </summary>
        public void StopWalkAnimation()
        {
            spriteAnimator.StopAnimating(true);
        }

        /// <summary>
        /// 通过输入设置移动方向（InputSystem回调）
        /// </summary>
        public void SetMoveDirection(InputAction.CallbackContext context)
        {
            moveDirection = context.action.ReadValue<Vector2>().normalized;
        }
    }
}