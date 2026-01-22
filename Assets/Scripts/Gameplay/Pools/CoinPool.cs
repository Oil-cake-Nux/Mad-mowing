using UnityEngine.Pool;
using UnityEngine;

namespace Vampire
{
    //金币对象池
    public class CoinPool : Pool
    {
        protected ObjectPool<Coin> pool;

        public override void Init(EntityManager entityManager, Character playerCharacter, GameObject prefab, bool collectionCheck = true, int defaultCapacity = 10, int maxSize = 10000)
        {
            base.Init(entityManager, playerCharacter, prefab, collectionCheck, defaultCapacity, maxSize);
            pool = new ObjectPool<Coin>(CreatePooledItem, OnTakeFromPool, OnReturnedToPool, OnDestroyPooledItem, collectionCheck, defaultCapacity, maxSize);
        }

        public Coin Get()
        {
            return pool.Get();
        }

        public void Release(Coin coin)
        {
            pool.Release(coin);
        }

        protected Coin CreatePooledItem()
        {
            //以 prefab 为 “模板”，创建一个全新的 GameObject 实例，并将这个实例设置为 transform对应的 GameObject 的子对象。
            Coin coin = Instantiate(prefab, transform).GetComponent<Coin>();
            coin.Init(entityManager, playerCharacter);
            return coin;
        }

        protected void OnTakeFromPool(Coin coin)
        {
            coin.gameObject.SetActive(true);
        }

        protected void OnReturnedToPool(Coin coin)
        {
            coin.gameObject.SetActive(false);
        }

        protected void OnDestroyPooledItem(Coin coin)
        {
            Destroy(coin.gameObject);
        }
    }
}
