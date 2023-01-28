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

		public NetClient(Transport transport, NetManager manager) : base(transport, manager) { }

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
				manager.Logger.LogWarning("Unable to start client (already active)");
				return;
			}

			// reset
			connId = 0;
			_stopRequested = false;

			// clear auth data queue
			manager.authenticator.dataQueue.Clear();

			// reset callbacks
			transport.clientOnConnected = () => {
				OnConnected?.Invoke();
				manager.authenticator.ClientOnAuth();
			};
			transport.clientOnData = data => ClientOnData(data);
			transport.clientOnError = error => OnTransportError?.Invoke(error);
			transport.clientOnDisconnected = disconnInfo => {
				OnDisconnected?.Invoke(disconnInfo);
				manager.Logger.Log("Disconnected");
				if (!_stopRequested) {
					Stop();
				}
			};

			// start
			transport.StartClient(address, port);

			// log
			manager.Logger.Log("Client has been started");

			// callback
			OnStarted?.Invoke();
		}

		public void Stop() {
			_stopRequested = true;

			if (!transport.IsClient) {
				manager.Logger.LogWarning("Unable to stop client (inactive)");
				return;
			}

			transport.StopClient();

			// log
			manager.Logger.Log("Client has been stopped");

			// callback
			OnStopped?.Invoke();
		}

		#region Send Message
		public void SendMessage(ushort messageUId, DeliveryMethod deliveryMethod = DeliveryMethod.Reliable) {
			SetMessage(messageUId);
			transport.ClientSend(writer.Data, deliveryMethod);
		}

		public void SendMessage(ushort messageUId, SerializerDelegate serializerDelegate, DeliveryMethod deliveryMethod = DeliveryMethod.Reliable) {
			SetMessage(messageUId, serializerDelegate);
			transport.ClientSend(writer.Data, deliveryMethod);
		}
		#endregion

		/// <param name="timeout">Milliseconds</param>
		public void SendRequest<TRequest>(ushort uId, TRequest request, int timeout, ResponseHandlerDelegate<INetSerializable> handlerDelegate = null, SerializerDelegate extra = null)
			where TRequest : struct, INetSerializable {

			CreateAndWriteRequest(writer, uId, request, handlerDelegate, timeout, extra);
			transport.ClientSend(writer.Data, DeliveryMethod.Reliable);
		}
	}
}
