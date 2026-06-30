using System.Collections;
using System.Collections.Generic;
using System.Linq;
//using UnityEditor.Localization.Plugins.XLIFF.V12;
using UnityEngine;
using UnityEngine.Pool;

namespace Vampire
{
    /// <summary>
    /// 实体管理器（EntityManager）
    /// 负责：怪物、经验宝石、金币、宝箱、伤害飘字、投射物/投掷物/回旋镖等“实体”的生成与回收（对象池），
    /// 以及空间网格（SpatialHashGrid）的维护，用于高效查找附近目标。
    /// </summary>
    public class EntityManager : MonoBehaviour
    {
        [Header("Monster Spawning Settings")]
        [SerializeField] private float monsterSpawnBufferDistance;  // 屏幕可视范围之外的额外缓冲距离：怪物会刷在“屏幕外圈+缓冲”位置
        [SerializeField] private float playerDirectionSpawnWeight;  // 刷怪时“偏向玩家移动方向”的权重系数（越大越倾向从玩家前进方向刷怪）

        [Header("Chest Spawning Settings")]
        [SerializeField] private float chestSpawnRange = 5;        // 宝箱刷新的额外随机范围（在基础刷点上再随机偏移）

        [Header("Object Pool Settings")]
        [SerializeField] private GameObject monsterPoolParent;      // 作为 MonsterPool 组件挂载的父对象（层级管理用）
        //怪物种类已经确定，所以使用数组
        private MonsterPool[] monsterPools;                         // 怪物对象池数组：通常每个怪物池对应一个 prefab 集合；最后一个通常留给 Boss

        //投射物有多个对象池，每个对象池对应一类物品，因此使用对象池列表来存储有类似功能的物品
        //并使用字典来记录对象池列表中的索引
        [SerializeField] private GameObject projectilePoolParent;   // ProjectilePool 挂载的父对象
        private List<ProjectilePool> projectilePools;               // 投射物对象池列表（可动态增长：不同 prefab 的子弹各自一个池）
        private Dictionary<GameObject, int> projectileIndexByPrefab;// prefab -> poolIndex 映射，用于复用/快速索引

        [SerializeField] private GameObject throwablePoolParent;    // ThrowablePool 挂载父对象
        private List<ThrowablePool> throwablePools;                 // 投掷物对象池列表（动态增长）
        private Dictionary<GameObject, int> throwableIndexByPrefab; // prefab -> poolIndex 映射

        [SerializeField] private GameObject boomerangPoolParent;    // BoomerangPool 挂载父对象
        private List<BoomerangPool> boomerangPools;                 // 回旋镖对象池列表（动态增长）
        private Dictionary<GameObject, int> boomerangIndexByPrefab; // prefab -> poolIndex 映射

        [SerializeField] private GameObject expGemPrefab;           // 经验宝石 prefab（用于初始化 ExpGemPool）
        [SerializeField] private ExpGemPool expGemPool;             // 经验宝石对象池
        [SerializeField] private GameObject coinPrefab;             // 金币 prefab
        [SerializeField] private CoinPool coinPool;                 // 金币对象池
        [SerializeField] private GameObject chestPrefab;            // 宝箱 prefab
        [SerializeField] private ChestPool chestPool;               // 宝箱对象池
        [SerializeField] private GameObject textPrefab;             // 伤害飘字 prefab
        [SerializeField] private DamageTextPool textPool;           // 伤害飘字对象池

        [Header("Spatial Hash Grid Settings")]
        [SerializeField] private Vector2 gridSize;                  // 空间哈希网格覆盖的世界尺寸（宽高）
        [SerializeField] private Vector2Int gridDimensions;         // 网格划分维度（x方向格子数, y方向格子数）

