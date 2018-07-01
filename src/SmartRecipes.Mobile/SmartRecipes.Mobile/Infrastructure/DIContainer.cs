﻿using System.Net.Http;
using Autofac;
using SmartRecipes.Mobile.ViewModels;

namespace SmartRecipes.Mobile.Infrastructure
{
    public class DIContainer
    {
        private static IContainer instance;

        private DIContainer()
        {
        }

        public static IContainer Instance
        {
            get { return instance ?? (instance = Initialize()); }
        }

        private static IContainer Initialize()
        {
            var builder = new ContainerBuilder();

            // Services
            builder.RegisterInstance(new HttpClient()).As<HttpClient>();
            builder.RegisterType<ApiClient>().SingleInstance();
            builder.RegisterType<Database>().SingleInstance();
            builder.RegisterType<Enviroment>().SingleInstance();

            // View models
            builder.RegisterType<SignInViewModel>();
            builder.RegisterType<ShoppingListItemsViewModel>();
            builder.RegisterType<ShoppingListRecipesViewModel>();
            builder.RegisterType<FoodstuffSearchViewModel>();
            builder.RegisterType<MyRecipesViewModel>();
            builder.RegisterType<EditRecipeViewModel>();

            return builder.Build();
        }
    }
}