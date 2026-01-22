using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 无限背景处理类，用于实现背景随玩家移动的无限滚动效果
    /// </summary>
    public class InfiniteBackground : MonoBehaviour
    {
        private Transform playerTransform; // 玩家的Transform组件，用于跟踪玩家位置
        [SerializeField] private Material backgroundMaterial; // 背景材质，用于控制背景渲染效果
        private Vector2 previousResetPosition = Vector2.zero; // 上一次重置背景时的位置
        private Vector2 resetOffset = Vector2.zero; // 重置偏移量，用于累积背景重置的总偏移
        private float resetDistance = 15; // 触发背景重置的距离阈值
        private float resetDuration = 5; // 背景重置动画的持续时间

        void Awake()
        {
            // 计算世界空间中的屏幕大小，以便在屏幕外生成敌人（此处用于适配背景大小）
            // 将视口左下角（0,0）转换为世界坐标
            Vector2 bottomLeft = Camera.main.ViewportToWorldPoint(new Vector3(0, 0, Camera.main.nearClipPlane));
            // 将视口右上角（1,1）转换为世界坐标
            Vector2 topRight = Camera.main.ViewportToWorldPoint(new Vector3(1, 1, Camera.main.nearClipPlane));
            // 计算世界空间中屏幕的宽高
            Vector3 screenSizeWorldSpace = new Vector3(topRight.x - bottomLeft.x, topRight.y - bottomLeft.y, 1);
            // 设置背景的缩放以适配屏幕大小
            transform.localScale = screenSizeWorldSpace;
            // 将背景材质赋值给MeshRenderer
            GetComponent<MeshRenderer>().sharedMaterial = backgroundMaterial;
        }

        /// <summary>
        /// 初始化背景
        /// </summary>
        /// <param name="backgroundTexture">背景纹理</param>
        /// <param name="playerTransform">玩家的Transform组件</param>
        public void Init(Texture2D backgroundTexture, Transform playerTransform)
        {
            this.playerTransform = playerTransform; // 保存玩家Transform引用
            backgroundMaterial.mainTexture = backgroundTexture; // 设置背景材质的主纹理
            backgroundMaterial.SetFloat("_Shockwave", 0); // 初始化冲击波效果参数为0（无效果）
            resetOffset = Vector2.zero; // 重置偏移量归零
            backgroundMaterial.SetVector("_ResetOffset", resetOffset); // 同步材质中的重置偏移量
            backgroundMaterial.SetInt("_Resetting", 0); // 重置材质中的重置状态（0表示未重置）
        }

        /// <summary>
        /// 冲击波效果协程，用于实现从玩家位置向外扩散的冲击波视觉效果
        /// </summary>
        /// <param name="distance">冲击波的最大传播距离</param>
        /// <returns>协程迭代器</returns>
        public IEnumerator Shockwave(float distance)
        {
            float d = 0; // 当前冲击波传播的距离
            // 当未达到最大距离时，持续更新冲击波效果
            while (d < distance)
            {
                // 随时间增加冲击波传播距离（速度为16单位/秒）
                d += Time.deltaTime * 16;
                // 更新材质中的冲击波距离参数
                backgroundMaterial.SetFloat("_Shockwave", d);
                // 更新材质中的玩家位置参数（用于定位冲击波中心）
                backgroundMaterial.SetVector("_PlayerPosition", playerTransform.position);
                yield return null; // 等待下一帧
            }
            // 冲击波结束后重置参数
            backgroundMaterial.SetFloat("_Shockwave", 0);
        }

        private void Update()
        {
            // 计算玩家当前位置与上一次重置位置的偏移向量
            Vector2 toReset = previousResetPosition - (Vector2)playerTransform.position;
            // 当偏移量的平方大于阈值的平方（避免开方运算，提高性能）时，触发背景重置
            if (toReset.sqrMagnitude > resetDistance * resetDistance)
            {
                // 启动背景重置协程
                StartCoroutine(ResetBackground(toReset));

                // 更新上一次重置位置为当前玩家位置
                previousResetPosition = playerTransform.position;
            }
        }

        /// <summary>
        /// 背景重置协程，用于平滑过渡背景位置，实现无限滚动效果
        /// </summary>
        /// <param name="toReset">需要重置的偏移量</param>
        /// <returns>协程迭代器</returns>
        private IEnumerator ResetBackground(Vector2 toReset)
        {
            backgroundMaterial.SetInt("_Resetting", 1); // 通知材质开始重置（1表示正在重置）
            backgroundMaterial.SetVector("_TempResetOffset", toReset); // 传递临时重置偏移量给材质

            float t = 0; // 重置动画的时间进度（0到1）
            // 当动画未结束时，持续更新过渡效果
            while (t < resetDuration)
            {
                t += Time.deltaTime; // 累加时间
                // 计算并更新材质中的重置混合参数（0表示初始状态，1表示完成过渡）
                backgroundMaterial.SetFloat("_ResetBlend", t / resetDuration);
                yield return null; // 等待下一帧
            }

            // 重置完成后，更新总重置偏移量
            resetOffset += toReset;
            // 同步材质中的总重置偏移量
            backgroundMaterial.SetVector("_ResetOffset", resetOffset);
            // 通知材质结束重置状态
            backgroundMaterial.SetInt("_Resetting", 0);
        }
    }
}