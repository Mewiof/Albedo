using System;

namespace Albedo {

	public partial class NetServer : DataHandler {

		public int maxNumOfConnections = 4;

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

		public NetServer(Transport transport, Logger logger, NetAuthenticator authenticator) : base(transport, logger, authenticator) {
			_connPool = new(() => new(), 1024);
		}

		public void Tick(ref float delta) {
			transport.ServerTick();
			foreach (ConnToClientData conn in connections.Values) {
				conn.Tick(ref delta);
			}
		}

		public void Start(ushort port) {
			if (transport.IsServer) {
				logger.LogWarning("Unable to start server (already active)");
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
				if (connections.Count >= maxNumOfConnections) {
					// disconnect & log
					string endPointStr = endPoint.ToString();
					transport.Disconnect(connId);
					logger.LogWarning(endPointStr, "has been disconnected (reached max num of connections)");
					return;
				}

				// assign connection
				ConnToClientData conn = _connPool.Get();
				conn.Set(connId, endPoint);
				connections[connId] = conn;

				// callback
				OnClientConnected?.Invoke(conn);

				// auth
				authenticator.ServerOnAuth(conn);
			};
			transport.serverOnData = (connId, data) => {
				try {
					ServerOnData(connections[connId], data);
				} catch (Exception e) {
					// disconnect & log
					string endPointStr = connections[connId].endPointStr;
					transport.Disconnect(connId);
					logger.LogWarning(endPointStr, "has been disconnected (caused an exception)\n\n" + e.ToString());
				}
			};
			transport.serverOnError = error => OnTransportError?.Invoke(error);
			transport.serverOnClientDisconnected = (connId, disconnInfo) => {
				ConnToClientData conn = connections[connId];
				// callback
				// NOTE: an exception will cause the following code not to execute, we do not catch it for performance
				OnClientDisconnected?.Invoke(conn, disconnInfo);
				// return
				_ = connections.Remove(connId);
				_connPool.Return(conn);
			};

			// start
			transport.StartServer(port);

			// log
			logger.Log("Server has been started");

			// callback
			OnStarted?.Invoke();
		}

		public void Stop() {
			if (!transport.IsServer) {
				logger.LogWarning("Unable to stop server (inactive)");
				return;
			}

			// stop
			transport.StopServer();

			// log
			logger.Log("Server has been stopped");

			// callback
			OnStopped?.Invoke();
		}

		public void Disconnect(uint connId) {
			transport.Disconnect(connId);
		}

		#region Send
		public void SendMessage(uint connId, ushort messageUId, DeliveryMethod deliveryMethod = DeliveryMethod.Reliable) {
			SetMessage(messageUId);
			transport.ServerSend(connId, writer.Data, deliveryMethod);
		}

		public void SendMessage(uint connId, ushort messageUId, SerializerDelegate serializerDelegate, DeliveryMethod deliveryMethod = DeliveryMethod.Reliable) {
			SetMessage(messageUId, serializerDelegate);
			transport.ServerSend(connId, writer.Data, deliveryMethod);
		}

		/// <param name="timeout">Milliseconds</param>
		public void SendRequest<TRequest>(uint connId, ushort requestUId, TRequest request, int timeout, ResponseHandlerDelegate<INetSerializable> handlerDelegate = null, SerializerDelegate extra = null)
			where TRequest : struct, INetSerializable {

			CreateAndWriteRequest(writer, requestUId, request, handlerDelegate, timeout, extra);
			transport.ServerSend(connId, writer.Data, DeliveryMethod.Reliable);
		}
		#endregion
	}
}
