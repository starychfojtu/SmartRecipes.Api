﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SmartRecipes.Mobile.ReadModels;
using System;
using SmartRecipes.Mobile.WriteModels;
using SmartRecipes.Mobile.ReadModels.Dto;
using SmartRecipes.Mobile.Services;
using System.Collections.Immutable;

namespace SmartRecipes.Mobile.ViewModels
{
    public class ShoppingListItemsViewModel : ViewModel
    {
        private readonly ShoppingListHandler commandHandler;

        private readonly ShoppingListRepository repository;

        private IImmutableList<Ingredient> ingredients { get; set; }

        public ShoppingListItemsViewModel(ShoppingListHandler commandHandler, ShoppingListRepository repository)
        {
            this.repository = repository;
            this.commandHandler = commandHandler;
            ingredients = ImmutableList.Create<Ingredient>();
        }

        public IEnumerable<IngredientCellViewModel> Ingredients
        {
            get { return ingredients.Select(i => ToViewModel(i)); }
        }

        public override async Task InitializeAsync()
        {
            await UpdateItemsAsync();
        }

        public async Task Refresh()
        {
            await InitializeAsync();
        }

        public async Task OpenAddIngredientDialog()
        {
            var selected = await Navigation.SelectFoodstuffDialog();
            var newIngredients = await commandHandler.Add(selected);
            var allIngredients = ingredients.Concat(newIngredients).ToImmutableList();
            UpdateIngredients(allIngredients);
        }

        private async Task IncreaseAmountAsync(Ingredient item)
        {
            await ItemAction(item, i => commandHandler.IncreaseAmount(i));
        }

        private async Task DecreaseAmountAsync(Ingredient item)
        {
            await ItemAction(item, i => commandHandler.DecreaseAmount(i));
        }

        private async Task ItemAction(Ingredient item, Func<Ingredient, Task<Ingredient>> action)
        {
            var newItem = await action(item);
            var oldItem = ingredients.First(i => i.Foodstuff.Id == item.Foodstuff.Id);
            var newItems = ingredients.Replace(oldItem, newItem);
            UpdateIngredients(newItems);
        }

        private async Task UpdateItemsAsync()
        {
            UpdateIngredients((await repository.GetItems()).ToImmutableList());
        }

        private void UpdateIngredients(IImmutableList<Ingredient> newIngredients)
        {
            ingredients = newIngredients.OrderBy(i => i.Foodstuff.Name).ToImmutableList();
            RaisePropertyChanged(nameof(Ingredients));
        }

        private IngredientCellViewModel ToViewModel(Ingredient ingredient)
        {
            return new IngredientCellViewModel(
                ingredient,
                () => IncreaseAmountAsync(ingredient),
                () => DecreaseAmountAsync(ingredient)
            );
        }
    }
}
