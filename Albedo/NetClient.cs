namespace Albedo {

	public class NetClient : DataHandler {

		public NetClient(Transport transport, NetManager manager) : base(transport, manager) { }

		public void Tick() {
			transport.ClientTick();
		}

		public void Start(string address, ushort port) {
			transport.clientOnConnected = () => {
				manager.ClientOnConnected();
				manager.authenticator.OnClientAuth();
			};
			transport.clientOnData = data => {
				ClientOnData(data);
			};
			transport.clientOnError = error => {
				manager.ClientOnTransportError(error);
			};
			transport.clientOnDisconnected = disconnInfo => {
				manager.ClientOnDisconnected(disconnInfo);
			};

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
	}
}
