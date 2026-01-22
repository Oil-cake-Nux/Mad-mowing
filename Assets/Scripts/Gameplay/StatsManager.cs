using UnityEngine;
using TMPro;

namespace Vampire
{
    /// <summary>
    /// 游戏状态统计管理器
    /// 负责统计游戏核心数据（击杀数、金币数、造成/受到的伤害），并同步更新对应UI显示
    /// </summary>
    public class StatsManager : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI monstersKilledText; // 显示"击杀怪物数量"的UI文本组件（TextMeshPro）
        [SerializeField] private TextMeshProUGUI coinsGainedText;    // 显示"获得金币数量"的UI文本组件（TextMeshPro）

        private int monstersKilled = 0; // 私有变量：记录击杀怪物的总数量
        private float damageDealt = 0; // 私有变量：记录玩家造成的总伤害值
        private float damageTaken = 0; // 私有变量：记录玩家受到的总伤害值
        private int coinsGained = 0;   // 私有变量：记录玩家获得的总金币数量

        /// <summary>
        /// 公共只读属性：供外部获取击杀怪物的总数量（无法外部修改，仅内部更新）
        /// </summary>
        public int MonstersKilled { get => monstersKilled; }
        /// <summary>
        /// 公共只读属性：供外部获取玩家造成的总伤害值（无法外部修改，仅内部更新）
        /// </summary>
        public float DamageDealt { get => damageDealt; }
        /// <summary>
        /// 公共只读属性：供外部获取玩家受到的总伤害值（无法外部修改，仅内部更新）
        /// </summary>
        public float DamageTaken { get => damageDealt; }
        /// <summary>
        /// 公共只读属性：供外部获取玩家获得的总金币数量（无法外部修改，仅内部更新）
        /// </summary>
        public int CoinsGained { get => coinsGained; }

        /// <summary>
        /// 增加击杀怪物数量（每次调用击杀数+1），并同步更新对应UI显示
        /// </summary>
        public void IncrementMonstersKilled()
        {
            monstersKilled++; // 击杀数自增1
            monstersKilledText.text = monstersKilled.ToString(); // 将击杀数转为字符串，更新UI文本
        }

        /// <summary>
        /// 增加获得的金币数量，并同步更新对应UI显示
        /// </summary>
        /// <param name="amount">本次要增加的金币数量</param>
        public void IncreaseCoinsGained(int amount)
        {
            coinsGained += amount; // 累加本次金币数量到总金币数
            coinsGainedText.text = coinsGained.ToString(); // 将总金币数转为字符串，更新UI文本
        }

        /// <summary>
        /// 增加玩家造成的总伤害值（仅累加数据，无UI更新）
        /// </summary>
        /// <param name="damage">本次造成的伤害值</param>
        public void IncreaseDamageDealt(float damage)
        {
            damageDealt += damage; // 累加本次伤害到总造成伤害值
        }

        /// <summary>
        /// 增加玩家受到的总伤害值（仅累加数据，无UI更新）
        /// </summary>
        /// <param name="damage">本次受到的伤害值</param>
        public void IncreaseDamageTaken(float damage)
        {
            damageTaken += damage; // 累加本次伤害到总受到伤害值
        }
    }
}