﻿namespace Albedo {

	public class NetServer : DataHandler {

		private readonly Pool<ConnToClientData> _connPool;
		public readonly System.Collections.Generic.Dictionary<uint, ConnToClientData> connections;
		public readonly TaskManager<uint> taskManager;

		public NetServer(ITransport transport, NetManager manager) : base(transport, manager) {
			_connPool = new(() => new(), manager.maxNumOfConnections);
			connections = new();
			taskManager = new();
		}

		public void Tick() {
			while (transport.ServerTryReceiveEvent(out _tempEventData)) {
				OnTransportEvent(_tempEventData);
			}
		}

		public void Start(ushort port) {
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

		#region Override

		protected override void OnTransportEvent(TransportEventData eventData) {
			switch (eventData.type) {
				case TransportEventData.Type.Conn:
					// new connection
					ConnToClientData conn = _connPool.Get();
					conn.Set(eventData.connId, eventData.endPoint);
					connections[eventData.connId] = conn;
					// callback
					manager.OnClientConnected(conn);
					if (manager.authenticator != null) {
						manager.authenticator.ServerStartAuth(conn);
					}
					return;

				case TransportEventData.Type.Data:
					ServerOnData(connections[eventData.connId], eventData.segment);
					return;

				case TransportEventData.Type.Error:
					manager.OnServerTransportError(eventData.error);
					return;

				case TransportEventData.Type.Disconn:
					conn = connections[eventData.connId];
					// callback
					manager.OnClientDisconnected(conn, eventData.disconnInfo);
					// return
					_ = connections.Remove(eventData.connId);
					_connPool.Return(conn);
					return;
			}
		}

		#endregion
	}
}
