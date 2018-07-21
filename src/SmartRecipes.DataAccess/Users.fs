module DataAccess.Users
    open System.Net.Mail
    open Context
    open DataAccess.Model
    open Models.Account
    open Models.Password
    open FSharpPlus.Data
    
    let private toDb account = {
        id = match account.id with AccountId id -> id
        email = account.credentials.email.Address
        password = match account.credentials.password with Password p -> p
    }
    
    let private toModel (dbAccount: DbAccount): Account = { 
        id = AccountId dbAccount.id
        credentials = 
        {
            email = new MailAddress(dbAccount.email)
            password = Password dbAccount.password
        }
    }
        
    let add account =
        Reader(fun (ctx: Context) ->
            toDb account |> ctx.Add |> ignore
            ctx.SaveChanges () |> ignore
            account
        )
        
    let getAccountByEmail = 
        Reader(fun (ctx: Context) -> (fun (email: MailAddress) ->
            ctx.Accounts 
            |> Seq.filter (fun a -> a.email = email.Address)
            |> Seq.tryHead
            |> Option.map (fun a -> toModel a)
        ))