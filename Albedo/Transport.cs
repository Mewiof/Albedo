using System;
using System.Net;
using UnityEngine;

namespace Albedo {

	public abstract class Transport : MonoBehaviour {

		public enum Error { // TODO
			Unknown
		}

		/* Some transports may output additional information such
		 * as reason for disconn.
		 * 
		 * It is better to leave it as it is for now
		 */
		public enum DisconnInfo { // TODO
			Unknown
		}

		public abstract bool IsServer { get; }
		public abstract bool IsClient { get; }
		internal abstract void StartServer(ushort port);
		internal abstract void StartClient(string address, ushort port);
		internal abstract void StopServer();
		internal abstract void StopClient();
		internal abstract void ServerSend(uint connId, ArraySegment<byte> segment, DeliveryMethod deliveryMethod);
		internal abstract void ClientSend(ArraySegment<byte> segment, DeliveryMethod deliveryMethod);

		// Server
		internal Action<uint, IPEndPoint> serverOnClientConnected;
		internal Action<uint, ArraySegment<byte>> serverOnData;
		internal Action<Error> serverOnError;
		internal Action<uint, DisconnInfo> serverOnClientDisconnected;

		// Client
		internal Action clientOnConnected;
		internal Action<ArraySegment<byte>> clientOnData;
		internal Action<Error> clientOnError;
		internal Action<DisconnInfo> clientOnDisconnected;

		internal abstract void ServerTick();
		internal abstract void ClientTick();

		/// <summary>Server</summary>
		internal abstract void Disconnect(uint connId);
	}
}
