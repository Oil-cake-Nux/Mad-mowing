using UnityEngine;
using System.Collections;

namespace Vampire
{
    /// <summary>
    /// 宝箱逻辑处理类，负责宝箱的初始化、显示、打开及战利品生成等功能
    /// </summary>
    public class Chest : MonoBehaviour
    {
        /// <summary>
        /// 宝箱蓝图，存储宝箱的各种配置信息（如不同状态的精灵、战利品表等）
        /// </summary>
        protected ChestBlueprint chestBlueprint;
        /// <summary>
        /// 实体管理器，用于管理游戏中的实体（如宝箱、物品等）
        /// </summary>
        protected EntityManager entityManager;
        /// <summary>
        /// 玩家角色实例
        /// </summary>
        protected Character playerCharacter;
        /// <summary>
        /// Z轴位置处理器，用于处理宝箱在Z轴上的位置
        /// </summary>
        protected ZPositioner zPositioner;
        /// <summary>
        /// 宝箱物品的父节点，生成的战利品会作为其子物体
        /// </summary>
        protected Transform chestItemsParent;
        /// <summary>
        /// 精灵渲染器，用于显示宝箱的精灵图像
        /// </summary>
        protected SpriteRenderer spriteRenderer;
        /// <summary>
        /// 标记宝箱是否已被打开
        /// </summary>
        protected bool opened = false;

        /// <summary>
        /// 初始化宝箱的核心依赖
        /// </summary>
        /// <param name="entityManager">实体管理器</param>
        /// <param name="playerCharacter">玩家角色</param>
        /// <param name="chestItemsParent">战利品的父节点</param>
        public void Init(EntityManager entityManager, Character playerCharacter, Transform chestItemsParent)
        {
            this.entityManager = entityManager;
            this.playerCharacter = playerCharacter;
            this.chestItemsParent = chestItemsParent;
            // 添加并初始化Z轴位置处理器，以玩家位置为参考
            (zPositioner = gameObject.AddComponent<ZPositioner>()).Init(playerCharacter.transform);
            // 获取子物体中的精灵渲染器组件
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        /// <summary>
        /// 设置宝箱的蓝图信息，并初始化显示状态
        /// </summary>
        /// <param name="chestBlueprint">要应用的宝箱蓝图</param>
        public void Setup(ChestBlueprint chestBlueprint)
        {
            this.chestBlueprint = chestBlueprint;
            // 初始化缩放为1
            transform.localScale = Vector3.one;
            // 设置初始显示为关闭状态的精灵
            spriteRenderer.sprite = chestBlueprint.closedChest;
            // 标记为未打开
            opened = false;
            // 开始宝箱出现的动画协程
            StartCoroutine(Appear());
        }

        /// <summary>
        /// 生成战利品并进行初始化
        /// </summary>
        /// <param name="loot">要生成的战利品数据</param>
        /// <param name="openedByPlayer">是否由玩家打开（决定是否立即收集）</param>
        private void SpawnLoot(Loot<GameObject> loot, bool openedByPlayer = true)
        {
            // 实例化战利品物体，并设置其父节点为宝箱物品父节点
            GameObject item = Instantiate(loot.item, chestItemsParent);
            // 设置战利品生成位置为宝箱位置
            item.transform.position = transform.position;
            // 将宝箱向后微移一点，使战利品显示在宝箱前方
            transform.position += Vector3.back * 0.001f;
            // 获取战利品的可收集组件
            Collectable collectable = item.GetComponent<Collectable>();
            // 初始化可收集物
            collectable.Init(entityManager, playerCharacter);
            // 如果是金币类型的可收集物，进行金币特有设置
            Coin coin = collectable as Coin;
            if (coin != null)
                coin.Setup(transform.position, loot.coinType, true, true);
            else
                collectable.Setup(true, true);
            // 如果是玩家打开的宝箱，立即收集该物品
            if (openedByPlayer)
                collectable.Collect(Collectable.CollectionMode.FromChest);
        }

        /// <summary>
        /// 打开宝箱的入口方法
        /// </summary>
        /// <param name="openedByPlayer">是否由玩家打开</param>
        public void OpenChest(bool openedByPlayer = true)
        {
            // 如果未打开，则标记为已打开并开始打开动画协程
            if (!opened)
            {
                opened = true;
                StartCoroutine(Open(openedByPlayer));
            }
        }

        // 这部分代码确实有点糟糕，谨慎处理
        /// <summary>
        /// 宝箱打开的动画及逻辑处理协程
        /// </summary>
        /// <param name="openedByPlayer">是否由玩家打开</param>
        private IEnumerator Open(bool openedByPlayer = true)
        {
            // 显示宝箱打开过程中的精灵
            spriteRenderer.sprite = chestBlueprint.openingChest;
            // 判断是否需要生成战利品：如果是技能宝箱且有可用技能，则不生成战利品（改为显示技能选择）
            bool spawnLoot = !chestBlueprint.abilityChest || !entityManager.AbilitySelectionDialog.HasAvailableAbilities();
            if (spawnLoot)
                // 从战利品表中获取并生成战利品
                SpawnLoot(chestBlueprint.lootTable.DropLootObject(), openedByPlayer);
            // 等待0.1秒（打开动画过渡）
            yield return new WaitForSeconds(0.1f);
            // 显示宝箱打开后的精灵
            spriteRenderer.sprite = chestBlueprint.openChest;
            // 如果不生成战利品，则打开技能选择对话框
            if (!spawnLoot)
                entityManager.AbilitySelectionDialog.Open(false);
            // 等待0.15秒（打开后状态过渡）
            yield return new WaitForSeconds(0.15f);
            // 宝箱消失动画：从原大小缩放到0
            float t = 0;
            while (t < 1.0f)
            {
                transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, EasingUtils.EaseOutQuart(t));
                t += Time.deltaTime * 2;
                yield return null;
            }
            // 通知实体管理器移除该宝箱
            entityManager.DespawnChest(this);
        }

        /// <summary>
        /// 宝箱出现的动画协程（从无到有缩放）
        /// </summary>
        private IEnumerator Appear()
        {
            // 初始禁用碰撞体，防止出现过程中被触发
            GetComponent<Collider2D>().enabled = false;
            float t = 0;
            // 从缩放0过渡到缩放1，使用 Quart 缓入曲线
            while (t < 1.0f)
            {
                transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, EasingUtils.EaseInQuart(t));
                t += Time.deltaTime * 2;
                yield return null;
            }
            // 确保最终缩放为1
            transform.localScale = Vector3.one;
            // 启用碰撞体，允许与玩家交互
            GetComponent<Collider2D>().enabled = true;
        }

        /// <summary>
        /// 2D碰撞检测：当玩家碰撞到宝箱时，打开宝箱
        /// </summary>
        /// <param name="col">碰撞信息</param>
        void OnCollisionEnter2D(Collision2D col)
        {
            if (col.collider.gameObject == playerCharacter.gameObject)
            {
                OpenChest();
            }
        }
    }
}