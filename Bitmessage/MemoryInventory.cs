using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SQLite;
using bitmessage.network;

namespace bitmessage
{
    public class MemoryInventory : IEnumerable<byte[]>
    {
        private static readonly IEqualityComparer<byte[]> ByteArrayEqualityComparer = new ByteArrayEqualityComparer();

        private readonly HashSet<byte[]> _items;
        private readonly HashSet<byte[]> _waiting = new HashSet<byte[]>(ByteArrayEqualityComparer);

        public MemoryInventory(int capacity = 3000)
        {
            _items = new HashSet<byte[]>( /* todo capacity, */ ByteArrayEqualityComparer);
        }

        public MemoryInventory(SQLiteAsyncConnection conn)
        {
            //Task<int> task = conn.Table<Payload>().CountAsync();
            _items = new HashSet<byte[]>( /* todo Math.Max(task.Result, 3000) , */ByteArrayEqualityComparer);

            var inventory = conn.Table<Payload>().ToListAsync();

            foreach (Payload payload in inventory.Result)
                _items.Add(payload.InventoryVector);
        }

        public bool Exists(byte[] hash, bool waiting = true)
        {
            lock (_items)
            {
                bool w = waiting && _waiting.Contains(hash); //.Exists(bytes => bytes.SequenceEqual(hash));
                // Exists in waiting (if need) or items ?
                bool result = w || _items.Contains(hash); //.Exists(bytes => bytes.SequenceEqual(hash));
                //if (result) 
                //    throw new Exception("");
                return result;
            }
        }

        public bool AddWait(byte[] hash)
        {
            if (hash.Length != 32)
                throw new ArgumentException("hash.Length!=32");
            lock (_items)
                if (!Exists(hash))
                {
                    _waiting.Add(hash);
                    return true;
                }
            return false;
        }

        public bool Insert(byte[] hash)
        {
            //bool result = false;
            if (hash.Length != 32)
                throw new ArgumentException("hash.Length!=32");
            lock (_items)
            {
                // delete from _waiting
                //int index = 
                _waiting.Remove(hash); // .FindIndex(bytes => bytes.SequenceEqual(hash));
                //if (index >= 0)
                //{
                //    _waiting.RemoveAt(index);
                //    result = true;
                //}
                // add to _items
                return _items.Add(hash);
                //if (!Exists(hash, false))
                //{
                //    _items.Add(hash);
                //    result = true;
                //}
            }
            //return result;
        }

        public int Count
        {
            get { return _items.Count; }
        }

        public void Remove(byte[] hash)
        {
            if (hash.Length != 32)
                throw new ArgumentException("hash.Length!=32");
            lock (_items)
            {
                _items.Remove(hash);
                //int index = _items.FindIndex(bytes => bytes.SequenceEqual(hash));
                //if (index >= 0) _items.RemoveAt(index);

                _waiting.Remove(hash);
                //index = _waiting.FindIndex(bytes => bytes.SequenceEqual(hash));
                //if (index >= 0) _waiting.RemoveAt(index);
            }
        }

        #region IEnumerator

        public IEnumerator<byte[]> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion IEnumerator
    }

    internal class ByteArrayEqualityComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[] x, byte[] y)
        {
            return x.SequenceEqual(y);
        }

        public int GetHashCode(byte[] obj)
        {
            return BitConverter.ToInt32(obj, 0);
        }
    }
}