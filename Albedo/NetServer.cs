namespace Albedo {

	public class NetServer : DataHandler {

		public NetServer(ITransport transport, NetManager manager) : base(transport, manager) { }

		public void Tick() {
			while (transport.ServerTryReceiveEvent(out _tempEventData)) {
				OnTransportEvent(_tempEventData);
			}
		}

		public void Start(ushort port) {
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
					// TODO: authenticate / send conn id
					manager.OnClientConnected(eventData.connId);
					return;
				case TransportEventData.Type.Data:
					ServerOnData(eventData.connId, eventData.segment);
					return;
				case TransportEventData.Type.Error:
					manager.OnServerTransportError(eventData.error);
					return;
				case TransportEventData.Type.Disconn:
					manager.OnClientDisconnected(eventData.connId, eventData.disconnInfo);
					return;
			}
		}

		#endregion
	}
}
