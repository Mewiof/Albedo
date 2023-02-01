using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Albedo {

	public enum ReqResStatusCode : byte {
		Unregistered,
		Timeout,
		Success
	}

	#region Req
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

	public delegate void RequestHandlingResultDelegate<TResponse>(ReqResStatusCode statusCode, TResponse response, SerializerDelegate extra = null)
			where TResponse : struct, INetSerializable;
	public delegate void RequestHandledDelegate(uint connId, uint requestId, ReqResStatusCode statusCode, INetSerializable response, SerializerDelegate extra);
	public delegate UniTaskVoid RequestHandlerDelegate<TRequest, TResponse>(RequestHandlerData data, TRequest request, RequestHandlingResultDelegate<TResponse> result)
		where TRequest : struct, INetSerializable
		where TResponse : struct, INetSerializable;

	public interface IRequestHandler {

		public void OnRequest(RequestHandlerData data, RequestHandledDelegate onHandled);
	}

	public readonly struct RequestHandler<TRequest, TResponse> : IRequestHandler
		where TRequest : struct, INetSerializable
		where TResponse : struct, INetSerializable {

		private readonly RequestHandlerDelegate<TRequest, TResponse> _registeredDelegate;

		public RequestHandler(RequestHandlerDelegate<TRequest, TResponse> handlerDelegate) {
			_registeredDelegate = handlerDelegate;
		}

		public void OnRequest(RequestHandlerData data, RequestHandledDelegate onHandled) {
			TRequest request = new();
			if (data.reader != null) {
				request.Deserialize(data.reader);
			}
			_registeredDelegate?.Invoke(data, request, (statusCode, response, extra) =>
				onHandled.Invoke(data.connId, data.id, statusCode, response, extra)).Forget();
		}
	}
	#endregion

	#region Res
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

		public ResponseHandler(ResponseHandlerDelegate<TResponse> handlerDelegate) {
			_registeredDelegate = handlerDelegate;
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

		/// <param name="connId">Sender</param>
		public void OnResponse(uint connId, Reader reader, ReqResStatusCode statusCode) {
			responseHandler.OnResponse(new(id, dataHandler, connId, reader), statusCode, handlerDelegate);
		}
	}

	public readonly struct AsyncResponseData<TResponse>
		where TResponse : struct, INetSerializable {

		public readonly ResponseHandlerData data;
		public readonly ReqResStatusCode statusCode;
		public readonly TResponse response;

		public AsyncResponseData(ResponseHandlerData data, ReqResStatusCode statusCode, TResponse response) {
			this.data = data;
			this.statusCode = statusCode;
			this.response = response;
		}
	}

	public abstract partial class DataHandler {

		/// <summary>
		/// Milliseconds
		/// </summary>
		public const int DEFAULT_REQUEST_TIMEOUT = 15000;

		#region Request Handlers
		private readonly Dictionary<ushort, IRequestHandler> _requestHandlers = new();

		public void RegisterRequestHandler<TRequest, TResponse>(ushort requestUId, RequestHandlerDelegate<TRequest, TResponse> handlerDelegate = null)
			where TRequest : struct, INetSerializable
			where TResponse : struct, INetSerializable {

			if (_requestHandlers.ContainsKey(requestUId)) {
				throw new Exception(logger.GetTaggedText($"A request handler with this identifier is already registered ({requestUId})"));
			}

			_requestHandlers[requestUId] = new RequestHandler<TRequest, TResponse>(handlerDelegate);
		}

		public void UnregisterRequestHandler(ushort requestUId) {
			_ = _requestHandlers.Remove(requestUId);
		}
		#endregion

		#region Response Handlers
		private readonly Dictionary<ushort, IResponseHandler> _responseHandlers = new();

		public void RegisterResponseHandler<TRequest, TResponse>(ushort requestUId, ResponseHandlerDelegate<TResponse> handlerDelegate = null)
			where TRequest : struct, INetSerializable
			where TResponse : struct, INetSerializable {

			if (_responseHandlers.ContainsKey(requestUId)) {
				throw new Exception(logger.GetTaggedText($"A response handler with this identifier is already registered ({requestUId})"));
			}

			_responseHandlers[requestUId] = new ResponseHandler<TRequest, TResponse>(handlerDelegate);
		}

		public void UnregisterResponseHandler(ushort requestUId) {
			_ = _responseHandlers.Remove(requestUId);
		}
		#endregion

		// NOTE: clear on server & client start
		protected readonly ConcurrentDictionary<uint, RequestCallback> _requestCallbacks = new();

		// NOTE: reset on server & client start
		#region NextRequestId
		private uint _nextRequestId;
		/// <summary>
		/// Increases by 1
		/// </summary>
		private uint GetNextRequestId() {
			return _nextRequestId++;
		}
		protected void ResetNextRequestId() {
			_nextRequestId = 1U;
		}
		#endregion

		/// <param name="time">Milliseconds</param>
		private async UniTask StartRequestTimeout(uint requestId, int time) {
			await UniTask.Delay(time, ignoreTimeScale: true);
			if (_requestCallbacks.TryRemove(requestId, out RequestCallback callback)) {
				callback.OnTimeout();
			}
		}

		/// <param name="timeout">Milliseconds</param>
		/// <returns>Id</returns>
		private uint CreateRequest(IResponseHandler responseHandler, ResponseHandlerDelegate<INetSerializable> handlerDelegate, int timeout) {
			// new id
			uint requestId = GetNextRequestId();
			// add callback
			_ = _requestCallbacks.TryAdd(requestId, new(requestId, this, responseHandler, handlerDelegate));
			if (timeout > 0) {
				StartRequestTimeout(requestId, timeout).Forget();
			}
			return requestId;
		}

		/// <summary>
		/// Throws an exception to prevent further execution
		/// </summary>
		protected void CreateAndWriteRequest<TRequest>(Writer writer, ushort uId, TRequest request, ResponseHandlerDelegate<INetSerializable> handlerDelegate, int timeout, SerializerDelegate extra)
			where TRequest : struct, INetSerializable {

			if (!_responseHandlers.ContainsKey(uId)) {
				handlerDelegate.Invoke(new(GetNextRequestId(), this, 0U, null), ReqResStatusCode.Unregistered, EmptyMessage.instance);
				throw new Exception(logger.GetTaggedText($"Unable to create a request ('{uId}' is not registered)"));
			}

			IResponseHandler responseHandler = _responseHandlers[uId];

			if (!responseHandler.IsRequestTypeValid(typeof(TRequest))) {
				handlerDelegate.Invoke(new(GetNextRequestId(), this, 0U, null), ReqResStatusCode.Unregistered, EmptyMessage.instance);
				throw new Exception(logger.GetTaggedText("Unable to create a request (invalid type)"));
			}

			// create
			uint requestId = CreateRequest(responseHandler, handlerDelegate, timeout);

			// reset
			writer.SetPosition(0);

			// write
			writer.PutUShort(SystemMessages.INT_REQUEST);
			// header
			writer.PutUShort(uId);
			writer.PutUInt(requestId);
			// body
			writer.Put(request);
			extra?.Invoke(writer);
		}

		#region Handle Request
		/// <summary>
		/// Sends a response message
		/// </summary>
		/// <param name="connId">Sender</param>
		private void OnRequestHandled(uint connId, uint requestId, ReqResStatusCode statusCode, INetSerializable response, SerializerDelegate extra) {
			// reset
			writer.SetPosition(0);
			// write
			writer.PutUShort(SystemMessages.INT_RESPONSE);
			// header
			writer.PutUInt(requestId);
			writer.PutByte((byte)statusCode);
			// body
			writer.Put(response);
			extra?.Invoke(writer);
			// send
			if (connId > 0) {
				transport.ServerSend(connId, writer.Data, DeliveryMethod.Reliable);
				return;
			}
			transport.ClientSend(writer.Data, DeliveryMethod.Reliable);
		}

		/// <param name="connId">Sender</param>
		private void HandleRequest(uint connId, Reader reader) {
			// read header (uId, id)
			ushort requestUId = reader.GetUShort();
			uint requestId = reader.GetUInt();

			// unregistered?
			if (!_requestHandlers.ContainsKey(requestUId)) {
				OnRequestHandled(connId, requestId, ReqResStatusCode.Unregistered, EmptyMessage.instance, null);
				throw new Exception(logger.GetTaggedText($"Received an unregistered request ({requestUId})"));
			}

			_requestHandlers[requestUId].OnRequest(new(requestUId, requestId, this, connId, reader), OnRequestHandled);
		}
		#endregion

		/// <param name="connId">Sender</param>
		private void HandleResponse(uint connId, Reader reader) {
			// read header (id, status)
			uint requestId = reader.GetUInt();
			ReqResStatusCode statusCode = (ReqResStatusCode)reader.GetByte();
			// call
			if (_requestCallbacks.TryRemove(requestId, out RequestCallback callback)) {
				callback.OnResponse(connId, reader, statusCode);
			}
		}

		/// <param name="connId">Sender</param>
		private bool TryHandleReqRes(ushort messageUId, uint connId, Reader reader) {
			if (messageUId == SystemMessages.INT_REQUEST) {
				HandleRequest(connId, reader);
				return true;
			}

			if (messageUId == SystemMessages.INT_RESPONSE) {
				HandleResponse(connId, reader);
				return true;
			}

			return false;
		}
	}
}