        [Header("Dependencies")]
        [SerializeField] private SpriteRenderer flashSpriteRenderer;// “闪白”特效用的 SpriteRenderer（炸弹/全屏伤害时）
        [SerializeField] private Camera playerCamera;  // 摄像头：用于计算屏幕在世界坐标中的宽高（刷怪/刷宝箱/可见判断）
        private Character playerCharacter;  // 玩家的角色：用于获取位置/速度/朝向（刷怪偏向、网格重建等）
        private StatsManager statsManager;              // 统计：击杀数、金币数等
        private Inventory inventory;                    // 背包：拾取物入包判断会用到
        private InfiniteBackground infiniteBackground;  // 无限背景：磁铁吸全屏时触发 Shockwave
        private FastList<Monster> livingMonsters;       // 当前存活怪物列表：用于全屏炸/清怪/统计等
        private FastList<Collectable> magneticCollectables; // 可被磁铁吸附的拾取物列表（金币、宝石等）
        public FastList<Chest> chests;                  // 当前场上的宝箱列表（刷箱防重叠/回收管理）

        // 计时器字段（本文件里保留但不一定都使用）
        private float timeSinceLastMonsterSpawned;
        private float timeSinceLastChestSpawned;

        //初始化四个道具
        [SerializeField] public Collectable[] collectables;

        // 屏幕在世界坐标中的尺寸：用于计算“屏幕外刷怪距离/是否在屏幕内”等
        private float screenWidthWorldSpace, screenHeightWorldSpace, screenDiagonalWorldSpace;
        private float minSpawnDistance;                 // 最小刷怪距离（通常是屏幕对角线的一半，保证在屏幕外）

        // 特效协程引用：避免重复启动多个相同协程
        private Coroutine flashCoroutine;               // 闪白协程
        private Coroutine shockwave;                    // 背景冲击波协程

        // 空间哈希网格：用于高效查询附近目标、插入/移除客户端（怪、玩家等）
        private SpatialHashGrid grid;

        //只读属性
        // 对外暴露：供其他系统读取（Ability、Chest、Collectable 等）
        public FastList<Monster> LivingMonsters { get => livingMonsters; }            // 读取当前存活怪物列表
        public FastList<Collectable> MagneticCollectables { get => magneticCollectables; } // 读取可磁吸拾取物列表
        public Inventory Inventory { get => inventory; }                              // 读取背包引用
        public AbilitySelectionDialog AbilitySelectionDialog { get; private set; }    // 升级弹窗引用（箱子可能用）
        public SpatialHashGrid Grid { get => grid; }                                  // 读取网格引用（技能找目标用）

