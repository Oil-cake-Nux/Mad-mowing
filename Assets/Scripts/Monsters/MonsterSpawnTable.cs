using UnityEngine;



namespace Vampire

{

    /// <summary>

    /// 怪物生成表（数据驱动）

    /// 该类用“关键帧(Keyframe)”的方式描述：

    /// 1) 随时间进度 t（0~1）变化的刷怪速率（spawnRate：每秒刷多少只）

    /// 2) 随时间进度 t（0~1）变化的各怪物出现概率（spawnChances：一个数组，对应怪物索引）

    /// 3) 随时间进度 t（0~1）变化的各怪物血量倍率/增益（healthBuffs：一个数组，对应怪物索引）

    ///

    /// LevelManager 里一般会这样使用：其中progress为游戏进度

    /// - spawnRate = GetSpawnRate(progress)

    /// - (monsterIdx, hpMultiplier) = SelectMonsterWithHPMultiplier(progress)

    ///

    /// 注意：这里的 t 是“关卡进度”，通常 = 当前关卡时间 / 关卡总时长，因此范围是 [0,1]。

    /// </summary>

    //可序列化特性

    [System.Serializable]

    public class MonsterSpawnTable

    {

        // 刷怪速率关键帧：不同 t 区间内，spawnRate 会线性插值

        public SpawnRateKeyframe[] spawnRateKeyframes;



        // 刷怪概率关键帧：不同 t 区间内，每个怪物的 spawnChance 会线性插值

        // spawnChances 数组的下标 j 表示“怪物索引”（与 LevelBlueprint.MonsterIndexMap 的索引体系一致）

        public SpawnChanceKeyframe[] spawnChanceKeyframes;



        // 血量增益/倍率关键帧：不同 t 区间内，每个怪物的 healthBuff 会线性插值

        // healthBuffs 数组的下标 j 同样表示“怪物索引”

        public HPMultiplierKeyframe[] hpMultiplierKeyframes;



        /// <summary>

        /// 根据关卡进度 t（0~1），返回当前刷怪速率 spawnRate。

        /// 实现方式：

        /// - 当 t==0，直接返回第 0 个关键帧的 spawnRate

        /// - 否则找到第一个 keyframe.t >= t 的区间 [i-1, i]

        /// - 在该区间内对 spawnRate 做线性插值

        /// - 如果 t 超过所有关键帧范围，返回 0（表示不再刷怪）

        /// </summary>

        public float GetSpawnRate(float t)

        {

            // t==0 直接用第一个关键帧数值（避免 Remap01 产生除零或边界问题）

            if (t == 0)

                return spawnRateKeyframes[0].spawnRate;



            // 从第 1 个关键帧开始找：因为插值要用到 i-1

            for (int i = 1; i < spawnRateKeyframes.Length; i++)

            {

                // 找到“当前 t 落入的关键帧区间”：第一个满足 keyframe.t >= t 的 i

                if (spawnRateKeyframes[i].t >= t)

                {

                    // 计算 t 在区间 [i-1, i] 的归一化位置（0~1）

                    float lerpT = Remap01(spawnRateKeyframes[i - 1].t, spawnRateKeyframes[i].t, t);



                    // 对区间端点的 spawnRate 做线性插值，得到当前刷怪速率

                    return Mathf.Lerp(spawnRateKeyframes[i - 1].spawnRate, spawnRateKeyframes[i].spawnRate, lerpT);

                }

            }



            // 超过所有关键帧：不再刷怪

            return 0;

        }



        /// <summary>

        /// 根据关卡进度 t（0~1），选择一个怪物索引，并同时返回该怪物此时对应的血量倍率/增益。

        ///

        /// 返回值：(monsterIndex, hpMultiplierOrBuff)

        /// - monsterIndex：刷出来的怪物“索引”（用于 LevelBlueprint.MonsterIndexMap 映射到具体 prefab/蓝图）

        /// - hpBuff：本次刷出的怪物血量倍率/增益（用于随时间增加难度）

        ///

        /// 内部流程：

        /// 1) 根据 spawnChanceKeyframes 在 t 时刻的概率分布抽取怪物索引

        /// 2) 再根据 hpMultiplierKeyframes 在 t 时刻对该怪物索引取 hpBuff（同样线性插值）

        /// </summary>

        public (int, float) SelectMonsterWithHPMultiplier(float t)

        {

            // t==0：特殊处理

            // 这里调用 SelectMonster(1, t)：意味着使用 [0,1] 这两个概率关键帧做插值抽取

            // 然后从 hpMultiplierKeyframes[0] 取对应怪物的 healthBuff（若存在 hp buff 表）

            if (t == 0)

            {

                int monsterIdx = SelectMonster(1, t);

                float hpBuff = hpMultiplierKeyframes.Length > 0 ? hpMultiplierKeyframes[0].healthBuffs[monsterIdx] : 0;

                return (monsterIdx, hpBuff);

            }



            // 遍历概率关键帧，找到 t 所在区间并抽怪

            for (int i = 0; i < spawnChanceKeyframes.Length; i++)

            {

                if (spawnChanceKeyframes[i].t >= t)

                {

                    // 在区间 [i-1, i] 内按插值后的概率分布抽一个怪物索引

                    int monsterIdx = SelectMonster(i, t);



                    // 再遍历 HP 倍率关键帧，找到 t 所在区间并计算该怪物的 hp buff

                    // 注意：这里复用了变量 i（把外层 i 改写），属于一种“写法不太推荐但逻辑成立”的实现；

                    // 我们不改源码，只解释其含义：这里第二个循环的 i 是 HP 关键帧索引。

                    for (i = 0; i < hpMultiplierKeyframes.Length; i++)

                    {

                        if (hpMultiplierKeyframes[i].t >= t)

                        {

                            // 在区间 [i-1, i] 内对该怪物 j 的 healthBuff 做插值，得到当前 hpBuff

                            return (monsterIdx, GetHPBuff(i, monsterIdx, t));

                        }

                    }



                    // 如果没有找到 HP 关键帧区间，返回 -1（异常）和 0 buff

                    return (-1, 0);

                }

            }



            // 未找到概率区间（异常/配置问题），返回 -1

            return (-1, 0);

        }



