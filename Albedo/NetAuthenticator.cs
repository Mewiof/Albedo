using System;
using UnityEngine;

namespace Albedo {

	public abstract class NetAuthenticator : MonoBehaviour {

		public NetServer server;
		public NetClient client;

		public const byte
			RESPONSE_MESSAGE_TYPE_REJECT = 0,
			RESPONSE_MESSAGE_TYPE_ACCEPT = 100;

		/// <summary>
		/// When disconnecting client, it is important to wait some
		/// time for response message to reach it
		/// </summary>
		public const float DELAY_BEFORE_DISCONN = 1f;

		/// <summary>
		/// Time for client to send auth request before disconn
		/// <para>(4 seconds by default)</para>
		/// </summary>
		public float timeout = 4f;

		/// <summary>
		/// 'client.connId' is now set, which can be used for identification
		/// </summary>
		public Action<Reader> clientOnAccepted;
		public Action<Reader> clientOnRejected;

		#region Abstract
		/// <summary>
		/// Server
		/// <para>Use 'Accept' / 'Reject' methods</para>
		/// </summary>
		protected abstract void OnRequestMessage(ConnToClientData conn, Reader reader);

		/// <summary>
		/// Client
		/// <para>Called at the beginning of auth process. Can be used to send an auth request</para>
		/// </summary>
		public abstract void ClientOnAuth();
		#endregion

		/// <summary>
		/// Server
		/// <para>Called at the beginning of auth process. By default starts a countdown to timeout</para>
		/// </summary>
		public virtual void ServerOnAuth(ConnToClientData conn) {
			conn.AddTask("auth_timeout", timeout, () => {
				if (conn.authStage != ConnToClientData.AuthStage.Authenticated) {
					server.transport.Disconnect(conn.id);
				}
			});
		}

		private void OnResponseMessage(Reader reader) {
			byte type = reader.GetByte();

			switch (type) {
				case RESPONSE_MESSAGE_TYPE_ACCEPT:
					// get 'connId'
					client.connId = reader.GetUInt();
					// callback
					clientOnAccepted?.Invoke(reader);
					// process all unprocessed data
					client.DequeueClientAuthDataQueue();
					return;

				case RESPONSE_MESSAGE_TYPE_REJECT:
					// callback
					clientOnRejected?.Invoke(reader);
					break;
			}

			client.Stop();
		}

		internal void RegisterMessageHandlers() {
			server.RegisterMessageHandler(SystemMessages.AUTH_REQUEST, OnRequestMessage);
			client.RegisterMessageHandler(SystemMessages.AUTH_RESPONSE, OnResponseMessage);
		}

		// 7 bytes for Accept(), 3 for Reject()
		#region Accept & Reject
		// 5 bytes
		private static void PutAccept(Writer writer, uint connId) {
			// type
			writer.PutByte(RESPONSE_MESSAGE_TYPE_ACCEPT);
			// connId
			writer.PutUInt(connId);
		}

		private static void PutAccept(Writer writer, uint connId, SerializerDelegate serializerDelegate) {
			// type & connId
			PutAccept(writer, connId);
			// additional data
			serializerDelegate.Invoke(writer);
		}

		protected void Accept(ConnToClientData conn) {
			conn.authStage = ConnToClientData.AuthStage.Authenticated;
			server.SendMessage(conn.id, SystemMessages.AUTH_RESPONSE, writer => PutAccept(writer, conn.id), DeliveryMethod.Reliable); // 7 bytes
		}

		/// <param name="serializerDelegate">Additional data</param>
		protected void Accept(ConnToClientData conn, SerializerDelegate serializerDelegate) {
			conn.authStage = ConnToClientData.AuthStage.Authenticated;
			server.SendMessage(conn.id, SystemMessages.AUTH_RESPONSE, writer => PutAccept(writer, conn.id, serializerDelegate), DeliveryMethod.Reliable);
		}

		// 1 byte
		private static void PutReject(Writer writer) {
			// type
			writer.PutByte(RESPONSE_MESSAGE_TYPE_REJECT);
		}

		// 1 byte + custom data
		private static void PutReject(Writer writer, SerializerDelegate serializerDelegate) {
			// type
			writer.PutByte(RESPONSE_MESSAGE_TYPE_REJECT);
			// additional data
			serializerDelegate.Invoke(writer);
		}

		private void DelayedDisconnect(ConnToClientData conn) {
			conn.AddTask("delayed_disconnect", DELAY_BEFORE_DISCONN, () =>
				server.transport.Disconnect(conn.id));
		}

		protected void Reject(ConnToClientData conn) {
			server.SendMessage(conn.id, SystemMessages.AUTH_RESPONSE, writer => PutReject(writer), DeliveryMethod.Reliable); // 3 bytes
			DelayedDisconnect(conn);
		}

		/// <param name="serializerDelegate">Additional data</param>
		protected void Reject(ConnToClientData conn, SerializerDelegate serializerDelegate) {
			server.SendMessage(conn.id, SystemMessages.AUTH_RESPONSE, writer => PutReject(writer, serializerDelegate), DeliveryMethod.Reliable); // 3 bytes + custom data
			DelayedDisconnect(conn);
		}
		#endregion
	}
}
