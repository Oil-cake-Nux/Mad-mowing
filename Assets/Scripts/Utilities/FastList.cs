using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    //普通的 List<T> 在删除元素时效率较低，而 FastList 通过使用字典来跟踪索引，实现了快速的查找和删除。
    public class FastList<T> : IEnumerable<T>
    {

        private List<T> itemList;
        //用于记录能力T在itemList中对应的索引值，在remove时更加方便
        Dictionary<T, int> indexByItem;

        public int Count { get => itemList.Count; }

        public FastList()
        {
            itemList = new List<T>();
            indexByItem = new Dictionary<T, int>();
        }

        public void Add(T item)
        {
            if (indexByItem.ContainsKey(item))
            {
                Debug.LogError("Item already exists in list.");
                return;
            }

            int index = itemList.Count;
            itemList.Add(item);

            indexByItem[item] = index;
        }

        public void Remove(T item)
        {
            int index;
            if (!indexByItem.TryGetValue(item, out index)) //如果item在indexByItem字典里，那么就会取出其对应的索引值index
            {
                Debug.LogError("Item not found in list.");
                return;
            }
            
            indexByItem.Remove(item);

            int last = itemList.Count - 1;
            //如果要remove的能力正好是列表最后一个
            if (index == last)
            {
                itemList.RemoveAt(last);
            }
            else
            {
                //如果不是最后一个，那么将最后一个元素的值赋值到要删除的那个位置，然后RemoveAt最后一个元素，并更行索引值
                T lastItem = itemList[last];
                itemList[index] = lastItem;
                itemList.RemoveAt(last);

                indexByItem[lastItem] = index;
            }
        }

        //判断indexByItem中是否包含item关键字
        public bool Contains(T item)
        {
            return indexByItem.ContainsKey(item);
        }

        public IEnumerator<T> GetEnumerator()
        {
            foreach (T item in itemList)
            {
                yield return item;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}