#r "System.Net.Http"
#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"

open System.Net
open System.Net.Http
open Newtonsoft.Json
open Microsoft.Azure
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Table

type UserFiles(partitionKey, rowid) = 
    inherit TableEntity(partitionKey, rowid)
    new () = UserFiles("username", Guid.NewGuid().ToString())    
    member val ContainerName = "" with get, set
    member val ParentFolder = "" with get, set
    member val IsFolder = false with get, set
    member val Name = "" with get, set
    member val Uri = "" with get, set

[<CLIMutable>]
type UserFilesRec = {
    Id:string
    ContainerName:string
    ParentFolder:string
    IsFolder:bool
    Name:string
    Uri:string
}

let Run(req: HttpRequestMessage, log: TraceWriter, name: string) =
    async {
        log.Info(sprintf 
            "F# HTTP trigger function processed a request.")
        
        let GetTable tableName= 
            ConfigurationManager.AppSettings.["ConnectionStringToUse"]
            |> fun s -> ConfigurationManager.ConnectionStrings.[s]
            |> CloudStorageAccount.Parse
            |> fun account -> account.CreateCloudTableClient()
            |> fun client -> client.GetTableReference tableName
        
        let ExecuteOperation o = 
            GetTable "UsersFiles"
            |> fun table -> table.Execute o

        let EntityToRec (e:UserFiles) =
            {
                Id= e.RowKey
                ContainerName= e.ContainerName
                ParentFolder= e.ParentFolder
                IsFolder= e.IsFolder
                Name= e.Name
                Uri= e.Uri             
            }

        let GetRecords username= 
            TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, username)
            |> TableQuery<UserFiles>().Where
            |> (GetTable "UsersFiles").ExecuteQuery<UserFiles>
            //|> Seq.map (fun e -> EntityToRec e) 
        
        let result = 
            GetRecords name
            |> Seq.map (fun i -> EntityToRec i)
            |> Seq.toArray
        log.Info (sprintf "%A" result)
        return req.CreateResponse(HttpStatusCode.OK, result);
    } |> Async.RunSynchronously
