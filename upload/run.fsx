#r "System.Net.Http"
#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"
#r "HttpMultipartParser"

open System.Text
open System.Net
open System.Net.Http
open System.Configuration
open Newtonsoft.Json
open Microsoft.Azure
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Blob
open Microsoft.WindowsAzure.Storage.Table
open HttpMultipartParser
 
// cette premiere declaration c'est celle d'un objet , 
// en c# c'est une class en f# c'est un type enfin c'est la structure d'un objet
// en f# ya pas de {} ou de ; les nlocs de codes sonts organiser en indentation plus exactement 4 espaces 

type UserFilesRec(partitionKey, rowKey, name) = 
    inherit TableEntity(partitionKey, rowKey) // le mot clef inherit pour heriter d'un autre type TableEntity dans notre cas
    new(name) = UserFilesRec("temp", Guid.NewGuid().ToString(), name) // declaration d'un constructeur en utilisant le mot clef new 
    
    member val ContainerName = "" with get, set // c'est l'equivalent de : public string ContainerName { get; set; } 
    member val ParentFolder = "" with get, set
    member val IsFolder = false with get, set
    member val Name = name with get, set
    member val Uri = "" with get, set 

let Run(req: HttpRequestMessage, log: TraceWriter, username: string, outTable: ICollector<UserFilesRec>) =
    async {
        log.Info(sprintf 
            "F# HTTP trigger function processed a request.")
      
        // CloudStorageAccount Instanciation based on the ActiveConnectionStringName on the application settings that 
        // point to the connection string to use
        let GetStorageAccount:CloudStorageAccount =    // le mot clef let c'est pour les declarations autrement c'est pour associer un nom a une valeur 
                                                       // et toutes les valeurs sont immutable cad inchangeable une fois on affecte une valeur on ne peu la changer  
            ConfigurationManager.AppSettings.["ConnectionStringToUse"]
            |> fun s -> ConfigurationManager.ConnectionStrings.[s].ConnectionString // le symbole |> c'est ce qu'on appel un pipe ca sert a utiliser le resultat "sortie" d'une fonction
            |> CloudStorageAccount.Parse            // comme valeur d'entree de la suivante 

        // Blobs operations
        let GetBlobClient (storageAccount:CloudStorageAccount):CloudBlobClient = 
            storageAccount.CreateCloudBlobClient()    

        let GetContainer (containerName:string) (client:CloudBlobClient) = 
            let container = client.GetContainerReference containerName
            container.CreateIfNotExists() |> ignore
            container
        
        let GetBlockBlob (blobName:string) (container:CloudBlobContainer) = 
            container.GetBlockBlobReference(blobName)
        
        // Tables operations
        let GetTableClient (storageAccount:CloudStorageAccount) = 
            storageAccount.CreateCloudTableClient()
        
        let GetTable (tableName:string) (client:CloudTableClient) =
            let table = client.GetTableReference(tableName)
            table.CreateIfNotExists() |> ignore
            table
        
        let ComposeQuery query f = 
            query >> f 
        
        let ComposeTableQuery condition f = 
            TableQuery().Where(condition) 
            |> f 
        
        let GetTableRows tableName = 
            GetStorageAccount
            |> GetTableClient
            |> GetTable tableName
        
        let Insert record = 
            let operation = TableOperation.Insert(record)
            (GetStorageAccount
            |> GetTableClient
            |> GetTable "UsersFiles").Execute(operation)
        
        let ExtractFilesFromRequestContentAsync (requestMessage:HttpRequestMessage) = 
            async {
                let! requestStream =  requestMessage.Content.ReadAsStreamAsync() |> Async.AwaitTask
                let parser = new MultipartFormDataParser(requestStream, Encoding.UTF8)
                return (Seq.toArray parser.Files)
            } |> Async.RunSynchronously        
        
        let ExtractFoldersHierarchy (files:FilePart array):string array = 
            files 
            |> Seq.map 
                (fun i -> 
                    let name = 
                        if i.Name.StartsWith("/") then 
                            i.Name.Substring(1, i.Name.Length - 1)
                        else 
                            i.Name
                    
                    let length = name.Length 
                    let structureTokens:string array = name.Split('/')   
                    if structureTokens.Length = 1 then
                        name
                    else  
                        name.Substring(0, (name.Length - structureTokens.Last().Length))
                )
            |> Seq.toArray 
        
        let ExtractPathKey file:string = 
            let rec removeSlash (i:string):string = 
                if i.StartsWith("/") then 
                    let r = i.Substring(1, i.Length - 1) 
                    removeSlash r 
                else 
                    i  
            let getPath (i:string):string = 
                let r = i.Split('/')
                if r.Length = 1 then 
                    i 
                else 
                    i.Substring(0, (i.Length - r.Last().Length))

            let name = 
                removeSlash file
                |> getPath
            name
            
        let containerName = sprintf "%s-files" username
        let files = (ExtractFilesFromRequestContentAsync req)   

        let container = 
            GetStorageAccount
            |> GetBlobClient
            |> GetContainer containerName

        let table = 
            GetStorageAccount
            |> GetTableClient
            |> GetTable "UsersFiles"
        
        let dirs = ExtractFoldersHierarchy files 
        log.Info(sprintf "%A" dirs)

        let filesUris = 
            files
            |> Seq.map 
                (fun i ->  
                    let path = 
                        (ExtractPathKey i.Name)                       
                    
                    let blockBlob = 
                        container 
                        |> GetBlockBlob (sprintf "%s%s" path i.FileName)
                    blockBlob.UploadFromStreamAsync(i.Data)
                    let row = new UserFilesRec(username, Guid.NewGuid().ToString(), i.FileName)
                    row.ContainerName <- containerName
                    row.Uri <- blockBlob.Uri.ToString() 
                    row.IsFolder <- false
                    row.ParentFolder <- path
                    Insert row
                    blockBlob.Uri)


        // match name with
        // | Some x ->
        //     return req.CreateResponse(HttpStatusCode.OK, "Hello " + x.Value);
        // | None ->
        //     let! data = req.Content.ReadAsStringAsync() |> Async.AwaitTask

        //     if not (String.IsNullOrEmpty(data)) then
        //         let named = JsonConvert.DeserializeObject<Named>(data)
        //         return req.CreateResponse(HttpStatusCode.OK, "Hello " + named.name);
        //     else
        return req.CreateResponse(HttpStatusCode.OK, filesUris);
    } |> Async.RunSynchronously
