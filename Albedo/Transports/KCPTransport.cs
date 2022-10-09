using System;
using System.Collections.Generic;
using kcp2k;

namespace Albedo.Transports {

	// Dead zone. Written in a hurry

	public sealed class KCPTransport : ITransport {

		public readonly NetManager manager;

		public KCPTransport(NetManager manager) {
			this.manager = manager;
		}

		private KcpServerNonAlloc _server;
		private KcpClientNonAlloc _client;

		public bool IsServer => _server != null;

		public bool IsClient => _client != null;

		// Transport data
		private readonly Queue<TransportEventData> _serverEventQueue = new();
		private readonly Queue<TransportEventData> _clientEventQueue = new();

		// TODO: allow to pass custom parameters to 'kcp2k'

		public void StartServer(ushort port) {
			if (IsServer) {
				throw new Exception($"[{manager.name}->{nameof(KCPTransport)}] Unable to start server (already active)");
			}

			// Transport data
			_serverEventQueue.Clear();

			_server = new(
				connId => _serverEventQueue.Enqueue(new() {
					type = TransportEventData.Type.Conn,
					connId = (uint)connId
				}),
				(connId, data, _) => _serverEventQueue.Enqueue(new() {
					type = TransportEventData.Type.Data,
					connId = (uint)connId,
					segment = data
				}),
				connId => _serverEventQueue.Enqueue(new() {
					type = TransportEventData.Type.Disconn,
					connId = (uint)connId
				}),
				(connId, errorCode, errorText) => _serverEventQueue.Enqueue(new() {
					type = TransportEventData.Type.Error
				}), false, true, 30);//!

			_server.Start(port);
		}

		public void StartClient(string address, ushort port) {
			if (IsClient) {
				throw new Exception($"[{manager.name}->{nameof(KCPTransport)}] Unable to start client (already active)");
			}

			// Transport data
			_clientEventQueue.Clear();

			_client = new(
				() => _clientEventQueue.Enqueue(new() {
					type = TransportEventData.Type.Conn
				}),
				(data, _) => _clientEventQueue.Enqueue(new() {
					type = TransportEventData.Type.Data,
					segment = data
				}),
				() => _clientEventQueue.Enqueue(new() {
					type = TransportEventData.Type.Disconn
				}),
				(errorCode, errorText) => _clientEventQueue.Enqueue(new() {
					type = TransportEventData.Type.Error
				}));

			_client.Connect(address, port, true, 30);//!
		}

		public void StopServer() {
			if (_server != null) {
				_server.Stop();
			}
			_server = null;
		}

		public void StopClient() {
			if (_client != null) {
				_client.Disconnect();
			}
			_client = null;
		}

		public void ServerSend(uint connId, ArraySegment<byte> segment, DeliveryMethod deliveryMethod) {
			if (!IsServer) {
				throw new Exception($"[{manager.name}->{nameof(KCPTransport)}->Server] Unable to send message->{connId} (inactive)");
			}

			// 'kcp2k' does the same check when sending but does nothing if id is wrong, so we have to do the same thing twice
			if (!_server.connections.TryGetValue((int)connId, out KcpServerConnection peer)) {
				throw new Exception($"[{manager.name}->{nameof(KCPTransport)}->Server] Unable to send message->{connId} (wrong conn id)");
			}

			_server.Send((int)connId, segment, deliveryMethod == DeliveryMethod.Reliable ? KcpChannel.Reliable : KcpChannel.Unreliable);
		}

		public void ClientSend(ArraySegment<byte> segment, DeliveryMethod deliveryMethod) {
			if (!IsClient) {
				throw new Exception($"[{manager.name}->{nameof(KCPTransport)}->Client] Unable to send message (inactive)");
			}

			_client.Send(segment, deliveryMethod == DeliveryMethod.Reliable ? KcpChannel.Reliable : KcpChannel.Unreliable);
		}

		public bool ServerTryReceiveEvent(out TransportEventData eventData) {
			eventData = default;

			if (!IsServer) {
				return false;
			}

			_server.Tick();

			if (_serverEventQueue.Count < 1) {
				return false;
			}

			eventData = _serverEventQueue.Dequeue();
			return true;
		}

		public bool ClientTryReceiveEvent(out TransportEventData eventData) {
			eventData = default;

			if (!IsClient) {
				return false;
			}

			// TODO: 'kcp2k' has 'TickIncoming' and 'TickOutgoing'. Can we find a use for this?
			_client.Tick();

			if (_clientEventQueue.Count < 1) {
				return false;
			}

			eventData = _clientEventQueue.Dequeue();
			return true;
		}

		public void ServerDisconnect(uint connId) {
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
