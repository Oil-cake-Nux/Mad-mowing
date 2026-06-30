using UnityEngine;

namespace Vampire
{
    [CreateAssetMenu(fileName = "Chest", menuName = "Blueprints/Chest", order = 1)]

    //宝箱配置：ChestBlueprint（宝箱外观、掉落表）
    public class ChestBlueprint : ScriptableObject
    {
        public bool abilityChest = false;
        public Sprite closedChest;
        public Sprite openingChest;
        public Sprite openChest;
        public LootTable<GameObject> lootTable;
    }
}
