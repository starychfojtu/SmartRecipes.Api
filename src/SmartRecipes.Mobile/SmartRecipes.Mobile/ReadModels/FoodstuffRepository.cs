﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartRecipes.Mobile.ApiDto;
using SmartRecipes.Mobile.Models;
using SmartRecipes.Mobile.Services;
using System.Linq;

namespace SmartRecipes.Mobile.ReadModels
{
    public class FoodstuffRepository : Repository
    {
        public FoodstuffRepository(ApiClient apiClient, Database database) : base(apiClient, database)
        {
        }

        public async Task<IEnumerable<IFoodstuff>> Search(string query)
        {
            return await RetrievalAction(
                client => client.SearchFoodstuffs(new SearchFoodstuffRequest(query)),
                db => Search(query, db),
                response => ToFoodstuffs(response),
                foodstuffs => foodstuffs
            );
        }

        private static async Task<IEnumerable<IFoodstuff>> Search(string query, Database database)
        {
            // TODO: implement proper search
            return await database.Foodstuffs.ToEnumerableAsync();
        }

        private static IEnumerable<IFoodstuff> ToFoodstuffs(SearchFoodstuffResponse response)
        {
            return response.Foodstuffs.Select(f => Foodstuff.Create(f.Id, f.Name, f.ImageUrl, f.BaseAmount, f.AmountStep));
        }
    }
}
