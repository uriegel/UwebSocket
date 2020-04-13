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
    
    let headers = 
        if extensions |> Array.contains "permessage-deflate" then
            headers.Add ("Sec-WebSocket-Extensions", "permessage-deflate; client_no_context_takeover")
        else
            headers

    let headerBytes = Response.createHeader responseData headers 101 "Switching Protocols" None
    do! requestData.session.networkStream.AsyncWrite (headerBytes, 0, headerBytes.Length)    
    let networkStream = requestSession.HandsOff ()
    let networkStream = new BufferedStream (networkStream, 8192)

    let onReceive, onClose = onSession {
        Send = Send.send networkStream
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

//         async Task WriteStreamAsync(MemoryStream payloadStream)
//         {
//             try
//             {
//                 var (buffer, deflate) = GetPayload(payloadStream);
//                 var header = WriteHeader(buffer.Length, deflate);
//                 await semaphoreSlim.WaitAsync();
//                 try
//                 {
//                     networkStream.Write(header, 0, header.Length);
//                     networkStream.Write(buffer, 0, buffer.Length);
//                 }
//                 catch
//                 {
//                     try
//                     {
//                         networkStream.Close();
//                     }
//                     catch { }
//                 }
//                 finally
//                 {
//                     semaphoreSlim.Release();
//                 }
//             }
// 			catch (ConnectionClosedException)
//             {
//             }
//         }

//         (byte[] buffer, bool deflate) GetPayload(MemoryStream payloadStream)
//         {
//             var deflate = useDeflate && payloadStream.Length > configuration.MinSizeForDeflate;
//             if (deflate)
//             {
//                 var ms = new MemoryStream();
//                 var compressedStream = new DeflateStream(ms, CompressionMode.Compress, true);
//                 payloadStream.CopyTo(compressedStream);
//                 compressedStream.Close();
//                 ms.WriteByte(0); // BFinal!
//                 payloadStream = ms;
//             }

//             payloadStream.Capacity = (int)payloadStream.Length;
//             return (payloadStream.GetBuffer(), deflate);
//         }

//         /// <summary>
//         /// Schreibt den WebSocketHeader
//         /// </summary>
//         /// <param name="payloadLength"></param>
//         /// <param name="deflate"></param>
//         /// <param name="opcode"></param>
//         byte[] WriteHeader(int payloadLength, bool deflate, OpCode? opcode = null)
//         {
//             if (opcode == null)
//                 opcode = OpCode.Text;
//             var length = payloadLength;
//             var FRRROPCODE = (byte)((deflate ? 0xC0 : 0x80) + (byte)(int)opcode.Value); //'FIN is set, and OPCODE is 1 (Text) or opCode

//             int headerLength;
//             if (length <= 125)
//                 headerLength = 2;
//             else if (length <= ushort.MaxValue)
//                 headerLength = 4;
//             else
//                 headerLength = 10;
//             var buffer = new byte[headerLength];
//             if (length <= 125)
//             {
//                 buffer[0] = FRRROPCODE;
//                 buffer[1] = Convert.ToByte(length);
//             }
//             else if (length <= ushort.MaxValue)
//             {
//                 buffer[0] = FRRROPCODE;
//                 buffer[1] = 126;
//                 var sl = (ushort)length;
//                 var byteArray = BitConverter.GetBytes(sl);
//                 var eins = byteArray[0];
//                 buffer[2] = byteArray[1];
//                 buffer[3] = eins;
//             }
//             else
//             {
//                 buffer[0] = FRRROPCODE;
//                 buffer[1] = 127;
//                 var byteArray = BitConverter.GetBytes((ulong)length);
//                 var eins = byteArray[0];
//                 var zwei = byteArray[1];
//                 var drei = byteArray[2];
//                 var vier = byteArray[3];
//                 var fünf = byteArray[4];
//                 var sechs = byteArray[5];
//                 var sieben = byteArray[6];
//                 buffer[2] = byteArray[7];
//                 buffer[3] = sieben;
//                 buffer[4] = sechs;
//                 buffer[5] = fünf;
//                 buffer[6] = vier;
//                 buffer[7] = drei;
//                 buffer[8] = zwei;
//                 buffer[9] = eins;
//             }
//             return buffer;
//         }


