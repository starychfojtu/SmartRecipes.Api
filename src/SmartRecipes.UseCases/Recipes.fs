namespace SmartRecipes.UseCases

module Recipes =
    open System
    open SmartRecipes.DataAccess
    open FSharpPlus.Data
    open Infrastructure
    open FSharpPlus
    open SmartRecipes.Domain
    open SmartRecipes.Domain.Foodstuff
    open SmartRecipes.Domain.NonEmptyString
    open SmartRecipes.Domain.NaturalNumber
    open SmartRecipes.Domain.Recipe
                
    // Get all by account
    
    type GetMyRecipesError =
        | Unauthorized
        
    let private getRecipes accountId = 
        Recipes.getByAccount accountId |> ReaderT.hoistOk
        
    let getMyRecipes accessToken =
        Users.authorize Unauthorized accessToken
        >>= getRecipes
    
    // Get by ids
    
    type GetByIdsError = 
        | Unauthorized

    let private getRecipesByIds ids = 
        Recipes.getByIds ids |> ReaderT.hoistOk
        
    let getByIds accessToken ids = 
        Users.authorize Unauthorized accessToken
        >>= (fun _ -> getRecipesByIds ids)
        
    // Search
    
    type SearchError = 
        | Unauthorized
        
    let private searchFoodstuff query = 
        Recipes.search query |> ReaderT.hoistOk
        
    let search accessToken query =
        Users.authorize Unauthorized accessToken
        >>= fun _ -> searchFoodstuff query
        
    // Create
    
    type CreateIngredientError = 
        | DuplicateFoodstuffIngredient
        | FoodstuffNotFound
    
    type CreateError =
        | Unauthorized
        | InvalidIngredients of CreateIngredientError list
        
    type IngredientParameters = {
        FoodstuffId: Guid
        Amount: Amount option
        Comment: NonEmptyString option
        DisplayLine: NonEmptyString
    }
    
    let createIngredientParameters foodstuffId amount comment displayLine = {
        FoodstuffId = foodstuffId
        Amount = amount
        Comment = comment
        DisplayLine = displayLine
    }
    
    type RecipeParameters = {
        Name: NonEmptyString
        PersonCount: NaturalNumber
        ImageUrl: Uri option
        Url: Uri option
        Description: NonEmptyString option
        Ingredients: NonEmptyList<IngredientParameters>
        Difficulty: Difficulty option
        CookingTime: CookingTime option
        Tags: NonEmptyString seq
        Rating: Rating option
        NutritionPerServing: NutritionPerServing
    }
    
    let createParameters name personCount imageUrl url description ingredients diffuculty cookingTime tags rating nutrition = {
        Name = name
        PersonCount = personCount
        ImageUrl = imageUrl
        Url = url
        Description = description
        Ingredients = ingredients
        Difficulty = diffuculty
        CookingTime = cookingTime
        Tags = tags
        Rating = rating
        NutritionPerServing = nutrition
    }

    let private getFoodstuffs parameters =
        Foodstuffs.getByIds <| Seq.map (fun i -> i.FoodstuffId) parameters

    let mkFoodstuffId guid (foodstuffMap: Map<_, Foodstuff> ) = 
        match Map.tryFind guid foodstuffMap with
        | Some f -> Success f.id
        | None -> Failure [FoodstuffNotFound]
        
    let private mkIngredient foodstuffMap parameters =
        Recipe.createIngredient
        <!> mkFoodstuffId parameters.FoodstuffId foodstuffMap
        <*> (Success parameters.Amount)
        <*> (Success parameters.DisplayLine)
        <*> (Success parameters.Comment)
    
    let private checkIngredientsNotDuplicate ingredients =
        let foodstuffIds = NonEmptyList.map (fun (i: Ingredient) -> i.FoodstuffId) ingredients
        if NonEmptyList.isDistinct foodstuffIds 
            then Success ingredients 
            else Failure [DuplicateFoodstuffIngredient]
            
    let private mkIngredients parameters foodstuffs =
        let foodstuffMap = Seq.map (fun (f: Foodstuff) -> (f.id.value, f)) foodstuffs |> Map.ofSeq
        NonEmptyList.map (mkIngredient foodstuffMap) parameters 
        |> Validation.traverseNonEmptyList
        |> Validation.bind checkIngredientsNotDuplicate
        |> Validation.mapFailure InvalidIngredients
        |> Validation.toResult
        |> ReaderT.id
            
    let private createIngredients parameters =
        getFoodstuffs parameters
        |> ReaderT.hoist
        |> ReaderT.bind (mkIngredients parameters)

    let private createRecipe parameters ingredients accountId = 
        Recipe.create
            parameters.Name
            accountId
            parameters.PersonCount
            ingredients
            parameters.Description
            parameters.CookingTime
            parameters.NutritionPerServing
            parameters.Difficulty
            (Seq.map RecipeTag parameters.Tags)
            parameters.ImageUrl
            parameters.Url
            parameters.Rating

    let private addToDatabase recipe = 
        Recipes.add recipe |> ReaderT.hoistOk
        
    let create accessToken parameters = monad {
        let! accountId = Users.authorize Unauthorized accessToken
        let! ingredients = createIngredients parameters.Ingredients
        let recipe = createRecipe parameters ingredients accountId
        return! addToDatabase recipe
    }
    
    // Update
    
    type UpdateError = 
        | RecipeNotFound
        | InvalidParameters of CreateError
    
    let update accessToken parameters =
        // TODO
        ()
    
    // Delete
    
    let delete accessToken parameters =
        // TODO
        ()