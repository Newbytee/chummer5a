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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Chummer.Annotations;

namespace Chummer
{
    public static class ListExtensions
    {
        public static void AddWithSort<T>(this IList<T> lstCollection, T objNewItem, Action<T, T> funcOverrideIfEquals = null, CancellationToken token = default) where T : IComparable
        {
            token.ThrowIfCancellationRequested();
            if (lstCollection == null)
                throw new ArgumentNullException(nameof(lstCollection));
            if (lstCollection is IHasLockObject objLocker)
                objLocker.LockObject.EnterReadLock(token);
            else
                objLocker = null;
            try
            {
                // Binary search for the place where item should be inserted
                int intIntervalEnd = lstCollection.Count - 1;
                int intTargetIndex = intIntervalEnd / 2;
                for (int intIntervalStart = 0;
                     intIntervalStart <= intIntervalEnd;
                     intTargetIndex = (intIntervalStart + intIntervalEnd) / 2)
                {
                    token.ThrowIfCancellationRequested();
                    T objLoopExistingItem = lstCollection[intTargetIndex];
                    int intCompareResult = objLoopExistingItem.CompareTo(objNewItem);
                    if (intCompareResult == 0)
                    {
                        // Make sure we insert new items at the end of any equalities (so that order is maintained when adding multiple items)
                        for (int i = intTargetIndex + 1; i < lstCollection.Count; ++i)
                        {
                            T objInnerLoopExistingItem = lstCollection[i];
                            if (objInnerLoopExistingItem.CompareTo(objNewItem) == 0)
                            {
                                ++intTargetIndex;
                                objLoopExistingItem = objInnerLoopExistingItem;
                            }
                            else
                                break;
                        }

                        if (funcOverrideIfEquals != null)
                        {
                            funcOverrideIfEquals.Invoke(objLoopExistingItem, objNewItem);
                            return;
                        }

                        break;
                    }

                    if (intIntervalStart == intIntervalEnd)
                    {
                        if (intCompareResult > 0)
                            ++intTargetIndex;
                        break;
                    }

                    if (intCompareResult > 0)
                        intIntervalStart = intTargetIndex + 1;
                    else
                        intIntervalEnd = intTargetIndex - 1;
                }

                lstCollection.Insert(intTargetIndex, objNewItem);
            }
            finally
            {
                objLocker?.LockObject.ExitReadLock();
            }
        }

        public static void AddWithSort<T>(this IList<T> lstCollection, T objNewItem, IComparer<T> comparer, Action<T, T> funcOverrideIfEquals = null, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (lstCollection == null)
                throw new ArgumentNullException(nameof(lstCollection));
            if (comparer == null)
                throw new ArgumentNullException(nameof(comparer));
            if (lstCollection is IHasLockObject objLocker)
                objLocker.LockObject.EnterReadLock(token);
            else
                objLocker = null;
            try
            {
                // Binary search for the place where item should be inserted
                int intIntervalEnd = lstCollection.Count - 1;
                int intTargetIndex = intIntervalEnd / 2;
                for (int intIntervalStart = 0;
                     intIntervalStart <= intIntervalEnd;
                     intTargetIndex = (intIntervalStart + intIntervalEnd) / 2)
                {
                    token.ThrowIfCancellationRequested();
                    T objLoopExistingItem = lstCollection[intTargetIndex];
                    int intCompareResult = comparer.Compare(objLoopExistingItem, objNewItem);
                    if (intCompareResult == 0)
                    {
                        // Make sure we insert new items at the end of any equalities (so that order is maintained when adding multiple items)
                        for (int i = intTargetIndex + 1; i < lstCollection.Count; ++i)
                        {
                            T objInnerLoopExistingItem = lstCollection[i];
                            if (comparer.Compare(objInnerLoopExistingItem, objNewItem) == 0)
                            {
                                ++intTargetIndex;
                                objLoopExistingItem = objInnerLoopExistingItem;
                            }
                            else
                                break;
                        }

                        if (funcOverrideIfEquals != null)
                        {
                            funcOverrideIfEquals.Invoke(objLoopExistingItem, objNewItem);
                            return;
                        }

                        break;
                    }

                    if (intIntervalStart == intIntervalEnd)
                    {
                        if (intCompareResult > 0)
                            ++intTargetIndex;
                        break;
                    }

                    if (intCompareResult > 0)
                        intIntervalStart = intTargetIndex + 1;
                    else
                        intIntervalEnd = intTargetIndex - 1;
                }

                lstCollection.Insert(intTargetIndex, objNewItem);
            }
            finally
            {
                objLocker?.LockObject.ExitReadLock();
            }
        }

