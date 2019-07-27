namespace SmartRecipes.Api

open System.Threading.Tasks
open FSharp.Json
open Infrastructure
open SmartRecipes.Domain
open Errors
open SmartRecipes.DataAccess.Foodstuffs
open SmartRecipes.DataAccess.Recipes
open SmartRecipes.DataAccess.ShoppingLists
open SmartRecipes.DataAccess.Tokens
open SmartRecipes.DataAccess.Users

module Parse =
    let option v =
        Option.map v >> Validation.traverseOption
        
    let seqOf v =
        Seq.map v >> Validation.traverseSeq
    
    let nonEmptyString error =
        NonEmptyString.create >> Validation.ofOption error
        
    let nonEmptyStringOption error =
        nonEmptyString error |> option
        
    let uriOption error =
        option (Uri.create >> Validation.mapFailure error)
        
    let naturalNumber error =
        NaturalNumber.create >> Validation.ofOption error
        
    let nonNegativeFloat error =
        NonNegativeFloat.create >> Validation.ofOption error
        
module Environment =     

    open System
    open SmartRecipes.DataAccess
    open SmartRecipes.UseCases.DateTimeProvider

    type ProductionEnvironment() =
    
        interface IUserDao with 
            member e.getById id = Users.Mongo.getById id
            member e.getByEmail email = Users.Mongo.getByEmail email
            member e.add user = Users.Mongo.add user
            
        interface ITokensDao with 
            member e.get v = Tokens.Mongo.get v
            member e.add user = Tokens.Mongo.add user
            
        interface IFoodstuffDao with 
            member e.getByIds ids = Foodstuffs.Mongo.getByIds ids
            member e.search q = Foodstuffs.Mongo.search q
            member e.add f = Foodstuffs.Mongo.add f
            
        interface IRecipesDao with 
            member e.getByIds ids = Recipes.Mongo.getByIds ids
            member e.getByAccount acc = Recipes.Mongo.getByAccount acc
            member e.search q = Recipes.Mongo.search q
            member e.add r = Recipes.Mongo.add r
            member e.getRecommendedationCandidates ids = Recipes.Mongo.getRecommendedationCandidates ids
            
        interface IShoppingsListsDao with 
            member e.getByAccount acc = ShoppingLists.Mongo.getByAccount acc
            member e.update l = ShoppingLists.Mongo.update l
            member e.add l = ShoppingLists.Mongo.add l
            
        interface IDateTimeProvider with
            member p.nowUtc () = DateTime.UtcNow
                
    let getEnvironment () = ProductionEnvironment()
            
module Generic =
    open Giraffe
    open Microsoft.AspNetCore.Http
    open FSharpPlus.Data
    open FSharp.Control.Tasks
    open Environment
    
    let private deserialize<'a> json =
        try
            Ok <| Json.deserialize<'a> json
        with
            | ex -> Error ex.Message
            
    let private serializeToJson obj =
        Json.serializeEx (JsonConfig.create(jsonFieldNaming = Json.lowerCamelCase)) obj
    
    let private setStatusCode (ctx: HttpContext) code =
        ctx.SetStatusCode code
            
    let private bindQueryString<'a> (ctx: HttpContext) =
        try 
            ctx.BindQueryString<'a>()
        with ex -> failwith ex.Message
        
    let private bindModelAsync<'a> (ctx: HttpContext): Task<Result<'a, string>> = task {
        let! body = ctx.ReadBodyFromRequestAsync ()
        return deserialize body
    }
        
    let private getHeader (ctx: HttpContext) name =
        ctx.GetRequestHeader(name)
        
    let private getResult env next ctx handler (serialize: 'c -> Result<'d, Errors.Error>) parameters =
        let result = handler parameters |> ReaderT.execute env |> serialize
        let response =
            match result with 
            | Ok s -> setStatusCode ctx 200 |> (fun _ -> text <| serializeToJson s) 
            | Error e -> setStatusCode ctx 400 |> (fun _ -> text <| serializeToJson e)
        response next ctx
        
    let getHandler handler serialize next ctx = 
        bindQueryString<'parameters> ctx |> getResult (getEnvironment ()) next ctx handler serialize
    
    let postHandler handler serialize next ctx = 
        task {
            let! parameters = bindModelAsync ctx
            return!
                match parameters with
                | Ok p -> getResult (getEnvironment ()) next ctx handler serialize p
                | Error e -> getResult () next ctx (fun _ -> error e |> Error |> ReaderT.id) id ()
        }
            
    let authorizedGetHandler handler serialize next ctx = 
        let accessToken = match getHeader ctx "authorization" with Ok t -> t | Error _ -> ""
        getHandler (handler accessToken) serialize next ctx
    
    let authorizedPostHandler handler serialize next ctx =
        let accessToken = match getHeader ctx "authorization" with Ok t -> t | Error _ -> ""
        postHandler (handler accessToken) serialize next ctx