using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    //可以在Project窗口下右击创建，然后点击以下Blueprints/Leve目录后，会创建一个名称为Level的ScriptableObject文件
    [CreateAssetMenu(fileName = "Level", menuName = "Blueprints/Level", order = 1)]

    //关卡配置类：关卡时长、背景贴图、怪物表、Boss、宝箱、初始经验宝石
    public class LevelBlueprint : ScriptableObject
    {
        [Header("Time")]
        public float levelTime = 600;
        [Header("Background")]
        public Texture2D backgroundTexture;     //背景贴图
        [Header("Abilities")]
        public GameObject[] abilityPrefabs;     //能力预制体数组
        [Header("Monster Settings")]
        public MonstersContainer[] monsters;    // 普通怪物容器数组
        public MiniBossContainer[] miniBosses;  // 小Boss容器数组
        public BossContainer finalBoss;         // 最终Boss容器
        public MonsterSpawnTable monsterSpawnTable; // 怪物生成表（控制何时生成哪种怪物）
        [Header("Chest Settings")]
        public ChestBlueprint chestBlueprint;   // 宝箱蓝图（定义宝箱内容、外观等）
        public float chestSpawnDelay = 30;      // 宝箱生成间隔（默认30秒）
        public float chestSpawnAmount = 2;      // 每次生成的宝箱数量（默认2个）
        [Header("Exp Gem Settings")]
        public int initialExpGemCount = 25; // 初始经验宝石数量（默认25个）
        public GemType initialExpGemType = GemType.White1; // 初始经验宝石类型

        private Dictionary<int, (int, int)> monsterIndexMap;  // 怪物索引映射表
        public Dictionary<int, (int, int)> MonsterIndexMap      
        { 
            get
            {

                if (monsterIndexMap == null)
                {
                    monsterIndexMap = new Dictionary<int, (int, int)>();
                    int monsterIndex = 0;
                    //将普通怪物容器数组中的所有怪物都装进monsterIndexMap怪物索引映射表
                    //存的数据代表，第几种怪的第几个对应的monsters[i].monsterBlueprints[j]中的索引值。同
                    for (int i = 0; i < monsters.Length; i++)
                    {
                        for (int j = 0; j < monsters[i].monsterBlueprints.Length; j++)
                            monsterIndexMap[monsterIndex++] = (i, j);
                    }
                }
                return monsterIndexMap;
            }
        }

        //小怪类
        [System.Serializable]
        public class MonstersContainer
        {
            public GameObject monstersPrefab;   //小怪预制体
            public MonsterBlueprint[] monsterBlueprints;    //小怪种类的数量
        }

        [System.Serializable]
        public class MiniBossContainer : BossContainer
        {
            public float spawnTime;
        }

        [System.Serializable]
        public class BossContainer
        {
            public GameObject bossPrefab;
            public BossMonsterBlueprint bossBlueprint;
        }
    }
}
