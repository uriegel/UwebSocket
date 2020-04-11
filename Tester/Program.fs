open System
open System.IO

let configuration = Configuration.create {
    Configuration.createEmpty() with 
        Port = 9865
        Requests = [ Static.useStatic (Path.Combine (Directory.GetCurrentDirectory (), "webroot")) "/" ]
}
let server = Server.create configuration 
server.start ()
Console.ReadLine () |> ignore
server.stop ()