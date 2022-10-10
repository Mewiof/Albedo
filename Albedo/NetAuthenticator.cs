using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Albedo {

	public abstract class NetAuthenticator {

		public const ushort
			REQUEST_MESSAGE_UNIQUE_ID = ushort.MaxValue,
			RESPONSE_MESSAGE_UNIQUE_ID = ushort.MaxValue - 1;

		public const byte
			RESPONSE_MESSAGE_TYPE_REJECT = 0,
			RESPONSE_MESSAGE_TYPE_ACCEPT = 1;

		public const float DELAY_BEFORE_DISCONNECTION = 1f;

		public readonly NetManager manager;

		public float timeout = 10f;

		public NetAuthenticator(NetManager manager) {
			this.manager = manager;
		}

		#region Abstract

		/// <summary>Server</summary>
		protected abstract void OnRequestMessage(ConnToClientData conn, Reader reader);

		/// <summary>Client</summary>
		protected abstract void OnServerAccepted(uint connId, Reader reader);

		/// <summary>Client</summary>
		protected abstract void OnServerRejected(Reader reader);

		#endregion

		private void Timeout(ConnToClientData conn) {
			if (conn.authStage != ConnToClientData.AuthStage.Authenticated) {
				manager.server.transport.Disconnect(conn.id);
			}
		}

		public void ServerStartAuth(ConnToClientData conn) {
			manager.server.taskManager.Add(timeout, () => Timeout(conn));
		}

		private void Internal_OnRequestMessage(ConnToClientData conn, Reader reader) {
			if (conn.authStage != ConnToClientData.AuthStage.NotAuthenticated) {
				// TODO: log
				manager.server.transport.Disconnect(conn.id);
				return;
			}

			conn.authStage = ConnToClientData.AuthStage.Requested;

			OnRequestMessage(conn, reader);
		}

		private void Internal_OnResponseMessage(Reader reader) {
			byte type = reader.GetByte();

			switch (type) {
				case RESPONSE_MESSAGE_TYPE_ACCEPT:
					OnServerAccepted(reader.GetUInt(), reader);
					break;
				case RESPONSE_MESSAGE_TYPE_REJECT:
					OnServerRejected(reader);
					manager.StopClient();
					break;
			}
		}

		public void RegisterMessageHandlers() {
			manager.server.RegisterMessageHandler(REQUEST_MESSAGE_UNIQUE_ID, Internal_OnRequestMessage);
			manager.client.RegisterMessageHandler(RESPONSE_MESSAGE_UNIQUE_ID, Internal_OnResponseMessage);
		}

		private static void PutAccept(Writer writer, uint connId) {
			// type
			writer.PutByte(RESPONSE_MESSAGE_TYPE_ACCEPT);
			// connId
			writer.PutUInt(connId);
		}

		protected void Accept(ConnToClientData conn) {
			conn.authStage = ConnToClientData.AuthStage.Authenticated;
			manager.server.SendMessage(conn.id, RESPONSE_MESSAGE_UNIQUE_ID, writer => PutAccept(writer, conn.id), DeliveryMethod.Reliable);
		}

		private static void PutReject(Writer writer) {
			// type
			writer.PutByte(RESPONSE_MESSAGE_TYPE_REJECT);
		}

		private static void PutReject(Writer writer, SerializerDelegate serializerDelegate) {
			// type
			writer.PutByte(RESPONSE_MESSAGE_TYPE_REJECT);
			// additional data
			serializerDelegate.Invoke(writer);
		}

		private void DelayedDisconnect(ConnToClientData conn) {
			manager.server.taskManager.Add(DELAY_BEFORE_DISCONNECTION, () =>
				manager.server.transport.Disconnect(conn.id));
		}

		protected void Reject(ConnToClientData conn) {
			manager.server.SendMessage(conn.id, RESPONSE_MESSAGE_UNIQUE_ID, writer => PutReject(writer), DeliveryMethod.Reliable);
			DelayedDisconnect(conn);
		}

		protected void Reject(ConnToClientData conn, SerializerDelegate serializerDelegate) {
			manager.server.SendMessage(conn.id, RESPONSE_MESSAGE_UNIQUE_ID, writer => PutReject(writer, serializerDelegate), DeliveryMethod.Reliable);
			DelayedDisconnect(conn);
		}
	}
}
