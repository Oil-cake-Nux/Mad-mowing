using UnityEngine;

namespace Vampire
{
    public class MonsterBlueprint : ScriptableObject
    {
        [Header("Stats")]
        public new string name;  // 名字
        public float hp;  // 血量
        public float atk;  // 攻击力
        public float recovery;  // 血量恢复再生率
        public float armor;  // 装甲减伤
        public float atkspeed;  // 攻击速度
        public float movespeed;  // 移动速度
        public float acceleration;
        [Header("Drops")]
        public LootTable<GemType> gemLootTable;
        public LootTable<CoinType> coinLootTable;
        [Header("Animation")]
        public Sprite[] walkSpriteSequence;
        public float walkFrameTime;
    }
}
