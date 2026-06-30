
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 一个最基础的对象池实现：
    /// 使用 Queue 存储当前空闲对象，Get 取出，Release 归还。
    /// </summary>
    public class SimpleObjectPool : MonoBehaviour
    {
        [Header("Pool Settings")]
        [SerializeField] private GameObject prefab;
        [SerializeField] private int initialSize = 10;
        [SerializeField] private bool expandIfEmpty = true;

        private readonly Queue<GameObject> availableObjects = new Queue<GameObject>();

        private void Awake()
        {
            Prewarm();
        }

        public void Prewarm()
        {
            if (prefab == null)
            {
                Debug.LogWarning($"{nameof(SimpleObjectPool)} on {name} has no prefab assigned.");
                return;
            }

            for (int i = availableObjects.Count; i < initialSize; i++)
            {
                GameObject instance = CreateInstance();
                availableObjects.Enqueue(instance);
            }
        }

        public GameObject Get()
        {
            if (availableObjects.Count == 0)
            {
                if (!expandIfEmpty)
                {
                    return null;
                }

                availableObjects.Enqueue(CreateInstance());
            }

            GameObject instance = availableObjects.Dequeue();
            instance.SetActive(true);
            return instance;
        }

        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            GameObject instance = Get();
            if (instance == null)
            {
                return null;
            }

            Transform cachedTransform = instance.transform;
            cachedTransform.SetPositionAndRotation(position, rotation);
            return instance;
        }

        public void Release(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            instance.SetActive(false);
            instance.transform.SetParent(transform);
            availableObjects.Enqueue(instance);
        }

        private GameObject CreateInstance()
        {
            GameObject instance = Instantiate(prefab, transform);
            instance.SetActive(false);
            return instance;
        }

        public int AvailableCount => availableObjects.Count;
    }
}
