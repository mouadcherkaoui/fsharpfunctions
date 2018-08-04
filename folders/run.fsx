#load "models.fsx"

#r "System.Net.Http"
#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"
#r "Microsoft.WindowsAzure.Configuration"

open System.Net
open System.Net.Http
open System.Configuration
open Newtonsoft.Json
open Microsoft.Azure
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Table

type UserFiles(partitionKey, rowKey) =
    inherit TableEntity(partitionKey, rowKey)
    new() = UserFiles("", Guid.NewGuid().ToString())
    member val ContainerName = "" with get, set
    member val ParentFolder = "" with get, set
    member val IsFolder = false with get, set
    member val Name = "" with get, set
    member val Uri = "" with get, set

let Run(req: HttpRequestMessage, log: TraceWriter, name: string) =
    async {
        let folderName =  
            req.GetQueryNameValuePairs()
            |> Seq.tryFind (fun q -> q.Key = "folder")
            |> fun r ->
                match r with
                | Some v -> v.Value
                | None -> ""
                
        let getStorageAccount = 
            CloudConfigurationManager.GetSetting("ConnectionStringToUse")
            |> fun s -> 
                log.Info(s)
                ConfigurationManager.ConnectionStrings.[s].ConnectionString
            |> CloudStorageAccount.Parse    
        
        let getTable tableName = 
            getStorageAccount
            |> fun acc -> acc.CreateCloudTableClient()
            |> fun clt -> 
                let table_ref = clt.GetTableReference tableName
                table_ref.CreateIfNotExists()
                table_ref

        let insert r = 
            TableOperation.Insert r 
            |> (getTable "UserFiles").Execute
        
        let id = Guid.NewGuid().ToString()
        let r = UserFiles(name, id)
        r.ContainerName <- String.Format("{0}-files", name)
        r.ParentFolder <- "" 
        r.IsFolder <- true
        r.Name <- name 
        r.Uri <- ""

        insert r |> ignore
        
        return req.CreateResponse(HttpStatusCode.OK, folderName);

    } |> Async.RunSynchronously
