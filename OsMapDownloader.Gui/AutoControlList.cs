using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace OsMapDownloader.Gui
{
    internal class AutoControlList<T> : IList<T> where T : IControl
    {
        private readonly Controls container;
        private readonly List<T> baseList = new List<T>();

        public AutoControlList(Controls container)
        {
            this.container = container;
        }

        public T this[int index]
        {
            get => baseList[index];
            set
            {
                container.Remove(baseList[index]);
                container.Add(value);
                baseList[index] = value;
            }
        }

        public int Count => baseList.Count;

        public bool IsReadOnly => false;

        public void Add(T item)
        {
            container.Add(item);
            baseList.Add(item);
        }

        public void Clear()
        {
            container.RemoveAll(baseList.Cast<IControl>().AsEnumerable());
            baseList.Clear();
        }

        public bool Contains(T item) => baseList.Contains(item);

        public void CopyTo(T[] array, int arrayIndex)
        {
            container.AddRange(array.Cast<IControl>().TakeLast(array.Length - arrayIndex));
            baseList.CopyTo(array, arrayIndex);
        }

        public IEnumerator<T> GetEnumerator() => baseList.GetEnumerator();

        public int IndexOf(T item) => baseList.IndexOf(item);

        public void Insert(int index, T item)
        {
            container.Add(item);
            baseList.Insert(index, item);
        }

        public bool Remove(T item)
        {
            container.Remove(item);
            return baseList.Remove(item);
        }

        public void RemoveAt(int index)
        {
            container.Remove(baseList[index]);
            baseList.RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator() => baseList.GetEnumerator();
    }
}
