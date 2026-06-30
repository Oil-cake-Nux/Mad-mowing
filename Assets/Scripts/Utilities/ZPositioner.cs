using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// ZPositioner：用于“2D 顶视角/俯视角”游戏的伪层级排序（Pseudo Depth Sorting）
    /// ------------------------------------------------------------
    /// 目的：
    /// - Unity 2D 中，Sprite 的遮挡通常靠 Sorting Layer / Order in Layer 控制；
    /// - 但本项目采用另一种常见技巧：根据物体在 Y 轴的位置动态计算 Z 值，
    ///   让“Y 更小（更靠下）的物体”在视觉上更靠前（更近镜头），从而自然遮挡。
    ///
    /// 核心思想：
    /// - 用相对玩家的 Y 位置差来设置 Z：
    ///   z = scale * ( objectY - playerY )
    ///   这样：物体越靠下（objectY 越小），z 越小/越靠前（取决于相机投影与渲染设置）
    ///
    /// 备注：
    /// - 这里放在 LateUpdate() 中执行，确保当帧内物体位置（尤其是跟随/移动）都更新完后再统一排序，
    ///   避免“本帧抖动”或排序落后一个帧的问题。
    /// </summary>
    public class ZPositioner : MonoBehaviour
    {
        /// <summary>
        /// 玩家 Transform（用来做相对 Y 计算）
        /// </summary>
        private Transform playerTransform;

        /// <summary>
        /// 缩放系数：把“Y 坐标差”缩小成合理的 Z 值范围。
        /// 例如 Y 差 100，scale=0.01，则 Z=1。
        /// 防止 Z 变化过大导致摄像机裁剪/渲染异常。
        /// </summary>
        private float scale = 0.01f;

        /// <summary>
        /// 是否启用“手动设置 Z 所使用的 Y 值”
        /// - false：使用物体自身的 transform.position.y 作为计算基准
        /// - true：使用 manualY 作为计算基准（常用于：投射物落点、特效中心等不等于物体当前 y 的情况）
        /// </summary>
        private bool manuallySetZ = false;

        /// <summary>
        /// 手动模式下使用的“用于计算 Z 的 Y 值”
        /// </summary>
        private float manualY;

        /// <summary>
        /// 初始化：把玩家 Transform 注入进来
        /// 通常由某个管理器（例如 EntityManager/LevelManager）在生成物体后调用。
        /// </summary>
        public void Init(Transform playerTransform)
        {
            this.playerTransform = playerTransform;
        }

        /// <summary>
        /// LateUpdate：在本帧所有 Update/移动逻辑执行完成后，更新当前物体的 z 值
        /// 计算公式：
        /// temp.z = scale * ( (基准Y) - playerY )
        /// 其中基准Y：
        /// - 若 manuallySetZ 为 true：用 manualY
        /// - 否则：用当前 transform.position.y
        ///
        /// 直观效果：
        /// - 当物体 y 比玩家 y 更大（在玩家“上方”）：(基准Y - playerY) > 0 → z > 0
        /// - 当物体 y 比玩家 y 更小（在玩家“下方”）：(基准Y - playerY) < 0 → z < 0
        /// 在“相机朝 -Z 看”的常见设置下，Z 更小往往意味着更靠近相机，更容易遮挡别的对象。
        /// </summary>
        void LateUpdate()
        {
            Vector3 temp = transform.position;

            // 计算 z：基于 “(物体Y或手动Y) - 玩家Y”
            temp.z = scale * ((manuallySetZ ? manualY : temp.y) - playerTransform.position.y);

            transform.position = temp;
        }

        /// <summary>
        /// 手动指定“用于计算 Z 的 Y 值”
        /// 典型用法：
        /// - 某些对象的 transform.position.y 不是它“视觉上应该用于排序的 y”
        ///   例如：投射物飞在空中，但落点在地面；你希望它按落点 y 排序而不是按当前位置 y 排序。
        /// - 或者某些特效对象高度变化很大，希望锁定一个固定 y 来稳定遮挡关系。
        /// </summary>
        public void ManuallySetZByY(float y)
        {
            manuallySetZ = true;
            manualY = y;
        }

        /// <summary>
        /// 取消手动模式：恢复使用物体自身 transform.position.y 作为排序基准
        /// </summary>
        public void AutomaticallySetZ()
        {
            manuallySetZ = false;
        }
    }
}
