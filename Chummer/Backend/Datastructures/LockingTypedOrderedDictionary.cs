/*  This file is part of Chummer5a.
 *
 *  Chummer5a is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  Chummer5a is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with Chummer5a.  If not, see <http://www.gnu.org/licenses/>.
 *
 *  You can obtain the full source code for Chummer5a at
 *  https://github.com/chummer5a/chummer5a
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Chummer
{
    /// <summary>
    /// Like OrderedDictionary, but with key and value types explicitly defined, therefore allowing it to be used in extension/interface methods for dictionaries and lists
    /// </summary>
    /// <typeparam name="TKey">Type used for unique keys in the internal dictionary</typeparam>
    /// <typeparam name="TValue">Type used for values in the internal dictionary</typeparam>
    public class LockingTypedOrderedDictionary<TKey, TValue> :
        IAsyncDictionary<TKey, TValue>,
        IAsyncList<KeyValuePair<TKey, TValue>>,
        IAsyncReadOnlyList<KeyValuePair<TKey, TValue>>,
        IAsyncReadOnlyDictionary<TKey, TValue>,
        ISerializable,
        IDeserializationCallback,
        IHasLockObject,
        IAsyncProducerConsumerCollection<KeyValuePair<TKey, TValue>>
    {
        private readonly Dictionary<TKey, TValue> _dicUnorderedData;
        private readonly List<TKey> _lstIndexes;

        public LockingTypedOrderedDictionary()
        {
            _dicUnorderedData = new Dictionary<TKey, TValue>();
            _lstIndexes = new List<TKey>();
        }

        public LockingTypedOrderedDictionary(IDictionary<TKey, TValue> dictionary)
        {
            _dicUnorderedData = new Dictionary<TKey, TValue>(dictionary);
            _lstIndexes = new List<TKey>(dictionary.Keys);
        }

        public LockingTypedOrderedDictionary(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer)
        {
            _dicUnorderedData = new Dictionary<TKey, TValue>(dictionary, comparer);
            _lstIndexes = new List<TKey>(dictionary.Keys);
        }

        public LockingTypedOrderedDictionary(IEqualityComparer<TKey> comparer)
        {
            _dicUnorderedData = new Dictionary<TKey, TValue>(comparer);
            _lstIndexes = new List<TKey>();
        }

        public LockingTypedOrderedDictionary(int capacity)
        {
            _dicUnorderedData = new Dictionary<TKey, TValue>(capacity);
            _lstIndexes = new List<TKey>(capacity);
        }

        public LockingTypedOrderedDictionary(int capacity, IEqualityComparer<TKey> comparer)
        {
            _dicUnorderedData = new Dictionary<TKey, TValue>(capacity, comparer);
            _lstIndexes = new List<TKey>(capacity);
        }

        /// <inheritdoc cref="Dictionary{TKey, TValue}.Clear" />
        public void Clear()
        {
            using (LockObject.EnterWriteLock())
            {
                _dicUnorderedData.Clear();
                _lstIndexes.Clear();
            }
        }

        /// <inheritdoc cref="IDictionary{TKey, TValue}.Clear" />
        public async ValueTask ClearAsync(CancellationToken token = default)
        {
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                _dicUnorderedData.Clear();
                _lstIndexes.Clear();
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async ValueTask<bool> ContainsAsync(KeyValuePair<TKey, TValue> item, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (!_dicUnorderedData.TryGetValue(item.Key, out TValue objValue))
                    return false;
                return objValue == null ? item.Value == null : objValue.Equals(item.Value);
            }
        }

        public async ValueTask CopyToAsync(KeyValuePair<TKey, TValue>[] array, int index, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                foreach (TKey key in _lstIndexes)
                {
                    array.SetValue(new KeyValuePair<TKey, TValue>(key, _dicUnorderedData[key]), index);
                    ++index;
                }
            }
        }

        /// <inheritdoc />
        public async ValueTask<bool> RemoveAsync(KeyValuePair<TKey, TValue> item, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
                return await ContainsAsync(item, token).ConfigureAwait(false) && await RemoveAsync(item.Key, token).ConfigureAwait(false);
        }

        /// <inheritdoc cref="IEnumerable.GetEnumerator()" />
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            LockingEnumerator<KeyValuePair<TKey, TValue>> objReturn = LockingEnumerator<KeyValuePair<TKey, TValue>>.Get(this);
            objReturn.SetEnumerator(new LockingTypedOrderedDictionaryEnumerator(this));
            return objReturn;
        }

        public async ValueTask<IEnumerator<KeyValuePair<TKey, TValue>>> GetEnumeratorAsync(CancellationToken token = default)
        {
            LockingEnumerator<KeyValuePair<TKey, TValue>> objReturn = await LockingEnumerator<KeyValuePair<TKey, TValue>>.GetAsync(this, token).ConfigureAwait(false);
            objReturn.SetEnumerator(new LockingTypedOrderedDictionaryEnumerator(this));
            return objReturn;
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            LockingDictionaryEnumerator objReturn = LockingDictionaryEnumerator.Get(this);
            objReturn.SetEnumerator(new LockingTypedOrderedDictionaryEnumerator(this));
            return objReturn;
        }

        /// <inheritdoc />
        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
        {
            using (EnterReadLock.Enter(LockObject))
            {
                for (int index = 0; index < _lstIndexes.Count - 1; ++index)
                {
                    TKey objKey = _lstIndexes[index];
                    yield return new KeyValuePair<TKey, TValue>(objKey, _dicUnorderedData[objKey]);
                }
            }
        }

        /// <inheritdoc />
        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            using (EnterReadLock.Enter(LockObject))
            {
                if (!_dicUnorderedData.TryGetValue(item.Key, out TValue objValue))
                    return false;
                return objValue == null ? item.Value == null : objValue.Equals(item.Value);
            }
        }

        public bool Contains(Tuple<TKey, TValue> item)
        {
            (TKey objKey, TValue objValue) = item;
            using (EnterReadLock.Enter(LockObject))
            {
                if (!_dicUnorderedData.TryGetValue(objKey, out TValue objExistingValue))
                    return false;
                return objExistingValue == null ? objValue == null : objExistingValue.Equals(objValue);
            }
        }

        /// <inheritdoc cref="Dictionary{TKey, TValue}.ContainsKey" />
        public bool ContainsKey(TKey key)
        {
            using (EnterReadLock.Enter(LockObject))
                return _dicUnorderedData.ContainsKey(key);
        }

        /// <inheritdoc cref="Dictionary{TKey, TValue}.ContainsValue" />
        public bool ContainsValue(TValue value)
        {
            using (EnterReadLock.Enter(LockObject))
                return _dicUnorderedData.ContainsValue(value);
        }

        /// <inheritdoc />
        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        /// <inheritdoc cref="Dictionary{TKey, TValue}.Add" />
        public void Add(Tuple<TKey, TValue> item)
        {
            (TKey objKey, TValue objValue) = item;
            Add(objKey, objValue);
        }

        /// <inheritdoc />
        public void Add(TKey key, TValue value)
        {
            using (LockObject.EnterWriteLock())
            {
                _dicUnorderedData.Add(key, value);
                _lstIndexes.Add(key);
            }
        }

        public ValueTask AddAsync(KeyValuePair<TKey, TValue> item, CancellationToken token = default)
        {
            return AddAsync(item.Key, item.Value, token);
        }

        /// <inheritdoc cref="Dictionary{TKey, TValue}.Add" />
        public ValueTask AddAsync(Tuple<TKey, TValue> item, CancellationToken token = default)
        {
            (TKey objKey, TValue objValue) = item;
            return AddAsync(objKey, objValue, token);
        }

        public ValueTask AddAsync(object key, object value, CancellationToken token = default)
        {
            if (!(key is TKey objKey))
                throw new ArgumentException(nameof(objKey));
            if (!(value is TValue objValue))
                throw new ArgumentException(nameof(objValue));
            return AddAsync(objKey, objValue, token);
        }

        public async ValueTask AddAsync(TKey key, TValue value, CancellationToken token = default)
        {
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                _dicUnorderedData.Add(key, value);
                _lstIndexes.Add(key);
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async ValueTask<bool> RemoveAsync(TKey key, CancellationToken token = default)
        {
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                if (!_dicUnorderedData.Remove(key))
                    return false;
                _lstIndexes.Remove(key);
                return true;
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <inheritdoc cref="ICollection{T}.CopyTo" />
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            using (EnterReadLock.Enter(LockObject))
            {
                if (arrayIndex + _lstIndexes.Count >= array.Length)
                    throw new ArgumentOutOfRangeException(nameof(arrayIndex));
                int i = 0;
                foreach (TKey objKey in _lstIndexes)
                {
                    array[i + arrayIndex] = new KeyValuePair<TKey, TValue>(objKey, _dicUnorderedData[objKey]);
                    ++i;
                }
            }
        }

        public void CopyTo(Tuple<TKey, TValue>[] array, int arrayIndex)
        {
            using (EnterReadLock.Enter(LockObject))
            {
                if (arrayIndex + _lstIndexes.Count >= array.Length)
                    throw new ArgumentOutOfRangeException(nameof(arrayIndex));
                int i = 0;
                foreach (TKey objKey in _lstIndexes)
                {
                    array[i + arrayIndex] = new Tuple<TKey, TValue>(objKey, _dicUnorderedData[objKey]);
                    ++i;
                }
            }
        }

        /// <inheritdoc />
        public void CopyTo(Array array, int index)
        {
            using (EnterReadLock.Enter(LockObject))
            {
                if (index + _lstIndexes.Count >= array.Length)
                    throw new ArgumentOutOfRangeException(nameof(index));
                int i = 0;
                foreach (TKey objKey in _lstIndexes)
                {
                    array.SetValue(new KeyValuePair<TKey, TValue>(objKey, _dicUnorderedData[objKey]), i + index);
                    ++i;
                }
            }
        }

        public bool TryAdd(TKey key, TValue value, CancellationToken token = default)
        {
            using (EnterReadLock.Enter(LockObject, token))
            {
                if (_dicUnorderedData.ContainsKey(key))
                    return false;
            }
            using (LockObject.EnterWriteLock(token))
            {
                token.ThrowIfCancellationRequested();
                if (_dicUnorderedData.ContainsKey(key))
                    return false;
                token.ThrowIfCancellationRequested();
                _dicUnorderedData.Add(key, value);
                _lstIndexes.Add(key);
            }
            return true;
        }

        public async ValueTask<bool> TryAddAsync(TKey key, TValue value, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (_dicUnorderedData.ContainsKey(key))
                    return false;
            }
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                if (_dicUnorderedData.ContainsKey(key))
                    return false;
                token.ThrowIfCancellationRequested();
                _dicUnorderedData.Add(key, value);
                _lstIndexes.Add(key);
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }
            return true;
        }

        /// <inheritdoc />
        public bool TryAdd(KeyValuePair<TKey, TValue> item)
        {
            return TryAdd(item.Key, item.Value);
        }

        public ValueTask<bool> TryAddAsync(KeyValuePair<TKey, TValue> item, CancellationToken token = default)
        {
            return TryAddAsync(item.Key, item.Value, token);
        }

        public bool TryUpdate(TKey key, TValue value)
        {
            using (EnterReadLock.Enter(LockObject))
            {
                if (!_dicUnorderedData.ContainsKey(key))
                    return false;
            }
            using (LockObject.EnterWriteLock())
            {
                if (!_dicUnorderedData.ContainsKey(key))
                    return false;
                _dicUnorderedData[key] = value;
            }
            return true;
        }

        public async ValueTask<bool> TryUpdateAsync(TKey key, TValue value, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (!_dicUnorderedData.ContainsKey(key))
                    return false;
            }
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                if (!_dicUnorderedData.ContainsKey(key))
                    return false;
                _dicUnorderedData[key] = value;
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }
            return true;
        }

        public bool TryUpdate(KeyValuePair<TKey, TValue> item)
        {
            return TryUpdate(item.Key, item.Value);
        }

        public ValueTask<bool> TryUpdateAsync(KeyValuePair<TKey, TValue> item, CancellationToken token = default)
        {
            return TryUpdateAsync(item.Key, item.Value, token);
        }

        /// <summary>
        /// Uses the specified functions to add a key/value pair to the dictionary if the key does not already exist (and return it) or return the original value in dictionary if the key already exists.
        /// </summary>
        /// <param name="key">The key to be added or whose value should be retrieved.</param>
        /// <param name="addValueFactory">The function used to generate a value for an absent key</param>
        /// <param name="token">Cancellation token to listen to.</param>
        /// <returns>The new value for the key. This will be either be the result of addValueFactory (if the key was absent) or the existing value in the dictionary (if the key was present).</returns>
        public TValue AddOrGet(TKey key, Func<TKey, TValue> addValueFactory, CancellationToken token = default)
        {
            using (EnterReadLock.Enter(LockObject, token))
            {
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                    return objExistingValue;
            }
            TValue objReturn = addValueFactory(key);
            using (LockObject.EnterWriteLock(token))
            {
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                    return objExistingValue;
                _dicUnorderedData.Add(key, objReturn);
                _lstIndexes.Add(key);
                return objReturn;
            }
        }

        /// <summary>
        /// Adds a key/value pair to the dictionary if the key does not already exist, or return the original value in dictionary if the key already exists.
        /// </summary>
        /// <param name="key">The key to be added or whose value should be retrieved.</param>
        /// <param name="addValue">The value to be added for an absent key</param>
        /// <param name="token">Cancellation token to listen to.</param>
        /// <returns>The new value for the key. This will be either be addValue (if the key was absent) or the existing value in the dictionary (if the key was present).</returns>
        public TValue AddOrGet(TKey key, TValue addValue, CancellationToken token = default)
        {
            using (EnterReadLock.Enter(LockObject, token))
            {
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                    return objExistingValue;
            }
            using (LockObject.EnterWriteLock(token))
            {
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                    return objExistingValue;
                _dicUnorderedData.Add(key, addValue);
                _lstIndexes.Add(key);
                return addValue;
            }
        }

        /// <summary>
        /// Uses the specified functions to add a key/value pair to the dictionary if the key does not already exist (and return it) or return the original value in dictionary if the key already exists.
        /// </summary>
        /// <param name="key">The key to be added or whose value should be retrieved.</param>
        /// <param name="addValueFactory">The function used to generate a value for an absent key</param>
        /// <param name="token">Cancellation token to listen to.</param>
        /// <returns>The new value for the key. This will be either be the result of addValueFactory (if the key was absent) or the existing value in the dictionary (if the key was present).</returns>
        public async ValueTask<TValue> AddOrGetAsync(TKey key, Func<TKey, TValue> addValueFactory, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                    return objExistingValue;
            }
            TValue objReturn = addValueFactory(key);
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                    return objExistingValue;
                _dicUnorderedData.Add(key, objReturn);
                _lstIndexes.Add(key);
                return objReturn;
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Adds a key/value pair to the dictionary if the key does not already exist, or return the original value in dictionary if the key already exists.
        /// </summary>
        /// <param name="key">The key to be added or whose value should be retrieved.</param>
        /// <param name="addValue">The value to be added for an absent key</param>
        /// <param name="token">Cancellation token to listen to.</param>
        /// <returns>The new value for the key. This will be either be addValue (if the key was absent) or the existing value in the dictionary (if the key was present).</returns>
        public async ValueTask<TValue> AddOrGetAsync(TKey key, TValue addValue, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                    return objExistingValue;
            }
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                    return objExistingValue;
                _dicUnorderedData.Add(key, addValue);
                _lstIndexes.Add(key);
                return addValue;
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Uses the specified functions to add a key/value pair to the dictionary if the key does not already exist (and return it) or return the original value in dictionary if the key already exists.
        /// </summary>
        /// <param name="key">The key to be added or whose value should be retrieved.</param>
        /// <param name="addValueFactory">The function used to generate a value for an absent key</param>
        /// <param name="token">Cancellation token to listen to.</param>
        /// <returns>The new value for the key. This will be either be the result of addValueFactory (if the key was absent) or the existing value in the dictionary (if the key was present).</returns>
        public async ValueTask<TValue> AddOrGetAsync(TKey key, Func<TKey, Task<TValue>> addValueFactory, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                    return objExistingValue;
            }
            TValue objReturn = await addValueFactory(key).ConfigureAwait(false);
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                    return objExistingValue;
                _dicUnorderedData.Add(key, objReturn);
                _lstIndexes.Add(key);
                return objReturn;
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Adds a key/value pair to the dictionary if the key does not already exist, or return the original value in dictionary if the key already exists.
        /// </summary>
        /// <param name="key">The key to be added or whose value should be retrieved.</param>
        /// <param name="addValue">The value to be added for an absent key</param>
        /// <param name="token">Cancellation token to listen to.</param>
        /// <returns>The new value for the key. This will be either be addValue (if the key was absent) or the existing value in the dictionary (if the key was present).</returns>
        public async ValueTask<TValue> AddOrGetAsync(TKey key, Task<TValue> addValue, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                    return objExistingValue;
            }

            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                TValue objReturn = await addValue.ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                    return objExistingValue;
                _dicUnorderedData.Add(key, objReturn);
                _lstIndexes.Add(key);
                return objReturn;
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Uses the specified functions to add a key/value pair to the dictionary if the key does not already exist (and return it) or return the original value in dictionary if the key already exists.
        /// This version requests a write lock before potentially calling the function to generate the value to add. This makes it better than AddOrGet when that function is expensive, but worse when that function is cheap.
        /// </summary>
        /// <param name="key">The key to be added or whose value should be retrieved.</param>
        /// <param name="addValueFactory">The function used to generate a value for an absent key. Should be an expensive function. If it isn't, use AddOrGet instead.</param>
        /// <param name="token">Cancellation token to listen to.</param>
        /// <returns>The new value for the key. This will be either be the result of addValueFactory (if the key was absent) or the existing value in the dictionary (if the key was present).</returns>
        public TValue AddCheapOrGet(TKey key, Func<TKey, TValue> addValueFactory, CancellationToken token = default)
        {
            using (EnterReadLock.Enter(LockObject, token))
            {
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                    return objExistingValue;
            }
            using (LockObject.EnterWriteLock(token))
            {
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                    return objExistingValue;
                TValue objReturn = addValueFactory(key);
                _dicUnorderedData.Add(key, objReturn);
                _lstIndexes.Add(key);
                return objReturn;
            }
        }

        /// <summary>
        /// Uses the specified functions to add a key/value pair to the dictionary if the key does not already exist (and return it) or return the original value in dictionary if the key already exists.
        /// This version requests a write lock before potentially calling the function to generate the value to add. This makes it better than AddOrGet when that function is expensive, but worse when that function is cheap.
        /// </summary>
        /// <param name="key">The key to be added or whose value should be retrieved.</param>
        /// <param name="addValueFactory">The function used to generate a value for an absent key. Should be an expensive function. If it isn't, use AddOrGet instead.</param>
        /// <param name="token">Cancellation token to listen to.</param>
        /// <returns>The new value for the key. This will be either be the result of addValueFactory (if the key was absent) or the existing value in the dictionary (if the key was present).</returns>
        public async ValueTask<TValue> AddCheapOrGetAsync(TKey key, Func<TKey, TValue> addValueFactory, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                    return objExistingValue;
            }
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                    return objExistingValue;
                TValue objReturn = addValueFactory(key);
                token.ThrowIfCancellationRequested();
                _dicUnorderedData.Add(key, objReturn);
                _lstIndexes.Add(key);
                return objReturn;
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Uses the specified functions to add a key/value pair to the dictionary if the key does not already exist (and return it) or return the original value in dictionary if the key already exists.
        /// This version requests a write lock before potentially calling the function to generate the value to add. This makes it better than AddOrGet when that function is expensive, but worse when that function is cheap.
        /// </summary>
        /// <param name="key">The key to be added or whose value should be retrieved.</param>
        /// <param name="addValueFactory">The function used to generate a value for an absent key. Should be an expensive function. If it isn't, use AddOrGet instead.</param>
        /// <param name="token">Cancellation token to listen to.</param>
        /// <returns>The new value for the key. This will be either be the result of addValueFactory (if the key was absent) or the existing value in the dictionary (if the key was present).</returns>
        public async ValueTask<TValue> AddCheapOrGetAsync(TKey key, Func<TKey, Task<TValue>> addValueFactory, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                    return objExistingValue;
            }
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                    return objExistingValue;
                TValue objReturn = await addValueFactory(key).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                _dicUnorderedData.Add(key, objReturn);
                _lstIndexes.Add(key);
                return objReturn;
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Uses the specified functions to add a key/value pair to the dictionary if the key does not already exist, or to update a key/value pair in the dictionary if the key already exists.
        /// </summary>
        /// <param name="key">The key to be added or whose value should be updated</param>
        /// <param name="addValueFactory">The function used to generate a value for an absent key</param>
        /// <param name="token">Cancellation token to listen to.</param>
        /// <param name="updateValueFactory">The function used to generate a new value for an existing key based on the key's existing value</param>
        /// <returns>The new value for the key. This will be either be the result of addValueFactory (if the key was absent) or the result of updateValueFactory (if the key was present).</returns>
        public TValue AddOrUpdate(TKey key, Func<TKey, TValue> addValueFactory,
                                  Func<TKey, TValue, TValue> updateValueFactory, CancellationToken token = default)
        {
            TValue objReturn;
            using (EnterReadLock.Enter(LockObject, token))
            {
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                {
                    objReturn = updateValueFactory(key, objExistingValue);
                    using (LockObject.EnterWriteLock(token))
                        _dicUnorderedData[key] = objReturn;
                    return objReturn;
                }
            }
            objReturn = addValueFactory(key);
            using (LockObject.EnterWriteLock(token))
            {
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                {
                    objReturn = updateValueFactory(key, objExistingValue);
                    _dicUnorderedData[key] = objReturn;
                    return objReturn;
                }
                _dicUnorderedData.Add(key, objReturn);
                _lstIndexes.Add(key);
                return objReturn;
            }
        }

        /// <summary>
        /// Adds a key/value pair to the dictionary if the key does not already exist, or to update a key/value pair in the dictionary if the key already exists.
        /// </summary>
        /// <param name="key">The key to be added or whose value should be updated</param>
        /// <param name="addValue">The value to be added for an absent key</param>
        /// <param name="updateValueFactory">The function used to generate a new value for an existing key based on the key's existing value</param>
        /// <param name="token">Cancellation token to listen to.</param>
        /// <returns>The new value for the key. This will be either be addValue (if the key was absent) or the result of updateValueFactory (if the key was present).</returns>
        public TValue AddOrUpdate(TKey key, TValue addValue, Func<TKey, TValue, TValue> updateValueFactory, CancellationToken token = default)
        {
            using (EnterReadLock.Enter(LockObject, token))
            {
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                {
                    TValue objNewValue = updateValueFactory(key, objExistingValue);
                    using (LockObject.EnterWriteLock(token))
                        _dicUnorderedData[key] = objNewValue;
                    return objNewValue;
                }
            }
            using (LockObject.EnterWriteLock(token))
            {
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                {
                    TValue objNewValue = updateValueFactory(key, objExistingValue);
                    _dicUnorderedData[key] = objNewValue;
                    return objNewValue;
                }
                _dicUnorderedData.Add(key, addValue);
                _lstIndexes.Add(key);
                return addValue;
            }
        }

        /// <summary>
        /// Uses the specified functions to add a key/value pair to the dictionary if the key does not already exist, or to update a key/value pair in the dictionary if the key already exists.
        /// </summary>
        /// <param name="key">The key to be added or whose value should be updated</param>
        /// <param name="addValueFactory">The function used to generate a value for an absent key</param>
        /// <param name="updateValueFactory">The function used to generate a new value for an existing key based on the key's existing value</param>
        /// <param name="token">Cancellation token to listen to.</param>
        /// <returns>The new value for the key. This will be either be the result of addValueFactory (if the key was absent) or the result of updateValueFactory (if the key was present).</returns>
        public async ValueTask<TValue> AddOrUpdateAsync(TKey key, Func<TKey, TValue> addValueFactory,
                                                        Func<TKey, TValue, TValue> updateValueFactory, CancellationToken token = default)
        {
            TValue objReturn;
            IAsyncDisposable objLocker;
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                {
                    objReturn = updateValueFactory(key, objExistingValue);
                    objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        _dicUnorderedData[key] = objReturn;
                    }
                    finally
                    {
                        await objLocker.DisposeAsync().ConfigureAwait(false);
                    }

                    return objReturn;
                }
            }
            objReturn = addValueFactory(key);
            objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                {
                    objReturn = updateValueFactory(key, objExistingValue);
                    token.ThrowIfCancellationRequested();
                    _dicUnorderedData[key] = objReturn;
                    return objReturn;
                }
                _dicUnorderedData.Add(key, objReturn);
                _lstIndexes.Add(key);
                return objReturn;
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Adds a key/value pair to the dictionary if the key does not already exist, or to update a key/value pair in the dictionary if the key already exists.
        /// </summary>
        /// <param name="key">The key to be added or whose value should be updated</param>
        /// <param name="addValue">The value to be added for an absent key</param>
        /// <param name="updateValueFactory">The function used to generate a new value for an existing key based on the key's existing value</param>
        /// <param name="token">Cancellation token to listen to.</param>
        /// <returns>The new value for the key. This will be either be addValue (if the key was absent) or the result of updateValueFactory (if the key was present).</returns>
        public async ValueTask<TValue> AddOrUpdateAsync(TKey key, TValue addValue, Func<TKey, TValue, TValue> updateValueFactory, CancellationToken token = default)
        {
            IAsyncDisposable objLocker;
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                {
                    TValue objNewValue = updateValueFactory(key, objExistingValue);
                    objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        _dicUnorderedData[key] = objNewValue;
                    }
                    finally
                    {
                        await objLocker.DisposeAsync().ConfigureAwait(false);
                    }
                    return objNewValue;
                }
            }
            objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                {
                    TValue objNewValue = updateValueFactory(key, objExistingValue);
                    token.ThrowIfCancellationRequested();
                    _dicUnorderedData[key] = objNewValue;
                    return objNewValue;
                }
                _dicUnorderedData.Add(key, addValue);
                _lstIndexes.Add(key);
                return addValue;
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Uses the specified functions to add a key/value pair to the dictionary if the key does not already exist, or to update a key/value pair in the dictionary if the key already exists.
        /// </summary>
        /// <param name="key">The key to be added or whose value should be updated</param>
        /// <param name="addValueFactory">The function used to generate a value for an absent key</param>
        /// <param name="updateValueFactory">The function used to generate a new value for an existing key based on the key's existing value</param>
        /// <param name="token">Cancellation token to listen to.</param>
        /// <returns>The new value for the key. This will be either be the result of addValueFactory (if the key was absent) or the result of updateValueFactory (if the key was present).</returns>
        public async ValueTask<TValue> AddOrUpdateAsync(TKey key, Func<TKey, Task<TValue>> addValueFactory,
                                                        Func<TKey, TValue, TValue> updateValueFactory, CancellationToken token = default)
        {
            TValue objReturn;
            IAsyncDisposable objLocker;
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                {
                    objReturn = updateValueFactory(key, objExistingValue);
                    objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        _dicUnorderedData[key] = objReturn;
                    }
                    finally
                    {
                        await objLocker.DisposeAsync().ConfigureAwait(false);
                    }
                    return objReturn;
                }
            }
            objReturn = await addValueFactory(key).ConfigureAwait(false);
            objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                {
                    objReturn = updateValueFactory(key, objExistingValue);
                    token.ThrowIfCancellationRequested();
                    _dicUnorderedData[key] = objReturn;
                    return objReturn;
                }
                _dicUnorderedData.Add(key, objReturn);
                _lstIndexes.Add(key);
                return objReturn;
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Adds a key/value pair to the dictionary if the key does not already exist, or to update a key/value pair in the dictionary if the key already exists.
        /// </summary>
        /// <param name="key">The key to be added or whose value should be updated</param>
        /// <param name="addValue">The value to be added for an absent key</param>
        /// <param name="updateValueFactory">The function used to generate a new value for an existing key based on the key's existing value</param>
        /// <param name="token">Cancellation token to listen to.</param>
        /// <returns>The new value for the key. This will be either be addValue (if the key was absent) or the result of updateValueFactory (if the key was present).</returns>
        public async ValueTask<TValue> AddOrUpdateAsync(TKey key, Task<TValue> addValue, Func<TKey, TValue, TValue> updateValueFactory, CancellationToken token = default)
        {
            TValue objReturn;
            IAsyncDisposable objLocker;
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                {
                    objReturn = updateValueFactory(key, objExistingValue);
                    objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        _dicUnorderedData[key] = objReturn;
                    }
                    finally
                    {
                        await objLocker.DisposeAsync().ConfigureAwait(false);
                    }
                    return objReturn;
                }
            }
            objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                objReturn = await addValue.ConfigureAwait(false);
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                {
                    objReturn = updateValueFactory(key, objExistingValue);
                    token.ThrowIfCancellationRequested();
                    _dicUnorderedData[key] = objReturn;
                    return objReturn;
                }
                _dicUnorderedData.Add(key, objReturn);
                _lstIndexes.Add(key);
                return objReturn;
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Uses the specified functions to add a key/value pair to the dictionary if the key does not already exist, or to update a key/value pair in the dictionary if the key already exists.
        /// </summary>
        /// <param name="key">The key to be added or whose value should be updated</param>
        /// <param name="addValueFactory">The function used to generate a value for an absent key</param>
        /// <param name="updateValueFactory">The function used to generate a new value for an existing key based on the key's existing value</param>
        /// <param name="token">Cancellation token to listen to.</param>
        /// <returns>The new value for the key. This will be either be the result of addValueFactory (if the key was absent) or the result of updateValueFactory (if the key was present).</returns>
        public async ValueTask<TValue> AddOrUpdateAsync(TKey key, Func<TKey, TValue> addValueFactory,
                                                        Func<TKey, TValue, Task<TValue>> updateValueFactory, CancellationToken token = default)
        {
            TValue objReturn;
            IAsyncDisposable objLocker;
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                {
                    objReturn = await updateValueFactory(key, objExistingValue).ConfigureAwait(false);
                    objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        _dicUnorderedData[key] = objReturn;
                    }
                    finally
                    {
                        await objLocker.DisposeAsync().ConfigureAwait(false);
                    }
                    return objReturn;
                }
            }
            objReturn = addValueFactory(key);
            objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                {
                    objReturn = await updateValueFactory(key, objExistingValue).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    _dicUnorderedData[key] = objReturn;
                    return objReturn;
                }
                _dicUnorderedData.Add(key, objReturn);
                _lstIndexes.Add(key);
                return objReturn;
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Adds a key/value pair to the dictionary if the key does not already exist, or to update a key/value pair in the dictionary if the key already exists.
        /// </summary>
        /// <param name="key">The key to be added or whose value should be updated</param>
        /// <param name="addValue">The value to be added for an absent key</param>
        /// <param name="updateValueFactory">The function used to generate a new value for an existing key based on the key's existing value</param>
        /// <param name="token">Cancellation token to listen to.</param>
        /// <returns>The new value for the key. This will be either be addValue (if the key was absent) or the result of updateValueFactory (if the key was present).</returns>
        public async ValueTask<TValue> AddOrUpdateAsync(TKey key, TValue addValue, Func<TKey, TValue, Task<TValue>> updateValueFactory, CancellationToken token = default)
        {
            TValue objReturn;
            IAsyncDisposable objLocker;
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                {
                    objReturn = await updateValueFactory(key, objExistingValue).ConfigureAwait(false);
                    objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        _dicUnorderedData[key] = objReturn;
                    }
                    finally
                    {
                        await objLocker.DisposeAsync().ConfigureAwait(false);
                    }
                    return objReturn;
                }
            }
            objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                {
                    objReturn = await updateValueFactory(key, objExistingValue).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    _dicUnorderedData[key] = objReturn;
                    return objReturn;
                }
                _dicUnorderedData.Add(key, addValue);
                _lstIndexes.Add(key);
                return addValue;
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Uses the specified functions to add a key/value pair to the dictionary if the key does not already exist, or to update a key/value pair in the dictionary if the key already exists.
        /// </summary>
        /// <param name="key">The key to be added or whose value should be updated</param>
        /// <param name="addValueFactory">The function used to generate a value for an absent key</param>
        /// <param name="updateValueFactory">The function used to generate a new value for an existing key based on the key's existing value</param>
        /// <param name="token">Cancellation token to listen to.</param>
        /// <returns>The new value for the key. This will be either be the result of addValueFactory (if the key was absent) or the result of updateValueFactory (if the key was present).</returns>
        public async ValueTask<TValue> AddOrUpdateAsync(TKey key, Func<TKey, Task<TValue>> addValueFactory,
                                                        Func<TKey, TValue, Task<TValue>> updateValueFactory, CancellationToken token = default)
        {
            TValue objReturn;
            IAsyncDisposable objLocker;
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                {
                    objReturn = await updateValueFactory(key, objExistingValue).ConfigureAwait(false);
                    objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        _dicUnorderedData[key] = objReturn;
                    }
                    finally
                    {
                        await objLocker.DisposeAsync().ConfigureAwait(false);
                    }
                    return objReturn;
                }
            }
            objReturn = await addValueFactory(key).ConfigureAwait(false);
            objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                {
                    objReturn = await updateValueFactory(key, objExistingValue).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    _dicUnorderedData[key] = objReturn;
                    return objReturn;
                }
                _dicUnorderedData.Add(key, objReturn);
                _lstIndexes.Add(key);
                return objReturn;
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Adds a key/value pair to the dictionary if the key does not already exist, or to update a key/value pair in the dictionary if the key already exists.
        /// </summary>
        /// <param name="key">The key to be added or whose value should be updated</param>
        /// <param name="addValue">The value to be added for an absent key</param>
        /// <param name="updateValueFactory">The function used to generate a new value for an existing key based on the key's existing value</param>
        /// <param name="token">Cancellation token to listen to.</param>
        /// <returns>The new value for the key. This will be either be addValue (if the key was absent) or the result of updateValueFactory (if the key was present).</returns>
        public async ValueTask<TValue> AddOrUpdateAsync(TKey key, Task<TValue> addValue, Func<TKey, TValue, Task<TValue>> updateValueFactory, CancellationToken token = default)
        {
            TValue objReturn;
            IAsyncDisposable objLocker;
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                {
                    objReturn = await updateValueFactory(key, objExistingValue).ConfigureAwait(false);
                    objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        _dicUnorderedData[key] = objReturn;
                    }
                    finally
                    {
                        await objLocker.DisposeAsync().ConfigureAwait(false);
                    }
                    return objReturn;
                }
            }
            objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                objReturn = await addValue.ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (_dicUnorderedData.TryGetValue(key, out TValue objExistingValue))
                {
                    objReturn = await updateValueFactory(key, objExistingValue).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    _dicUnorderedData[key] = objReturn;
                    return objReturn;
                }
                _dicUnorderedData.Add(key, objReturn);
                _lstIndexes.Add(key);
                return objReturn;
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async ValueTask<Tuple<bool, KeyValuePair<TKey, TValue>>> TryTakeAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (_lstIndexes.Count == 0)
                    return new Tuple<bool, KeyValuePair<TKey, TValue>>(false, default);
            }
            bool blnTakeSuccessful = false;
            TKey objKeyToTake = default;
            TValue objValue = default;
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                if (_lstIndexes.Count > 0)
                {
                    // FIFO to be compliant with how the default for BlockingCollection<T> is ConcurrentQueue
                    objKeyToTake = _lstIndexes[0];
                    if (_dicUnorderedData.TryGetValue(objKeyToTake, out objValue))
                    {
                        blnTakeSuccessful = _dicUnorderedData.Remove(objKeyToTake);
                        if (blnTakeSuccessful)
                            _lstIndexes.RemoveAt(0);
                    }
                }
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }

            return blnTakeSuccessful
                ? new Tuple<bool, KeyValuePair<TKey, TValue>>(
                    true, new KeyValuePair<TKey, TValue>(objKeyToTake, objValue))
                : new Tuple<bool, KeyValuePair<TKey, TValue>>(false, default);
        }

        /// <inheritdoc />
        public async ValueTask<KeyValuePair<TKey, TValue>[]> ToArrayAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                KeyValuePair<TKey, TValue>[] akvpReturn = new KeyValuePair<TKey, TValue>[_lstIndexes.Count];
                for (int i = 0; i < _lstIndexes.Count; ++i)
                {
                    TKey objLoopKey = _lstIndexes[i];
                    akvpReturn[i] = new KeyValuePair<TKey, TValue>(objLoopKey, _dicUnorderedData[objLoopKey]);
                }
                return akvpReturn;
            }
        }

        /// <inheritdoc />
        public bool TryTake(out KeyValuePair<TKey, TValue> item)
        {
            using (EnterReadLock.Enter(LockObject))
            {
                if (_lstIndexes.Count == 0)
                {
                    item = default;
                    return false;
                }
            }
            bool blnTakeSuccessful = false;
            TKey objKeyToTake = default;
            TValue objValue = default;
            using (LockObject.EnterWriteLock())
            {
                if (_lstIndexes.Count > 0)
                {
                    // FIFO to be compliant with how the default for BlockingCollection<T> is ConcurrentQueue
                    objKeyToTake = _lstIndexes[0];
                    if (_dicUnorderedData.TryGetValue(objKeyToTake, out objValue))
                    {
                        blnTakeSuccessful = _dicUnorderedData.Remove(objKeyToTake);
                        if (blnTakeSuccessful)
                            _lstIndexes.RemoveAt(0);
                    }
                }
            }

            if (blnTakeSuccessful)
            {
                item = new KeyValuePair<TKey, TValue>(objKeyToTake, objValue);
                return true;
            }
            item = default;
            return false;
        }

        /// <inheritdoc />
        public KeyValuePair<TKey, TValue>[] ToArray()
        {
            using (EnterReadLock.Enter(LockObject))
            {
                KeyValuePair<TKey, TValue>[] akvpReturn = new KeyValuePair<TKey, TValue>[_lstIndexes.Count];
                int i = 0;
                foreach (TKey objKey in _lstIndexes)
                {
                    akvpReturn[i] = new KeyValuePair<TKey, TValue>(objKey, _dicUnorderedData[objKey]);
                    ++i;
                }
                return akvpReturn;
            }
        }

        /// <inheritdoc cref="Dictionary{TKey, TValue}.Count" />
        public int Count
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _dicUnorderedData.Count;
            }
        }

        /// <inheritdoc />
        public object SyncRoot
        {
            get
            {
                if (_objSyncRoot == null)
                {
                    Interlocked.CompareExchange<object>(ref _objSyncRoot, new object(), null);
                }

                return _objSyncRoot;
            }
        }

        private object _objSyncRoot;

        /// <inheritdoc />
        public bool IsSynchronized => false;

        /// <inheritdoc cref="Dictionary{TKey, TValue}.Comparer" />
        public IEqualityComparer<TKey> Comparer
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _dicUnorderedData.Comparer;
            }
        }

        /// <inheritdoc />
        public ICollection<TKey> Keys
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _lstIndexes;
            }
        }

        public async ValueTask<ICollection<TKey>> GetKeysAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
                return _lstIndexes;
        }

        public async ValueTask<IReadOnlyCollection<TKey>> GetReadOnlyKeysAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
                return _lstIndexes;
        }

        /// <inheritdoc />
        public ICollection<TValue> Values
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    // Needed to make sure ordering is retained
                    List<TValue> lstReturn = new List<TValue>(_lstIndexes.Count);
                    foreach (TKey objKey in _lstIndexes)
                        lstReturn.Add(_dicUnorderedData[objKey]);
                    return lstReturn;
                }
            }
        }

        public async ValueTask<ICollection<TValue>> GetValuesAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                // Needed to make sure ordering is retained
                List<TValue> lstReturn = new List<TValue>(_lstIndexes.Count);
                foreach (TKey objKey in _lstIndexes)
                    lstReturn.Add(_dicUnorderedData[objKey]);
                return lstReturn;
            }
        }

        public async ValueTask<IReadOnlyCollection<TValue>> GetReadOnlyValuesAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                // Needed to make sure ordering is retained
                List<TValue> lstReturn = new List<TValue>(_lstIndexes.Count);
                foreach (TKey objKey in _lstIndexes)
                    lstReturn.Add(_dicUnorderedData[objKey]);
                return lstReturn;
            }
        }

        public ICollection<TKey> KeysUnsorted
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _dicUnorderedData.Keys;
            }
        }

        public ICollection<TValue> ValuesUnsorted
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _dicUnorderedData.Values;
            }
        }

        public IReadOnlyList<TKey> ReadOnlyKeys
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _lstIndexes;
            }
        }

        public IReadOnlyList<TValue> ReadOnlyValues
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    // Needed to make sure ordering is retained
                    List<TValue> lstReturn = new List<TValue>(_lstIndexes.Count);
                    foreach (TKey objKey in _lstIndexes)
                        lstReturn.Add(_dicUnorderedData[objKey]);
                    return lstReturn;
                }
            }
        }

        public IReadOnlyCollection<TKey> ReadOnlyKeysUnsorted
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _dicUnorderedData.Keys;
            }
        }

        public IReadOnlyCollection<TValue> ReadOnlyValuesUnsorted
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _dicUnorderedData.Values;
            }
        }

        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _lstIndexes;
            }
        }

        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    // Needed to make sure ordering is retained
                    foreach (TKey objKey in _lstIndexes)
                        yield return _dicUnorderedData[objKey];
                }
            }
        }

        public bool IsReadOnly => false;

        public bool IsFixedSize => false;

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return Contains(item) && Remove(item.Key);
        }

        public bool Remove(Tuple<TKey, TValue> item)
        {
            return item != null && Contains(item) && Remove(item.Item1);
        }

        public void Remove(object key)
        {
            switch (key)
            {
                case TKey objKey:
                    Remove(objKey);
                    break;

                case int intIndex:
                    RemoveAt(intIndex);
                    break;
            }
        }

        public bool Remove(TKey key)
        {
            using (LockObject.EnterWriteLock())
            {
                if (!_dicUnorderedData.Remove(key))
                    return false;
                _lstIndexes.Remove(key);
                return true;
            }
        }

        /// <inheritdoc cref="Dictionary{TKey, TValue}.TryGetValue" />
        public bool TryGetValue(TKey key, out TValue value)
        {
            using (EnterReadLock.Enter(LockObject))
                return _dicUnorderedData.TryGetValue(key, out value);
        }

        public TValue this[TKey key]
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _dicUnorderedData[key];
            }
            set
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    TValue objOldValue = _dicUnorderedData[key];
                    if (objOldValue == null)
                    {
                        if (value == null)
                            return;
                    }
                    else if (objOldValue.Equals(value))
                        return;
                    using (LockObject.EnterWriteLock())
                        _dicUnorderedData[key] = value;
                }
            }
        }

        public async ValueTask<TValue> GetValueAtAsync(TKey key, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
                return _dicUnorderedData[key];
        }

        public async ValueTask SetValueAtAsync(TKey key, TValue value, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                TValue objOldValue = _dicUnorderedData[key];
                if (objOldValue == null)
                {
                    if (value == null)
                        return;
                }
                else if (objOldValue.Equals(value))
                    return;
                IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
                try
                {
                    _dicUnorderedData[key] = value;
                }
                finally
                {
                    await objLocker.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        public async ValueTask SetValueAtAsync(int index, TValue value, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                TKey objKey = _lstIndexes[index];
                TValue objOldValue = _dicUnorderedData[objKey];
                if (objOldValue == null)
                {
                    if (value == null)
                        return;
                }
                else if (objOldValue.Equals(value))
                    return;
                IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
                try
                {
                    _dicUnorderedData[objKey] = value;
                }
                finally
                {
                    await objLocker.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        public object this[object key]
        {
            get
            {
                switch (key)
                {
                    case TKey objKey:
                        return this[objKey];

                    case int intKey:
                        return this[intKey];

                    default:
                        throw new ArgumentException(null, nameof(key));
                }
            }
            set
            {
                switch (value)
                {
                    case TValue objValue when key is TKey objKey:
                        this[objKey] = objValue;
                        break;

                    case KeyValuePair<TKey, TValue> objKeyValuePair when key is int intKey:
                        this[intKey] = objKeyValuePair;
                        break;

                    case Tuple<TKey, TValue> objTuple when key is int intKey:
                        this[intKey] = new KeyValuePair<TKey, TValue>(objTuple.Item1, objTuple.Item2);
                        break;

                    default:
                        throw new InvalidOperationException(nameof(value));
                }
            }
        }

        public KeyValuePair<TKey, TValue> this[int index]
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    TKey objKey = _lstIndexes[index];
                    return new KeyValuePair<TKey, TValue>(objKey, _dicUnorderedData[objKey]);
                }
            }
            set
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    if (_dicUnorderedData.TryGetValue(value.Key, out TValue objOldValue))
                    {
                        int intOriginalIndex = _lstIndexes.IndexOf(value.Key);
                        if (index == intOriginalIndex)
                            return;
                        TKey objKeyToRemove = _lstIndexes[index];
                        using (LockObject.EnterWriteLock())
                        {
                            _lstIndexes[index] = value.Key;
                            for (int i = intOriginalIndex; i < _lstIndexes.Count - 2; ++i)
                            {
                                if (i != index)
                                    _lstIndexes[i] = _lstIndexes[i + 1];
                            }

                            _lstIndexes.RemoveAt(_lstIndexes.Count - 1);
                            if (objKeyToRemove != null)
                                _dicUnorderedData.Remove(objKeyToRemove);
                            if (objOldValue == null ? value.Value != null : !objOldValue.Equals(value.Value))
                                _dicUnorderedData[value.Key] = value.Value;
                        }
                    }
                    else
                    {
                        using (LockObject.EnterWriteLock())
                        {
                            TKey objKeyToRemove = _lstIndexes[index];
                            _dicUnorderedData.Remove(objKeyToRemove);
                            _dicUnorderedData.Add(value.Key, value.Value);
                            _lstIndexes[index] = value.Key;
                        }
                    }
                }
            }
        }

        public KeyValuePair<TKey, TValue> GetValueAt(int index, CancellationToken token = default)
        {
            using (EnterReadLock.Enter(LockObject, token))
            {
                TKey objKey = _lstIndexes[index];
                return new KeyValuePair<TKey, TValue>(objKey, _dicUnorderedData[objKey]);
            }
        }

        public async ValueTask<KeyValuePair<TKey, TValue>> GetValueAtAsync(int index, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                TKey objKey = _lstIndexes[index];
                return new KeyValuePair<TKey, TValue>(objKey, _dicUnorderedData[objKey]);
            }
        }

        public async ValueTask SetValueAtAsync(int index, KeyValuePair<TKey, TValue> value, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (_dicUnorderedData.TryGetValue(value.Key, out TValue objOldValue))
                {
                    int intOriginalIndex = _lstIndexes.IndexOf(value.Key);
                    if (index == intOriginalIndex)
                        return;
                    TKey objKeyToRemove = _lstIndexes[index];
                    IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
                    try
                    {
                        _lstIndexes[index] = value.Key;
                        for (int i = intOriginalIndex; i < _lstIndexes.Count - 2; ++i)
                        {
                            if (i != index)
                                _lstIndexes[i] = _lstIndexes[i + 1];
                        }

                        _lstIndexes.RemoveAt(_lstIndexes.Count - 1);
                        if (objKeyToRemove != null)
                            _dicUnorderedData.Remove(objKeyToRemove);
                        if (objOldValue == null ? value.Value != null : !objOldValue.Equals(value.Value))
                            _dicUnorderedData[value.Key] = value.Value;
                    }
                    finally
                    {
                        await objLocker.DisposeAsync().ConfigureAwait(false);
                    }
                }
                else
                {
                    IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
                    try
                    {
                        TKey objKeyToRemove = _lstIndexes[index];
                        _dicUnorderedData.Remove(objKeyToRemove);
                        _dicUnorderedData.Add(value.Key, value.Value);
                        _lstIndexes[index] = value.Key;
                    }
                    finally
                    {
                        await objLocker.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
        }

        /// <inheritdoc />
        public async ValueTask<int> IndexOfAsync(KeyValuePair<TKey, TValue> item, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
                return _dicUnorderedData.TryGetValue(item.Key, out TValue objValue) && objValue.Equals(item.Value)
                    ? _lstIndexes.IndexOf(item.Key)
                    : -1;
        }

        /// <inheritdoc />
        public async ValueTask InsertAsync(int index, KeyValuePair<TKey, TValue> item, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (_dicUnorderedData.ContainsKey(item.Key))
                    throw new ArgumentException(null, nameof(item));
                IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
                try
                {
                    _dicUnorderedData.Add(item.Key, item.Value);
                    _lstIndexes.Insert(index, item.Key);
                }
                finally
                {
                    await objLocker.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        public int IndexOf(TKey key)
        {
            using (EnterReadLock.Enter(LockObject))
                return _dicUnorderedData.ContainsKey(key)
                    ? _lstIndexes.IndexOf(key)
                    : -1;
        }

        public int IndexOf(KeyValuePair<TKey, TValue> item)
        {
            using (EnterReadLock.Enter(LockObject))
                return _dicUnorderedData.TryGetValue(item.Key, out TValue objValue) && objValue.Equals(item.Value)
                    ? _lstIndexes.IndexOf(item.Key)
                    : -1;
        }

        public int IndexOf(Tuple<TKey, TValue> item)
        {
            using (EnterReadLock.Enter(LockObject))
                return item != null && _dicUnorderedData.TryGetValue(item.Item1, out TValue objValue) && objValue.Equals(item.Item2)
                    ? _lstIndexes.IndexOf(item.Item1)
                    : -1;
        }

        public int LastIndexOf(TKey key)
        {
            using (EnterReadLock.Enter(LockObject))
                return _dicUnorderedData.ContainsKey(key)
                    ? _lstIndexes.LastIndexOf(key)
                    : -1;
        }

        public int LastIndexOf(KeyValuePair<TKey, TValue> item)
        {
            using (EnterReadLock.Enter(LockObject))
                return _dicUnorderedData.TryGetValue(item.Key, out TValue objValue) && objValue.Equals(item.Value)
                    ? _lstIndexes.LastIndexOf(item.Key)
                    : -1;
        }

        public int LastIndexOf(Tuple<TKey, TValue> item)
        {
            using (EnterReadLock.Enter(LockObject))
                return item != null && _dicUnorderedData.TryGetValue(item.Item1, out TValue objValue) && objValue.Equals(item.Item2)
                    ? _lstIndexes.LastIndexOf(item.Item1)
                    : -1;
        }

        /// <inheritdoc cref="List{T}.Find" />
        public KeyValuePair<TKey, TValue> Find(Predicate<TKey> predicate, CancellationToken token = default)
        {
            using (EnterReadLock.Enter(LockObject, token))
            {
                TKey objKey = _lstIndexes.Find(predicate);
                return new KeyValuePair<TKey, TValue>(objKey, _dicUnorderedData.TryGetValue(objKey, out TValue objValue) ? objValue : default);
            }
        }

        /// <inheritdoc cref="List{T}.Find" />
        public async ValueTask<KeyValuePair<TKey, TValue>> FindAsync(Predicate<TKey> predicate, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                TKey objKey = _lstIndexes.Find(predicate);
                return new KeyValuePair<TKey, TValue>(objKey, _dicUnorderedData.TryGetValue(objKey, out TValue objValue) ? objValue : default);
            }
        }

        public List<TKey> FindAllKeys(Predicate<TKey> predicate, CancellationToken token = default)
        {
            using (EnterReadLock.Enter(LockObject, token))
                return _lstIndexes.FindAll(predicate);
        }

        public async ValueTask<List<TKey>> FindAllKeysAsync(Predicate<TKey> predicate, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
                return _lstIndexes.FindAll(predicate);
        }

        /// <inheritdoc cref="List{T}.FindAll" />
        public List<KeyValuePair<TKey, TValue>> FindAll(Predicate<TKey> predicate, CancellationToken token = default)
        {
            using (EnterReadLock.Enter(LockObject, token))
            {
                List<TKey> lstKeys = _lstIndexes.FindAll(predicate);
                List<KeyValuePair<TKey, TValue>> lstReturn = new List<KeyValuePair<TKey, TValue>>(lstKeys.Count);
                foreach (TKey objKey in lstKeys)
                    lstReturn.Add(new KeyValuePair<TKey, TValue>(
                                      objKey,
                                      _dicUnorderedData.TryGetValue(objKey, out TValue objValue) ? objValue : default));
                return lstReturn;
            }
        }

        /// <inheritdoc cref="List{T}.FindAll" />
        public TypedOrderedDictionary<TKey, TValue> FindAll(Predicate<KeyValuePair<TKey, TValue>> predicate, CancellationToken token = default)
        {
            using (EnterReadLock.Enter(LockObject, token))
            {
                TypedOrderedDictionary<TKey, TValue> dicReturn
                    = new TypedOrderedDictionary<TKey, TValue>(_lstIndexes.Count);
                foreach (TKey objKey in _lstIndexes)
                {
                    KeyValuePair<TKey, TValue> kvpLoop = new KeyValuePair<TKey, TValue>(objKey, _dicUnorderedData[objKey]);
                    if (predicate(kvpLoop))
                        dicReturn.Add(kvpLoop);
                }
                return dicReturn;
            }
        }

        /// <inheritdoc cref="List{T}.FindAll" />
        public List<Tuple<TKey, TValue>> FindAll(Predicate<Tuple<TKey, TValue>> predicate, CancellationToken token = default)
        {
            using (EnterReadLock.Enter(LockObject, token))
            {
                List<Tuple<TKey, TValue>> lstReturn = new List<Tuple<TKey, TValue>>(_lstIndexes.Count);
                foreach (TKey objKey in _lstIndexes)
                {
                    Tuple<TKey, TValue> tupLoop
                        = new Tuple<TKey, TValue>(objKey, _dicUnorderedData[objKey]);
                    if (predicate(tupLoop))
                        lstReturn.Add(tupLoop);
                }

                return lstReturn;
            }
        }

        /// <inheritdoc cref="List{T}.FindAll" />
        public async ValueTask<List<KeyValuePair<TKey, TValue>>> FindAllAsync(Predicate<TKey> predicate, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                List<TKey> lstKeys = _lstIndexes.FindAll(predicate);
                List<KeyValuePair<TKey, TValue>> lstReturn = new List<KeyValuePair<TKey, TValue>>(lstKeys.Count);
                foreach (TKey objKey in lstKeys)
                    lstReturn.Add(new KeyValuePair<TKey, TValue>(
                                      objKey,
                                      _dicUnorderedData.TryGetValue(objKey, out TValue objValue) ? objValue : default));
                return lstReturn;
            }
        }

        /// <inheritdoc cref="List{T}.FindAll" />
        public async ValueTask<TypedOrderedDictionary<TKey, TValue>> FindAllAsync(Predicate<KeyValuePair<TKey, TValue>> predicate, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                TypedOrderedDictionary<TKey, TValue> dicReturn
                    = new TypedOrderedDictionary<TKey, TValue>(_lstIndexes.Count);
                foreach (TKey objKey in _lstIndexes)
                {
                    KeyValuePair<TKey, TValue> kvpLoop = new KeyValuePair<TKey, TValue>(objKey, _dicUnorderedData[objKey]);
                    if (predicate(kvpLoop))
                        dicReturn.Add(kvpLoop);
                }
                return dicReturn;
            }
        }

        /// <inheritdoc cref="List{T}.FindAll" />
        public async ValueTask<List<Tuple<TKey, TValue>>> FindAllAsync(Predicate<Tuple<TKey, TValue>> predicate, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                List<Tuple<TKey, TValue>> lstReturn = new List<Tuple<TKey, TValue>>(_lstIndexes.Count);
                foreach (TKey objKey in _lstIndexes)
                {
                    Tuple<TKey, TValue> tupLoop
                        = new Tuple<TKey, TValue>(objKey, _dicUnorderedData[objKey]);
                    if (predicate(tupLoop))
                        lstReturn.Add(tupLoop);
                }

                return lstReturn;
            }
        }

        public void Insert(int index, KeyValuePair<TKey, TValue> item)
        {
            using (EnterReadLock.Enter(LockObject))
            {
                if (_dicUnorderedData.ContainsKey(item.Key))
                    throw new ArgumentException(null, nameof(item));
                using (LockObject.EnterWriteLock())
                {
                    _dicUnorderedData.Add(item.Key, item.Value);
                    _lstIndexes.Insert(index, item.Key);
                }
            }
        }

        public void Insert(int index, Tuple<TKey, TValue> item)
        {
            using (EnterReadLock.Enter(LockObject))
            {
                if (item == null)
                    throw new ArgumentNullException(nameof(item));
                if (_dicUnorderedData.ContainsKey(item.Item1))
                    throw new ArgumentException(null, nameof(item));
                using (LockObject.EnterWriteLock())
                {
                    _dicUnorderedData.Add(item.Item1, item.Item2);
                    _lstIndexes.Insert(index, item.Item1);
                }
            }
        }

        public void RemoveAt(int index)
        {
            using (EnterReadLock.Enter(LockObject))
            {
                TKey objKeyToRemove = _lstIndexes[index];
                if (objKeyToRemove.Equals(default))
                    return;
                using (LockObject.EnterWriteLock())
                {
                    _dicUnorderedData.Remove(objKeyToRemove);
                    _lstIndexes.RemoveAt(index);
                }
            }
        }

        /// <inheritdoc cref="List{T}.RemoveAt" />
        public async ValueTask RemoveAtAsync(int index, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                TKey objKeyToRemove = _lstIndexes[index];
                if (objKeyToRemove.Equals(default))
                    return;
                IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
                try
                {
                    _dicUnorderedData.Remove(objKeyToRemove);
                    _lstIndexes.RemoveAt(index);
                }
                finally
                {
                    await objLocker.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        public void Reverse(int index, int count)
        {
            using (LockObject.EnterWriteLock())
                _lstIndexes.Reverse(index, count);
        }

        public async Task ReverseAsync(int index, int count, CancellationToken token = default)
        {
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                _lstIndexes.Reverse(index, count);
            }
            finally
            {
                await objLocker.DisposeAsync();
            }
        }

        public void Sort()
        {
            using (LockObject.EnterWriteLock())
                _lstIndexes.Sort();
        }

        public void Sort(Comparison<TKey> comparison)
        {
            using (LockObject.EnterWriteLock())
                _lstIndexes.Sort(comparison);
        }

        public void Sort(Comparison<KeyValuePair<TKey, TValue>> comparison)
        {
            using (LockObject.EnterWriteLock())
                _lstIndexes.Sort((x, y) => comparison(new KeyValuePair<TKey, TValue>(x, _dicUnorderedData[x]),
                                                      new KeyValuePair<TKey, TValue>(y, _dicUnorderedData[y])));
        }

        public void Sort(Comparison<Tuple<TKey, TValue>> comparison)
        {
            using (LockObject.EnterWriteLock())
                _lstIndexes.Sort((x, y) => comparison(new Tuple<TKey, TValue>(x, _dicUnorderedData[x]),
                                                      new Tuple<TKey, TValue>(y, _dicUnorderedData[y])));
        }

        public void Sort(IComparer<TKey> comparer)
        {
            using (LockObject.EnterWriteLock())
                _lstIndexes.Sort(comparer);
        }

        public void Sort(IComparer<KeyValuePair<TKey, TValue>> comparer)
        {
            using (LockObject.EnterWriteLock())
                _lstIndexes.Sort((x, y) => comparer.Compare(new KeyValuePair<TKey, TValue>(x, _dicUnorderedData[x]),
                                                            new KeyValuePair<TKey, TValue>(y, _dicUnorderedData[y])));
        }

        public void Sort(IComparer<Tuple<TKey, TValue>> comparer)
        {
            using (LockObject.EnterWriteLock())
                _lstIndexes.Sort((x, y) => comparer.Compare(new Tuple<TKey, TValue>(x, _dicUnorderedData[x]),
                                                            new Tuple<TKey, TValue>(y, _dicUnorderedData[y])));
        }

        public void Sort(int index, int count, IComparer<TKey> comparer)
        {
            using (LockObject.EnterWriteLock())
                _lstIndexes.Sort(index, count, comparer);
        }

        public void Sort(int index, int count, IComparer<KeyValuePair<TKey, TValue>> comparer)
        {
            using (LockObject.EnterWriteLock())
                _lstIndexes.Sort(index, count, new KeyValueToKeyComparer(this, comparer));
        }

        public void Sort(int index, int count, IComparer<Tuple<TKey, TValue>> comparer)
        {
            using (LockObject.EnterWriteLock())
                _lstIndexes.Sort(index, count, new KeyValueToKeyComparer(this, comparer));
        }

        public async Task SortAsync(CancellationToken token = default)
        {
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                _lstIndexes.Sort();
            }
            finally
            {
                await objLocker.DisposeAsync();
            }
        }

        public async Task SortAsync(Comparison<TKey> comparison, CancellationToken token = default)
        {
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                _lstIndexes.Sort(comparison);
            }
            finally
            {
                await objLocker.DisposeAsync();
            }
        }

        public async Task SortAsync(Comparison<KeyValuePair<TKey, TValue>> comparison, CancellationToken token = default)
        {
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                _lstIndexes.Sort((x, y) => comparison(new KeyValuePair<TKey, TValue>(x, _dicUnorderedData[x]),
                                                      new KeyValuePair<TKey, TValue>(y, _dicUnorderedData[y])));
            }
            finally
            {
                await objLocker.DisposeAsync();
            }
        }

        public async Task SortAsync(Comparison<Tuple<TKey, TValue>> comparison, CancellationToken token = default)
        {
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                _lstIndexes.Sort((x, y) => comparison(new Tuple<TKey, TValue>(x, _dicUnorderedData[x]),
                                                      new Tuple<TKey, TValue>(y, _dicUnorderedData[y])));
            }
            finally
            {
                await objLocker.DisposeAsync();
            }
        }

        public async Task SortAsync(IComparer<TKey> comparer, CancellationToken token = default)
        {
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                _lstIndexes.Sort(comparer);
            }
            finally
            {
                await objLocker.DisposeAsync();
            }
        }

        public async Task SortAsync(IComparer<KeyValuePair<TKey, TValue>> comparer, CancellationToken token = default)
        {
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                _lstIndexes.Sort((x, y) => comparer.Compare(new KeyValuePair<TKey, TValue>(x, _dicUnorderedData[x]),
                                                            new KeyValuePair<TKey, TValue>(y, _dicUnorderedData[y])));
            }
            finally
            {
                await objLocker.DisposeAsync();
            }
        }

        public async Task SortAsync(IComparer<Tuple<TKey, TValue>> comparer, CancellationToken token = default)
        {
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                _lstIndexes.Sort((x, y) => comparer.Compare(new Tuple<TKey, TValue>(x, _dicUnorderedData[x]),
                                                            new Tuple<TKey, TValue>(y, _dicUnorderedData[y])));
            }
            finally
            {
                await objLocker.DisposeAsync();
            }
        }

        public async Task SortAsync(int index, int count, IComparer<TKey> comparer, CancellationToken token = default)
        {
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                _lstIndexes.Sort(index, count, comparer);
            }
            finally
            {
                await objLocker.DisposeAsync();
            }
        }

        public async Task SortAsync(int index, int count, IComparer<KeyValuePair<TKey, TValue>> comparer, CancellationToken token = default)
        {
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                _lstIndexes.Sort(index, count, new KeyValueToKeyComparer(this, comparer));
            }
            finally
            {
                await objLocker.DisposeAsync();
            }
        }

        public async Task SortAsync(int index, int count, IComparer<Tuple<TKey, TValue>> comparer, CancellationToken token = default)
        {
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                _lstIndexes.Sort(index, count, new KeyValueToKeyComparer(this, comparer));
            }
            finally
            {
                await objLocker.DisposeAsync();
            }
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            using (EnterReadLock.Enter(LockObject))
                _dicUnorderedData.GetObjectData(info, context);
        }

        public void OnDeserialization(object sender)
        {
            using (EnterReadLock.Enter(LockObject))
                _dicUnorderedData.OnDeserialization(sender);
        }

        private sealed class KeyValueToKeyComparer : IComparer<TKey>
        {
            private readonly LockingTypedOrderedDictionary<TKey, TValue> _dicMyDictionary;
            private readonly IComparer<KeyValuePair<TKey, TValue>> _objMyComparer;
            private readonly IComparer<Tuple<TKey, TValue>> _objMyTupleComparer;

            public KeyValueToKeyComparer(LockingTypedOrderedDictionary<TKey, TValue> dictionary, IComparer<KeyValuePair<TKey, TValue>> comparer)
            {
                _dicMyDictionary = dictionary;
                _objMyComparer = comparer;
                _objMyTupleComparer = null;
            }

            public KeyValueToKeyComparer(LockingTypedOrderedDictionary<TKey, TValue> dictionary, IComparer<Tuple<TKey, TValue>> comparer)
            {
                _dicMyDictionary = dictionary;
                _objMyComparer = null;
                _objMyTupleComparer = comparer;
            }

            public int Compare(TKey x, TKey y)
            {
                if (x == null)
                {
                    if (y == null)
                        return 0;
                    return -1;
                }

                if (y == null)
                    return 1;

                return _objMyComparer?.Compare(new KeyValuePair<TKey, TValue>(x, _dicMyDictionary._dicUnorderedData[x]),
                    new KeyValuePair<TKey, TValue>(y, _dicMyDictionary._dicUnorderedData[y]))
                       ?? _objMyTupleComparer.Compare(new Tuple<TKey, TValue>(x, _dicMyDictionary._dicUnorderedData[x]),
                           new Tuple<TKey, TValue>(y, _dicMyDictionary._dicUnorderedData[y]));
            }
        }

        private sealed class LockingTypedOrderedDictionaryEnumerator : IDictionaryEnumerator, IEnumerator<KeyValuePair<TKey, TValue>>
        {
            // A copy of the SimpleDictionary object's key/value pairs.
            private readonly LockingTypedOrderedDictionary<TKey, TValue> _dicMyDictionary;

            private int _intIndex = -1;

            public LockingTypedOrderedDictionaryEnumerator(LockingTypedOrderedDictionary<TKey, TValue> dictionary)
            {
                _dicMyDictionary = dictionary;
            }

            // Return the current item.
            public object Current
            {
                get
                {
                    ValidateIndex();
                    TKey objKey = _dicMyDictionary._lstIndexes[_intIndex];
                    return new KeyValuePair<TKey, TValue>(objKey, _dicMyDictionary._dicUnorderedData[objKey]);
                }
            }

            // Return the current dictionary entry.
            public DictionaryEntry Entry => (DictionaryEntry?)Current ?? default;

            // Return the key of the current item.
            public object Key
            {
                get
                {
                    ValidateIndex();
                    return _dicMyDictionary._lstIndexes[_intIndex];
                }
            }

            // Return the value of the current item.
            public object Value
            {
                get
                {
                    ValidateIndex();
                    return _dicMyDictionary._dicUnorderedData[_dicMyDictionary._lstIndexes[_intIndex]];
                }
            }

            // Advance to the next item.
            public bool MoveNext()
            {
                ++_intIndex;
                return _intIndex < _dicMyDictionary._lstIndexes.Count;
            }

            // Validate the enumeration index and throw an exception if the index is out of range.
            private void ValidateIndex()
            {
                if (_intIndex < 0 || _intIndex >= _dicMyDictionary._lstIndexes.Count)
                    throw new InvalidOperationException("Enumerator is before or after the collection.");
            }

            // Reset the index to restart the enumeration.
            public void Reset()
            {
                _intIndex = -1;
            }

            /// <inheritdoc />
            KeyValuePair<TKey, TValue> IEnumerator<KeyValuePair<TKey, TValue>>.Current
            {
                get
                {
                    ValidateIndex();
                    TKey objKey = _dicMyDictionary._lstIndexes[_intIndex];
                    return new KeyValuePair<TKey, TValue>(objKey, _dicMyDictionary._dicUnorderedData[objKey]);
                }
            }

            /// <inheritdoc />
            public void Dispose()
            {
                _intIndex = -1;
            }
        }

        private int _intIsDisposed;

        public bool IsDisposed => _intIsDisposed > 0;

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Interlocked.CompareExchange(ref _intIsDisposed, 1, 0) > 0)
                    return;
                LockObject.Dispose();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual async ValueTask DisposeAsync(bool disposing)
        {
            if (disposing)
            {
                if (Interlocked.CompareExchange(ref _intIsDisposed, 1, 0) > 0)
                    return;
                await LockObject.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await DisposeAsync(true).ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public AsyncFriendlyReaderWriterLock LockObject { get; } = new AsyncFriendlyReaderWriterLock();

        public async ValueTask<int> GetCountAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
                return _dicUnorderedData.Count;
        }

        /// <inheritdoc cref="IAsyncDictionary{TKey, TValue}.ContainsKeyAsync" />
        public async ValueTask<bool> ContainsKeyAsync(TKey key, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
                return _dicUnorderedData.ContainsKey(key);
        }

        /// <inheritdoc cref="IAsyncDictionary{TKey, TValue}.TryGetValueAsync" />
        public async ValueTask<Tuple<bool, TValue>> TryGetValueAsync(TKey key, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                bool blnSuccess = _dicUnorderedData.TryGetValue(key, out TValue value);
                return new Tuple<bool, TValue>(blnSuccess, value);
            }
        }
    }
}
