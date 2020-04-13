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
            headers.Add ("Sec-WebSocket-Extensions", "permessage-deflate; client_no_context_takeover; server_no_context_takeover")
        else
            headers

    let headerBytes = Response.createHeader responseData headers 101 "Switching Protocols" None
    do! requestData.session.networkStream.AsyncWrite (headerBytes, 0, headerBytes.Length)    
    let networkStream = requestSession.HandsOff ()
    let networkStream = new BufferedStream (networkStream, 8192)

    let onReceive, onClose = onSession {
        Send = Send.send networkStream
    }
    Receive.start networkStream onReceive onClose []
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



//         public void Send(string payload)
//         {
//             var memStm = new MemoryStream(Encoding.UTF8.GetBytes(payload));
//             WriteStream(memStm);
//         }

//         public void SendJson(object jsonObject)
//         {
//             var type = jsonObject.GetType();
//             var jason = new DataContractJsonSerializer(type);
//             var memStm = new MemoryStream();
//             jason.WriteObject(memStm, jsonObject);
//             memStm.Position = 0;
//             WriteStream(memStm);
//         }

//         public Task SendAsync(string payload)
//         {
//             var buffer = Encoding.UTF8.GetBytes(payload);
//             var memStm = new MemoryStream(buffer, 0, buffer.Length, false, true);
//             return WriteStreamAsync(memStm);
//         }

//         public Task SendJsonAsync(object jsonObject)
//         {
//             var type = jsonObject.GetType();
//             var jason = new DataContractJsonSerializer(type);
//             var memStm = new MemoryStream();
//             jason.WriteObject(memStm, jsonObject);
//             memStm.Position = 0;
//             return WriteStreamAsync(memStm);
//         }

//         public void StartMessageReceiving()
//         {
//             var wsr = new WebSocketReceiver(networkStream);
//             wsr.BeginMessageReceiving(async (wsDecodedStream, exception) =>
//             {
//                 try
//                 {
//                     if (isClosed || exception != null)
//                     {
//                         isClosed = true;
//                         Logger.Current.LowTrace(() => "Connection closed");
//                         if (exception is ConnectionClosedException)
//                             await OnClose();
//                         Instances.DecrementActive();
//                     }
//                     else
//                     {
//                         var payload = wsDecodedStream.Payload;
//                         await OnMessage(payload);
//                     }
//                 }
//                 catch (Exception e)
//                 {
//                     Logger.Current.Warning($"Exception occurred while processing web socket request: {e}");
//                     Instances.DecrementActive();
//                 }
//             }, this);
//         }

//         public void Close()
//         {
//             if (isClosed)
//                 return;
//             Instances.DecrementActive();
//             isClosed = true;
//             try
//             {
//                 networkStream.Close();
//             }
//             catch { }
//         }

//         public void SendPong(string payload)
//         {
//             var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));
//             WriteStream(stream, OpCode.Pong);
//         }

//         protected virtual Task OnClose() => Task.FromResult(0);
//         protected abstract Task OnMessage(string payload);

//         void WriteStream(MemoryStream payloadStream, OpCode? opCode = null)
//         {
//             try
//             {
//                 var (buffer, deflate) = GetPayload(payloadStream);
//                 var header = WriteHeader(buffer.Length, deflate, opCode);
//                 semaphoreSlim.Wait();
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

//         async Task<int> Read(byte[] buffer, int offset, int length)
//         {
//             var result = await networkStream.ReadAsync(buffer, offset, length);
//             if (result == 0)
//                 throw new ConnectionClosedException();
//             return result;
//         }

//         #endregion

//         #region Fields	

//         static int sessionIDCreator;
//         readonly RequestSession session;
//         readonly IServer server;
//         readonly Configuration configuration;
//         Stream networkStream;
//         readonly string host;
//         readonly object locker = new object();
//         bool isClosed;
//         SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);
//         bool useDeflate;

//         #endregion
//     }
// }


// using Caseris.Http.Interfaces;
// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Text;
// using System.Threading.Tasks;

// namespace Caseris.Http.WebSockets
// {
// 	public class WebSocketReceiver
// 	{
// 		#region Constructor	

// 		public WebSocketReceiver(Stream networkStream) => this.networkStream = networkStream;

// 		#endregion

// 		#region Methods	

// 		public void BeginMessageReceiving(Func<WsDecodedStream, Exception, Task> action, IWebSocketInternalSession internalSession)
// 		{
// 			var headerBuffer = new byte[14];
// 			this.internalSession = internalSession;
// 			networkStream.BeginRead(headerBuffer, 0, 2, async ar =>
// 			{
// 				try
// 				{
// 					var read = networkStream.EndRead(ar);
// 					if (read == 1)
// 						read = Read(headerBuffer, 1, 1);
// 					if (read == 0)
// 						throw new ConnectionClosedException();
// 					await MessageReceiving(headerBuffer, action);
// 				}
// 				catch (ConnectionClosedException ce)
// 				{
// 					await action(null, ce);
// 				}
// 				catch (IOException)
// 				{
// 					await action(null, new ConnectionClosedException());
// 				}
// 				catch
// 				{
// 				}
// 			}, null);
// 		}