        /// <summary>
        /// 初始化 EntityManager（由 LevelManager.Init 调用）
        /// 主要做：
        /// 1) 记录依赖引用（playerCharacter/inventory/statsManager/背景/弹窗）
        /// 2) 计算屏幕世界尺寸（用于屏幕外刷怪/刷箱）
        /// 3) 初始化 FastList 容器
        /// 4) 创建并初始化怪物对象池（按关卡配置 monsters 数量 +1 个 Boss 池）
        /// 5) 初始化动态对象池结构（Projectile/Throwable/Boomerang 的 list + prefab->index 映射）
        /// 6) 初始化固定对象池（宝石/金币/宝箱/飘字）
        /// 7) 初始化空间哈希网格（以玩家为中心）
        /// </summary>
        public void Init(LevelBlueprint levelBlueprint, Character character, Inventory inventory, StatsManager statsManager, InfiniteBackground infiniteBackground, AbilitySelectionDialog abilitySelectionDialog)
        {
            this.playerCharacter = character;
            this.inventory = inventory;
            this.infiniteBackground = infiniteBackground;
            this.statsManager = statsManager;
            AbilitySelectionDialog = abilitySelectionDialog;

            // 计算屏幕在世界坐标中的大小：用于在屏幕外生成敌人/宝箱
            // ViewportToWorldPoint：把视口坐标转换为世界空间坐标，必须指定z值，其中z值为近裁剪面距离
            Vector2 bottomLeft = playerCamera.ViewportToWorldPoint(new Vector3(0, 0, playerCamera.nearClipPlane));
            Vector2 topRight = playerCamera.ViewportToWorldPoint(new Vector3(1, 1, playerCamera.nearClipPlane));
            screenWidthWorldSpace = topRight.x - bottomLeft.x;
            screenHeightWorldSpace = topRight.y - bottomLeft.y;
            //屏幕对角线模长
            screenDiagonalWorldSpace = (topRight - bottomLeft).magnitude;
            minSpawnDistance = screenDiagonalWorldSpace / 2; // 最小刷怪距离（屏幕对角线的一半）

            // 初始化 FastList：用于维护运行时集合（活怪/可磁吸拾取物/宝箱）
            livingMonsters = new FastList<Monster>();
            magneticCollectables = new FastList<Collectable>();
            chests = new FastList<Chest>();

            //levelBlueprint.monsters.Length即为怪物种类数
            // 初始化怪物对象池：每个关卡配置的 MonstersContainer对应一个池，额外+1池用于最终 Boss
            monsterPools = new MonsterPool[levelBlueprint.monsters.Length + 1];
            for (int i = 0; i < levelBlueprint.monsters.Length; i++)
            {
                // 动态添加 MonsterPool 组件到 monsterPoolParent 上（便于层级管理）
                monsterPools[i] = monsterPoolParent.AddComponent<MonsterPool>();
                // monstersPrefab：该池包含的一组怪物 prefab（随机/按索引取）
                monsterPools[i].Init(this, playerCharacter, levelBlueprint.monsters[i].monstersPrefab);
            }

            // 最后一个怪物池用于最终 Boss（bossPrefab）
            monsterPools[monsterPools.Length - 1] = monsterPoolParent.AddComponent<MonsterPool>();
            monsterPools[monsterPools.Length - 1].Init(this, playerCharacter, levelBlueprint.finalBoss.bossPrefab);

            // 投射物对象池结构：按 projectilePrefab 动态创建池（第一次见到该 prefab 时建池）
            projectileIndexByPrefab = new Dictionary<GameObject, int>();
            projectilePools = new List<ProjectilePool>();

            // 投掷物对象池结构：按 throwablePrefab 动态创建池
            throwableIndexByPrefab = new Dictionary<GameObject, int>();
            throwablePools = new List<ThrowablePool>();

            // 回旋镖对象池结构：按 boomerangPrefab 动态创建池
            boomerangIndexByPrefab = new Dictionary<GameObject, int>();
            boomerangPools = new List<BoomerangPool>();

            // 初始化固定的一次性对象池：经验宝石/金币/宝箱/伤害飘字
            expGemPool.Init(this, playerCharacter, expGemPrefab);
            coinPool.Init(this, playerCharacter, coinPrefab);
            chestPool.Init(this, playerCharacter, chestPrefab);
            textPool.Init(this, playerCharacter, textPrefab);

            // 初始化空间哈希网格：以玩家当前位置为中心，覆盖 gridSize 的范围
            // bounds[0] = 左下角，bounds[1] = 右上角
            Vector2[] bounds = new Vector2[] { (Vector2)playerCharacter.transform.position - gridSize / 2, (Vector2)playerCharacter.transform.position + gridSize / 2 };
            grid = new SpatialHashGrid(bounds, gridDimensions);

            //实例化四种能力
            //for(int i=0;i<4;i++)
            //{
            //    collectables[i] = Instantiate("Bomb");
            //}
        }

        void Update()
        {
            // 如果玩家接近网格边缘，则重建网格（使网格中心跟随玩家移动）
            // 目的：保持附近查询有效，避免玩家跑出网格覆盖范围导致查找错误/性能问题
            if (grid.CloseToEdge(playerCharacter))
            {
                grid.Rebuild(playerCharacter.transform.position);
            }
        }

        ////////////////////////////////////////////////////////////////////////////////
        /// Special Functions（全局效果/工具功能）
        ////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// “磁铁”效果：吸引全屏金币与经验宝石
        /// 1) 触发背景冲击波特效
        /// 2) 遍历所有可磁吸拾取物，调用 Collect()，让它们飞向玩家/背包槽位
        /// </summary>
        public void CollectAllCoinsAndGems()
        {
            //shockwave背景冲击波协程
            if (shockwave != null) StopCoroutine(shockwave);
            // Shockwave 半径用屏幕对角线的一半（大致覆盖屏幕）
            //调用infiniteBackground类中的Shockwave协程，通过随时间修改shader中的_Shockwave来实现磁铁特效
            shockwave = StartCoroutine(infiniteBackground.Shockwave(screenDiagonalWorldSpace / 2));

            // ToList()：避免遍历过程中列表被 Collect() 修改导致枚举异常
            foreach (Collectable collectable in magneticCollectables.ToList())
            {
                collectable.Collect();
            }
        }

