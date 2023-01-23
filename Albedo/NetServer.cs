using UnityEngine;

namespace Albedo {

	public partial class NetServer : DataHandler {

		private readonly Pool<ConnToClientData> _connPool;
		public readonly System.Collections.Generic.Dictionary<uint, ConnToClientData> connections;

		#region Events
		public delegate void ClientConnectedHandler(ConnToClientData conn);
		public event ClientConnectedHandler OnClientConnected;

		public delegate void TransportErrorHandler(Transport.Error error);
		public event TransportErrorHandler OnTransportError;

		public delegate void ClientDisconnectedHandler(ConnToClientData conn, Transport.DisconnInfo disconnInfo);
		public event ClientDisconnectedHandler OnClientDisconnected;
		#endregion

		public NetServer(Transport transport, NetManager manager) : base(transport, manager) {
			_connPool = new(() => new(), manager.maxNumOfConnections);
			connections = new();
		}

		public void Tick(ref float delta) {
			transport.ServerTick();
			// TODO: avoid using 'foreach' in 'Tick'
			foreach (ConnToClientData conn in connections.Values) {
				conn.Tick(ref delta);
			}
		}

		public void Start(ushort port) {
			// reset callbacks
			transport.serverOnClientConnected = (connId, endPoint) => {
				// reached 'maxNumOfConnections'?
				if (connections.Count >= manager.maxNumOfConnections) {
					// TODO: log
					transport.Disconnect(connId);
					return;
				}

				// assign connection
				ConnToClientData conn = _connPool.Get();
				conn.Set(connId, endPoint);
				connections[connId] = conn;

				// callback
				OnClientConnected.Invoke(conn);

				manager.authenticator.ServerOnAuth(conn);
			};
			transport.serverOnData = (connId, data) => {
				try {
					ServerOnData(connections[connId], data);
				} catch (System.Exception e) {
					transport.Disconnect(connId);

					Debug.Log(Utils.GetLogText(manager.debugName,
						string.Concat('\'', connections[connId].endPointStr, "' caused an exception and was disconnected\n\n", e)));
				}
			};
			transport.serverOnError = error => OnTransportError.Invoke(error);
			transport.serverOnClientDisconnected = (connId, disconnInfo) => {
				ConnToClientData conn = connections[connId];
				// callback
				OnClientDisconnected.Invoke(conn, disconnInfo);
				// return
				_ = connections.Remove(connId);
				_connPool.Return(conn);
			};

			// clear
			foreach (ConnToClientData conn in connections.Values) {
				_connPool.Return(conn);
			}
			connections.Clear();

			// start
			transport.StartServer(port);
		}

		// May be redundant
		public void Stop() {
			transport.StopServer();
		}

		public void SendMessage(uint connId, ushort messageUId, DeliveryMethod deliveryMethod) {
			SetMessage(messageUId);
			transport.ServerSend(connId, writer.Data, deliveryMethod);
		}

		public void SendMessage(uint connId, ushort messageUId, SerializerDelegate serializerDelegate, DeliveryMethod deliveryMethod) {
			SetMessage(messageUId, serializerDelegate);
			transport.ServerSend(connId, writer.Data, deliveryMethod);
		}
	}
}
