using UnityEngine;

namespace Vampire
{
    [System.Serializable] // 使该类可在Unity编辑器中序列化，方便可视化配置
    public class LootTable<T>
    {
        //Loot<T>为带有掉落概率的掉落物类型
        public Loot<T>[] lootTable; // 存储战利品项的数组，每个元素包含物品及其掉落概率

        /// <summary>
        /// 用于战利品概率总和不为100%的战利品表（即不总是掉落战利品）。
        /// </summary>
        /// <param name="loot">输出参数，成功时为掉落的物品，失败时为默认值</param>
        /// <returns>是否成功掉落物品</returns>
        public bool TryDropLoot(out T loot)
        {
            if (TryDropLootObject(out Loot<T> lootObject))
            {
                loot = lootObject.item;
                return true;
            }
            else
            {
                loot = default(T);
                return false;
            }
        }

        /// <summary>
        /// 用于战利品概率总和不为100%的战利品表（即不总是掉落战利品）。
        /// </summary>
        /// <param name="loot">输出参数，成功时为掉落的战利品对象（包含物品和概率），失败时为null</param>
        /// <returns>是否成功掉落战利品</returns>
        public bool TryDropLootObject(out Loot<T> loot)
        {
            float rand = Random.Range(0f, 1.0f); // 生成0到1之间的随机数
            float cumulative = 0; // 累计概率值
            foreach (Loot<T> drop in lootTable)
            {
                cumulative += drop.dropChance; // 累加当前战利品的掉落概率
                if (rand < cumulative) // 当随机数小于累计概率时，选中当前战利品
                {
                    loot = drop;
                    return true;
                }
            }
            loot = null; // 若未选中任何战利品，返回null
            return false;
        }

        /// <summary>
        /// 用于战利品概率总和为100%的战利品表（即总是掉落战利品）。
        /// </summary>
        /// <returns>掉落的物品</returns>
        public T DropLoot()
        {
            return DropLootObject().item;
        }

        /// <summary>
        /// 用于战利品概率总和为100%的战利品表（即总是掉落战利品）。
        /// </summary>
        /// <returns>掉落的战利品对象（包含物品和概率）</returns>
        public Loot<T> DropLootObject()
        {
            float rand = Random.Range(0f, 1.0f); // 生成0到1之间的随机数
            float cumulative = 0; // 累计概率值
            foreach (Loot<T> drop in lootTable)
            {
                cumulative += drop.dropChance; // 累加当前战利品的掉落概率
                if (rand < cumulative) // 当随机数小于累计概率时，选中当前战利品
                {
                    return drop;
                }
            }
            // 容错处理：应对浮点精度误差或战利品表配置错误（概率总和不足100%）
            if (lootTable.Length > 0)
            {
                Debug.LogError("战利品掉落失败，请确保所有掉落概率总和为100%。");
                return lootTable[lootTable.Length - 1]; // 返回最后一个战利品作为保底
            }
            Debug.LogError("战利品掉落失败，战利品表为空。");
            return null;
        }
    }
}