        /// <summary>
        /// “炸弹/全屏伤害”效果：对屏幕内可见怪物造成一次伤害，并闪白
        /// </summary>
        //执行爆炸特效加伤害
        public void DamageAllVisibileEnemies(float damage)
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(Flash());

            foreach (Monster monster in livingMonsters.ToList())
            {
                // 只对“在屏幕范围内（允许 buffer）”的怪生效
                if (TransformOnScreen(monster.transform, Vector2.one))
                    monster.TakeDamage(damage, Vector2.zero);
            }
        }

        /// <summary>
        /// 清场：杀死除 Boss 之外的所有怪
        /// 通常用于 Boss 出场/转场等
        /// </summary>
        public void KillAllMonsters()
        {
            foreach (Monster monster in livingMonsters.ToList())
            {
                // BossMonster 不杀（交给关卡逻辑处理）
                if (!(monster as BossMonster))
                    StartCoroutine(monster.Killed(false));
            }
        }

        /// <summary>
        /// 屏幕闪白协程（使用 unscaledDeltaTime，不受 Time.timeScale 影响）
        /// </summary>
        private IEnumerator Flash()
        {
            flashSpriteRenderer.enabled = true;
            float t = 0;
            while (t < 1)
            {
                // EaseOutQuart：让闪白淡出更“顺滑”
                flashSpriteRenderer.color = new Color(1, 1, 1, 1 - EasingUtils.EaseOutQuart(t));
                t += Time.unscaledDeltaTime * 4;
                yield return null;
            }
            flashSpriteRenderer.enabled = false;
        }

        /// <summary>
        /// 判断某个 transform 是否位于“以玩家为中心的屏幕范围”内
        /// buffer：额外容忍边界（例如 Vector2.one 表示在边界外再扩展 1 个单位）
        /// 注意：这里不是用 Camera 的 Viewport 判断，而是用玩家位置 + screenWidth/HeightWorldSpace 做矩形判断。
        /// </summary>
        public bool TransformOnScreen(Transform transform, Vector2 buffer = default(Vector2))
        {
            //判断transform的位置是否大于左下角坐标的x，y并且小于右上角坐标的x，y
            return (
                transform.position.x > playerCharacter.transform.position.x - screenWidthWorldSpace / 2 - buffer.x &&
                transform.position.x < playerCharacter.transform.position.x + screenWidthWorldSpace / 2 + buffer.x &&
                transform.position.y > playerCharacter.transform.position.y - screenHeightWorldSpace / 2 - buffer.y &&
                transform.position.y < playerCharacter.transform.position.y + screenHeightWorldSpace / 2 + buffer.y
            );
        }

        ////////////////////////////////////////////////////////////////////////////////
        /// Monster Spawning（怪物生成/回收）
        ////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// 在“屏幕外随机位置”刷怪（常用入口）
        /// 如果玩家在移动，会使用“按玩家移动方向加权”的刷怪点生成方式，提高从前方来怪的概率。
        /// </summary>
        public Monster SpawnMonsterRandomPosition(int monsterPoolIndex, MonsterBlueprint monsterBlueprint, float hpBuff = 0)
        {
            // Find a random position offscreen

            // 玩家在移动：更偏向从移动方向刷怪；否则均匀从四边刷
            Vector2 spawnPosition = (playerCharacter.Velocity != Vector2.zero) ? GetRandomMonsterSpawnPositionPlayerVelocity() : GetRandomMonsterSpawnPosition();

            // Spawn the monster（真正刷怪）
            return SpawnMonster(monsterPoolIndex, spawnPosition, monsterBlueprint, hpBuff);
        }

