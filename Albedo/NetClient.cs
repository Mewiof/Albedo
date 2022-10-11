namespace Albedo {

	public class NetClient : DataHandler {

		public NetClient(ITransport transport, NetManager manager) : base(transport, manager) { }

		public void Tick() {
			while (transport.ClientTryReceiveEvent(out _tempEventData)) {
				OnTransportEvent(_tempEventData);
			}
		}

		public void Start(string address, ushort port) {
			transport.StartClient(address, port);
		}

		public void Stop() {
			transport.StopClient();
		}

		public void SendMessage(ushort messageUId, DeliveryMethod deliveryMethod) {
			SetMessage(messageUId);
			transport.ClientSend(writer.Data, deliveryMethod);
		}

		public void SendMessage(ushort messageUId, SerializerDelegate serializerDelegate, DeliveryMethod deliveryMethod) {
			SetMessage(messageUId, serializerDelegate);
			transport.ClientSend(writer.Data, deliveryMethod);
		}

		#region Override

		protected override void OnTransportEvent(TransportEventData eventData) {
			switch (eventData.type) {
				case TransportEventData.Type.Conn:
					manager.OnConnected();
					manager.authenticator.OnClientAuth();
					return;
				case TransportEventData.Type.Data:
					ClientOnData(eventData.segment);
					return;
				case TransportEventData.Type.Error:
					manager.OnClientTransportError(eventData.error);
					return;
				case TransportEventData.Type.Disconn:
					manager.OnDisconnected(eventData.disconnInfo);
					return;
			}
		}

		#endregion
	}
}
