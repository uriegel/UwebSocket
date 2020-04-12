module Receive
open System.IO
open Types
open System
open System.Buffers.Binary
open System.Text
open System.IO.Compression

let rec start (networkStream: Stream) =
    async { 
        let! buffer = networkStream.AsyncRead 2
        // TODO: EndofStreamException
        // TODO: buffer.length = 0: Connection Closed
        let fin = buffer.[0] &&& 0x80uy = 0x80uy
        let deflated = buffer.[0] &&& 0x40uy = 0x40uy
        let opcode: Opcode = LanguagePrimitives.EnumOfValue (buffer.[0] &&& 0xfuy)
        match opcode with
        | Opcode.Close -> () // TODO: close ()
        | Opcode.Ping | Opcode.Pong | Opcode.Text | Opcode.ContinuationFrame -> ()
        | _ -> () // TODO: close ()
        let mask = buffer.[1] >>> 7 = 1uy
        let lengthCode = buffer.[1] &&& ~~~0x80uy
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
            | 0uy when opcode = Opcode.Ping -> 
                // TODO: Send pong
                // await MessageReceivingAsync(action);
                // return
                return 0L
            | _ -> return 0L // TODO: close ()
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

        if fin then
            // TODO: if ping SendPong(receivedStream.Payload)
            if opcode = Opcode.Text then
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
                printfn "Die Wagenladung: %s" <| Encoding.Default.GetString buffer
            else if opcode = Opcode.Ping then
               printfn "Ping"
            
        start networkStream
    
    } |> Async.Start   

