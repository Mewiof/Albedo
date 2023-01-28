using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Albedo {

	public delegate void SerializerDelegate(Writer writer);

	public delegate void ServerMessageHandlerDelegate(ConnToClientData sender);
	public delegate void ServerAltMessageHandlerDelegate(ConnToClientData sender, Reader reader);
	public delegate void ClientMessageHandlerDelegate();
	public delegate void ClientAltMessageHandlerDelegate(Reader reader);

	public abstract class DataHandler {

		public readonly Writer writer;
		public readonly Transport transport;
		public readonly NetManager manager;

		/// <summary>uId, delegate</summary>
		private readonly Dictionary<ushort, ServerMessageHandlerDelegate> _serverMessageHandlers;
		/// <summary>uId, delegate</summary>
		private readonly Dictionary<ushort, ServerAltMessageHandlerDelegate> _serverAltMessageHandlers;
		/// <summary>uId, delegate</summary>
		private readonly Dictionary<ushort, ClientMessageHandlerDelegate> _clientMessageHandlers;
		/// <summary>uId, delegate</summary>
		private readonly Dictionary<ushort, ClientAltMessageHandlerDelegate> _clientAltMessageHandlers;

		public DataHandler(Transport transport, NetManager manager) {
			writer = new();
			this.transport = transport;
			this.manager = manager;
			_serverMessageHandlers = new();
			_serverAltMessageHandlers = new();
			_clientMessageHandlers = new();
			_clientAltMessageHandlers = new();
		}

		#region Registration

		// TODO: prevent registering system message ids

		private ArgumentException GetAlreadyRegisteredException(uint messageUId, string handlerDelegateName) {
			return new ArgumentException(manager.Logger.GetTaggedText($"Unable to register a message ('{handlerDelegateName}->{messageUId}' is already registered)"));
		}

		/// <summary>[!] Server</summary>
		public void RegisterMessageHandler(ushort messageUId, ServerMessageHandlerDelegate handler) {
			if (_serverMessageHandlers.ContainsKey(messageUId)) {
				throw GetAlreadyRegisteredException(messageUId, typeof(ServerMessageHandlerDelegate).Name);
			}
			_serverMessageHandlers[messageUId] = handler;
		}

		/// <summary>[!] Server</summary>
		public void RegisterMessageHandler(ushort messageUId, ServerAltMessageHandlerDelegate handler) {
			if (_serverAltMessageHandlers.ContainsKey(messageUId)) {
				throw GetAlreadyRegisteredException(messageUId, typeof(ServerAltMessageHandlerDelegate).Name);
			}
			_serverAltMessageHandlers[messageUId] = handler;
		}

		/// <summary>[!] Client</summary>
		public void RegisterMessageHandler(ushort messageUId, ClientMessageHandlerDelegate handler) {
			if (_clientMessageHandlers.ContainsKey(messageUId)) {
				throw GetAlreadyRegisteredException(messageUId, typeof(ClientMessageHandlerDelegate).Name);
			}
			_clientMessageHandlers[messageUId] = handler;
		}

		/// <summary>[!] Client</summary>
		public void RegisterMessageHandler(ushort messageUId, ClientAltMessageHandlerDelegate handler) {
			if (_clientAltMessageHandlers.ContainsKey(messageUId)) {
				throw GetAlreadyRegisteredException(messageUId, typeof(ClientAltMessageHandlerDelegate).Name);
			}
			_clientAltMessageHandlers[messageUId] = handler;
		}

		public void UnregisterMessageHandler(ushort messageUId) {
			_ = _serverMessageHandlers.Remove(messageUId);
			_ = _serverAltMessageHandlers.Remove(messageUId);
			_ = _clientMessageHandlers.Remove(messageUId);
			_ = _clientAltMessageHandlers.Remove(messageUId);
		}

		#endregion

		#region Read

		private Exception GetUnregisteredMessageException(ushort messageUId, string handlerDelegateName) {
			return new Exception(manager.Logger.GetTaggedText($"Received an unregistered message ('{handlerDelegateName}->{messageUId}')"));
		}

		public void ServerOnData(ConnToClientData sender, ArraySegment<byte> segment) {
			using PooledReader reader = ReaderPool.Get(segment);
			ushort messageUId = reader.GetUShort();

			// auth
			if (messageUId == NetAuthenticator.REQUEST_MESSAGE_UNIQUE_ID) {
				if (sender.authStage != ConnToClientData.AuthStage.NotAuthenticated) {
					throw new Exception(manager.Logger.GetTaggedText("Received multiple auth requests"));
				}
				sender.authStage = ConnToClientData.AuthStage.Requested;
			} else if (sender.authStage != ConnToClientData.AuthStage.Authenticated) {
				throw new Exception(manager.Logger.GetTaggedText("Received an unauthorized message"));
			}

			// try req & res
			if (TryHandleReqRes(messageUId, sender.id, reader)) {
				return;
			}

			// with additional data?
			if (reader.Available > 0) {
				if (!_serverAltMessageHandlers.TryGetValue(messageUId, out ServerAltMessageHandlerDelegate altHandler)) {
					throw GetUnregisteredMessageException(messageUId, typeof(ServerAltMessageHandlerDelegate).Name);
				}
				altHandler.Invoke(sender, reader);
				return;
			}

			if (!_serverMessageHandlers.TryGetValue(messageUId, out ServerMessageHandlerDelegate handler)) {
				throw GetUnregisteredMessageException(messageUId, typeof(ServerMessageHandlerDelegate).Name);
			}
			handler.Invoke(sender);
		}

		public void ClientOnData(ArraySegment<byte> segment) {
			using PooledReader reader = ReaderPool.Get(segment);
			ushort messageUId = reader.GetUShort();

			// auth
			if (manager.Client.connId == 0 && messageUId != NetAuthenticator.RESPONSE_MESSAGE_UNIQUE_ID) {
				// pack & enqueue
				writer.SetPosition(0);
				writer.PutUShort(messageUId);
				writer.PutRaw(reader.GetRemainingDataSegment().ToArray());
				manager.authenticator.dataQueue.Enqueue(writer.Data.ToArray());
				return;
			}

			// try req & res
			if (TryHandleReqRes(messageUId, 0U, reader)) {
				return;
			}

			// with additional data?
			if (reader.Available > 0) {
				if (!_clientAltMessageHandlers.TryGetValue(messageUId, out ClientAltMessageHandlerDelegate altHandler)) {
					throw GetUnregisteredMessageException(messageUId, typeof(ClientAltMessageHandlerDelegate).Name);
				}
				altHandler.Invoke(reader);
				return;
			}

			if (!_clientMessageHandlers.TryGetValue(messageUId, out ClientMessageHandlerDelegate handler)) {
				throw GetUnregisteredMessageException(messageUId, typeof(ClientMessageHandlerDelegate).Name);
			}
			handler.Invoke();
		}

		#endregion

		#region Write

		/// <summary>Resets the internal 'writer' and writes 'uId' to it</summary>
		protected void SetMessage(ushort uId) {
			writer.SetPosition(0);
			writer.PutUShort(uId);
		}

		/// <summary>Resets the internal 'writer' and writes to it</summary>
		protected void SetMessage(ushort uId, SerializerDelegate serializerDelegate) {
			SetMessage(uId);
			serializerDelegate.Invoke(writer);
		}

		#endregion

		#region Req & Res
		public const ushort
			REQUEST_MESSAGE_U_ID = ushort.MaxValue - 1,
			RESPONSE_MESSAGE_U_ID = ushort.MaxValue - 2;

		public enum ReqResStatusCode : byte {
			Unregistered,
			Timeout,
			Success
		}

		public readonly struct EmptyMessage : INetSerializable {

			public static readonly EmptyMessage instance = new();

			public void Serialize(Writer writer) { }

			public void Deserialize(Reader reader) { }
		}

		#region Request Handler
		public delegate void RequestHandlingResultDelegate<TResponse>(ReqResStatusCode statusCode, TResponse response, SerializerDelegate extra)
			where TResponse : struct, INetSerializable;
		public delegate void RequestHandledDelegate(uint connId, uint requestId, ReqResStatusCode statusCode, INetSerializable response, SerializerDelegate extra);
		public delegate UniTaskVoid RequestHandlerDelegate<TRequest, TResponse>(RequestHandlerData data, TRequest request, RequestHandlingResultDelegate<TResponse> result)
			where TRequest : struct, INetSerializable
			where TResponse : struct, INetSerializable;

		public readonly struct RequestHandlerData {

			public readonly ushort uId;
			public readonly uint id;
			public readonly DataHandler dataHandler;
			public readonly uint connId;
			public readonly Reader reader;

			public RequestHandlerData(ushort uId, uint id, DataHandler dataHandler, uint connId, Reader reader) {
				this.uId = uId;
				this.id = id;
				this.dataHandler = dataHandler;
				this.connId = connId;
				this.reader = reader;
			}
		}

		public interface IRequestHandler {

			public void OnRequest(RequestHandlerData data, RequestHandledDelegate onHandled);
		}

		public readonly struct RequestHandler<TRequest, TResponse> : IRequestHandler
			where TRequest : struct, INetSerializable
			where TResponse : struct, INetSerializable {

			private readonly RequestHandlerDelegate<TRequest, TResponse> _registeredDelegate;

			public RequestHandler(RequestHandlerDelegate<TRequest, TResponse> requestDelegate) {
				_registeredDelegate = requestDelegate;
			}

			public void OnRequest(RequestHandlerData data, RequestHandledDelegate onHandled) {
				TRequest request = new();
				if (data.reader != null) {
					request.Deserialize(data.reader);
				}
				_registeredDelegate?.Invoke(data, request, (statusCode, response, extra) =>
					onHandled.Invoke(data.connId, data.id, statusCode, response, extra));
			}
		}

		private readonly Dictionary<ushort, IRequestHandler> _requestHandlers = new();

		public void RegisterRequestHandler<TRequest, TResponse>(ushort requestUId, RequestHandlerDelegate<TRequest, TResponse> handlerDelegate = null)
			where TRequest : struct, INetSerializable
			where TResponse : struct, INetSerializable {

			if (_requestHandlers.ContainsKey(requestUId)) {
				throw new Exception($"A request handler with this identifier is already registered ({requestUId})");
			}

			_requestHandlers[requestUId] = new RequestHandler<TRequest, TResponse>(handlerDelegate);
		}

		public void UnregisterRequestHandler(ushort requestUId) {
			_ = _requestHandlers.Remove(requestUId);
		}
		#endregion

		#region Response Handler
		public delegate void ResponseHandlerDelegate<TResponse>(ResponseHandlerData data, ReqResStatusCode statusCode, TResponse response)
			where TResponse : INetSerializable;

		public readonly struct ResponseHandlerData {

			public readonly uint id;
			public readonly DataHandler dataHandler;
			public readonly uint connId;
			public readonly Reader reader;

			public ResponseHandlerData(uint id, DataHandler dataHandler, uint connId, Reader reader) {
				this.id = id;
				this.dataHandler = dataHandler;
				this.connId = connId;
				this.reader = reader;
			}
		}

		public interface IResponseHandler {

			public void OnResponse(ResponseHandlerData data, ReqResStatusCode statusCode, ResponseHandlerDelegate<INetSerializable> handlerDelegate);
			public bool IsRequestTypeValid(Type value);
		}

		public readonly struct ResponseHandler<TRequest, TResponse> : IResponseHandler
			where TRequest : struct, INetSerializable
			where TResponse : struct, INetSerializable {

			private readonly ResponseHandlerDelegate<TResponse> _registeredDelegate;

			public ResponseHandler(ResponseHandlerDelegate<TResponse> responseDelegate) {
				_registeredDelegate = responseDelegate;
			}

			public void OnResponse(ResponseHandlerData data, ReqResStatusCode statusCode, ResponseHandlerDelegate<INetSerializable> handlerDelegate) {
				TResponse response = new();
				if (statusCode == ReqResStatusCode.Success && data.reader != null) {
					response.Deserialize(data.reader);
				}
				_registeredDelegate?.Invoke(data, statusCode, response);
				handlerDelegate?.Invoke(data, statusCode, response);
			}

			public bool IsRequestTypeValid(Type value) {
				return typeof(TRequest) == value;
			}
		}

		private readonly Dictionary<ushort, IResponseHandler> _responseHandlers = new();

		public void RegisterResponseHandler<TRequest, TResponse>(ushort requestUId, ResponseHandlerDelegate<TResponse> handlerDelegate = null)
			where TRequest : struct, INetSerializable
			where TResponse : struct, INetSerializable {

			if (_responseHandlers.ContainsKey(requestUId)) {
				throw new Exception($"A response handler with this identifier is already registered ({requestUId})");
			}

			_responseHandlers[requestUId] = new ResponseHandler<TRequest, TResponse>(handlerDelegate);
		}

		public void UnregisterResponseHandler(ushort requestUId) {
			_ = _responseHandlers.Remove(requestUId);
		}
		#endregion

		public readonly struct RequestCallback {

			public readonly uint id;
			public readonly DataHandler dataHandler;
			public readonly IResponseHandler responseHandler;
			public readonly ResponseHandlerDelegate<INetSerializable> handlerDelegate;

			public RequestCallback(uint id, DataHandler dataHandler, IResponseHandler responseHandler, ResponseHandlerDelegate<INetSerializable> handlerDelegate) {
				this.id = id;
				this.dataHandler = dataHandler;
				this.responseHandler = responseHandler;
				this.handlerDelegate = handlerDelegate;
			}

			public void OnTimeout() {
				responseHandler.OnResponse(new(id, dataHandler, 0U, null), ReqResStatusCode.Timeout, handlerDelegate);
			}

			public void OnResponse(uint connId, Reader reader, ReqResStatusCode statusCode) {
				responseHandler.OnResponse(new(id, dataHandler, connId, reader), statusCode, handlerDelegate);
			}
		}

		// NOTE: clear on server & client start
		private readonly ConcurrentDictionary<uint, RequestCallback> _requestCallbacks = new();

		// NOTE: reset on server & client start
		#region NextRequestId
		private uint _nextRequestId;
		/// <summary>
		/// Increases by 1
		/// </summary>
		private uint GetNextRequestId() {
			return _nextRequestId++;
		}
		private void ResetNextRequestId() {
			_nextRequestId = 1U;
		}
		#endregion

		/// <param name="time">Milliseconds</param>
		private async UniTask StartRequestTimeout(uint requestId, int time) {
			await UniTask.Delay(time);
			if (_requestCallbacks.TryRemove(requestId, out RequestCallback callback)) {
				callback.OnTimeout();
			}
		}

		/// <param name="timeout">Milliseconds</param>
		/// <returns>Id</returns>
		private uint CreateRequest(IResponseHandler responseHandler, ResponseHandlerDelegate<INetSerializable> handlerDelegate, int timeout) {
			uint requestId = GetNextRequestId();
			_requestCallbacks.TryAdd(requestId, new RequestCallback(requestId, this, responseHandler, handlerDelegate));
			if (timeout > 0) {
				StartRequestTimeout(requestId, timeout).Forget();
			}
			return requestId;
		}

		protected void CreateAndWriteRequest<TRequest>(Writer writer, ushort uId, TRequest request, ResponseHandlerDelegate<INetSerializable> handlerDelegate, int timeout, SerializerDelegate extra)
			where TRequest : struct, INetSerializable {

			if (!_responseHandlers.ContainsKey(uId)) {
				handlerDelegate.Invoke(new(GetNextRequestId(), this, 0U, null), ReqResStatusCode.Unregistered, EmptyMessage.instance);
				throw new Exception($"Unable to create a request ('{uId}' is not registered)");
			}

			IResponseHandler responseHandler = _responseHandlers[uId];

			if (!responseHandler.IsRequestTypeValid(typeof(TRequest))) {
				handlerDelegate.Invoke(new(GetNextRequestId(), this, 0U, null), ReqResStatusCode.Unregistered, EmptyMessage.instance);
				throw new Exception("Unable to create a request (invalid type)");
			}

			// create
			uint requestId = CreateRequest(responseHandler, handlerDelegate, timeout);

			// reset
			writer.SetPosition(0);

			// write
			writer.PutUShort(REQUEST_MESSAGE_U_ID);
			writer.PutUShort(uId);
			writer.PutUInt(requestId);
			writer.Put(request);
			extra?.Invoke(writer);
		}

		#region Handle Request
		/// <summary>
		/// Sends a response message
		/// </summary>
		private void OnRequestHandled(uint connId, uint requestId, ReqResStatusCode statusCode, INetSerializable response, SerializerDelegate extra) {
			// reset
			writer.SetPosition(0);
			// write
			writer.PutUShort(RESPONSE_MESSAGE_U_ID);
			writer.PutUInt(requestId);
			writer.PutByte((byte)statusCode);
			writer.Put(response);
			extra?.Invoke(writer);
			// send
			if (connId > 0) {
				transport.ServerSend(connId, writer.Data, DeliveryMethod.Reliable);
				return;
			}
			transport.ClientSend(writer.Data, DeliveryMethod.Reliable);
		}

		private void HandleRequest(uint connId, Reader reader) {
			ushort requestUId = reader.GetUShort();
			uint requestId = reader.GetUInt();

			if (!_requestHandlers.ContainsKey(requestUId)) {
				OnRequestHandled(connId, requestId, ReqResStatusCode.Unregistered, EmptyMessage.instance, null);
				throw new Exception($"Received an unregistered request ({requestUId})");
			}

			_requestHandlers[requestUId].OnRequest(new(requestUId, requestId, this, connId, reader), OnRequestHandled);
		}
		#endregion

		private void HandleResponse(uint connId, Reader reader) {
			uint requestId = reader.GetUInt();
			ReqResStatusCode statusCode = (ReqResStatusCode)reader.GetByte();
			if (_requestCallbacks.ContainsKey(requestId)) {
				_requestCallbacks[requestId].OnResponse(connId, reader, statusCode);
				_ = _requestCallbacks.TryRemove(requestId, out _);
			}
		}

		private bool TryHandleReqRes(ushort messageUId, uint connId, Reader reader) {
			if (messageUId == REQUEST_MESSAGE_U_ID) {
				HandleRequest(connId, reader);
				return true;
			}

			if (messageUId == RESPONSE_MESSAGE_U_ID) {
				HandleResponse(connId, reader);
				return true;
			}

			return false;
		}
		#endregion
	}
}
