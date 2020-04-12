module Receive
open System.IO
open Types
open System
open System.Buffers.Binary

let start (networkStream: Stream) =
    async { 
        let! buffer = networkStream.AsyncRead 2
        // TODO: buffer.length = 0: Connection Closed
        let fin = buffer.[0] &&& 0x80uy = 0x80uy
        let deflated = buffer.[0] &&& 0x40uy = 0x40uy
        let opcode: Opcode = LanguagePrimitives.EnumOfValue (buffer.[0] &&& 0xfuy)
        match opcode with
        | Opcode.Close -> () // TODO: close ()
        | Opcode.Ping | Opcode.Pong | Opcode.Text | Opcode.ContinuationFrame -> ()
        | _ -> () // TODO: close ()
        let mask = buffer.[1] >>> 7
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
        

        printfn "Länge: %d" (int length)

// 			if (length < 126)
// 			{
// 				if (mask == 1)
// 					read += Read(headerBuffer, read, 4);
// 			}
// 			else if (length == 126)
// 			{
// 				// If length is 126, the following 2 bytes (16-bit unsigned integer), if 127, the following 8 bytes (64-bit unsigned integer) are the length.
// 				read += Read(headerBuffer, read, mask == 1 ? 6 : 2);
// 				var ushortbytes = new byte[2];
// 				ushortbytes[0] = headerBuffer[3];
// 				ushortbytes[1] = headerBuffer[2];
// 				length = BitConverter.ToUInt16(ushortbytes, 0);
// 			}
// 			else if (length == 127)
// 			{
// 				// If length is 127, the following 8 bytes (64-bit unsigned integer) is the length of message
// 				read += Read(headerBuffer, read, mask == 1 ? 12 : 8);
// 				var ulongbytes = new byte[8];
// 				ulongbytes[0] = headerBuffer[9];
// 				ulongbytes[1] = headerBuffer[8];
// 				ulongbytes[2] = headerBuffer[7];
// 				ulongbytes[3] = headerBuffer[6];
// 				ulongbytes[4] = headerBuffer[5];
// 				ulongbytes[5] = headerBuffer[4];
// 				ulongbytes[6] = headerBuffer[3];
// 				ulongbytes[7] = headerBuffer[2];
// 				length = BitConverter.ToUInt64(ulongbytes, 0);
// 			}

// 			byte[] key = null;
// 			if (mask == 1)
// 				key = new byte[4] { headerBuffer[read - 4], headerBuffer[read - 3], headerBuffer[read - 2], headerBuffer[read - 1] };
// 			if (wsDecodedStream == null)
// 				wsDecodedStream = new WsDecodedStream(networkStream, (int)length, key, mask == 1, deflated);
// 			else
// 				wsDecodedStream.AddContinuation((int)length, key, mask == 1);
// 			if (fin)
// 			{
// 				var receivedStream = wsDecodedStream;
// 				wsDecodedStream = null;
// 				if (opcode != OpCode.Ping)
// 					await action(receivedStream, null);
// 				else
// 					internalSession.SendPong(receivedStream.Payload);
// 			}

// 			BeginMessageReceiving(action, internalSession);
    
    } |> Async.Start   

