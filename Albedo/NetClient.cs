namespace Albedo {

	public partial class NetClient : DataHandler {

		public NetClient(Transport transport, NetManager manager) : base(transport, manager) { }

		/// <summary>
		/// Set on successful auth
		/// <para>[!] transp 'connId' must never be '0'</para>
		/// </summary>
		public uint connId;

		// May be redundant
		public void Tick() {
			transport.ClientTick();
		}

		public void Start(string address, ushort port) {
			// reset callbacks
			transport.clientOnConnected = () => {
				manager.ClientOnConnected();
				manager.authenticator.ClientOnAuth();
			};
			transport.clientOnData = data => ClientOnData(data);
			transport.clientOnError = error => manager.ClientOnTransportError(error);
			transport.clientOnDisconnected = disconnInfo => manager.ClientOnDisconnected(disconnInfo);

			// reset 'connId'
			connId = 0;

			// clear auth data queue
			manager.authenticator.dataQueue.Clear();

			// start
			transport.StartClient(address, port);
		}

		// May be redundant
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
