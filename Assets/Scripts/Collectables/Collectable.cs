using System.Collections;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 可磁吸拾取物抽象类
    /// 所有游戏中可被玩家拾取的物品（金币、道具等）的基类，定义了拾取物的核心行为（生成、收集、飞向玩家/背包等）
    /// 抽象类不可直接实例化，需子类继承并实现抽象方法OnCollected
    /// </summary>
    public abstract class Collectable : MonoBehaviour
    {
        [Header("Type")] // 标题：拾取物类型配置
        // 拾取物类型（支持在编辑器序列化，同时提供公共只读属性访问）
        //数据文件，记载该拾取物可以存储的数量
        [field: SerializeField] public CollectableType CollectableType;

        [Header("Spawn Animation")] // 标题：生成动画配置
        [SerializeField] protected float saSpeed = 1; // 生成动画的播放速度（值越大，动画播放越快）
        [SerializeField] protected float saHeight = 1; // 生成动画的弹跳高度（控制拾取物生成时的垂直位移上限）
        [SerializeField] protected float saOffsetMax = 0.2f; // 生成动画的水平最大偏移量（随机左右移动的范围）

        [Header("Collect Animation")] // 标题：收集动画配置
        [SerializeField] protected float juiciness = 2; // 收集动画的缓动弹性系数（控制飞向目标时的回弹效果强度）
        [SerializeField] protected float lerpTime = 1; // 收集动画的插值时间（控制飞向目标的整体耗时）

        [Header("Attributes")] // 标题：拾取物基础属性
        [SerializeField] protected bool magnetic = false; // 是否开启磁吸效果（开启后会被玩家磁铁吸引）

        protected EntityManager entityManager; // 实体管理器引用（用于管理磁吸拾取物、背包等）
        protected Character playerCharacter; // 玩家角色引用（用于定位玩家、触发玩家相关逻辑）
        protected ZPositioner zPositioner; // Z轴定位器（控制2D场景中拾取物的显示层级）
        protected Collider2D col; // 拾取物碰撞体（用于检测玩家触发拾取）
        protected bool beingCollected = false; // 拾取状态标记（是否正在被收集，防止重复拾取）

        /// <summary>
        /// 组件唤醒时调用（虚拟方法，支持子类重写）
        /// 初始化拾取物的核心组件引用
        /// </summary>
        protected virtual void Awake()
        {
            col = GetComponent<Collider2D>(); // 获取自身的2D碰撞体组件
            zPositioner = gameObject.AddComponent<ZPositioner>(); // 为当前对象添加Z轴定位器组件
        }

        /// <summary>
        /// 初始化拾取物的依赖引用
        /// </summary>
        /// <param name="entityManager">实体管理器实例</param>
        /// <param name="playerCharacter">玩家角色实例</param>
        public virtual void Init(EntityManager entityManager, Character playerCharacter)
        {
            this.entityManager = entityManager;
            this.playerCharacter = playerCharacter;
            zPositioner.Init(playerCharacter.transform); // 以玩家Transform为参考，初始化Z轴定位器
        }

        /// <summary>
        /// 设置拾取物的初始状态
        /// </summary>
        /// <param name="spawnAnimation">是否播放生成动画</param>
        /// <param name="collectableDuringSpawn">生成动画播放期间，是否允许被玩家拾取</param>
        public virtual void Setup(bool spawnAnimation = true, bool collectableDuringSpawn = true)
        {
            // 启用碰撞体的条件：不播放生成动画，或播放生成动画但期间允许拾取
            col.enabled = !spawnAnimation || collectableDuringSpawn;
            beingCollected = false; // 重置拾取状态标记

            // 若开启磁吸效果，将当前拾取物添加到实体管理器的磁吸拾取物列表中
            if (magnetic)
                entityManager.MagneticCollectables.Add(this);

            // 若需要播放生成动画，启动生成动画协程
            if (spawnAnimation)
                StartCoroutine(SpawnAnimation());

            gameObject.SetActive(true); // 激活当前拾取物对象
        }

        // 磁铁吸附效果代码：核心拾取逻辑（公共方法，支持子类重写）
        /// <summary>
        /// 触发拾取物的收集逻辑
        /// </summary>
        /// <param name="collectionMode">收集模式（默认从地面拾取）</param>
        public virtual void Collect(CollectionMode collectionMode = CollectionMode.FromGround)
        {
            // 若当前正在被收集，直接返回，防止重复执行收集逻辑
            if (beingCollected)
                return;

            // 判断是否需要存入玩家背包：拾取物类型支持背包堆叠（inventoryStackSize>0）
            bool storeInInventory = CollectableType.inventoryStackSize > 0;
            // 尝试获取背包中可存放该拾取物的槽位
            bool hasInventorySlot = entityManager.Inventory.TryGetInventorySlot(this, out InventorySlot inventorySlot);

            // 若需要存入背包、存在可用槽位，但该槽位已堆满，则不执行收集逻辑
            if (storeInInventory && hasInventorySlot && inventorySlot.IsFull())
                return;

            // 标记当前拾取物正在被收集，避免重复拾取
            beingCollected = true;
            col.enabled = false; // 禁用碰撞体，防止后续重复触发拾取检测

            // 若开启了磁吸效果，将当前拾取物从实体管理器的磁吸列表中移除
            if (magnetic)
                entityManager.MagneticCollectables.Remove(this);

            // 分情况处理收集逻辑：
            // 1. 支持背包存储且有可用槽位：飞向背包槽位
            // 2. 不支持背包存储或无可用槽位：直接飞向玩家
            if (storeInInventory && hasInventorySlot)
                StartCoroutine(FlyToInventory(inventorySlot, collectionMode));
            else
                StartCoroutine(FlyToPlayer(collectionMode));
        }

        /// <summary>
        /// 执行拾取后的逻辑（内部调用抽象方法OnCollected）
        /// </summary>
        public void Use()
        {
            OnCollected();
        }

        /// <summary>
        /// 拾取后的具体逻辑（抽象方法，必须由子类实现）
        /// 用于定义不同拾取物被收集后的效果（如金币增加、道具生效等）
        /// </summary>
        protected abstract void OnCollected();

        /// <summary>
        /// 拾取物飞向玩家的协程（虚拟方法，支持子类重写）
        /// </summary>
        /// <param name="collectionMode">收集模式（地面拾取/宝箱拾取）</param>
        /// <returns>协程迭代器</returns>
        protected virtual IEnumerator FlyToPlayer(CollectionMode collectionMode = CollectionMode.FromGround)
        {
            // 若收集模式为宝箱拾取，先播放宝箱生成动画，再启用Z轴定位器
            if (collectionMode == CollectionMode.FromChest)
            {
                yield return StartCoroutine(ChestAnimation());
                zPositioner.enabled = true;
            }

            // 计算拾取物当前位置与玩家中心位置的距离
            float distance = Vector2.Distance(transform.position, playerCharacter.CenterTransform.position);
            // 避免距离为0导致后续计算异常，设置一个极小值
            if (distance == 0.0f) distance = Mathf.Epsilon;

            // 计算缓动弹性系数（与距离成反比，确保不同距离下弹性效果一致）
            float c = juiciness / distance;
            // 计算时间缩放系数（与距离平方根成反比，确保不同距离下动画耗时一致）
            float timeScale = 1.0f / (lerpTime * Mathf.Sqrt(distance));
            // 初始化动画进度（提前扣除一帧时间，避免初始帧无变化）
            float t = -Time.deltaTime * timeScale;

            Vector3 pickupPos = transform.position; // 记录拾取物的初始位置
            // 动画循环：直到进度达到1
            while (t < 1)
            {
                t += Time.deltaTime * timeScale; // 累加动画进度
                // 通过缓动函数计算插值进度（EaseInBack：先回弹后加速的缓动效果）
                float lerpT = EasingUtils.EaseInBack(t, c);
                if (lerpT >= 1) break; // 若插值进度达到1，提前结束循环

                // 非限制插值计算，更新拾取物位置（从初始位置飞向玩家中心）
                transform.position = Vector3.LerpUnclamped(pickupPos, playerCharacter.CenterTransform.position, lerpT);
                yield return null; // 等待下一帧，继续执行动画
            }

            // 动画结束后，强制将拾取物位置设为玩家中心位置
            transform.position = playerCharacter.CenterTransform.position;
            yield return null; // 等待下一帧
            OnCollected(); // 执行拾取后的具体逻辑
        }

        /// <summary>
        /// 拾取物飞向玩家背包槽位的协程（虚拟方法，支持子类重写）
        /// </summary>
        /// <param name="inventorySlot">目标背包槽位</param>
        /// <param name="collectionMode">收集模式（地面拾取/宝箱拾取）</param>
        /// <returns>协程迭代器</returns>
        protected virtual IEnumerator FlyToInventory(InventorySlot inventorySlot, CollectionMode collectionMode = CollectionMode.FromGround)
        {
            // 将当前拾取物添加到背包槽位的"待收集物品"列表中
            inventorySlot.AddItemBeingCollected(this);

            // 若收集模式为宝箱拾取，先播放宝箱生成动画
            if (collectionMode == CollectionMode.FromChest)
                yield return StartCoroutine(ChestAnimation());

            float t = 0; // 动画进度（0=未开始，1=完成）
            float c = 0; // 缓动弹性系数（此处设为0，无回弹效果）
            float timeScale = 2.0f; // 动画时间缩放系数（控制动画播放速度）
            t = -Time.deltaTime * timeScale; // 初始化动画进度，扣除一帧时间

            Vector3 pickupPos = transform.position; // 记录拾取物的初始位置
            // 动画循环：直到进度达到1
            while (t < 1)
            {
                t += Time.deltaTime * timeScale; // 累加动画进度
                // 计算插值进度（无回弹的缓动效果）
                float lerpT = EasingUtils.EaseInBack(t, c);
                if (lerpT >= 1) break; // 插值进度达到1，提前结束循环

                // 将背包槽位的屏幕坐标转换为世界坐标，作为目标位置
                Vector3 targetPos = Camera.main.ScreenToWorldPoint(inventorySlot.transform.position);
                // 非限制插值计算，更新拾取物位置（从初始位置飞向背包槽位）
                transform.position = Vector3.LerpUnclamped(pickupPos, targetPos, lerpT);
                yield return null; // 等待下一帧，继续执行动画
            }

            // 动画结束后，强制将拾取物位置设为背包槽位的世界坐标
            transform.position = Camera.main.ScreenToWorldPoint(inventorySlot.transform.position);
            yield return null; // 等待下一帧
            gameObject.SetActive(false); // 禁用拾取物对象
            // 通知背包槽位，完成该拾取物的添加操作
            inventorySlot.FinalizeAddItemBeingCollected(this);
        }

        /// <summary>
        /// 拾取物生成动画协程（虚拟方法，支持子类重写）
        /// 实现拾取物生成时的弹跳+随机水平偏移效果
        /// </summary>
        /// <returns>协程迭代器</returns>
        protected virtual IEnumerator SpawnAnimation()
        {
            zPositioner.enabled = false; // 生成动画期间禁用Z轴定位器，避免干扰动画位置
            float t = 0; // 动画进度（0=未开始，1=完成）
            // 随机生成水平移动速度（在[-saOffsetMax, saOffsetMax]范围内）
            float horizontalSpeed = Random.Range(-saOffsetMax, saOffsetMax);
            Vector3 spawnPosition = transform.position; // 记录拾取物的初始生成位置

            // 动画循环：未被收集 且 动画进度未完成
            while (!beingCollected && t < 1)
            {
                // 计算垂直位置：弹跳缓动效果（Bounce），结合弹跳高度
                float verticalPos = EasingUtils.Bounce(t, saHeight);
                // 计算水平位置：随时间线性偏移
                float horizontalPos = horizontalSpeed * t;
                // 更新拾取物位置（初始位置 + 垂直偏移 + 水平偏移）
                transform.position = spawnPosition + Vector3.up * verticalPos + Vector3.right * horizontalPos;

                t += Time.deltaTime * saSpeed; // 累加动画进度
                yield return null; // 等待下一帧，继续执行动画
            }

            col.enabled = true; // 生成动画结束，启用碰撞体，允许被拾取
            zPositioner.enabled = true; // 启用Z轴定位器，恢复层级控制
        }

        /// <summary>
        /// 拾取物从宝箱生成的动画协程（虚拟方法，支持子类重写）
        /// 类似生成动画，但耗时更短，仅播放0.2秒
        /// </summary>
        /// <returns>协程迭代器</returns>
        protected virtual IEnumerator ChestAnimation()
        {
            float t = 0; // 动画进度（0=未开始，0.2=完成）
            // 随机生成水平移动速度（在[-saOffsetMax, saOffsetMax]范围内）
            float horizontalSpeed = Random.Range(-saOffsetMax, saOffsetMax);
            Vector3 spawnPosition = transform.position; // 记录拾取物的初始位置

            // 动画循环：直到进度达到0.2秒
            while (t < 0.2f)
            {
                zPositioner.enabled = false; // 动画期间禁用Z轴定位器
                // 计算垂直弹跳位置 + 水平偏移位置，更新拾取物坐标
                //EasingUtils.Bounce函数专门计算
                float verticalPos = EasingUtils.Bounce(t, saHeight);
                float horizontalPos = horizontalSpeed * t;
                transform.position = spawnPosition + Vector3.up * verticalPos + Vector3.right * horizontalPos;

                t += Time.deltaTime * saSpeed; // 累加动画进度
                yield return null; // 等待下一帧，继续执行动画
            }
        }

        /// <summary>
        /// 2D触发器持续碰撞回调
        /// 当拾取物碰撞体与玩家拾取碰撞体持续重叠时，触发拾取逻辑
        /// </summary>
        /// <param name="collider">碰撞到的2D碰撞体</param>
        void OnTriggerStay2D(Collider2D collider)
        {
            // 判断碰撞对象是否为玩家的拾取专用碰撞体
            if (collider == playerCharacter.CollectableCollider)
            {
                Collect(); // 触发拾取逻辑
            }
        }

        /// <summary>
        /// 收集模式枚举
        /// 定义拾取物的两种来源场景
        /// </summary>
        public enum CollectionMode
        {
            FromGround, // 从地面拾取（默认场景）
            FromChest   // 从宝箱中拾取（特殊场景，播放专属动画）
        }
    }
}