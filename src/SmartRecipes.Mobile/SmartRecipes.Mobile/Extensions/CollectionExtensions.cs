﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using SmartRecipes.Mobile.Models;

namespace SmartRecipes.Mobile.Extensions
{
    public static class CollectionExtensions
    {
        public static IImmutableList<T> Replace<T>(this IImmutableList<T> list, T item, T replacement)
        {
            return list.Remove(item).Add(replacement);
        }

        public class EntityEqualityComparer<T> : IEqualityComparer<T> where T : Entity
        {
            public bool Equals(T x, T y) => x.Equals(y);
            public int GetHashCode(T obj) => obj.GetHashCode();
        }

        public static IEnumerable<T> Without<T>(this IEnumerable<T> first, IEnumerable<T> second)
            where T : Entity
        {
            return first.Except(second, new EntityEqualityComparer<T>());
        }

        public static IList<T> AddRange<T>(this IList<T> list, IEnumerable<T> items)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            if (items == null) throw new ArgumentNullException(nameof(items));

            if (list is List<T>)
            {
                ((List<T>)list).AddRange(items);
            }
            else
            {
                foreach (var item in items)
                {
                    list.Add(item);
                }
            }
            return list;
        }
    }
}
