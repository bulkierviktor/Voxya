using System;
using System.Collections.Generic;

namespace Voxya.Voxel.Unity
{
    // Cola de prioridad mínima simple para coordinar trabajo
    internal class PriorityQueue<T>
    {
        private readonly List<(float key, T val)> heap = new();

        public int Count => heap.Count;

        public void Enqueue(float key, T val)
        {
            heap.Add((key, val));
            SiftUp(heap.Count - 1);
        }

        public T Dequeue(out float key)
        {
            var root = heap[0];
            key = root.key;
            int last = heap.Count - 1;
            heap[0] = heap[last];
            heap.RemoveAt(last);
            SiftDown(0);
            return root.val;
        }

        public void Clear() => heap.Clear();

        private void SiftUp(int i)
        {
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (heap[p].key <= heap[i].key) break;
                (heap[p], heap[i]) = (heap[i], heap[p]);
                i = p;
            }
        }

        private void SiftDown(int i)
        {
            int n = heap.Count;
            while (true)
            {
                int l = (i << 1) + 1, r = l + 1, s = i;
                if (l < n && heap[l].key < heap[s].key) s = l;
                if (r < n && heap[r].key < heap[s].key) s = r;
                if (s == i) break;
                (heap[s], heap[i]) = (heap[i], heap[s]);
                i = s;
            }
        }
    }
}