using System.Collections;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 对话框组件
    /// 负责对话框的显示/隐藏、动画播放，以及对话框之间的上下导航切换
    /// </summary>
    public class DialogBox : MonoBehaviour
    {
        // 是否立即显示对话框（跳过打开动画，直接显示完整尺寸）
        [SerializeField] private bool appearInstantly = false;
        // 对话框缩放动画的播放速度（值越大，动画播放越快）
        [SerializeField] private float animationSpeed;
        // 上一个对话框（用于返回上一页对话）/ 下一个对话框（用于继续下一页对话，实现对话框导航）
        [SerializeField] private DialogBox previousDialog, nextDialog;

        /// <summary>
        /// 打开对话框（公共方法，支持子类重写）
        /// </summary>
        public virtual void Open()
        {
            // 激活对话框游戏对象，使其可见
            gameObject.SetActive(true);
            // 如果设置为立即显示，直接将缩放设为1（完整尺寸），跳过动画
            if (appearInstantly)
            {
                transform.localScale = Vector3.one;
            }
            else
            {
                // 停止所有正在运行的协程（避免动画叠加混乱）
                StopAllCoroutines();
                // 启动打开动画协程
                StartCoroutine(OpenAnimation());
            }
        }

        /// <summary>
        /// 关闭对话框（公共方法，支持子类重写）
        /// </summary>
        public virtual void Close()
        {
            // 将对话框缩放设为0（隐藏）
            transform.localScale = Vector3.zero;
            // 禁用对话框游戏对象，使其不可见
            gameObject.SetActive(false);
            // 以下是关闭动画的注释代码（当前未启用，保留可后续启用）
            // StopAllCoroutines();
            // StartCoroutine(CloseAnimation());
        }

        /// <summary>
        /// 返回上一个对话框（导航逻辑）
        /// </summary>
        public void Return()
        {
            // 空值安全调用：如果存在上一个对话框，则打开它（避免空引用错误）
            previousDialog?.Open();
            // 关闭当前对话框
            Close();
        }

        /// <summary>
        /// 继续下一个对话框（导航逻辑）
        /// </summary>
        public void Continue()
        {
            // 空值安全调用：如果存在下一个对话框，则打开它（避免空引用错误）
            nextDialog?.Open();
            // 关闭当前对话框
            Close();
        }

        /// <summary>
        /// 对话框打开动画协程（缩放从0到1，带回弹缓动效果）
        /// </summary>
        private IEnumerator OpenAnimation()
        {
            float t = 0; // 动画进度（0=未开始，1=完成）
            // 循环直到动画进度达到1
            while (t < 1)
            {
                // 1.  使用EaseOutBack缓动函数计算插值，实现“超出再回弹”的平滑缩放效果
                // 2.  LerpUnclamped：不限制插值范围，配合EaseOutBack实现回弹效果
                // 3.  缩放从Vector3.zero（完全隐藏）插值到Vector3.one（完整显示）
                transform.localScale = Vector3.LerpUnclamped(Vector3.zero, Vector3.one, EasingUtils.EaseOutBack(t));
                // 累加动画进度（使用unscaledDeltaTime，不受游戏时间缩放影响，如暂停时仍可播放动画）
                t += Time.unscaledDeltaTime * animationSpeed;
                yield return null; // 等待下一帧，继续执行动画
            }
            // 动画结束后，强制将缩放设为1，避免因浮点误差导致尺寸不全
            transform.localScale = Vector3.one;
        }

        /// <summary>
        /// 对话框关闭动画协程（缩放从1到0，带快速衰减缓动效果）
        /// 注：当前代码中未被调用（Close方法里已注释相关逻辑）
        /// </summary>
        private IEnumerator CloseAnimation()
        {
            float t = 0; // 动画进度（0=未开始，1=完成）
            // 循环直到动画进度达到1
            while (t < 1)
            {
                // 1.  使用EaseOutQuart缓动函数计算插值，实现“先慢后快”的平滑缩放隐藏效果
                // 2.  Lerp：限制插值范围在0~1之间，缩放从Vector3.one（完整显示）插值到Vector3.zero（完全隐藏）
                transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, EasingUtils.EaseOutQuart(t));
                // 累加动画进度（使用deltaTime，受游戏时间缩放影响）
                t += Time.deltaTime * animationSpeed;
                yield return null; // 等待下一帧，继续执行动画
            }
            // 动画结束后，强制将缩放设为0，确保完全隐藏
            transform.localScale = Vector3.zero;
            // 禁用对话框游戏对象
            gameObject.SetActive(false);
        }
    }
}