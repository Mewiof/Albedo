namespace Albedo {

	public partial class NetServer : DataHandler {

		/* Cleared & reassigned when connected, returned to the pool when
		 * disconnected after a callback call
		 */
		private readonly Pool<ConnToClientData> _connPool;

		public readonly System.Collections.Generic.Dictionary<uint, ConnToClientData> connections = new();

		#region Events
		public delegate void StartedHandler();
		public event StartedHandler OnStarted;

		public delegate void StoppedHandler();
		public event StoppedHandler OnStopped;

		public delegate void ClientConnectedHandler(ConnToClientData conn);
		public event ClientConnectedHandler OnClientConnected;

		public delegate void TransportErrorHandler(Transport.Error error);
		public event TransportErrorHandler OnTransportError;

		public delegate void ClientDisconnectedHandler(ConnToClientData conn, Transport.DisconnInfo disconnInfo);
		public event ClientDisconnectedHandler OnClientDisconnected;
		#endregion

		public NetServer(Transport transport, NetManager manager) : base(transport, manager) {
			_connPool = new(() => new(), manager.maxNumOfConnections);
		}

		public void Tick(ref float delta) {
			transport.ServerTick();
			foreach (ConnToClientData conn in connections.Values) {
				conn.Tick(ref delta);
			}
		}

		public void Start(ushort port) {
			if (transport.IsServer) {
				manager.Logger.LogWarning("Unable to start server (already active)");
				return;
			}

			// clear connection dict
			foreach (ConnToClientData conn in connections.Values) {
				_connPool.Return(conn);
			}
			connections.Clear();

			// reset callbacks
			transport.serverOnClientConnected = (connId, endPoint) => {
				// reached 'maxNumOfConnections'?
				if (connections.Count >= manager.maxNumOfConnections) {
					string endPointStr = endPoint.ToString();
					transport.Disconnect(connId);
					manager.Logger.LogWarning(endPointStr, "has been disconnected (reached max num of connections)");
					return;
				}

				// assign connection
				ConnToClientData conn = _connPool.Get();
				conn.Set(connId, endPoint);
				connections[connId] = conn;

				// callback
				OnClientConnected?.Invoke(conn);

				// auth
				manager.authenticator.ServerOnAuth(conn);
			};
			transport.serverOnData = (connId, data) => {
				try {
					ServerOnData(connections[connId], data);
				} catch (System.Exception e) {
					string endPointStr = connections[connId].endPointStr;
					transport.Disconnect(connId);
					manager.Logger.LogWarning(endPointStr, "has been disconnected (caused an exception)\n\n" + e.ToString());
				}
			};
			transport.serverOnError = error => OnTransportError?.Invoke(error);
			transport.serverOnClientDisconnected = (connId, disconnInfo) => {
				ConnToClientData conn = connections[connId];
				// callback
				OnClientDisconnected?.Invoke(conn, disconnInfo);
				// return
				_ = connections.Remove(connId);
				_connPool.Return(conn);
			};

			// start
			transport.StartServer(port);

			// log
			manager.Logger.Log("Server has been started");

			// callback
			OnStarted?.Invoke();
		}

		public void Stop() {
			if (!transport.IsServer) {
				manager.Logger.LogWarning("Unable to stop server (inactive)");
				return;
			}

			transport.StopServer();

			// log
			manager.Logger.Log("Server has been stopped");

			// callback
			OnStopped?.Invoke();
		}

		#region Send Message
		public void SendMessage(uint connId, ushort messageUId, DeliveryMethod deliveryMethod = DeliveryMethod.Reliable) {
			SetMessage(messageUId);
			transport.ServerSend(connId, writer.Data, deliveryMethod);
		}

		public void SendMessage(uint connId, ushort messageUId, SerializerDelegate serializerDelegate, DeliveryMethod deliveryMethod = DeliveryMethod.Reliable) {
			SetMessage(messageUId, serializerDelegate);
			transport.ServerSend(connId, writer.Data, deliveryMethod);
		}
		#endregion

		/// <param name="timeout">Milliseconds</param>
		public void SendRequest<TRequest>(uint connId, ushort uId, TRequest request, int timeout, ResponseHandlerDelegate<INetSerializable> handlerDelegate = null, SerializerDelegate extra = null)
			where TRequest : struct, INetSerializable {

			CreateAndWriteRequest(writer, uId, request, handlerDelegate, timeout, extra);
			transport.ServerSend(connId, writer.Data, DeliveryMethod.Reliable);
		}
	}
}
