module Receive
open System.IO
open Types
open System
open System.Buffers.Binary
open System.Text
open System.IO.Compression

let rec start (networkStream: Stream) onReceive onClose buffers deflated =
    let textReceived fin opcode deflated byte2 = async {
        let mask = byte2 >>> 7 = 1uy
        let lengthCode = byte2 &&& ~~~0x80uy
        let! length = async {
            match lengthCode with
            | l when l < 126uy -> return int64 l
            | 126uy -> 
                let! buffer = networkStream.AsyncRead 2
                let n = BitConverter.ToUInt16 (buffer, 0)
                return int64 (BinaryPrimitives.ReverseEndianness n)
            | 127uy -> 
                let! buffer = networkStream.AsyncRead 8
                let n = BitConverter.ToUInt64 (buffer, 0)
                return int64 (BinaryPrimitives.ReverseEndianness n)
            | _ -> return 0L 
        }

        let! key = async {
            match mask with
            | true -> 
                let! buffer = networkStream.AsyncRead 4
                return Some buffer
            | false -> return None
        }

        let! buffer = networkStream.AsyncRead (int length)
        match key with
        | Some key ->
            for i in [0..(int length) - 1] do
                buffer.[i] <- buffer.[i] ^^^ key.[i % 4]
        | None -> ()

        let buffers = buffer :: buffers
        let buffers =
            if fin then
                let completeLength = buffers |> List.fold (fun acc elem -> acc + elem.Length) 0
                let buffer: byte array = Array.zeroCreate completeLength

                let rec copyBuffers (bufferList: byte array list) pos =
                    match bufferList with
                    | head :: tail -> 
                        let newPos = pos - head.Length
                        Array.Copy (head, 0, buffer, newPos, head.Length)
                        copyBuffers tail newPos
                    | [] -> bufferList

                copyBuffers buffers completeLength |> ignore
                    
                let buffer =
                    if deflated then
                        let ms = new MemoryStream (buffer, 0, buffer.Length)
                        let uncompressd = new MemoryStream ()
                        let compressedStream = new DeflateStream (ms, CompressionMode.Decompress, true)
                        compressedStream.CopyTo uncompressd
                        compressedStream.Close ()
                        uncompressd.Capacity <- int uncompressd.Length
                        uncompressd.GetBuffer ()
                    else
                        buffer
                onReceive <| Encoding.Default.GetString buffer                    
                []
            else
                buffers

        start networkStream onReceive onClose buffers deflated
    }

    async { 
        try
            let! buffer = networkStream.AsyncRead 2
            let fin = buffer.[0] &&& 0x80uy = 0x80uy
            let deflated = deflated || buffer.[0] &&& 0x40uy = 0x40uy
            let opcode: Opcode = LanguagePrimitives.EnumOfValue (buffer.[0] &&& 0xfuy)
            match opcode with
            | Opcode.Ping | Opcode.Pong | Opcode.Text | Opcode.ContinuationFrame 
                -> do! textReceived fin opcode deflated buffer.[1] 
            | Opcode.Close -> onClose ()
            | _ -> onClose ()
            with
            | :? EndOfStreamException -> onClose ()
            | e -> 
                printfn "Exception occurred: %O" e
                onClose ()
    
    } |> Async.Start   

