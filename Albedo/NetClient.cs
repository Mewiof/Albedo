using Cysharp.Threading.Tasks;

namespace Albedo {

	public partial class NetClient : DataHandler {

		#region Events
		public delegate void StartedHandler();
		public event StartedHandler OnStarted;

		public delegate void StoppedHandler();
		public event StoppedHandler OnStopped;

		public delegate void ConnectedHandler();
		public event ConnectedHandler OnConnected;

		public delegate void TransportErrorHandler(Transport.Error error);
		public event TransportErrorHandler OnTransportError;

		public delegate void DisconnectedHandler(Transport.DisconnInfo disconnInfo);
		public event DisconnectedHandler OnDisconnected;
		#endregion

		public NetClient(Transport transport, Logger logger, NetAuthenticator authenticator) : base(transport, logger, authenticator) { }

		/// <summary>
		/// Set on successful auth
		/// <para>[!] transp 'connId' must never be '0'</para>
		/// </summary>
		public uint connId;

		/* We want the client to stop automatically on disconnection, but
		 * calling 'Stop' can also trigger disconnection event. We use this to
		 * avoid calling it twice
		 */
		private bool _stopRequested;

		// May be redundant
		public void Tick() {
			transport.ClientTick();
		}

		public void Start(string address, ushort port) {
			if (transport.IsClient) {
				logger.LogWarning("Unable to start client (already active)");
				return;
			}

			// reset
			connId = 0;
			_stopRequested = false;

			// clear auth data queue
			ClearClientAuthDataQueue();

			// reset req & res
			_requestCallbacks.Clear();
			ResetNextRequestId();

			// reset callbacks
			transport.clientOnConnected = () => {
				OnConnected?.Invoke();
				authenticator.ClientOnAuth();
			};
			transport.clientOnData = data => ClientOnData(data);
			transport.clientOnError = error => OnTransportError?.Invoke(error);
			transport.clientOnDisconnected = disconnInfo => {
				// log
				logger.Log("Client has been disconnected");
				// callback
				OnDisconnected?.Invoke(disconnInfo);
				// stop the client if it was not called earlier
				if (!_stopRequested) {
					Stop();
				}
			};

			// start
			transport.StartClient(address, port);

			// log
			logger.Log("Client has been started");

			// callback
			OnStarted?.Invoke();
		}

		public void Stop() {
			_stopRequested = true;

			if (!transport.IsClient) {
				logger.LogWarning("Unable to stop client (inactive)");
				return;
			}

			// stop
			transport.StopClient();

			// log
			logger.Log("Client has been stopped");

			// callback
			OnStopped?.Invoke();
		}

		#region Send
		public void SendMessage(ushort messageUId, DeliveryMethod deliveryMethod = DeliveryMethod.Reliable) {
			SetMessage(messageUId);
			transport.ClientSend(writer.Data, deliveryMethod);
		}

		public void SendMessage(ushort messageUId, SerializerDelegate serializerDelegate, DeliveryMethod deliveryMethod = DeliveryMethod.Reliable) {
			SetMessage(messageUId, serializerDelegate);
			transport.ClientSend(writer.Data, deliveryMethod);
		}

		/// <param name="timeout">Milliseconds</param>
		public void SendRequest<TRequest>(ushort requestUId, TRequest request, ResponseHandlerDelegate<INetSerializable> responseHandler = null, int timeout = DEFAULT_REQUEST_TIMEOUT, SerializerDelegate extra = null)
			where TRequest : struct, INetSerializable {

			// create & write
			CreateAndWriteRequest(writer, requestUId, request, responseHandler, timeout, extra);
			// send
			transport.ClientSend(writer.Data, DeliveryMethod.Reliable);
		}

		/// <summary>Waits for response</summary>
		/// <param name="timeout">Milliseconds</param>
		public async UniTask<AsyncResponseData<TResponse>> SendRequestAsync<TRequest, TResponse>(ushort requestUId, TRequest request, int timeout = DEFAULT_REQUEST_TIMEOUT, SerializerDelegate extra = null)
			where TRequest : struct, INetSerializable
			where TResponse : struct, INetSerializable {

			bool completed = false;
			AsyncResponseData<TResponse> responseData = default;

			// create & write
			CreateAndWriteRequest(writer, requestUId, request, (data, statusCode, response) => {
				if (response is not TResponse) {
					response = default(TResponse);
				}
				responseData = new AsyncResponseData<TResponse>(data, statusCode, (TResponse)response);
				completed = true;
			}, timeout, extra);

			// send
			transport.ClientSend(writer.Data, DeliveryMethod.Reliable);

			// wait for response
			do {
				await UniTask.Yield();
			} while (!completed);

			return responseData;
		}
		#endregion
	}
}
