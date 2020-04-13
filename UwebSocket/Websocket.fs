module Websocket
open System
open System.Security.Cryptography
open System.Text
open System.IO
open Session
open Types
open System.Buffers.Binary

let upgradeWebsocket (onSession: Types.Session -> ((string->unit)*(unit->unit))) (requestSession: RequestSession) secKey = async {
    let secKey = secKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
    let sha1 = SHA1.Create ()
    let hashKey = sha1.ComputeHash (Encoding.UTF8.GetBytes secKey)
    let base64Key = Convert.ToBase64String hashKey

    let extensions = 
        match requestSession.Header.Header "Sec-WebSocket-Extensions" with
        | Some ext -> ext |> String.splitChar ';'
        | None -> [||]

    let requestData = requestSession.RequestData :?> RequestData.RequestData
    let responseData = ResponseData.create requestData
    let headers = Map.empty
    let headers = headers.Add ("Connection", "Upgrade")
    let headers = headers.Add ("Upgrade", "websocket")
    let headers = headers.Add ("Sec-WebSocket-Accept", base64Key)
    
    let headers, deflate = 
        if extensions |> Array.contains "permessage-deflate" then
            headers.Add ("Sec-WebSocket-Extensions", "permessage-deflate; client_no_context_takeover"), true
        else
            headers, false

    let headerBytes = Response.createHeader responseData headers 101 "Switching Protocols" None
    do! requestData.session.networkStream.AsyncWrite (headerBytes, 0, headerBytes.Length)    
    let networkStream = requestSession.HandsOff ()

    let onReceive, onClose = onSession {
        // TODO: onClose in send
        Send = Send.send networkStream deflate
    }
    Receive.start networkStream onReceive onClose [] false
    return true
}
    
let useWebsocket url onSession (requestSession: RequestSession) = async {
    match requestSession.Header.Header "Upgrade", requestSession.Header.Header "Sec-WebSocket-Key" with
    | Some upgrade, Some secKey when upgrade = "websocket" && requestSession.Url = url -> 
        return! upgradeWebsocket onSession requestSession secKey
    | _ -> return false    
}

// TODO: close
// TODO: protocols
