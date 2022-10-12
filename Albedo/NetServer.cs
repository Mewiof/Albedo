﻿namespace Albedo {

	public class NetServer : DataHandler {

		private readonly Pool<ConnToClientData> _connPool;
		public readonly System.Collections.Generic.Dictionary<uint, ConnToClientData> connections;

		public NetServer(Transport transport, NetManager manager) : base(transport, manager) {
			_connPool = new(() => new(), manager.maxNumOfConnections);
			connections = new();
		}

		public void Tick(float delta) {
			transport.ServerTick();
			// TODO: avoid using 'foreach' in 'Tick'
			foreach (ConnToClientData conn in connections.Values) {
				conn.Tick(ref delta);
			}
		}

		public void Start(ushort port) {
			transport.serverOnClientConnected = (connId, endPoint) => {
				// new connection
				ConnToClientData conn = _connPool.Get();
				conn.Set(connId, endPoint);
				connections[connId] = conn;
				// callback
				manager.ServerOnClientConnected(conn);
				manager.authenticator.OnServerAuth(conn);
			};
			transport.serverOnData = (connId, data) => {
				try {
					ServerOnData(connections[connId], data);
				}
				catch (System.Exception e) {
					System.Console.Error.WriteLine(e);
					transport.Disconnect(connId);
				}
			};
			transport.serverOnError = error => {
				manager.ServerOnTransportError(error);
			};
			transport.serverOnClientDisconnected = (connId, disconnInfo) => {
				ConnToClientData conn = connections[connId];
				// callback
				manager.ServerOnClientDisconnected(conn, disconnInfo);
				// return
				_ = connections.Remove(connId);
				_connPool.Return(conn);
			};

			foreach (ConnToClientData conn in connections.Values) {
				_connPool.Return(conn);
			}
			connections.Clear();
			transport.StartServer(port);
		}

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
