using System.Collections;
using UnityEngine;

namespace Vampire
{
    // 大蒜能力：一种持续伤害能力，在一定范围内对怪物造成持续伤害和击退效果
    public class GarlicAbility : Ability
    {
        [Header("大蒜能力属性")]
        [SerializeField] protected LayerMask monsterLayer; // 怪物所在的图层（用于检测碰撞）
        [SerializeField] protected UpgradeableDamage damage; // 可升级的伤害值
        [SerializeField] protected UpgradeableAOE radius; // 可升级的作用范围
        [SerializeField] protected UpgradeableDamageRate damageRate; // 可升级的伤害频率（每秒伤害次数）
        [SerializeField] protected UpgradeableKnockback knockback; // 可升级的击退力度
        private float timeSinceLastAttack; // 记录上次攻击到现在的时间
        private FastList<GameObject> hitMonsters; // 记录已击中的怪物（用于触发检测）
        private CircleCollider2D damageCollider; // 伤害范围碰撞体
        private SpriteRenderer spriteRenderer; // 大蒜效果的精灵渲染器

        void Awake()
        {
            // 初始化组件：获取碰撞体和子物体的精灵渲染器
            damageCollider = GetComponent<CircleCollider2D>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        public override void Init(AbilityManager abilityManager, EntityManager entityManager, Character playerCharacter)
        {
            // 调用父类初始化方法
            base.Init(abilityManager, entityManager, playerCharacter);
            // 设置大蒜能力的父物体为玩家，位置设为玩家中心
            transform.SetParent(playerCharacter.transform);
            transform.localPosition = Vector3.zero;
        }

        protected override void Use()
        {
            // 调用父类使用方法
            base.Use();
            // 激活物体，初始化已击中怪物列表，设置碰撞体范围和精灵大小
            gameObject.SetActive(true);
            hitMonsters = new FastList<GameObject>();
            damageCollider.radius = radius.Value;
            spriteRenderer.transform.localScale = Vector3.one * radius.Value * 2;
        }

        protected override void Upgrade()
        {
            // 调用父类升级方法
            base.Upgrade();
            // 升级后更新碰撞体范围和精灵大小（随范围升级变化）
            damageCollider.radius = radius.Value;
            spriteRenderer.transform.localScale = Vector3.one * radius.Value * 2;
        }

        void Update()
        {
            // 累加时间，当达到伤害间隔时触发范围伤害
            timeSinceLastAttack += Time.deltaTime;
            if (timeSinceLastAttack >= 1 / damageRate.Value) // 1/频率 = 每次伤害间隔时间
            {
                // 检测范围内所有怪物碰撞体
                Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, radius.Value, monsterLayer);
                // 对每个碰撞体对应的怪物造成伤害
                foreach (Collider2D collider in hitColliders)
                {
                    Damage(collider.GetComponentInParent<IDamageable>());
                }
                // 重置时间（保留余数，避免时间累积误差）
                timeSinceLastAttack = Mathf.Repeat(timeSinceLastAttack, 1 / damageRate.Value);
            }
        }

        private void Damage(IDamageable damageable)
        {
            // 计算击退方向（从大蒜中心指向怪物）
            Vector2 knockbackDirection = (damageable.transform.position - transform.position).normalized;
            // 调用怪物的受伤害方法（传入伤害值和击退力度+方向）
            damageable.TakeDamage(damage.Value, knockback.Value * knockbackDirection);
            // 触发玩家的"造成伤害"事件
            playerCharacter.OnDealDamage.Invoke(damage.Value);
        }

        private void DeregisterMonster(Monster monster)
        {
            // 从已击中列表中移除被杀死的怪物
            hitMonsters.Remove(monster.gameObject);
        }

        void OnTriggerEnter2D(Collider2D collider)
        {
            // 当怪物进入碰撞范围且未被记录时，添加到列表并造成一次伤害
            if (!hitMonsters.Contains(collider.gameObject) && (monsterLayer & (1 << collider.gameObject.layer)) != 0)
            {
                hitMonsters.Add(collider.gameObject);
                Monster monster = collider.gameObject.GetComponentInParent<Monster>();
                // 监听怪物死亡事件，死亡后从列表移除
                monster.OnKilled.AddListener(DeregisterMonster);
                // 对进入的怪物造成伤害
                Damage(monster);
            }
        }
    }
}