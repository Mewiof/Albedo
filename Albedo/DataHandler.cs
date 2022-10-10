using System;
using System.Collections.Generic;

namespace Albedo {

	public delegate void SerializerDelegate(Writer writer);

	public delegate void ServerMessageHandlerDelegate(ConnToClientData sender);
	public delegate void ServerAltMessageHandlerDelegate(ConnToClientData sender, Reader reader);
	public delegate void ClientMessageHandlerDelegate();
	public delegate void ClientAltMessageHandlerDelegate(Reader reader);

	public abstract class DataHandler {

		public readonly Writer writer;
		public readonly Reader reader;
		public readonly ITransport transport;
		public readonly NetManager manager;

		/// <summary>uId, delegate</summary>
		private readonly Dictionary<ushort, ServerMessageHandlerDelegate> _serverMessageHandlers;
		/// <summary>uId, delegate</summary>
		private readonly Dictionary<ushort, ServerAltMessageHandlerDelegate> _serverAltMessageHandlers;
		/// <summary>uId, delegate</summary>
		private readonly Dictionary<ushort, ClientMessageHandlerDelegate> _clientMessageHandlers;
		/// <summary>uId, delegate</summary>
		private readonly Dictionary<ushort, ClientAltMessageHandlerDelegate> _clientAltMessageHandlers;

		#region Abstract

		protected TransportEventData _tempEventData;
		protected abstract void OnTransportEvent(TransportEventData eventData);

		#endregion

		public DataHandler(ITransport transport, NetManager manager) {
			writer = new();
			reader = new();
			this.transport = transport;
			this.manager = manager;
			_serverMessageHandlers = new();
			_serverAltMessageHandlers = new();
			_clientMessageHandlers = new();
			_clientAltMessageHandlers = new();
		}

		#region Registration

		private ArgumentException GetAlreadyRegisteredException(uint messageUId) {
			return new ArgumentException($"[{manager.name}] Unable to register a message ('{nameof(messageUId)}'->{messageUId} is already registered)");
		}

		/// <summary>[!] Server</summary>
		public void RegisterMessageHandler(ushort messageUId, ServerMessageHandlerDelegate handler) {
			if (_serverMessageHandlers.ContainsKey(messageUId)) {
				throw GetAlreadyRegisteredException(messageUId);
			}
			_serverMessageHandlers[messageUId] = handler;
		}

		/// <summary>[!] Server</summary>
		public void RegisterMessageHandler(ushort messageUId, ServerAltMessageHandlerDelegate handler) {
			if (_serverAltMessageHandlers.ContainsKey(messageUId)) {
				throw GetAlreadyRegisteredException(messageUId);
			}
			_serverAltMessageHandlers[messageUId] = handler;
		}

		/// <summary>[!] Client</summary>
		public void RegisterMessageHandler(ushort messageUId, ClientMessageHandlerDelegate handler) {
			if (_clientMessageHandlers.ContainsKey(messageUId)) {
				throw GetAlreadyRegisteredException(messageUId);
			}
			_clientMessageHandlers[messageUId] = handler;
		}

		/// <summary>[!] Client</summary>
		public void RegisterMessageHandler(ushort messageUId, ClientAltMessageHandlerDelegate handler) {
			if (_clientAltMessageHandlers.ContainsKey(messageUId)) {
				throw GetAlreadyRegisteredException(messageUId);
			}
			_clientAltMessageHandlers[messageUId] = handler;
		}

		public void UnregisterMessageHandler(ushort messageUId) {
			_ = _serverMessageHandlers.Remove(messageUId);
			_ = _serverAltMessageHandlers.Remove(messageUId);
			_ = _clientMessageHandlers.Remove(messageUId);
			_ = _clientAltMessageHandlers.Remove(messageUId);
		}

		#endregion

		#region Read

		private Exception GetUnregisteredMessageException(ushort messageUId) {
			return new Exception($"[{manager.name}] Received an unregistered '{nameof(messageUId)}'->{messageUId}");
		}

		public void ServerOnData(ConnToClientData sender, ArraySegment<byte> segment) {
			reader.Set(segment);
			ushort messageUId = reader.GetUShort();

			if (reader.Available > 0) {
				if (!_serverAltMessageHandlers.TryGetValue(messageUId, out ServerAltMessageHandlerDelegate altHandler)) {
					throw GetUnregisteredMessageException(messageUId);
				}
				altHandler.Invoke(sender, reader);
				return;
			}

			if (!_serverMessageHandlers.TryGetValue(messageUId, out ServerMessageHandlerDelegate handler)) {
				throw GetUnregisteredMessageException(messageUId);
			}
			handler.Invoke(sender);
		}

		public void ClientOnData(ArraySegment<byte> segment) {
			reader.Set(segment);
			ushort messageUId = reader.GetUShort();

			if (reader.Available > 0) {
				if (!_clientAltMessageHandlers.TryGetValue(messageUId, out ClientAltMessageHandlerDelegate altHandler)) {
					throw GetUnregisteredMessageException(messageUId);
				}
				altHandler.Invoke(reader);
				return;
			}

			if (!_clientMessageHandlers.TryGetValue(messageUId, out ClientMessageHandlerDelegate handler)) {
				throw GetUnregisteredMessageException(messageUId);
			}
			handler.Invoke();
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