        /// <summary>
        /// 在指定位置刷怪：
        /// 1) 从 monsterPools[monsterPoolIndex] 取出一个 Monster（对象池）
        /// 2) Setup 应用蓝图/血量 buff
        /// 3) 插入空间网格（供技能/怪物 AI 附近查询使用）
        /// </summary>
        public Monster SpawnMonster(int monsterPoolIndex, Vector2 position, MonsterBlueprint monsterBlueprint, float hpBuff = 0)
        {
            //生成怪物实体
            Monster newMonster = monsterPools[monsterPoolIndex].Get();
            newMonster.Setup(monsterPoolIndex, position, monsterBlueprint, hpBuff);
            grid.InsertClient(newMonster);
            return newMonster;
        }

        /// <summary>
        /// 回收怪物：
        /// 1) 如果是玩家击杀则统计击杀数
        /// 2) 从空间网格移除
        /// 3) 归还对象池
        /// </summary>
        public void DespawnMonster(int monsterPoolIndex, Monster monster, bool killedByPlayer = true)
        {
            if (killedByPlayer)
            {
                //统计系统中怪物死亡数+1
                statsManager.IncrementMonstersKilled();
            }
            grid.RemoveClient(monster);
            monsterPools[monsterPoolIndex].Release(monster);
        }

        /// <summary>
        /// 从四边（左/上/右/下）随机选择一边，在屏幕外缓冲距离处生成怪物刷新点。
        /// 这样怪物不会突然出现在镜头内。
        /// </summary>
        private Vector2 GetRandomMonsterSpawnPosition()
        {
            Vector2[] sideDirections = new Vector2[] { Vector2.left, Vector2.up, Vector2.right, Vector2.down };
            int sideIndex = Random.Range(0, 4);
            Vector2 spawnPosition;

            // sideIndex%2==0 -> 左/右边：x 超出屏幕范围，y 在屏幕高度范围内随机
            if (sideIndex % 2 == 0)
            {
                spawnPosition = (Vector2)playerCharacter.transform.position
                              + sideDirections[sideIndex] * (screenWidthWorldSpace / 2 + monsterSpawnBufferDistance)    //与playerCharacterx轴上的距离
                                                                                                                        //与playerCharactery轴上的距离
                              + Vector2.up * Random.Range(-screenHeightWorldSpace / 2 - monsterSpawnBufferDistance, screenHeightWorldSpace / 2 + monsterSpawnBufferDistance);
            }
            // 上/下边：y 超出屏幕范围，x 在屏幕宽度范围内随机
            else
            {
                spawnPosition = (Vector2)playerCharacter.transform.position
                              + sideDirections[sideIndex] * (screenHeightWorldSpace / 2 + monsterSpawnBufferDistance)
                              + Vector2.right * Random.Range(-screenWidthWorldSpace / 2 - monsterSpawnBufferDistance, screenWidthWorldSpace / 2 + monsterSpawnBufferDistance);
            }
            return spawnPosition;
        }