        public static void AddWithSort<T>(this IList<T> lstCollection, T objNewItem, Comparison<T> funcComparison, Action<T, T> funcOverrideIfEquals = null, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (lstCollection == null)
                throw new ArgumentNullException(nameof(lstCollection));
            if (funcComparison == null)
                throw new ArgumentNullException(nameof(funcComparison));
            if (lstCollection is IHasLockObject objLocker)
                objLocker.LockObject.EnterReadLock(token);
            else
                objLocker = null;
            try
            {
                // Binary search for the place where item should be inserted
                int intIntervalEnd = lstCollection.Count - 1;
                int intTargetIndex = intIntervalEnd / 2;
                for (int intIntervalStart = 0;
                     intIntervalStart <= intIntervalEnd;
                     intTargetIndex = (intIntervalStart + intIntervalEnd) / 2)
                {
                    token.ThrowIfCancellationRequested();
                    T objLoopExistingItem = lstCollection[intTargetIndex];
                    int intCompareResult = funcComparison.Invoke(objLoopExistingItem, objNewItem);
                    if (intCompareResult == 0)
                    {
                        // Make sure we insert new items at the end of any equalities (so that order is maintained when adding multiple items)
                        for (int i = intTargetIndex + 1; i < lstCollection.Count; ++i)
                        {
                            T objInnerLoopExistingItem = lstCollection[i];
                            if (funcComparison.Invoke(objInnerLoopExistingItem, objNewItem) == 0)
                            {
                                ++intTargetIndex;
                                objLoopExistingItem = objInnerLoopExistingItem;
                            }
                            else
                                break;
                        }

                        if (funcOverrideIfEquals != null)
                        {
                            funcOverrideIfEquals.Invoke(objLoopExistingItem, objNewItem);
                            return;
                        }

                        break;
                    }

                    if (intIntervalStart == intIntervalEnd)
                    {
                        if (intCompareResult > 0)
                            ++intTargetIndex;
                        break;
                    }

                    if (intCompareResult > 0)
                        intIntervalStart = intTargetIndex + 1;
                    else
                        intIntervalEnd = intTargetIndex - 1;
                }

                lstCollection.Insert(intTargetIndex, objNewItem);
            }
            finally
            {
                objLocker?.LockObject.ExitReadLock();
            }
        }

        public static void AddRangeWithSort<T>(this IList<T> lstCollection, IEnumerable<T> lstToAdd, Action<T, T> funcOverrideIfEquals = null, CancellationToken token = default) where T : IComparable
        {
            if (lstCollection == null)
                throw new ArgumentNullException(nameof(lstCollection));
            if (lstToAdd == null)
                throw new ArgumentNullException(nameof(lstToAdd));
            foreach (T objItem in lstToAdd)
                AddWithSort(lstCollection, objItem, funcOverrideIfEquals, token);
        }

        public static void AddRangeWithSort<T>(this IList<T> lstCollection, IEnumerable<T> lstToAdd, IComparer<T> comparer, Action<T, T> funcOverrideIfEquals = null, CancellationToken token = default)
        {
            if (lstCollection == null)
                throw new ArgumentNullException(nameof(lstCollection));
            if (lstToAdd == null)
                throw new ArgumentNullException(nameof(lstToAdd));
            if (comparer == null)
                throw new ArgumentNullException(nameof(comparer));
            foreach (T objItem in lstToAdd)
                AddWithSort(lstCollection, objItem, comparer, funcOverrideIfEquals, token);
        }

        public static void AddRangeWithSort<T>(this IList<T> lstCollection, IEnumerable<T> lstToAdd, Comparison<T> funcComparison, Action<T, T> funcOverrideIfEquals = null, CancellationToken token = default)
        {
            if (lstCollection == null)
                throw new ArgumentNullException(nameof(lstCollection));
            if (lstToAdd == null)
                throw new ArgumentNullException(nameof(lstToAdd));
            if (funcComparison == null)
                throw new ArgumentNullException(nameof(funcComparison));
            foreach (T objItem in lstToAdd)
                AddWithSort(lstCollection, objItem, funcComparison, funcOverrideIfEquals, token);
        }

        public static void RemoveRange<T>(this IList<T> lstCollection, int index, int count, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (count == 0)
                return;
            if (lstCollection == null)
                throw new ArgumentNullException(nameof(lstCollection));
            if (lstCollection.Count == 0)
                return;
            if (index < 0 || index >= lstCollection.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            IDisposable objLocker = lstCollection is IHasLockObject objHasLockObject
                ? objHasLockObject.LockObject.EnterWriteLock(token)
                : null;
            try
            {
                for (int i = Math.Min(index + count - 1, lstCollection.Count); i >= index; --i)
                {
                    token.ThrowIfCancellationRequested();
                    lstCollection.RemoveAt(i);
                }
            }
            finally
            {
                objLocker?.Dispose();
            }
        }

        public static void RemoveAll<T>(this IList<T> lstCollection, Predicate<T> predicate, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (lstCollection == null)
                throw new ArgumentNullException(nameof(lstCollection));
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));
            IDisposable objLocker = lstCollection is IHasLockObject objHasLockObject
                ? objHasLockObject.LockObject.EnterWriteLock(token)
                : null;
            try
            {
                for (int i = lstCollection.Count - 1; i >= 0; --i)
                {
                    token.ThrowIfCancellationRequested();
                    if (predicate(lstCollection[i]))
                    {
                        lstCollection.RemoveAt(i);
                    }
                }
            }
            finally
            {
                objLocker?.Dispose();
            }
        }

        public static void InsertRange<T>(this IList<T> lstCollection, int index, [NotNull] IEnumerable<T> collection, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (lstCollection == null)
                throw new ArgumentNullException(nameof(lstCollection));
            IDisposable objLocker = lstCollection is IHasLockObject objHasLockObject
                ? objHasLockObject.LockObject.EnterWriteLock(token)
                : null;
            try
            {
                foreach (T item in collection.Reverse())
                {
                    token.ThrowIfCancellationRequested();
                    lstCollection.Insert(index, item);
                }
            }
            finally
            {
                objLocker?.Dispose();
            }
        }
    }
}
