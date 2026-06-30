using UnityEngine;

namespace Vampire
{
    [CreateAssetMenu(fileName = "Exp Gem", menuName = "Blueprints/Gem", order = 1)]

    //经验宝石外观映射
    public class ExpGemBlueprint : ScriptableObject
    {
        public EnumDataContainer<GemType, Sprite, Color> gemSpritesAndColors;
    }
}
