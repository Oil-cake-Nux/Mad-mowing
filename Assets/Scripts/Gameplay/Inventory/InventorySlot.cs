using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
//using static UnityEditor.Progress;

namespace Vampire
{
    /// <summary>
    /// 库存槽类，用于管理单个库存槽中的可收集物品，挂载在道具按钮UI上
    /// </summary>
    public class InventorySlot : MonoBehaviour
    {
        /// <summary>
        /// 可收集物品类型（带SerializeField属性，可在Inspector面板中设置）
        /// </summary>
        [field: SerializeField] public CollectableType CollectableType;
        /// <summary>
        /// 显示物品数量的游戏物体
        /// </summary>
        [SerializeField] private GameObject countObject;
        /// <summary>
        /// 显示物品数量的文本组件
        /// </summary>
        [SerializeField] private TextMeshProUGUI countText;
        /// <summary>
        /// 物品图标对应的图像组件
        /// </summary>
        [SerializeField] private Image itemImage;
        /// <summary>
        /// 存储当前槽位中已确认的物品列表
        /// </summary>
        private List<Collectable> items;
        /// <summary>
        /// 存储正在添加到槽位中的物品的快速列表（临时状态）
        /// 存在原因，因为要播放各种动画，所以先将物品存储到该列表，然后播放完
        /// </summary>
        private FastList<Collectable> itemsBeingAdded;
        private EntityManager entityManager;
        //public Collectable collectable;
        //private Bomb bomb;
        //private Health health;
        //private Magnet magnet;
        //private RedPotion RedPotion;
        /// <summary>
        /// 初始化库存槽
        /// </summary>
        public void Init()
        {
            this.entityManager = entityManager;
            items = new List<Collectable>();
            itemsBeingAdded = new FastList<Collectable>();
            countObject.SetActive(false); // 初始隐藏数量显示
            itemImage.color = new Color(1, 1, 1, 0.5f); // 初始物品图标设为半透明（未激活状态）
 
            //AddItem(collectable);
                
            //InitNum();
        }
       
        /// <summary>
        /// 将正在收集的物品添加到临时列表
        /// </summary>
        /// <param name="item">正在收集的物品</param>
        public void AddItemBeingCollected(Collectable item)
        {
            itemsBeingAdded.Add(item);
        }

        /// <summary>
        /// 完成正在收集物品的添加流程（从临时列表移除并正式加入物品列表）
        /// </summary>
        /// <param name="item">需要完成添加的物品</param>
        public void FinalizeAddItemBeingCollected(Collectable item)
        {
            if (itemsBeingAdded.Contains(item))
                itemsBeingAdded.Remove(item);
            AddItem(item);
        }

        /// <summary>
        /// 向槽位正式添加物品
        /// </summary>
        /// <param name="item">要添加的物品</param>
        public void AddItem(Collectable item)
        {
            items.Add(item);
            countText.text = items.Count.ToString(); // 更新数量显示文本
            //如果countObject没有处于激活状态
            if (!countObject.activeInHierarchy)
            {
                countObject.SetActive(true); // 激活数量显示
                itemImage.color = Color.white; // 物品图标设为不透明（激活状态）
            }
        }

        /// <summary>
        /// 使用槽位中的物品（使用第一个物品并移除）
        /// </summary>
        public void UseItem()
        {
            if (items.Count > 0)
            {
                items[0].Use(); // 使用列表中的第一个物品
                items.RemoveAt(0); // 移除已使用的物品
                countText.text = items.Count.ToString(); // 更新数量显示

                if (items.Count == 0)
                {
                    countObject.SetActive(false); // 物品数量为0时隐藏数量显示
                    itemImage.color = new Color(1, 1, 1, 0.5f); // 物品图标恢复半透明（未激活状态）
                }
            }
        }

        /// <summary>
        /// 判断槽位是否已满
        /// </summary>
        /// <returns>若当前物品数+正在添加的物品数达到堆叠上限则返回true，否则返回false</returns>
        public bool IsFull()
        {
            return items.Count + itemsBeingAdded.Count >= CollectableType.inventoryStackSize;
        }
    }
}