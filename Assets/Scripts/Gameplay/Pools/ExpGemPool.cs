using UnityEngine.Pool;
using UnityEngine;

namespace Vampire
{
    public class ExpGemPool : Pool
    {
        protected ObjectPool<ExpGem> pool;

        public override void Init(EntityManager entityManager, Character playerCharacter, GameObject prefab, bool collectionCheck = true, int defaultCapacity = 10, int maxSize = 10000)
        {
            base.Init(entityManager, playerCharacter, prefab, collectionCheck, defaultCapacity, maxSize);
            pool = new ObjectPool<ExpGem>(CreatePooledItem, OnTakeFromPool, OnReturnedToPool, OnDestroyPooledItem, collectionCheck, defaultCapacity, maxSize);
        }

        //泛型对象池（Object Pool）中获取对象的核心方法，用于从池中取出一个类型为T的对象（复用已有对象或创建新对象）
        public ExpGem Get()
        {
            return pool.Get();
        }

        public void Release(ExpGem expGem)
        {
            pool.Release(expGem);
        }

        //当对象池没有可复用的ExpGem时，会调用此方法创建新实例
        protected ExpGem CreatePooledItem()
        {
            // 实例化预制体，挂载到对象池节点下，并获取ExpGem组件
            ExpGem expGem = Instantiate(prefab, transform).GetComponent<ExpGem>();
            expGem.Init(entityManager, playerCharacter);
            return expGem;
        }

        protected void OnTakeFromPool(ExpGem expGem)
        {
            expGem.gameObject.SetActive(true);
        }

        protected void OnReturnedToPool(ExpGem expGem)
        {
            expGem.gameObject.SetActive(false);
        }

        protected void OnDestroyPooledItem(ExpGem expGem)
        {
            Destroy(expGem.gameObject);
        }
    }
}