        /// <summary>

        /// 仅选择怪物索引（不返回 hpBuff）

        /// - t==0 时走 SelectMonster(1,t)（用第 0 和第 1 个概率关键帧插值）

        /// - 否则找 t 落入的概率关键帧区间 [i-1,i] 并抽取怪物

        /// </summary>

        public int SelectMonster(float t)

        {

            if (t == 0)

                return SelectMonster(1, t);



            for (int i = 0; i < spawnChanceKeyframes.Length; i++)

            {

                if (spawnChanceKeyframes[i].t >= t)

                {

                    return SelectMonster(i, t);

                }

            }



            // t 超出概率关键帧范围（或配置不全），返回 -1

            return -1;

        }



        /// <summary>

        /// 在指定概率关键帧区间 [i-1, i] 内按“插值后的概率分布”抽取怪物索引。

        ///

        /// 实现：轮盘赌（Roulette Wheel Selection）

        /// - rand ∈ [0,1)

        /// - cumulative 累加每个怪物 j 在时刻 t 的概率 p_j

        /// - rand 落在哪段累加区间，就选中哪个 j

        ///

        /// 关键点：

        /// - 每个怪物的概率 p_j 由：Lerp( keyframe[i-1].spawnChances[j], keyframe[i].spawnChances[j], tLerp )

        /// - 其中 tLerp 是 t 在区间 [keyframe[i-1].t, keyframe[i].t] 的归一化位置

        ///

        /// 注意：这里默认 spawnChances 是一组“概率”（最好总和≈1）。

        /// 如果配置总和 < 1，则 rand 落在尾部可能抽不到，最终返回 -1。

        /// </summary>

        private int SelectMonster(int i, float t)

        {

            // 在 0~1 之间取随机数，用于按概率抽取

            float rand = Random.Range(0f, 1.0f);

            float cumulative = 0;



            // 当前 t 在该区间内的位置（0~1）

            float tLerp = Remap01(spawnChanceKeyframes[i - 1].t, spawnChanceKeyframes[i].t, t);



            // 遍历每个怪物索引 j：累加其插值后的概率

            for (int j = 0; j < spawnChanceKeyframes[i - 1].spawnChances.Length; j++)

            {

                // p_j(t) = Lerp( p_j(i-1), p_j(i), tLerp )

                cumulative += Mathf.Lerp(spawnChanceKeyframes[i - 1].spawnChances[j], spawnChanceKeyframes[i].spawnChances[j], tLerp);



                // rand 落入当前累计区间：选中该怪物 j

                if (rand < cumulative)

                    return j;

            }



            // 没抽到：通常意味着概率配置总和 < 1 或 keyframe 数组长度不一致等配置问题

            return -1;

        }



        /// <summary>

        /// 在指定 HP 关键帧区间 [i-1, i] 内，计算怪物索引 j 在时刻 t 的 HP buff（线性插值）。

        /// </summary>

        private float GetHPBuff(int i, int j, float t)

        {

            // 当前 t 在 HP 区间内的位置（0~1）

            float tLerp = Remap01(hpMultiplierKeyframes[i - 1].t, hpMultiplierKeyframes[i].t, t);



            // 对该怪物 j 的 healthBuff 进行插值

            return Mathf.Lerp(hpMultiplierKeyframes[i - 1].healthBuffs[j], hpMultiplierKeyframes[i].healthBuffs[j], tLerp);

        }



        /// <summary>

        /// 将 t 从区间 [min,max] 映射到 [0,1]

        /// 例如：min=0.2, max=0.5, t=0.35 -> 0.5

        /// 注意：若 max==min 会除零，因此上层一般对 t==0 做了特殊处理。

        /// </summary>

        private float Remap01(float min, float max, float t)

        {

            return (t - min) / (max - min);

        }



        // =============================

        // 以下是用于 Inspector 配置的关键帧数据结构

        // =============================



        /// <summary>

        /// 刷怪速率关键帧：

        /// t：关卡进度（0~1）

        /// spawnRate：该时刻的刷怪速率（单位：只/秒）

        /// </summary>

        [System.Serializable]

        public class SpawnRateKeyframe

        {

            public float t;

            public float spawnRate;

        }



        /// <summary>

        /// 刷怪概率关键帧：

        /// t：关卡进度（0~1）

        /// spawnChances：每种怪物在该时刻的出现概率数组（下标 j 对应怪物索引）

        /// </summary>

        [System.Serializable]

        public class SpawnChanceKeyframe
        {

            public float t;

            public float[] spawnChances;

        }



        /// <summary>

        /// HP 倍率/增益关键帧：

        /// t：关卡进度（0~1）

        /// healthBuffs：每种怪物在该时刻的血量倍率/增益数组（下标 j 对应怪物索引）

        /// </summary>

        [System.Serializable]

        public class HPMultiplierKeyframe

        {

            public float t;

            public float[] healthBuffs;

        }

    }

}

