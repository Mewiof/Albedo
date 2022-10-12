using System;
using System.Net;

namespace Albedo {

	public abstract class Transport {

		public enum Error {
		}

		public struct DisconnInfo {
		}

		public abstract bool IsServer { get; }
		public abstract bool IsClient { get; }
		public abstract void StartServer(ushort port);
		public abstract void StartClient(string address, ushort port);
		public abstract void StopServer();
		public abstract void StopClient();
		public abstract void ServerSend(uint connId, ArraySegment<byte> segment, DeliveryMethod deliveryMethod);
		public abstract void ClientSend(ArraySegment<byte> segment, DeliveryMethod deliveryMethod);

		// Server
		public Action<uint, EndPoint> serverOnClientConnected;
		public Action<uint, ArraySegment<byte>> serverOnData;
		public Action<Error> serverOnError;
		public Action<uint, DisconnInfo> serverOnClientDisconnected;

		// Client
		public Action clientOnConnected;
		public Action<ArraySegment<byte>> clientOnData;
		public Action<Error> clientOnError;
		public Action<DisconnInfo> clientOnDisconnected;

		public abstract void ServerTick();
		public abstract void ClientTick();

		/// <summary>Server</summary>
		public abstract void Disconnect(uint connId);
	}
}
