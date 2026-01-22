using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 库存系统类，负责管理物品的存储与相关操作
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        /// <summary>
        /// 存储所有库存槽的数组（可在Inspector面板中设置）
        /// </summary>
        [SerializeField] private InventorySlot[] inventorySlots;
        public EntityManager entityManager;
        /// <summary>
        /// 通过物品类型快速查找对应库存槽的字典
        /// </summary>
        private Dictionary<CollectableType, InventorySlot> inventorySlotByType;

        [SerializeField] public Collectable[] collectables;

        /// <summary>
        /// 初始化库存系统
        /// </summary>
        public void Init()
        {
            // 初始化字典
            inventorySlotByType = new Dictionary<CollectableType, InventorySlot>();
            // 遍历所有库存槽，初始化并添加到字典中
            for (int i = 0; i < inventorySlots.Length; i++)
            {
                InventorySlot inventorySlot = inventorySlots[i];
                inventorySlot.Init();
                // 以物品类型为键，库存槽为值存入字典
                inventorySlotByType[inventorySlot.CollectableType] = inventorySlot;
            }
        }

        /// <summary>
        /// 检查库存中该类型物品是否还有空位
        /// </summary>
        /// <param name="item">要检查的物品</param>
        /// <returns>是否有剩余空间</returns>
        public bool RoomInInventory(Collectable item)
        {
            // 尝试获取物品类型对应的库存槽，若存在且未满则返回true
            if (inventorySlotByType.TryGetValue(item.CollectableType, out InventorySlot inventorySlot))
                return !inventorySlot.IsFull();
            // 不存在对应库存槽则返回false
            return false;
        }

        /// <summary>
        /// 尝试获取指定物品类型对应的库存槽
        /// </summary>
        /// <param name="item">目标物品</param>
        /// <param name="inventorySlot">输出参数，获取到的库存槽</param>
        /// <returns>是否成功获取到库存槽</returns>
        public bool TryGetInventorySlot(Collectable item, out InventorySlot inventorySlot)
        {
            // 尝试从字典中获取对应库存槽
            if (inventorySlotByType.TryGetValue(item.CollectableType, out inventorySlot))
            {
                return true;
            }
            // 未找到则返回null并返回false
            inventorySlot = null;
            return false;
        }

        /// <summary>
        /// 向库存中添加物品
        /// </summary>
        /// <param name="item">要添加的物品</param>
        /// <returns>添加成功的库存槽，失败则返回null</returns>
        public InventorySlot AddItem(Collectable item)
        {
            // 检查是否存在对应库存槽且未满
            if (inventorySlotByType.ContainsKey(item.CollectableType) && !inventorySlotByType[item.CollectableType].IsFull())
            {
                // 向对应库存槽添加物品
                inventorySlotByType[item.CollectableType].AddItem(item);
                return inventorySlotByType[item.CollectableType];
            }
            // 无法添加则返回null
            return null;
        }
    }
}