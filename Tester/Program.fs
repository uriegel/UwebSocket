open System
open System.IO
open Websocket
open System.Text
open FSharpTools

let mutable send = None

if Environment.CurrentDirectory.Contains "netcoreapp" then
    Environment.CurrentDirectory <- Path.Combine (Environment.CurrentDirectory, "../../../../")

let onSocketSession (session: Types.Session) = 
    let onReceive (payload: Stream) =
        use tr = new StreamReader (payload)
        printfn "Message received: %s" <| tr.ReadToEnd ()
    let onClose () =
        send <- None            
        printfn "Client has disconnected"

    let sendBytes = session.Start onReceive onClose
    
    // let getBytes (text: string) = Encoding.Default.GetBytes text
    // let sendString = getBytes >> sendBytes
    // send <- Some sendString

    let readStream (stream: Stream) = 
        use br = new BinaryReader (stream)
        br.ReadBytes(int stream.Length)
    let sendObject = Json.serializeToBuffer >> sendBytes
    send <- Some sendObject

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
        for i in [1..1_000_000] do
            match send with
            | Some send -> 
                send "Das wäre auch schön gewesen"
                //send {| name = "Der schöne Name"; number = 12345 |}
            | None -> ()

server.stop ()