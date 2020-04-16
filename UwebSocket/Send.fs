module Send
open System
open System.IO
open System.Threading
open System.IO.Compression
open System.Text
open Types

let private locker = new SemaphoreSlim (1)
let private minSizeForDeflate = 50
let private bFinal = 0uy

let send (networkStream: Stream) deflate onClose (payloadBuffer: byte array) = async {
    do! locker.WaitAsync () |> Async.AwaitTask
    try 
        try
            let deflate = deflate && payloadBuffer.Length > minSizeForDeflate
            let payloadBuffer = 
                if deflate then
                    let ms = new MemoryStream ()
                    let compressedStream = new DeflateStream (ms, CompressionMode.Compress, true)
                    compressedStream.Write (payloadBuffer, 0, payloadBuffer.Length)
                    compressedStream.Close ()
                    ms.WriteByte bFinal
                    ms.Capacity <- int ms.Length
                    ms.GetBuffer ()
                else
                    payloadBuffer
            let frmopcode = (if deflate then 0xC0uy else 0x80uy) + (byte Opcode.Text)  //'FIN is set, and OPCODE is 1 (Text) or opCode
            do! networkStream.AsyncWrite [| frmopcode |]
            let len = payloadBuffer.Length
            if len <= 125 then
                do! networkStream.AsyncWrite [| byte len |]
            elif len <= (int UInt16.MaxValue) then
                let byteArray = BitConverter.GetBytes (uint16 len)
                do! networkStream.AsyncWrite [| 126uy; byteArray.[1]; byteArray.[0] |]
            else
                let byteArray = BitConverter.GetBytes (uint64 len)
                do! networkStream.AsyncWrite [| 
                    127uy
                    byteArray.[7]
                    byteArray.[6]
                    byteArray.[5]
                    byteArray.[4]
                    byteArray.[3]
                    byteArray.[2]
                    byteArray.[1]
                    byteArray.[0]
                |]
            do! networkStream.AsyncWrite (payloadBuffer, 0, len)
        with
        | e -> 
            printf "Exception in send: %O" e
            onClose ()
    finally
        locker.Release () |> ignore
}
