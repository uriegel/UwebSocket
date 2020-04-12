open System
open System.IO
open Websocket

let onSocketSession (session: Types.Session) = 
    session.Send "Das wäre schön"

let configuration = Configuration.create {
    Configuration.createEmpty() with 
        Port = 9865
        Requests = [ useWebsocket "/websocketurl" onSocketSession; Static.useStatic (Path.Combine (Directory.GetCurrentDirectory (), "webroot")) "/" ]
}
let server = Server.create configuration 
server.start ()
Console.ReadLine () |> ignore
server.stop ()