// 		async Task MessageReceiving(byte[] headerBuffer, Func<WsDecodedStream, Exception, Task> action)
// 		{
// 			var read = 2;
// 			var fin = (byte)((byte)headerBuffer[0] & 0x80) == 0x80;
// 			var deflated = (byte)((byte)headerBuffer[0] & 0x40) == 0x40;
// 			var opcode = (OpCode)((byte)headerBuffer[0] & 0xf);
// 			switch (opcode)
// 			{
// 				case OpCode.Close:
// 					Close();
// 					break;
// 				case OpCode.Ping:
// 				case OpCode.Pong:
// 				case OpCode.Text:
// 				case OpCode.ContinuationFrame:
// 					break;
// 				default:
// 				{
// 					Close();
// 					break;
// 				}
// 			}
// 			var mask = (byte)(headerBuffer[1] >> 7);
// 			var length = (ulong)(headerBuffer[1] & ~0x80);

// 			//If the second byte minus 128 is between 0 and 125, this is the length of message. 
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
// 			else if (length > 127)
// 				Close();
// 			if (length == 0)
// 			{
// 				//if (opcode == OpCode.Ping)
// 				// await MessageReceivingAsync(action);
// 				return;
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
// 		}

// 		int Read(byte[] buffer, int offset, int length)
// 		{
// 			var result = networkStream.Read(buffer, offset, length);
// 			if (result == 0)
// 				throw new ConnectionClosedException();
// 			return result;
// 		}

// 		void Close()
// 		{
// 			try
// 			{
// 				networkStream.Close();
// 			}
// 			catch { }
// 			throw new ConnectionClosedException();
// 		}

// 		#endregion

// 		#region Fields	

// 		IWebSocketInternalSession internalSession;
// 		Stream networkStream;
// 		WsDecodedStream wsDecodedStream;

// 		#endregion
// 	}
// }

// namespace Caseris.Http.WebSockets
// {
// 	public class WsDecodedStream : Stream
// 	{
// 		#region Properties	

// 		public int DataPosition { get; protected set; }
// 		public string Payload { get; protected set; }

// 		#endregion

// 		#region Constructor	

// 		public WsDecodedStream(Stream stream, int length, byte[] key, bool encode, bool isDeflated)
// 		{
// 			this.stream = stream;
// 			this.length = length;
// 			this.key = key;
// 			this.encode = encode;
// 			buffer = new byte[length];
// 			this.isDeflated = isDeflated;
// 			ReadStream(0);
// 		}

// 		protected WsDecodedStream()
// 		{
// 		}

// 		#endregion

// 		#region Stream	

// 		public override bool CanRead { get { return true; } }

// 		public override bool CanSeek { get { return false; } }

// 		public override bool CanWrite { get { return false; } }

// 		public override long Length { get { return length - DataPosition; } }

// 		public override long Position
// 		{
// 			get { return _Position; }
// 			set
// 			{
// 				if (value > Length)
// 					throw new IndexOutOfRangeException();
// 				_Position = value;
// 			}
// 		}
// 		long _Position;

// 		public override void Flush()
// 		{
// 		}

// 		public override int Read(byte[] buffer, int offset, int count)
// 		{
// 			if (Position + count > length - DataPosition)
// 				count = (int)length - DataPosition - (int)Position;
// 			if (count == 0)
// 				return 0;

// 			Array.Copy(this.buffer, offset + DataPosition + Position, buffer, offset, count);
// 			Position += count;

// 			return count;
// 		}

// 		public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();

// 		public override void SetLength(long value) => throw new NotImplementedException();

// 		public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();

// 		public virtual int WriteHeaderToAnswer(byte[] bytes, int position)
// 		{
// 			Array.Copy(buffer, 0, bytes, position, DataPosition);
// 			return DataPosition;
// 		}

// 		void ReadStream(int position)
// 		{
// 			var read = 0;
// 			while (read < length - position)
// 			{
// 				var newlyRead = stream.Read(buffer, read + position, (int)length - position - read);
// 				if (newlyRead == 0)
// 					throw new ConnectionClosedException();
// 				read += newlyRead;
// 			}

// 			if (encode)
// 				for (var i = 0; i < length - position; i++)
// 					buffer[i + position] = (Byte)(buffer[i + position] ^ key[i % 4]);

// 			if (position == 0)
// 			{
// 				if (isDeflated)
// 				{
// 					var ms = new MemoryStream(buffer, 0, (int)length);
// 					var outputStream = new MemoryStream();
// 					var compressedStream = new DeflateStream(ms, CompressionMode.Decompress, true);
// 					compressedStream.CopyTo(outputStream);
// 					compressedStream.Close();
// 					outputStream.Capacity = (int)outputStream.Length;
// 					var deflatedBuffer = outputStream.GetBuffer();
// 					Payload = Encoding.UTF8.GetString(deflatedBuffer, 0, deflatedBuffer.Length);
// 				}
// 				else
// 					Payload = Encoding.UTF8.GetString(buffer, 0, (int)length);
// 				DataPosition = Payload.Length + 1;
// 			}
// 		}

// 		#endregion

// 		#region Methods

// 		public void AddContinuation(int length, byte[] key, bool encode)
// 		{
// 			var oldLength = buffer.Length;
// 			Array.Resize<byte>(ref buffer, oldLength + length);
// 			this.key = key;
// 			this.encode = encode;
// 			this.length += length;
// 			ReadStream(oldLength);
// 		}

// 		#endregion

// 		#region Fields

// 		Stream stream;
// 		byte[] buffer;
// 		long length;
// 		byte[] key;
// 		bool encode;
// 		bool isDeflated;

// 		#endregion
// 	}
//}
