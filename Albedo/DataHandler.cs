using System;
using System.Collections.Generic;

namespace Albedo {

	public delegate void SerializerDelegate(Writer writer);

	public delegate void ServerMessageHandlerDelegate(ConnToClientData sender);
	public delegate void ServerAltMessageHandlerDelegate(ConnToClientData sender, Reader reader);
	public delegate void ClientMessageHandlerDelegate();
	public delegate void ClientAltMessageHandlerDelegate(Reader reader);

	public abstract partial class DataHandler {

		public readonly Writer writer;
		public readonly Transport transport;
		public readonly Logger logger;
		public readonly NetAuthenticator authenticator;

		/// <summary>uId, delegate</summary>
		private readonly Dictionary<ushort, ServerMessageHandlerDelegate> _serverMessageHandlers;
		/// <summary>uId, delegate</summary>
		private readonly Dictionary<ushort, ServerAltMessageHandlerDelegate> _serverAltMessageHandlers;
		/// <summary>uId, delegate</summary>
		private readonly Dictionary<ushort, ClientMessageHandlerDelegate> _clientMessageHandlers;
		/// <summary>uId, delegate</summary>
		private readonly Dictionary<ushort, ClientAltMessageHandlerDelegate> _clientAltMessageHandlers;

		public DataHandler(Transport transport, Logger logger, NetAuthenticator authenticator) {
			writer = new();
			this.transport = transport;
			this.logger = logger;
			this.authenticator = authenticator;
			_serverMessageHandlers = new();
			_serverAltMessageHandlers = new();
			_clientMessageHandlers = new();
			_clientAltMessageHandlers = new();
		}

		#region Registration
		private ArgumentException GetAlreadyRegisteredException(ushort messageUId, string handlerDelegateName) {
			return new ArgumentException(logger.GetTaggedText($"Unable to register a message ('{handlerDelegateName}->{messageUId}' is already registered)"));
		}

		private void CheckIfRegisteredBySystem(ushort messageUId) {
			// do not include auth messages
			if (messageUId == SystemMessages.INT_REQUEST ||
				messageUId == SystemMessages.INT_RESPONSE) {
				throw GetAlreadyRegisteredException(messageUId, "SYSTEM");
			}
		}

		/// <summary>[!] Server</summary>
		public void RegisterMessageHandler(ushort messageUId, ServerMessageHandlerDelegate handler) {
			CheckIfRegisteredBySystem(messageUId);
			if (_serverMessageHandlers.ContainsKey(messageUId)) {
				throw GetAlreadyRegisteredException(messageUId, typeof(ServerMessageHandlerDelegate).Name);
			}
			_serverMessageHandlers[messageUId] = handler;
		}

		/// <summary>[!] Server</summary>
		public void RegisterMessageHandler(ushort messageUId, ServerAltMessageHandlerDelegate handler) {
			CheckIfRegisteredBySystem(messageUId);
			if (_serverAltMessageHandlers.ContainsKey(messageUId)) {
				throw GetAlreadyRegisteredException(messageUId, typeof(ServerAltMessageHandlerDelegate).Name);
			}
			_serverAltMessageHandlers[messageUId] = handler;
		}

		/// <summary>[!] Client</summary>
		public void RegisterMessageHandler(ushort messageUId, ClientMessageHandlerDelegate handler) {
			CheckIfRegisteredBySystem(messageUId);
			if (_clientMessageHandlers.ContainsKey(messageUId)) {
				throw GetAlreadyRegisteredException(messageUId, typeof(ClientMessageHandlerDelegate).Name);
			}
			_clientMessageHandlers[messageUId] = handler;
		}

		/// <summary>[!] Client</summary>
		public void RegisterMessageHandler(ushort messageUId, ClientAltMessageHandlerDelegate handler) {
			CheckIfRegisteredBySystem(messageUId);
			if (_clientAltMessageHandlers.ContainsKey(messageUId)) {
				throw GetAlreadyRegisteredException(messageUId, typeof(ClientAltMessageHandlerDelegate).Name);
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
		private Exception GetUnregisteredMessageException(ushort messageUId, string handlerDelegateName) {
			return new Exception(logger.GetTaggedText($"Received an unregistered message ({handlerDelegateName}->{messageUId})"));
		}

		public void ServerOnData(ConnToClientData sender, ArraySegment<byte> data) {
			using PooledReader reader = ReaderPool.Get(data);
			ushort messageUId = reader.GetUShort();

			// auth
			ServerOnDataAuth(sender, messageUId);

			// req & res
			if (TryHandleReqRes(messageUId, sender.id, reader)) {
				return;
			}

			// with additional data?
			if (reader.Available > 0) {
				if (!_serverAltMessageHandlers.TryGetValue(messageUId, out ServerAltMessageHandlerDelegate altHandler)) {
					throw GetUnregisteredMessageException(messageUId, typeof(ServerAltMessageHandlerDelegate).Name);
				}
				altHandler.Invoke(sender, reader);
				return;
			}

			if (!_serverMessageHandlers.TryGetValue(messageUId, out ServerMessageHandlerDelegate handler)) {
				throw GetUnregisteredMessageException(messageUId, typeof(ServerMessageHandlerDelegate).Name);
			}
			handler.Invoke(sender);
		}

		public void ClientOnData(ArraySegment<byte> data) {
			using PooledReader reader = ReaderPool.Get(data);
			ushort messageUId = reader.GetUShort();

			// auth
			if (TryToQueueDataIfNotAuthorized(messageUId, reader)) {
				return;
			}

			// req & res
			if (TryHandleReqRes(messageUId, 0U, reader)) {
				return;
			}

			// with additional data?
			if (reader.Available > 0) {
				if (!_clientAltMessageHandlers.TryGetValue(messageUId, out ClientAltMessageHandlerDelegate altHandler)) {
					throw GetUnregisteredMessageException(messageUId, typeof(ClientAltMessageHandlerDelegate).Name);
				}
				altHandler.Invoke(reader);
				return;
			}

			if (!_clientMessageHandlers.TryGetValue(messageUId, out ClientMessageHandlerDelegate handler)) {
				throw GetUnregisteredMessageException(messageUId, typeof(ClientMessageHandlerDelegate).Name);
			}
			handler.Invoke();
		}
		#endregion

		#region Write
		/// <summary>Resets the internal 'writer' and writes 'uId' to it</summary>
		protected void SetMessage(ushort uId) {
			writer.SetPosition(0);
			writer.PutUShort(uId);
		}

		/// <summary>Resets the internal 'writer' and writes to it</summary>
		protected void SetMessage(ushort uId, SerializerDelegate serializerDelegate) {
			SetMessage(uId);
			serializerDelegate.Invoke(writer);
		}
		#endregion
	}
}
