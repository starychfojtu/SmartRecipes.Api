namespace SmartRecipes.Api

open FSharpPlus
open SmartRecipes.Domain.Foodstuff
open SmartRecipes.Domain.Recipe
open SmartRecipes.IO

module ShoppingLists =
    open Dto
    open Errors
    open SmartRecipes
    open SmartRecipes.DataAccess
    open SmartRecipes.Domain
    open FSharpPlus.Data
    open System
    open Generic
    open SmartRecipes.UseCases.ShoppingLists
    open SmartRecipes.UseCases
    open Infrastracture
    open Infrastructure
        
    // Get 
    
    type ShoppingListResponse = {
        ShoppingList: ShoppingListDto
    }
        
    let private serializeGet =
        Result.bimap (fun sl -> { ShoppingList = serializeShoppingList sl }) (function GetShoppingListError.Unauthorized -> error "Unauthorized.")
        
    let getHandler<'a> = 
        authorizedGetHandler (fun token _ -> ShoppingLists.get token) serializeGet
    
    // Add
    
    [<CLIMutable>]
    type AddItemsParameters = {
        itemIds: Guid list
    }
    
    type AddItemsError =
        | InvalidIds
        | BusinessError of ShoppingLists.AddItemsError
        
    let getItems parameters getByIds =
        let itemIds = parameters.itemIds
        getByIds itemIds
        |> IO.toEIO (fun items -> 
            let foundAll = Seq.length items = Seq.length itemIds
            if foundAll
                then Ok items
                else Error InvalidIds)
        
    let private addItemsToShoppingList accessToken action items = 
        action accessToken items
        |> ReaderT.mapError BusinessError
        
    let private serializeAddItemsBusinessError = function
        | ShoppingLists.AddItemsError.Unauthorized -> "Unauthorized."
        | ShoppingLists.AddItemsError.DomainError de -> 
            match de with 
            | ShoppingList.AddItemError.ItemAlreadyAdded -> "Item already added."
        
    let private serializeAddItemsError = function
        | InvalidIds -> invalidParameters [{ message = "Invalid."; parameter = "Ids" }]
        | BusinessError e -> error <| serializeAddItemsBusinessError e
                
    let private serializeAddItems = 
        Result.map (fun sl -> { ShoppingList = serializeShoppingList sl }) >> Result.mapError serializeAddItemsError

    let addItems action accessToken parameters getByIds = 
        getItems parameters getByIds
        >>= addItemsToShoppingList accessToken action  
    

    // Add foodstuffs
    
    let addFoodstuffs accessToken parameters =
        addItems ShoppingLists.addFoodstuffs accessToken parameters (List.toSeq >> IO.Foodstuffs.getByIds)
        
    let addFoodstuffsHandler<'a> =
        authorizedPostHandler addFoodstuffs serializeAddItems
        
    // Add recipes
    
    let addRecipes accessToken parameters =
        addItems ShoppingLists.addRecipes accessToken parameters (List.toSeq >> IO.Recipes.getByIds)
        
    let addRecipesHandler<'a> =
        authorizedPostHandler addRecipes serializeAddItems
        
    // Change foodstuff amount
    
    [<CLIMutable>]
    type ChangeAmountParameters = {
        foodstuffId: Guid
        amount: float
    }
    
    type ChangeAmountError = 
        | FoodstuffNotFound
        | AmountMustBePositive
        | BusinessError of ShoppingLists.ChangeAmountError
        
    let private getFoodstuff id =
        IO.Foodstuffs.getByIds [id]
        |> IO.toEIO (Seq.tryHead >> Option.toResult FoodstuffNotFound)
        
    let private parseAmount amount = 
        NonNegativeFloat.create amount
        |> Option.toResult AmountMustBePositive
        |> ReaderT.id
        
    let private changeFoodstuffAmount accessToken foodstuff amount =
        changeAmount accessToken foodstuff amount
        |> IO.mapError BusinessError
        
    let private serializeChangeAmountError = function
        | FoodstuffNotFound -> invalidParameters [{ message = "Not found."; parameter = "Foodstuff" }]
        | AmountMustBePositive -> invalidParameters [{ message = "Must be positive."; parameter = "Amount" }]
        | BusinessError e ->
            match e with
            | ShoppingLists.ChangeAmountError.Unauthorized -> error "Unauthorized."
            | ShoppingLists.ChangeAmountError.DomainError de ->
                match de with
                | ShoppingList.ChangeAmountError.ItemNotInList -> error "Foodstuff not in list."
                
                
    let private serializeChangeAmount = 
        Result.map (fun sl -> { ShoppingList = serializeShoppingList sl }) >> Result.mapError serializeChangeAmountError
        
    let changeAmount accessToken parameters = monad {
        let! foodstuff = getFoodstuff parameters.foodstuffId
        let! amount = parseAmount parameters.amount
        return! changeFoodstuffAmount accessToken foodstuff.id amount
    }

    let changeAmountHandler<'a> =
        authorizedPostHandler changeAmount serializeChangeAmount
        
    // Chnage person count
    
    [<CLIMutable>]
    type ChangePersonCountParameters = {
        recipeId: Guid
        personCount: int
    }
    
    type ChangePersonCountError = 
        | RecipeNotFound
        | PersonCountMustBePositive
        | BusinessError of ShoppingLists.ChangeAmountError
        
    let private getRecipe id e =
        IO.Recipes.getByIds [id]
        |> IO.toEIO (Seq.tryHead >> Option.toResult e)
        
    let private parsePersonCount personCount = 
        NaturalNumber.create personCount
        |> Option.toResult PersonCountMustBePositive
        |> IO.fromResult
        
    let private changeRecipePersonCount accessToken recipe amount =
        changePersonCount accessToken recipe amount
        |> ReaderT.mapError BusinessError
        
    let private serializeChangePersonCountError = function
        | RecipeNotFound -> invalidParameters [{ message = "Invalid."; parameter = "Recipe id" }]
        | PersonCountMustBePositive -> invalidParameters [{ message = "Must be positive."; parameter = "Person count" }]
        | BusinessError e ->
            match e with
            | ShoppingLists.ChangeAmountError.Unauthorized -> error "Unauthorized."
            | ShoppingLists.ChangeAmountError.DomainError de ->
                match de with 
                | ShoppingList.ChangeAmountError.ItemNotInList -> error "Recipe not in list."
                
    let private serializeChangePersonCount = 
        Result.bimap (fun sl -> { ShoppingList = serializeShoppingList sl }) serializeChangePersonCountError
        
    let changePersonCount accessToken parameters = monad {
        let! recipe = getRecipe parameters.recipeId RecipeNotFound
        let! personCount = parsePersonCount parameters.personCount
        return! changeRecipePersonCount accessToken recipe.Id personCount
    }
    
    let changePersonCountHandler<'a> =
        authorizedPostHandler changePersonCount serializeChangePersonCount

    // Remove foodstuff
    
    type RemoveFoodstuffsParameters = {
        ids: Guid list
    }
    
    type RemoveFoodstuffsError = 
        | FoodstuffNotFound
        | BusinessError of ShoppingLists.RemoveItemsError
        | GetByIdsBusinessError of Foodstuffs.GetByIdsError
        
    let private getFoodstuffIds accessToken parameters =
       Foodstuffs.getByIds accessToken parameters.ids
       |> IO.mapError GetByIdsBusinessError
       |> IO.map (Seq.map (fun (f: Foodstuff) -> f.id))

    let private checkAllFoodstuffsFound parameters foodstuffIds =
        let result =
           if (Seq.length foodstuffIds) <> (Seq.length parameters.ids)
               then Error FoodstuffNotFound
               else Ok foodstuffIds
            
        IO.fromResult result
    
    let private removeFoodstuffsFromList accessToken foodstuffIds = 
        ShoppingLists.removeFoodstuffs accessToken foodstuffIds
        |> IO.mapError BusinessError
        
    let private serializeRemoveFoodstuffsError = function 
        | FoodstuffNotFound -> invalidParameters [{ message = "Invalid."; parameter = "Foodstuff id" }]
        | BusinessError e -> 
            match e with
            | ShoppingLists.RemoveItemsError.Unauthorized -> error "Unauthorized."
            | ShoppingLists.RemoveItemsError.DomainError de ->
                match de with 
                | ShoppingList.RemoveItemError.ItemNotInList -> error "Foodstuff not in list."
        | GetByIdsBusinessError e -> 
            match e with
            | Foodstuffs.GetByIdsError.Unauthorized -> error "Unauthorized."
        
    let private serializeRemoveFoodstuffs = 
        Result.bimap (fun sl -> { ShoppingList = serializeShoppingList sl }) serializeRemoveFoodstuffsError
    
    let removeFoodstuffs accessToken parameters = 
        getFoodstuffIds accessToken parameters
        >>= checkAllFoodstuffsFound parameters
        >>= removeFoodstuffsFromList accessToken
        
    let removeFoodstuffsHandler<'a> = 
        authorizedPostHandler removeFoodstuffs serializeRemoveFoodstuffs
        
    // Remove recipe
    
    type RemoveRecipeParameters = {
        ids: Guid list
    }
    
    type RemoveRecipeError = 
        | RecipeNotFound
        | BusinessError of ShoppingLists.RemoveItemsError
        | GetByIdsBusinessError of Recipes.GetByIdsError
        
    let private getRecipeIds accessToken parameters =
       Recipes.getByIds accessToken parameters.ids
       |> IO.mapError GetByIdsBusinessError
       |> IO.map (Seq.map (fun (f: Recipe) -> f.Id))

    let private checkAllRecipesFound parameters recipeIds =
        let result =
           if (Seq.length recipeIds) <> (Seq.length parameters.ids)
               then Error RecipeNotFound
               else Ok recipeIds
            
        IO.fromResult result
    
    let private removeRecipesFromList accessToken recipes = 
        ShoppingLists.removeRecipes accessToken recipes
        |> IO.mapError BusinessError
        
    let private serializeRemoveRecipesError = function 
        | RecipeNotFound -> invalidParameters [{ message = "Invalid."; parameter = "Recipe id" }]
        | BusinessError e -> 
            match e with
            | ShoppingLists.RemoveItemsError.Unauthorized -> error "Unauthorized."
            | ShoppingLists.RemoveItemsError.DomainError de ->
                match de with 
                | ShoppingList.RemoveItemError.ItemNotInList -> error "Recipe not in list."
        | GetByIdsBusinessError e -> 
            match e with
            | Recipes.GetByIdsError.Unauthorized -> error "Unauthorized."
        
    let private serializeRemoveRecipes = 
        Result.bimap (fun sl -> { ShoppingList = serializeShoppingList sl }) serializeRemoveRecipesError
    
    let removeRecipes accessToken parameters = 
        getRecipeIds accessToken parameters
        >>= checkAllRecipesFound parameters
        >>= removeRecipesFromList accessToken
        
    let removeRecipesHandler<'a> = 
        authorizedPostHandler removeRecipes serializeRemoveRecipes
    
    module Recommend =
        
        type Error =
            | BusinessError of ShoppingLists.RecommendError
            
        type Response = {
            Recipes: RecipeDto list
        }
        
        let private serialize =
            Result.bimap (fun rs -> { Recipes = Seq.map serializeRecipe rs |> Seq.toList }) (function RecommendError.Unaturhorized -> error "Uauthorized.")
            
        let handler<'a> = 
            authorizedGetHandler (fun token _ -> ShoppingLists.recommend token) serialize