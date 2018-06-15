﻿using System.Collections.Generic;
using System.Threading.Tasks;
using SmartRecipes.Mobile.ApiDto;
using SmartRecipes.Mobile.Models;
using SmartRecipes.Mobile.Services;
using System.Linq;
using System;

namespace SmartRecipes.Mobile.ReadModels
{
    public static class FoodstuffRepository
    {
        public static Monad.Reader<DataAccess, Task<IEnumerable<IFoodstuff>>> Search(string query)
        {
            return Repository.RetrievalAction(
                client => client.SearchFoodstuffs(new SearchFoodstuffRequest(query)),
                SearchDb(query),
                response => ToFoodstuffs(response),
                foodstuffs => foodstuffs
            );
        }

        public static Monad.Reader<DataAccess, Task<IEnumerable<IFoodstuff>>> GetByIds(IEnumerable<Guid> ids)
        {
            return da => da.Db.Foodstuffs.Where(f => ids.Contains(f.Id)).ToEnumerableAsync<Foodstuff, IFoodstuff>();
        }

        private static Monad.Reader<DataAccess, Task<IEnumerable<IFoodstuff>>> SearchDb(string query)
        {
            return da =>
            {
                var foodstuffs = da.Db.GetTableMapping<Foodstuff>();
                var name = foodstuffs.FindColumnWithPropertyName(nameof(Foodstuff.Name)).Name; // TODO: add helper that takes lambda
                var sql = $@"SELECT * FROM {foodstuffs.TableName} WHERE LOWER({name}) LIKE ?";
                return da.Db.Execute<Foodstuff>(sql, $"%{query}%").Map(fs => fs.Select(f => f as IFoodstuff));
            };
        }

        private static IEnumerable<IFoodstuff> ToFoodstuffs(SearchFoodstuffResponse response)
        {
            return response.Foodstuffs.Select(f => Foodstuff.Create(f.Id, f.Name, f.ImageUrl, f.BaseAmount, f.AmountStep));
        }
    }
}
