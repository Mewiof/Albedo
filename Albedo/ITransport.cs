using System;

namespace Albedo {

	public interface ITransport {

		bool IsServer { get; }
		bool IsClient { get; }
		void StartServer(ushort port);
		void StartClient(string address, ushort port);
		void StopServer();
		void StopClient();
		void ServerSend(uint connId, ArraySegment<byte> segment, DeliveryMethod deliveryMethod);
		void ClientSend(ArraySegment<byte> segment, DeliveryMethod deliveryMethod);
		bool ServerTryReceiveEvent(out TransportEventData eventData);
		bool ClientTryReceiveEvent(out TransportEventData eventData);
		/// <summary>Server</summary>
		void Disconnect(uint connId);
	}
}
