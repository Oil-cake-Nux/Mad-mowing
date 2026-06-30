using UnityEngine;

namespace Vampire
{
    [System.Serializable]
    public class Loot<T>
    {
        [Tooltip("掉落的物品/预制体")]
        public T item;
        [Tooltip("掉落概率（0~1，总和建议为1）")]
        [Range(0,1f)]
        public float dropChance;
        [HideInInspector]
        public CoinType coinType = CoinType.Bronze1;
    }
}