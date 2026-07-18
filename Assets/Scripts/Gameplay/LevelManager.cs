using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vampire
{

    public class LevelManager : MonoBehaviour
    {
        // ====== 关卡与系统引用（在 Inspector 里拖拽） ======

        [SerializeField] private LevelBlueprint levelBlueprint;          // 本关配置（时长/刷怪表/Boss/宝箱/背景等），已拖动赋值
        [SerializeField] private Character playerCharacter;              // 玩家角色对象
        [SerializeField] private EntityManager entityManager;            // 实体总管：刷怪、掉落、对象池、网格查询
        [SerializeField] private AbilityManager abilityManager;          // 能力系统：技能池、升级三选一、UpgradeValue 等
        [SerializeField] private AbilitySelectionDialog abilitySelectionDialog; // 升级弹窗 UI
        [SerializeField] private InfiniteBackground infiniteBackground;  // 无限背景
        [SerializeField] private Inventory inventory;                    // 背包/道具栏
        [SerializeField] private StatsManager statsManager;              // 统计：击杀、金币、伤害等
        [SerializeField] private GameOverDialog gameOverDialog;          // 结算界面 UI
        [SerializeField] private GameTimer gameTimer;                    // 计时器 UI（显示 mm:ss）

        // ====== 运行时状态 ======

        private float levelTime = 0f;                    // 当前关卡已进行的时间（秒）
        private float timeSinceLastMonsterSpawned;       // 距离上次刷普通怪过去多久
        private float timeSinceLastChestSpawned;         // 距离上次刷宝箱过去多久
        private bool miniBossSpawned = false;            // miniBoss 是否已经刷过
        private bool finalBossSpawned = false;           // 最终 Boss 是否已经刷过

        /// <summary>
        /// 初始化关卡（把所有系统串起来）
        /// 注意：初始化顺序很重要。
        /// </summary>
        public void Init(LevelBlueprint levelBlueprint)
        {
            // 记录关卡配置，并重置关卡时间
            this.levelBlueprint = levelBlueprint;
            levelTime = 0f;

            // 1) 初始化实体总管（对象池/网格/掉落等）
            //    后续刷怪、刷宝箱、刷宝石都依赖 entityManager
            entityManager.Init(this.levelBlueprint, playerCharacter, inventory, statsManager, infiniteBackground, abilitySelectionDialog);

            // 2) 初始化能力系统（构建技能池：起始技能 + 新技能）
            abilityManager.Init(this.levelBlueprint, entityManager, playerCharacter, abilityManager);

            // 3) 初始化升级弹窗（它需要 abilityManager 生成候选，需要 entityManager 生成保底宝箱等）
            abilitySelectionDialog.Init(abilityManager, entityManager, playerCharacter);

            // 4) 初始化玩家角色（血条/经验条、可升级属性注册、动画等）
            playerCharacter.Init(entityManager, abilityManager, statsManager);

            // 5) 绑定玩家死亡事件：玩家死了 → 关卡失败结算
            playerCharacter.OnDeath.AddListener(GameOver);

            // 6) 开局在玩家周围生成初始经验宝石（让玩家尽快升级进入循环）
            entityManager.SpawnGemsAroundPlayer(this.levelBlueprint.initialExpGemCount,
                                               this.levelBlueprint.initialExpGemType);

            // 7) 开局生成一个宝箱（引导玩家开箱/升级/掉落）
            entityManager.SpawnChest(levelBlueprint.chestBlueprint);

            // 8) 初始化无限背景（贴图 + 跟随玩家）
            infiniteBackground.Init(this.levelBlueprint.backgroundTexture, playerCharacter.transform);

            // 9) 初始化背包系统（建立 CollectableType→Slot 映射等）
            inventory.Init();
            inventory.AddInitialItems(entityManager.collectables, entityManager, playerCharacter, 2);
        }

        /// <summary>
        /// Unity 生命周期：场景开始时调用
        /// 默认用 Inspector 填的 levelBlueprint 来启动关卡
        /// </summary>
        private void Start()
        {
            Init(levelBlueprint);
        }

        /// <summary>
        /// Unity 生命周期：每帧调用
        /// 负责推进时间、刷怪、刷 Boss、刷宝箱
        /// </summary>
        private void Update()
        {
            // ====== 1) 时间推进与 UI 显示 ======
            levelTime += Time.deltaTime;
            gameTimer.SetTime(levelTime);

            // ====== 2) 普通怪刷怪（只在关卡时间未结束前进行） ======
            if (levelTime < levelBlueprint.levelTime)
            {
                timeSinceLastMonsterSpawned += Time.deltaTime;

                // progress：关卡进度 0~1，用于从 MonsterSpawnTable 的曲线取值
                float progress = levelTime / levelBlueprint.levelTime;

                // spawnRate：每秒刷多少只怪（越到后期可能越高）
                float spawnRate = levelBlueprint.monsterSpawnTable.GetSpawnRate(progress);

                // 把“每秒刷多少只”换算成“每只间隔多少秒”
                float monsterSpawnDelay = spawnRate > 0 ? 1.0f / spawnRate : float.PositiveInfinity;

                // 到达刷怪时间 → 刷一只
                if (timeSinceLastMonsterSpawned >= monsterSpawnDelay)
                {
                    // 从刷怪表中抽取：刷哪种怪 + 本次怪的血量倍率（用于难度增长）
                    (int monsterIndex, float hpMultiplier) =
                        levelBlueprint.monsterSpawnTable.SelectMonsterWithHPMultiplier(progress);

                    // monsterIndex 需要映射到：哪个池(poolIndex) + 哪个蓝图(blueprintIndex)
                    (int poolIndex, int blueprintIndex) = levelBlueprint.MonsterIndexMap[monsterIndex];

                    // 从该池对应的蓝图数组中取出怪物配置
                    MonsterBlueprint monsterBlueprint = levelBlueprint.monsters[poolIndex].monsterBlueprints[blueprintIndex];

                    // 生成怪物：随机位置（通常在屏幕外圈），并把 hp 按倍率放大
                    entityManager.SpawnMonsterRandomPosition(poolIndex, monsterBlueprint, monsterBlueprint.hp * hpMultiplier);

                    // 使用 Repeat 保留超出的时间，避免帧率波动造成刷怪频率不稳定
                    timeSinceLastMonsterSpawned = Mathf.Repeat(timeSinceLastMonsterSpawned, monsterSpawnDelay);
                }
            }

            // ====== 3) miniBoss 刷新（当前实现：只刷第 0 个） ======
            if (!miniBossSpawned && levelTime > levelBlueprint.miniBosses[0].spawnTime)
            {
                miniBossSpawned = true;

                // Boss 通常用 monsters.Length 作为“Boss 池索引”（独立池）
                entityManager.SpawnMonsterRandomPosition(levelBlueprint.monsters.Length,
                                                         levelBlueprint.miniBosses[0].bossBlueprint);
            }

            // ====== 4) 最终 Boss 刷新（关卡时间结束后刷） ======
            if (!finalBossSpawned && levelTime > levelBlueprint.levelTime)
            {
                // 可选：如果想清场再出 Boss，可以打开这一句（当前注释）
                 entityManager.KillAllMonsters();

                finalBossSpawned = true;

                Monster finalBoss = entityManager.SpawnMonsterRandomPosition(levelBlueprint.monsters.Length,
                                                                             levelBlueprint.finalBoss.bossBlueprint);

                // 监听最终 Boss 死亡 → 通关结算
                finalBoss.OnKilled.AddListener(LevelPassed);
            }

            // ====== 5) 宝箱定时刷新 ======
            timeSinceLastChestSpawned += Time.deltaTime;

            if (timeSinceLastChestSpawned >= levelBlueprint.chestSpawnDelay)
            {
                // 每次刷新可能刷多个箱子
                for (int i = 0; i < levelBlueprint.chestSpawnAmount; i++)
                {
                    entityManager.SpawnChest(levelBlueprint.chestBlueprint);
                }

                // 同样用 Repeat 保留超出的时间，避免帧率波动造成刷箱不稳定
                timeSinceLastChestSpawned = Mathf.Repeat(timeSinceLastChestSpawned, levelBlueprint.chestSpawnDelay);
            }
        }

        /// <summary>
        /// 玩家死亡：关卡失败结算
        /// </summary>
        public void GameOver()
        {
            // 暂停游戏世界
            Time.timeScale = 0;

            // 把本局获得金币写回到 PlayerPrefs 的总金币
            //本地持久化存储的工具类，可将简单数据（如 int、float、string）保存在设备本地（如注册表、plist 文件等）
            int coinCount = PlayerPrefs.GetInt("Coins");
            //statsManager.CoinsGained表示玩家此局获得的金币总量
            PlayerPrefs.SetInt("Coins", coinCount + statsManager.CoinsGained);

            // 打开结算界面：false 表示未通关
            gameOverDialog.Open(false, statsManager);
        }

        /// <summary>
        /// 最终 Boss 被击杀：关卡通关结算
        /// （参数 finalBossKilled 是 Boss 实例，本函数里没用到，但便于事件签名一致）
        /// </summary>
        public void LevelPassed(Monster finalBossKilled)
        {
            Time.timeScale = 0;

            int coinCount = PlayerPrefs.GetInt("Coins");
            PlayerPrefs.SetInt("Coins", coinCount + statsManager.CoinsGained);

            // true 表示通关
            gameOverDialog.Open(true, statsManager);
        }

        /// <summary>
        /// 重新开始当前关卡
        /// </summary>
        public void Restart()
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        /// <summary>
        /// 返回主菜单（buildIndex=0）
        /// </summary>
        public void ReturnToMainMenu()
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(0);
        }
    }
}
