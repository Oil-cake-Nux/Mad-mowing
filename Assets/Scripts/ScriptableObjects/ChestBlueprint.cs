using UnityEngine;

namespace Vampire
{
    [CreateAssetMenu(fileName = "Chest", menuName = "Blueprints/Chest", order = 1)]

    //±¶œ‰≈‰÷√£∫ChestBlueprint£®±¶œ‰Õ‚π€°¢µÙ¬‰±Ì£©
    public class ChestBlueprint : ScriptableObject
    {
        public bool abilityChest = false;
        public Sprite closedChest;
        public Sprite openingChest;
        public Sprite openChest;
        public LootTable<GameObject> lootTable;
    }
}
