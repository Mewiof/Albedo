using System;
using System.Collections.Generic;

namespace Albedo {

	public delegate void SerializerDelegate(Writer writer);

	public delegate void ServerMessageHandlerDelegate(uint senderConnId, Reader reader);
	public delegate void ClientMessageHandlerDelegate(Reader reader);

	public abstract class DataHandler {

		public readonly Writer writer;
		public readonly Reader reader;
		public readonly ITransport transport;
		/// <summary>For debugging</summary>
		public string name;

		/// <summary>uId, delegate</summary>
		private readonly Dictionary<ushort, ServerMessageHandlerDelegate> _serverMessageHandlers;
		/// <summary>uId, delegate</summary>
		private readonly Dictionary<ushort, ClientMessageHandlerDelegate> _clientMessageHandlers;

		#region Abstract

		protected TransportEventData _tempEventData;
		protected abstract void OnTransportEvent(TransportEventData eventData);

		#endregion

		public DataHandler(ITransport transport, string name) {
			writer = new();
			reader = new();
			this.transport = transport;
			this.name = name;
			_serverMessageHandlers = new();
			_clientMessageHandlers = new();
		}

		#region Read

		public void ServerOnData(uint senderConnId, ArraySegment<byte> segment) {
			reader.Set(segment);
			ushort messageUId = reader.GetUShort();
			if (!_serverMessageHandlers.TryGetValue(messageUId, out ServerMessageHandlerDelegate messageHandlerDelegate)) {
				throw new Exception($"[{name}->{nameof(ServerOnData)}] Unknown 'messageUId'->{messageUId}");
			}
			messageHandlerDelegate.Invoke(senderConnId, reader);
		}

		public void ClientOnData(ArraySegment<byte> segment) {
			reader.Set(segment);
			ushort messageUId = reader.GetUShort();
			if (!_clientMessageHandlers.TryGetValue(messageUId, out ClientMessageHandlerDelegate messageHandlerDelegate)) {
				throw new Exception($"[{name}->{nameof(ClientOnData)}] Unknown 'messageUId'->{messageUId}");
			}
			messageHandlerDelegate.Invoke(reader);
		}

		#endregion

		#region Registration

		/// <summary>[!] Server</summary>
		public void RegisterMessageHandler(ushort messageUId, ServerMessageHandlerDelegate handler) {
			if (_serverMessageHandlers.ContainsKey(messageUId)) {
				throw new ArgumentException($"[{name}->Server] Unable to register message ('messageUId'->{messageUId} is already registered)");
			}
			_serverMessageHandlers[messageUId] = handler;
		}

		/// <summary>[!] Client</summary>
		public void RegisterMessageHandler(ushort messageUId, ClientMessageHandlerDelegate handler) {
			if (_clientMessageHandlers.ContainsKey(messageUId)) {
				throw new ArgumentException($"[{name}->Client] Unable to register message ('messageUId'->{messageUId} is already registered)");
			}
			_clientMessageHandlers[messageUId] = handler;
		}

		public void UnregisterMessageHandler(ushort messageUId) {
			_ = _serverMessageHandlers.Remove(messageUId);
			_ = _clientMessageHandlers.Remove(messageUId);
		}

		#endregion

		#region Write

		/// <summary>Resets the internal 'writer' and writes 'uId' to it</summary>
		public void SetMessage(ushort uId) {
			writer.SetPosition(0);
			writer.PutUShort(uId);
		}

		/// <summary>Resets the internal 'writer' and writes to it</summary>
		public void SetMessage(ushort uId, SerializerDelegate serializer) {
			SetMessage(uId);
			serializer.Invoke(writer);
		}

		#endregion
	}
}
