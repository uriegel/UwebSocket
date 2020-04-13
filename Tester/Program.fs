open System
open System.IO
open Websocket

let mutable send = None

if Environment.CurrentDirectory.Contains "netcoreapp" then
    Environment.CurrentDirectory <- Path.Combine (Environment.CurrentDirectory, "../../../../")

let onSocketSession (session: Types.Session) = 
    let onReceive payload =
        printfn "Message received: %s" payload
    let onClose () =
        send <- None            
        printfn "Client has disconnected"

    send <- Some (session.Start onReceive onClose)
    

let configuration = Configuration.create {
    Configuration.createEmpty() with 
        Port = 9865
        Requests = [ useWebsocket "/websocketurl" onSocketSession; Static.useStatic (Path.Combine (Directory.GetCurrentDirectory (), "webroot")) "/" ]
}
let server = Server.create configuration 
server.start ()

let mutable running = true

while running do
    let line = Console.ReadLine () 
    if line.Length = 0 then 
        running <- false
    else
        async { 
            for i in [1..1_000_000] do
                match send with
                | Some send -> 
                    do! send "Das wäre auch schön gewesen"
                | None -> ()
        }|> Async.Start

server.stop ()