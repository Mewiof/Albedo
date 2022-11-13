using System;
using System.Runtime.CompilerServices;
using kcp2k;

namespace Albedo.Transports {

	public sealed class KCPTransport : Transport {

		public readonly NetManager manager;

		/* TODO: write a more detailed '<summary>'. The current one does not reveal all 
		 * the pros/cons of using a particular value, which may confuse and lead to unexpected transport behavior
		 */

		// 'kcp2k' configuration
		/// <summary>
		/// Listen to IPv6 and IPv4 simultaneously (disable if the platform only supports IPv4)
		/// <para>'true' by default</para>
		/// </summary>
		public bool dualMode = true;
		/// <summary>
		/// Recommended to reduce latency (also scales better without buffers getting full)
		/// <para>'true' by default</para>
		/// </summary>
		public bool noDelay = true;
		/// <summary>'15' by default</summary>
		public uint interval = 15;
		/// <summary>'10000' by default</summary>
		public int timeout = 10000;

		/// <summary>'2' by default</summary>
		public int fastResend = 2;
		/// <summary>'false' by default</summary>
		public bool congestionWindow = false;
		/// <summary>'4096' by default</summary>
		public uint sendWindowSize = 4096;
		/// <summary>'4096' by default</summary>
		public uint receiveWindowSize = 4096;
		/// <summary>'Kcp.DEADLINK * 2' by default</summary>
		public uint maxRetransmits = Kcp.DEADLINK * 2;
		///// <summary>'true' by default</summary> TODO: add 'nonAlloc' support
		//public bool nonAlloc = true;
		/// <summary>'true' by default</summary>
		public bool maximizeSendReceiveBuffToOSLimit = true;

		public KCPTransport(NetManager manager) {
			this.manager = manager;
		}

		private KcpServer _server;
		private KcpClient _client;

		public override bool IsServer => _server != null;

		public override bool IsClient => _client != null;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static DeliveryMethod Convert(KcpChannel value) {
			return value == KcpChannel.Reliable ? DeliveryMethod.Reliable : DeliveryMethod.Unreliable;
		}

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
			if (IsServer) {
				throw new Exception($"[{manager.name}->{nameof(KCPTransport)}] Unable to start server (already active)");
			}

			_server = new(
				connId => serverOnClientConnected.Invoke((uint)connId, _server.connections[connId].GetRemoteEndPoint()),
				(connId, data, _) => serverOnData.Invoke((uint)connId, data),
				connId => serverOnClientDisconnected.Invoke((uint)connId, default), // TODO: convert 'disconnInfo'
				(connId, errorCode, errorText) => serverOnError.Invoke(Convert(errorCode)),
				dualMode,
				noDelay,
				interval,
				fastResend,
				congestionWindow,
				sendWindowSize,
				receiveWindowSize,
				timeout,
				maxRetransmits,
				maximizeSendReceiveBuffToOSLimit);

			_server.Start(port);
		}

		public override void StartClient(string address, ushort port) {
			if (IsClient) {
				throw new Exception($"[{manager.name}->{nameof(KCPTransport)}] Unable to start client (already active)");
			}

			_client = new(clientOnConnected,
				(data, _) => clientOnData.Invoke(data),
				() => clientOnDisconnected.Invoke(default), // TODO: convert 'disconnInfo'
				(errorCode, errorText) => clientOnError.Invoke(Convert(errorCode)));

			_client.Connect(address, port,
				noDelay,
				interval,
				fastResend,
				congestionWindow,
				sendWindowSize,
				receiveWindowSize,
				timeout,
				maxRetransmits,
				maximizeSendReceiveBuffToOSLimit);
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

			// 'kcp2k' does the same check when sending but does nothing if 'connId' is wrong, so we have to do it twice
			if (!_server.connections.ContainsKey((int)connId)) {
				throw new Exception($"[{manager.name}->{nameof(KCPTransport)}->Server] Unable to send message->{connId} (wrong conn id)");
			}

			_server.Send((int)connId, segment, Convert(deliveryMethod));
		}

		public override void ClientSend(ArraySegment<byte> segment, DeliveryMethod deliveryMethod) {
			if (!IsClient) {
				throw new Exception($"[{manager.name}->{nameof(KCPTransport)}->Client] Unable to send message (inactive)");
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
				throw new Exception($"[{manager.name}->{nameof(KCPTransport)}->Server] Unable to disconnect->{connId} (inactive server)");
			}

			if (!_server.connections.TryGetValue((int)connId, out KcpServerConnection conn)) {
				throw new Exception($"[{manager.name}->{nameof(KCPTransport)}->Server] Unable to disconnect->{connId} (wrong conn id)");
			}

			conn.Disconnect();
		}
	}
}
