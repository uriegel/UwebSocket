open System
open System.IO
open Websocket

if Environment.CurrentDirectory.Contains "netcoreapp" then
    Environment.CurrentDirectory <- Path.Combine (Environment.CurrentDirectory, "../../../../")

let onSocketSession (session: Types.Session) = 
    let onReceive payload =
        printfn "Message received: %s" payload
    let onClose () =
        printfn "Client has disconnected"
    session.Send "Das wäre schön"
    onReceive, onClose

let configuration = Configuration.create {
    Configuration.createEmpty() with 
        Port = 9865
        Requests = [ useWebsocket "/websocketurl" onSocketSession; Static.useStatic (Path.Combine (Directory.GetCurrentDirectory (), "webroot")) "/" ]
}
let server = Server.create configuration 
server.start ()
Console.ReadLine () |> ignore
server.stop ()