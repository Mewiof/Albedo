﻿using System;
using System.Runtime.CompilerServices;
using kcp2k;
using UnityEngine;

namespace Albedo.Transports {

	public sealed class KCPTransport : Transport {

		public readonly NetManager manager;

		[SerializeField] private KcpConfig _config = new();

		public KCPTransport(NetManager manager) {
			this.manager = manager;
		}

		private KcpServer _server;
		private KcpClient _client;

		public override bool IsServer => _server != null;

		public override bool IsClient => _client != null;

		/*[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static DeliveryMethod Convert(KcpChannel value) {
			return value == KcpChannel.Reliable ? DeliveryMethod.Reliable : DeliveryMethod.Unreliable;
		}*/

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static KcpChannel Convert(DeliveryMethod value) {
			return value == DeliveryMethod.Reliable ? KcpChannel.Reliable : KcpChannel.Unreliable;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#pragma warning disable IDE0060 // Remove unused parameter
		private static Error Convert(ErrorCode value) { // TODO
			return Error.Unknown;
		}
#pragma warning restore IDE0060 // Remove unused parameter

		public override void StartServer(ushort port) {
			_server = new(
				connId => serverOnClientConnected.Invoke((uint)connId, _server.connections[connId].remoteEndPoint),
				(connId, data, _) => serverOnData.Invoke((uint)connId, data),
				connId => serverOnClientDisconnected.Invoke((uint)connId, default), // TODO: convert 'disconnInfo'
				(connId, errorCode, errorText) => serverOnError.Invoke(Convert(errorCode)),
				_config);

			_server.Start(port);
		}

		public override void StartClient(string address, ushort port) {
			_client = new(clientOnConnected,
				(data, _) => clientOnData.Invoke(data),
				() => clientOnDisconnected.Invoke(default), // TODO: convert 'disconnInfo'
				(errorCode, errorText) => clientOnError.Invoke(Convert(errorCode)));

			_client.Connect(address, port, _config);
		}

		public override void StopServer() {
			_server?.Stop();
			_server = null;
		}

		public override void StopClient() {
			_client?.Disconnect();
			_client = null;
		}

		public override void ServerSend(uint connId, ArraySegment<byte> segment, DeliveryMethod deliveryMethod) {
			if (!IsServer) {
				throw new Exception(manager.Logger.GetTaggedText($"[Server] Unable to send a message to '{connId}' (inactive)"));
			}

			// 'kcp2k' does the same check when sending but does nothing if 'connId' is wrong, so we have to do it twice
			if (!_server.connections.ContainsKey((int)connId)) {
				throw new Exception(manager.Logger.GetTaggedText($"[Server] Unable to send a message to '{connId}' (wrong conn id)"));
			}

			_server.Send((int)connId, segment, Convert(deliveryMethod));
		}

		public override void ClientSend(ArraySegment<byte> segment, DeliveryMethod deliveryMethod) {
			if (!IsClient) {
				throw new Exception(manager.Logger.GetTaggedText($"[Client] Unable to send a message (inactive)"));
			}

			_client.Send(segment, Convert(deliveryMethod));
		}

		public override void ServerTick() {
			_server.Tick();
		}

		public override void ClientTick() {
			_client.Tick();
		}

		public override void Disconnect(uint connId) {
			if (!IsServer) {
				throw new Exception(manager.Logger.GetTaggedText($"[Server] Unable to disconnect '{connId}' (inactive)"));
			}

			if (!_server.connections.TryGetValue((int)connId, out KcpServerConnection conn)) {
				throw new Exception(manager.Logger.GetTaggedText($"[Server] Unable to disconnect '{connId}' (wrong conn id)"));
			}

			conn.peer.Disconnect();
		}
	}
}
