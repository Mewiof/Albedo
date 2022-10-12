using System;
using System.Collections.Generic;
using kcp2k;

namespace Albedo.Transports {

	// Dead zone. Written in a hurry

	public sealed class KCPTransport : Transport {

		public readonly NetManager manager;

		public KCPTransport(NetManager manager) {
			this.manager = manager;
		}

		private KcpServer _server;
		private KcpClient _client;

		public override bool IsServer => _server != null;

		public override bool IsClient => _client != null;

		// TODO: allow to pass custom parameters to 'kcp2k'

		public override void StartServer(ushort port) {
			if (IsServer) {
				throw new Exception($"[{manager.name}->{nameof(KCPTransport)}] Unable to start server (already active)");
			}

			_server = new(
				connId => serverOnClientConnected.Invoke((uint)connId, _server.connections[connId].GetRemoteEndPoint()),
				(connId, data, _) => serverOnData.Invoke((uint)connId, data),
				connId => serverOnClientDisconnected.Invoke((uint)connId, default),
				(connId, errorCode, errorText) => serverOnError.Invoke(default), false, true, 30);

			_server.Start(port);
		}

		public override void StartClient(string address, ushort port) {
			if (IsClient) {
				throw new Exception($"[{manager.name}->{nameof(KCPTransport)}] Unable to start client (already active)");
			}

			_client = new(clientOnConnected,
				(data, _) => clientOnData.Invoke(data),
				() => clientOnDisconnected.Invoke(default),
				(errorCode, errorText) => clientOnError.Invoke(default));

			_client.Connect(address, port, true, 30);//!
		}

		public override void StopServer() {
			if (_server != null) {
				_server.Stop();
			}
			_server = null;
		}

		public override void StopClient() {
			if (_client != null) {
				_client.Disconnect();
			}
			_client = null;
		}

		public override void ServerSend(uint connId, ArraySegment<byte> segment, DeliveryMethod deliveryMethod) {
			if (!IsServer) {
				throw new Exception($"[{manager.name}->{nameof(KCPTransport)}->Server] Unable to send message->{connId} (inactive)");
			}

			// 'kcp2k' does the same check when sending but does nothing if id is wrong, so we have to do the same thing twice
			if (!_server.connections.TryGetValue((int)connId, out KcpServerConnection peer)) {
				throw new Exception($"[{manager.name}->{nameof(KCPTransport)}->Server] Unable to send message->{connId} (wrong conn id)");
			}

			_server.Send((int)connId, segment, deliveryMethod == DeliveryMethod.Reliable ? KcpChannel.Reliable : KcpChannel.Unreliable);
		}

		public override void ClientSend(ArraySegment<byte> segment, DeliveryMethod deliveryMethod) {
			if (!IsClient) {
				throw new Exception($"[{manager.name}->{nameof(KCPTransport)}->Client] Unable to send message (inactive)");
			}

			_client.Send(segment, deliveryMethod == DeliveryMethod.Reliable ? KcpChannel.Reliable : KcpChannel.Unreliable);
		}

		public override void ServerTick() {
			_server.Tick();
		}

		public override void ClientTick() {
			_client.Tick();
		}

		public override void Disconnect(uint connId) {
			if (!IsServer) {
				throw new Exception($"[{manager.name}->{nameof(KCPTransport)}->Server] Unable to disconnect->{connId} (inactive server)");
			}

			// 'kcp2k' does the same check but does nothing if id is wrong, so we have to do the same thing twice
			if (!_server.connections.TryGetValue((int)connId, out KcpServerConnection peer)) {
				throw new Exception($"[{manager.name}->{nameof(KCPTransport)}->Server] Unable to disconnect->{connId} (wrong conn id)");
			}

			_server.Disconnect((int)connId);
		}
	}
}
