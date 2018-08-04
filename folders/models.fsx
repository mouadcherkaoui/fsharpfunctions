#r "Microsoft.WindowsAzure.Storage"

open System
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Table

type UserFiles(partitionKey, rowKey) =
    inherit TableEntity(partitionKey, rowKey)
    new() = UserFiles("", Guid.NewGuid().ToString())
    member val ContainerName = "" with get, set
    member val ParentFolder = "" with get, set
    member val IsFolder = "" with get, set
    member val Name = "" with get, set
    member val Uri = "" with get, set

let newUserFiles pkey = UserFiles(pkey, Guid.NewGuid().ToString())
    