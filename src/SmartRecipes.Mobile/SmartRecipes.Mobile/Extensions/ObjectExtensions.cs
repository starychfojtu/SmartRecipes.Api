﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Xamarin.Forms;
using SmartRecipes.Mobile.Controls;

namespace SmartRecipes.Mobile
{
    public static class ObjectExtensions
    {
        public static IEnumerable<T> ToEnumerable<T>(this T obj)
        {
            yield return obj;
        }

        public static T Tee<T>(this T obj, Action<T> action)
        {
            action(obj);
            return obj;
        }

        public static void Bind<TContext, TProperty>(this TContext context, BindableObject obj, BindableProperty property, Expression<Func<TContext, TProperty>> propertyAccessor)
        {
            obj.SetBinding(property, propertyAccessor.ToPropertyPathName());
        }

        public static void BindText<TContext, TProperty>(this TContext context, Entry entry, Expression<Func<TContext, TProperty>> propertyAccessor)
        {
            context.Bind(entry, Entry.TextProperty, propertyAccessor);
        }

        public static void BindErrors<TContext>(this TContext context, ValidatableEntry entry, Expression<Func<TContext, IEnumerable<string>>> propertyAccessor)
        {
            context.Bind(entry, ValidatableEntry.ErrorsProperty, propertyAccessor);
        }
    }
}
