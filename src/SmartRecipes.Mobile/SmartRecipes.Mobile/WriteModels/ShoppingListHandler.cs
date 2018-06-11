﻿using SmartRecipes.Mobile.ApiDto;
using System.Threading.Tasks;
using System;
using LanguageExt;
using SmartRecipes.Mobile.Services;
using SmartRecipes.Mobile.Models;
using LanguageExt.SomeHelp;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Immutable;
using SmartRecipes.Mobile.ReadModels;
using SmartRecipes.Mobile.ReadModels.Dto;

namespace SmartRecipes.Mobile.WriteModels
{
    public static class ShoppingListHandler
    {
        public static IShoppingListItemAmount Increase(IShoppingListItemAmount foodstuffAmount, IFoodstuff foodstuff)
        {
            return ChangeAmount((a1, a2) => Amount.Add(a1, a2), foodstuffAmount, foodstuff);
        }

        public static IShoppingListItemAmount Decrease(IShoppingListItemAmount foodstuffAmount, IFoodstuff foodstuff)
        {
            return ChangeAmount((a1, a2) => Amount.Substract(a1, a2), foodstuffAmount, foodstuff);
        }

        private static IShoppingListItemAmount ChangeAmount(Func<IAmount, IAmount, Option<IAmount>> action, IShoppingListItemAmount foodstuffAmount, IFoodstuff foodstuff)
        {
            if (foodstuffAmount.FoodstuffId != foodstuff.Id)
            {
                throw new ArgumentException();
            }

            var newAmount = action(foodstuffAmount.Amount, foodstuff.AmountStep).IfNone(() => throw new ArgumentException());
            return foodstuffAmount.WithAmount(newAmount);
        }

        public static async Task AddToShoppingList(
            DataAccess dataAccess,
            Some<IRecipe> recipe,
            Some<IAccount> owner,
            int personCount)
        {
            var ingredients = await (RecipeRepository.GetIngredients(recipe)(dataAccess)).Value;
            var recipeFoodstuffs = ingredients.Select(i => i.Foodstuff);

            var shoppingListItems = await (ShoppingListRepository.GetItems(owner)(dataAccess)).Value;
            var alreadyAddedFoodstuffs = shoppingListItems.Select(i => i.Foodstuff);

            var notAddedFoodstuffs = recipeFoodstuffs.Except(alreadyAddedFoodstuffs);
            var itemAmounts = notAddedFoodstuffs.Select(f => ShoppingListItemAmount.Create(owner, f.ToSome(), Amount.Zero(f.BaseAmount.Unit)));

            await dataAccess.Db.AddAsync(RecipeInShoppingList.Create(recipe, owner, personCount).ToEnumerable());
            await dataAccess.Db.AddAsync(itemAmounts);
        }

        public static async Task<IEnumerable<ShoppingListItem>> AddToShoppingList(ApiClient apiClient, Database database, Some<IAccount> owner, IEnumerable<IFoodstuff> foodstuffs)
        {
            var shoppingListItems = await ShoppingListRepository.GetItems(apiClient, database, owner);
            var alreadyAddedFoodstuffs = shoppingListItems.Select(i => i.Foodstuff);
            var newFoodstuffs = foodstuffs.Except(alreadyAddedFoodstuffs).ToImmutableDictionary(f => f.Id, f => f);
            var newItemAmounts = newFoodstuffs.Values.Select(f => ShoppingListItemAmount.Create(owner, f.ToSome(), f.BaseAmount));

            await database.AddAsync(newItemAmounts);
            await Update(apiClient, database, newItemAmounts);

            return newItemAmounts.Select(fa => new ShoppingListItem(newFoodstuffs[fa.FoodstuffId].ToSome(), fa.ToSome()));
        }

        public static async Task Update(ApiClient apiClient, Database database, IEnumerable<IShoppingListItemAmount> itemAmounts)
        {
            foreach (var itemAmount in itemAmounts)
            {
                // TODO: create job to update api when this fai
                var request = new ChangeFoodstuffAmountRequest(itemAmount.FoodstuffId, itemAmount.Amount);
                var response = await apiClient.Post(request);
            }

            await database.UpdateAsync(itemAmounts);
        }
    }
}
