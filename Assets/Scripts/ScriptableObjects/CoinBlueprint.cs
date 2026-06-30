using UnityEngine;

namespace Vampire
{
    [CreateAssetMenu(fileName = "Coin", menuName = "Blueprints/Coin", order = 1)]

    //金币/经验宝石外观映射：CoinBlueprint、ExpGemBlueprint
    public class CoinBlueprint : ScriptableObject
    {
        public EnumDataContainer<CoinType, Sprite> coinSprites;
    }
}