        /// <summary>
        /// 按玩家移动方向加权的刷怪点选择：
        /// 思路：计算玩家速度方向与四边方向的点积（dot），dot 越大表示越朝向该边，则提高该边被选中的概率。
        /// 结果：怪更倾向从玩家前进方向出现，提高压力与追逐感。
        /// </summary>
        private Vector2 GetRandomMonsterSpawnPositionPlayerVelocity()
        {
            Vector2[] sideDirections = new Vector2[] { Vector2.left, Vector2.up, Vector2.right, Vector2.down };

            // 计算玩家移动方向与四个边方向的点积作为权重基础（dot>0 表示前向，dot<0 表示后向）
            float[] sideWeights = new float[]
            {
                Vector2.Dot(playerCharacter.Velocity.normalized, sideDirections[0]),
                Vector2.Dot(playerCharacter.Velocity.normalized, sideDirections[1]),
                Vector2.Dot(playerCharacter.Velocity.normalized, sideDirections[2]),
                Vector2.Dot(playerCharacter.Velocity.normalized, sideDirections[3])
            };

            // 注意：这里的权重处理方式比较“粗糙但可用”：
            // 先计算一个 extraWeight，用于把“<=0 的边”也分配到一定概率，避免完全刷不到后方
            float extraWeight = sideWeights.Sum() / playerDirectionSpawnWeight;
            int badSideCount = sideWeights.Where(x => x <= 0).Count();
            for (int i = 0; i < sideWeights.Length; i++)
            {
                if (sideWeights[i] <= 0)
                    sideWeights[i] = extraWeight / badSideCount;
            }
            float totalSideWeight = sideWeights.Sum();

            // 轮盘赌随机选择边（按权重）
            float rand = Random.Range(0f, totalSideWeight);
            float cumulative = 0;
            int sideIndex = -1;
            for (int i = 0; i < sideWeights.Length; i++)
            {
                cumulative += sideWeights[i];
                if (rand < cumulative)
                {
                    sideIndex = i;
                    break;
                }
            }

            // 根据选中的边生成屏幕外刷新点（逻辑与 GetRandomMonsterSpawnPosition 类似）
            Vector2 spawnPosition;
            if (sideIndex % 2 == 0)
            {
                spawnPosition = (Vector2)playerCharacter.transform.position
                              + sideDirections[sideIndex] * (screenWidthWorldSpace / 2 + monsterSpawnBufferDistance)
                              + Vector2.up * Random.Range(-screenHeightWorldSpace / 2 - monsterSpawnBufferDistance, screenHeightWorldSpace / 2 + monsterSpawnBufferDistance);
            }
            else
            {
                spawnPosition = (Vector2)playerCharacter.transform.position
                              + sideDirections[sideIndex] * (screenHeightWorldSpace / 2 + monsterSpawnBufferDistance)
                              + Vector2.right * Random.Range(-screenWidthWorldSpace / 2 - monsterSpawnBufferDistance, screenWidthWorldSpace / 2 + monsterSpawnBufferDistance);
            }
            return spawnPosition;
        }

        ////////////////////////////////////////////////////////////////////////////////
        /// Exp Gem Spawning（经验宝石生成/回收）
        ////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// 生成经验宝石：
        /// 1) 从 expGemPool 取对象
        /// 2) 调用 Setup 设置位置/类型/是否播放生成动画
        /// </summary>
        public ExpGem SpawnExpGem(Vector2 position, GemType gemType = GemType.White1, bool spawnAnimation = true)
        {
            //检查对象池的空闲列表是否为空，不为空则取出最后一个并返回
            //若为空则调用创建函数生成新对象
            ExpGem newGem = expGemPool.Get();
            newGem.Setup(position, gemType, spawnAnimation);
            return newGem;
        }

        /// <summary>
        /// 回收经验宝石（归还对象池）
        /// </summary>
        public void DespawnGem(ExpGem gem)
        {
            expGemPool.Release(gem);
        }

        /// <summary>
        /// 在玩家周围生成一批经验宝石（常用于开局或奖励）
        /// 使用 sqrt(Random) 让点在圆盘区域内更均匀分布（不容易集中在中心）
        /// </summary>
        public void SpawnGemsAroundPlayer(int gemCount, GemType gemType = GemType.White1)
        {
            for (int i = 0; i < gemCount; i++)
            {
                //Random.insideUnitCircle返回单位圆（半径 = 1，圆心在原点）内的随机 2D 向量（x / y 范围：[-1,1]，且向量长度≤1
                Vector2 spawnDirection = Random.insideUnitCircle.normalized;
                Vector2 spawnPosition = (Vector2)playerCharacter.transform.position
                                      + spawnDirection * Mathf.Sqrt(Random.Range(1, Mathf.Pow(minSpawnDistance, 2)));
                SpawnExpGem(spawnPosition, gemType, false);
            }
        }

        ////////////////////////////////////////////////////////////////////////////////
        /// Coin Spawning（金币生成/回收）
        ////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// 生成金币（对象池）
        /// </summary>
        public Coin SpawnCoin(Vector2 position, CoinType coinType = CoinType.Bronze1, bool spawnAnimation = true)
        {
            Coin newCoin = coinPool.Get();
            newCoin.Setup(position, coinType, spawnAnimation);
            return newCoin;
        }

