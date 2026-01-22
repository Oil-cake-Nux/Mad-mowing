using System.Text;
using TMPro;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 性能面板（只输出你要的正确计数）：
    /// - Monsters / ExpGems / Coins（可统计 Active 或 All）
    /// - 三者总和
    /// - FPS / Frame Time(ms) / GC Memory（托管堆）
    ///
    /// 统计方式：
    /// - Active：FindObjectsOfType<T>(false)  => 只统计 activeInHierarchy 的对象（游戏中真正生效的）
    /// - All：Resources.FindObjectsOfTypeAll<T>() 并过滤到已加载场景 => 包含 inactive（池里待机）但不包含 Project 里的 prefab 资产
    /// </summary>
    public class PerformanceOverlay : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private TextMeshProUGUI text;

        [Header("Update")]
        [SerializeField] private float refreshInterval = 0.25f;

        [Header("Counts")]
        [Tooltip("开启后：统计 All（包括 inactive/对象池待机）。关闭：只统计 Active（正在场景中生效）。")]
        [SerializeField] private bool countAllIncludingInactive = false;

        [Header("FPS Smoothing (EMA)")]
        [SerializeField, Range(0.01f, 0.5f)] private float fpsSmoothing = 0.1f;
        private float smoothedDeltaTime;

        private float timer;
        private readonly StringBuilder sb = new StringBuilder(512);

        private void Awake()
        {
            if (text == null) text = GetComponent<TextMeshProUGUI>();
            smoothedDeltaTime = Time.unscaledDeltaTime;
        }

        private void Update()
        {
            // FPS（unscaled，暂停时也稳定）
            float dt = Time.unscaledDeltaTime;
            smoothedDeltaTime = Mathf.Lerp(smoothedDeltaTime, dt, fpsSmoothing);

            timer += dt;
            if (timer < refreshInterval) return;
            timer = 0f;

            int monsterCount;
            int gemCount;
            int coinCount;

            if (countAllIncludingInactive)
            {
                monsterCount = CountAllInLoadedScenes<Monster>();
                gemCount = CountAllInLoadedScenes<ExpGem>();
                coinCount = CountAllInLoadedScenes<Coin>();
            }
            else
            {
                // 只统计 activeInHierarchy 的对象（游戏里真正存在并运行的）
                monsterCount = FindObjectsOfType<Monster>(false).Length;
                gemCount = FindObjectsOfType<ExpGem>(false).Length;
                coinCount = FindObjectsOfType<Coin>(false).Length;
            }

            int total = monsterCount + gemCount + coinCount;

            long bytes = System.GC.GetTotalMemory(false);
            float mb = bytes / (1024f * 1024f);

            float fps = smoothedDeltaTime > 0 ? (1f / smoothedDeltaTime) : 0f;
            float frameMs = smoothedDeltaTime * 1000f;

            sb.Clear();
            sb.AppendLine($"FPS: {fps:F1}   Frame: {frameMs:F2} ms");
            sb.AppendLine($"GC Memory: {mb:F1} MB");
            sb.AppendLine();

            sb.AppendLine(countAllIncludingInactive ? "Counts (All, incl. inactive):" : "Counts (Active only):");
            sb.AppendLine($"Monsters: {monsterCount}");
            sb.AppendLine($"ExpGems : {gemCount}");
            sb.AppendLine($"Coins   : {coinCount}");
            sb.AppendLine($"Total   : {total}");

            text.text = sb.ToString();
        }

        /// <summary>
        /// 统计“所有已加载场景”中该类型对象数量（包含 inactive），并排除 Project 里的 prefab 资产。
        /// </summary>
        private static int CountAllInLoadedScenes<T>() where T : Component
        {
            var all = Resources.FindObjectsOfTypeAll<T>();
            int count = 0;

            for (int i = 0; i < all.Length; i++)
            {
                var comp = all[i];
                if (comp == null) continue;

                var go = comp.gameObject;

                // 过滤掉不属于任何已加载场景的对象（例如 Project 里的 prefab 资产）
                var scene = go.scene;
                if (!scene.IsValid() || !scene.isLoaded) continue;

                count++;
            }

            return count;
        }
    }
}
