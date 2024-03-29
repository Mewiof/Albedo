﻿using System;
using System.Runtime.CompilerServices;
using kcp2k;
using UnityEngine;

namespace Albedo.Transports {

	public sealed class KCPTransport : Transport {

		public Logger logger;
		private Logger Logger {
			get {
				if (logger == null) {
					logger = new(name);
				}
				return logger;
			}
		}

		[SerializeField] private KcpConfig _config = new();
		[SerializeField] private bool _logInfo;
		[SerializeField] private bool _logWarnings = true;
		[SerializeField] private bool _logErrors = true;

		private void ApplyLogSettings() {
			Log.Info = _logInfo ? text => Logger.Log(string.Concat("[KCP] ", text)) : _ => { };
			Log.Warning = _logWarnings ? text => Logger.LogWarning(string.Concat("[KCP] ", text)) : _ => { };
			Log.Error = _logErrors ? text => Logger.LogError(string.Concat("[KCP] ", text)) : _ => { };
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

		internal override void StartServer(ushort port) {
			_server = new(
				connId => serverOnClientConnected.Invoke((uint)connId, (System.Net.IPEndPoint)_server.connections[connId].remoteEndPoint),
				(connId, data, _) => serverOnData.Invoke((uint)connId, data),
				connId => serverOnClientDisconnected.Invoke((uint)connId, default), // TODO: convert 'disconnInfo'
				(connId, errorCode, errorText) => serverOnError.Invoke(Convert(errorCode)),
				_config);

			_server.Start(port);
		}

		internal override void StartClient(string address, ushort port) {
			_client = new(clientOnConnected,
				(data, _) => clientOnData.Invoke(data),
				() => clientOnDisconnected.Invoke(default), // TODO: convert 'disconnInfo'
				(errorCode, errorText) => clientOnError.Invoke(Convert(errorCode)));

			_client.Connect(address, port, _config);
		}

		internal override void StopServer() {
			_server?.Stop();
			_server = null;
		}

		internal override void StopClient() {
			_client?.Disconnect();
			_client = null;
		}

		internal override void ServerSend(uint connId, ArraySegment<byte> segment, DeliveryMethod deliveryMethod) {
			if (!IsServer) {
				throw new Exception(Logger.GetTaggedText($"[Server] Unable to send a message to '{connId}' (inactive)"));
			}

			_server.Send((int)connId, segment, Convert(deliveryMethod));
		}

		internal override void ClientSend(ArraySegment<byte> segment, DeliveryMethod deliveryMethod) {
			if (!IsClient) {
				throw new Exception(Logger.GetTaggedText($"[Client] Unable to send a message (inactive)"));
			}

			_client.Send(segment, Convert(deliveryMethod));
		}

		internal override void ServerTick() {
			_server.Tick();
		}

		internal override void ClientTick() {
			_client.Tick();
		}

		internal override void Disconnect(uint connId) {
			if (!IsServer) {
				throw new Exception(Logger.GetTaggedText($"[Server] Unable to disconnect '{connId}' (inactive)"));
			}

			if (!_server.connections.TryGetValue((int)connId, out KcpServerConnection conn)) {
				return;
			}

			conn.peer.Disconnect();
		}

		private void Awake() {
			ApplyLogSettings();
		}
	}
}