        /// <summary>
        /// 回收金币：
        /// pickedUpByPlayer=true 表示被玩家拾取，此时增加本局金币统计
        /// </summary>
        public void DespawnCoin(Coin coin, bool pickedUpByPlayer = true)
        {
            if (pickedUpByPlayer)
            {
                // CoinType 的枚举值本身就是“面额”，因此可直接转 int 累加
                statsManager.IncreaseCoinsGained((int)coin.CoinType);
            }
            coinPool.Release(coin);
        }

        ////////////////////////////////////////////////////////////////////////////////
        /// Chest Spawning（宝箱生成/回收）
        ////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// 生成宝箱，并确保不要刷在另一个宝箱上（简单距离检测防重叠）
        /// 刷新位置：以玩家为中心的“屏幕外圈 + 缓冲 + 随机范围”
        /// </summary>
        public Chest SpawnChest(ChestBlueprint chestBlueprint)
        {
            Chest newChest = chestPool.Get();
            newChest.Setup(chestBlueprint);

            // Ensure the chest is not spawned on top of another chest（避免宝箱重叠）
            bool overlapsOtherChest = false;
            int tries = 0;
            do
            {
                Vector2 spawnDirection = Random.insideUnitCircle.normalized;

                // 宝箱在屏幕外圈附近生成：minSpawnDistance + buffer + (0~chestSpawnRange)
                Vector2 spawnPosition = (Vector2)playerCharacter.transform.position
                                      + spawnDirection * (minSpawnDistance + monsterSpawnBufferDistance + Random.Range(0, chestSpawnRange));
                newChest.transform.position = spawnPosition;

                // 检查与已有宝箱的距离，小于0.5认为重叠，重新选点
                overlapsOtherChest = false;
                foreach (Chest chest in chests)
                {
                    if (Vector2.Distance(chest.transform.position, spawnPosition) < 0.5f)
                    {
                        overlapsOtherChest = true;
                        break;
                    }
                }
            } while (overlapsOtherChest && tries++ < 100); // 最多尝试 100 次，避免死循环

            chests.Add(newChest);
            return newChest;
        }

        /// <summary>
        /// 在指定位置生成宝箱（不做防重叠检查）,打败boss时调用的
        /// </summary>
        public Chest SpawnChest(ChestBlueprint chestBlueprint, Vector2 position)
        {
            Chest newChest = chestPool.Get();
            newChest.transform.position = position;
            newChest.Setup(chestBlueprint);
            chests.Add(newChest);
            return newChest;
        }

        /// <summary>
        /// 回收宝箱：从列表移除并归还对象池
        /// </summary>
        public void DespawnChest(Chest chest)
        {
            chests.Remove(chest);
            chestPool.Release(chest);
        }

        ////////////////////////////////////////////////////////////////////////////////
        /// Text Spawning（伤害飘字生成/回收）
        ////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// 生成伤害飘字（对象池）
        /// </summary>
        public DamageText SpawnDamageText(Vector2 position, float damage)
        {
            DamageText newText = textPool.Get();
            newText.Setup(position, damage);
            return newText;
        }

        /// <summary>
        /// 回收伤害飘字
        /// </summary>
        public void DespawnDamageText(DamageText text)
        {
            textPool.Release(text);
        }

        ////////////////////////////////////////////////////////////////////////////////
        /// Projectile Spawning（投射物生成/回收/动态建池）
        ////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// 生成投射物：
        /// projectileIndex 是 AddPoolForProjectile(prefab) 返回的索引，用来定位对应的对象池
        /// </summary>
        public Projectile SpawnProjectile(int projectileIndex, Vector2 position, float damage, float knockback, float speed, LayerMask targetLayer)
        {
            Projectile projectile = projectilePools[projectileIndex].Get();
            projectile.Setup(projectileIndex, position, damage, knockback, speed, targetLayer);
            return projectile;
        }

        /// <summary>
        /// 回收投射物到对应池
        /// </summary>
        public void DespawnProjectile(int projectileIndex, Projectile projectile)
        {
            projectilePools[projectileIndex].Release(projectile);
        }

