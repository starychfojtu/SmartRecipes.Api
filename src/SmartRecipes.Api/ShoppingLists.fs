namespace SmartRecipes.Api

module ShoppingLists =
    open Dto
    open Errors
    open SmartRecipes.DataAccess
    open SmartRecipes.DataAccess.Foodstuffs
    open SmartRecipes.DataAccess.Recipes
    open SmartRecipes.Domain
    open FSharpPlus
    open FSharpPlus.Data
    open System
    open Generic
    open SmartRecipes.UseCases.ShoppingLists
    open SmartRecipes.UseCases
    open Environment
    open Infrastracture
    open Infrastructure
        
    // Get 
    
    type ShoppingListResponse = {
        ShoppingList: ShoppingListDto
    }
    
    type GetShoppingListError =
        | Unauthorized
        
    let private getShoppingList accountId =
        ReaderT(fun env -> env.IO.ShoppingLists.get accountId |> Ok)
        
    let private serializeGet =
        Result.bimap (fun sl -> { ShoppingList = serializeShoppingList sl }) (function Unauthorized -> error "Unaturhorized.")
    
    let get accessToken _ =
        Users.authorize Unauthorized accessToken
        >>= getShoppingList
        
    let getHandler<'a> = 
        authorizedGetHandler get serializeGet
    
    // Add
    
    [<CLIMutable>]
    type AddItemsParameters = {
        itemIds: Guid list
    }
    
    type AddItemsError =
        | InvalidIds
        | BusinessError of AddItemError
        
    let getItems parameters getByIds = ReaderT(fun env ->
        let itemIds = parameters.itemIds
        let items = (getByIds env) itemIds
        let foundAll = Seq.length items = Seq.length itemIds
        if foundAll
            then Ok items
            else Error InvalidIds
    )
        
    let private addItemsToShoppingList accesstToken action items = 
        action accesstToken items
        |> ReaderT.mapError BusinessError
        
    let private serializeAddItemsBusinessError = function
        | ShoppingLists.AddItemError.Unauthorized -> "Unauthorized."
        | ShoppingLists.AddItemError.DomainError de -> 
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
        addItems ShoppingLists.addFoodstuffs accessToken parameters (fun env ids -> List.toSeq ids |> env.IO.Foodstuffs.getByIds)
        
    let addFoodstuffsHandler<'a> =
        authorizedPostHandler addFoodstuffs serializeAddItems
        
    // Add recipes
    
    let addRecipes accessToken parameters =
        addItems ShoppingLists.addRecipes accessToken parameters (fun env ids -> List.toSeq ids |> env.IO.Recipes.getByIds)
        
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
        ReaderT(fun env -> env.IO.Foodstuffs.getById id |> Option.toResult FoodstuffNotFound)
        
    let private parseAmount amount = 
        NonNegativeFloat.create amount
        |> Option.toResult AmountMustBePositive
        |> ReaderT.id
        
    let private changeFoodtuffAmount accessToken foodstuff amount =
        changeAmount accessToken foodstuff amount
        |> ReaderT.mapError BusinessError
        
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
        return! changeFoodtuffAmount accessToken foodstuff.id amount
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
        ReaderT(fun env -> env.IO.Recipes.getById id |> Option.toResult e)
        
    let private parsePersonCount personCount = 
        NaturalNumber.create personCount
        |> Option.toResult PersonCountMustBePositive
        |> ReaderT.id
        
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
        return! changeRecipePersonCount accessToken recipe personCount
    }
    
    let changePersonCountHandler<'a> =
        authorizedPostHandler changePersonCount serializeChangePersonCount

    // Remove foodstuff
    
    type RemoveFoodstuffParameters = {
        foodstuffId: Guid
    }
    
    type RemoveFoodstuffError = 
        | FoodstuffNotFound
        | BusinessError of ShoppingLists.RemoveItemError
    
    let private getFoodstuffId parameters = 
        ReaderT(fun env -> env.IO.Foodstuffs.getById parameters.foodstuffId |> Option.map (fun f -> f.id) |> Option.toResult FoodstuffNotFound )
    
    let private removeFoodstuffFromList accessToken foodstuffId = 
        ShoppingLists.removeFoodstuff accessToken foodstuffId
        |> ReaderT.mapError BusinessError
        
    let private serializeRemoveFoodstuffError = function 
        | FoodstuffNotFound -> invalidParameters [{ message = "Invalid."; parameter = "Foodstuff id" }]
        | BusinessError e -> 
            match e with
            | ShoppingLists.RemoveItemError.Unauthorized -> error "Unauthorized."
            | ShoppingLists.RemoveItemError.DomainError de ->
                match de with 
                | ShoppingList.RemoveItemError.ItemNotInList -> error "Foodstuff not in list."
        
    let private serializeRemoveFoodstuff = 
        Result.bimap (fun sl -> { ShoppingList = serializeShoppingList sl }) serializeRemoveFoodstuffError
    
    let removeFoodstuff accessToken parameters = 
        getFoodstuffId parameters
        >>= removeFoodstuffFromList accessToken
        
    let removeFoodstuffHandler<'a> = 
        authorizedPostHandler removeFoodstuff serializeRemoveFoodstuff
        
    // Remove recipe
    
    type RemoveRecipeParameters = {
        recipeId: Guid
    }
    
    type RemoveRecipeError = 
        | RecipeNotFound
        | BusinessError of ShoppingLists.RemoveItemError
    
    let private removeRecipeFromList accessToken recipe = 
        ShoppingLists.removeRecipe accessToken recipe
        |> ReaderT.mapError BusinessError
        
    let private serializeRemoveRecipeError = function 
        | RecipeNotFound -> invalidParameters [{ message = "Invalid."; parameter = "Recipe id" }]
        | BusinessError e -> 
            match e with
            | ShoppingLists.RemoveItemError.Unauthorized -> error "Unauthorized."
            | ShoppingLists.RemoveItemError.DomainError de ->
                match de with 
                | ShoppingList.RemoveItemError.ItemNotInList -> error "Recipe not in list."
        
    let private serializeRemoveRecipe = 
        Result.bimap (fun sl -> { ShoppingList = serializeShoppingList sl }) serializeRemoveRecipeError
    
    let removeRecipe accessToken parameters = 
        getRecipe parameters RecipeNotFound
        >>= removeRecipeFromList accessToken
        
    let removeRecipeHandler<'a> = 
        authorizedPostHandler removeRecipe serializeRemoveRecipe