        /// <summary>
        /// 为某个 projectilePrefab 动态创建对象池（若已存在则复用旧池）
        /// 返回该 prefab 对应的 poolIndex（后续 SpawnProjectile 用这个 index 快速取池）
        /// </summary>
        public int AddPoolForProjectile(GameObject projectilePrefab)
        {
            if (!projectileIndexByPrefab.ContainsKey(projectilePrefab))
            {
                // 新 prefab：记录索引并创建 ProjectilePool
                //假设projectilePools列表中已经有三个元素（及三种projectile对象池），projectilePools.Count=3
                //则存入projectileIndexByPrefab字典的值就为3，正好对应projectilePools数组中该种projectilePrefab对应的Index。
                //此时projectilePools.Count=4了，所以返回projectilePools.Count - 1
                projectileIndexByPrefab[projectilePrefab] = projectilePools.Count;
                ProjectilePool projectilePool = projectilePoolParent.AddComponent<ProjectilePool>();
                projectilePool.Init(this, playerCharacter, projectilePrefab);
                projectilePools.Add(projectilePool);
                return projectilePools.Count - 1;
            }
            // 已存在：直接返回索引
            return projectileIndexByPrefab[projectilePrefab];
        }

        ////////////////////////////////////////////////////////////////////////////////
        /// Throwable Spawning（投掷物生成/回收/动态建池）
        ////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// 生成投掷物（注意：这里比投射物多了 speed 参数，投掷物通常走抛物线/飞行时间等）
        /// </summary>
        public Throwable SpawnThrowable(int throwableIndex, Vector2 position, float damage, float knockback, float speed, LayerMask targetLayer)
        {
            Throwable throwable = throwablePools[throwableIndex].Get();
            throwable.Setup(throwableIndex, position, damage, knockback, speed, targetLayer);
            return throwable;
        }

        /// <summary>
        /// 回收投掷物
        /// </summary>
        public void DespawnThrowable(int throwableIndex, Throwable throwable)
        {
            throwablePools[throwableIndex].Release(throwable);
        }

        /// <summary>
        /// 为某个 throwablePrefab 动态创建对象池（若已存在则复用）
        /// </summary>
        public int AddPoolForThrowable(GameObject throwablePrefab)
        {
            if (!throwableIndexByPrefab.ContainsKey(throwablePrefab))
            {
                throwableIndexByPrefab[throwablePrefab] = throwablePools.Count;
                ThrowablePool throwablePool = throwablePoolParent.AddComponent<ThrowablePool>();
                throwablePool.Init(this, playerCharacter, throwablePrefab);
                throwablePools.Add(throwablePool);
                return throwablePools.Count - 1;
            }
            return throwableIndexByPrefab[throwablePrefab];
        }

        ////////////////////////////////////////////////////////////////////////////////
        /// Boomerang Spawning（回旋镖生成/回收/动态建池）
        ////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// 生成回旋镖（与投射物/投掷物不同：回旋镖有 throwDistance/throwTime）
        /// </summary>
        public Boomerang SpawnBoomerang(int boomerangIndex, Vector2 position, float damage, float knockback, float throwDistance, float throwTime, LayerMask targetLayer)
        {
            Boomerang boomerang = boomerangPools[boomerangIndex].Get();
            boomerang.Setup(boomerangIndex, position, damage, knockback, throwDistance, throwTime, targetLayer);
            return boomerang;
        }

        /// <summary>
        /// 回收回旋镖
        /// </summary>
        public void DespawnBoomerang(int boomerangIndex, Boomerang boomerang)
        {
            boomerangPools[boomerangIndex].Release(boomerang);
        }

        /// <summary>
        /// 为某个 boomerangPrefab 动态创建对象池（若已存在则复用）
        /// </summary>
        public int AddPoolForBoomerang(GameObject boomerangPrefab)
        {
            if (!boomerangIndexByPrefab.ContainsKey(boomerangPrefab))
            {
                boomerangIndexByPrefab[boomerangPrefab] = boomerangPools.Count;
                BoomerangPool boomerangPool = boomerangPoolParent.AddComponent<BoomerangPool>();
                boomerangPool.Init(this, playerCharacter, boomerangPrefab);
                boomerangPools.Add(boomerangPool);
                return boomerangPools.Count - 1;
            }
            return boomerangIndexByPrefab[boomerangPrefab];
        }
    }
}
