using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Albedo {

	public abstract class NetAuthenticator : MonoBehaviour {

		public NetServer server;
		public NetClient client;

		public const byte
			RESPONSE_MESSAGE_TYPE_REJECT = 0,
			RESPONSE_MESSAGE_TYPE_ACCEPT = 100;

		/// <summary>
		/// When disconnecting a client, it is important to wait some
		/// time for response message to reach it
		/// </summary>
		public const int DELAY_BEFORE_DISCONN = 2000;

		/// <summary>
		/// Milliseconds
		/// <para>'5000' by default</para>
		/// </summary>
		[Tooltip("Milliseconds\nSet to zero to disable")] public int timeout = 5000;

		/// <summary>
		/// Called on client when a response message is received
		/// <para>'client.connId' is set before call, which can be used for identification</para>
		/// </summary>
		public Action<Reader> clientOnAccepted;
		/// <summary>
		/// Called on client when a response message is received
		/// </summary>
		public Action<Reader> clientOnRejected;

		private async UniTaskVoid StartTimeoutFor(ConnToClientData conn) {
			await UniTask.Delay(timeout, ignoreTimeScale: true);

			if (!conn.disconnected && conn.authStage != ConnToClientData.AuthStage.Authenticated) {
				string endPointStr = conn.address;
				server.Disconnect(conn.id);
				server.logger.Log(string.Concat("[Auth] ", Logger.GetParamDescText(endPointStr, "has been timed out")));
			}
		}

		#region Abstract
		/// <summary>
		/// Called on server when a request message is received
		/// <para>Use 'Accept(<paramref name="conn"/>, [extra])' or 'Reject(<paramref name="conn"/>, [extra])'</para>
		/// <para>Use 'server.Disconnect(<paramref name="conn"/>)' to disconnect immediately (for spam and the like)</para>
		/// </summary>
		/// <param name="conn">Sender</param>
		protected abstract void OnRequest(ConnToClientData conn, Reader reader);

		/// <summary>
		/// Do not call the base on override
		/// <para>Called on client at the beginning of auth process. Can be used to send an auth request</para>
		/// </summary>
		public virtual void ClientOnAuth() { }
		#endregion

		/// <summary>
		/// Call the base on override (starts a timeout)!
		/// <para>Called on server at the beginning of auth process</para>
		/// </summary>
		public virtual void ServerOnAuth(ConnToClientData conn) {
			StartTimeoutFor(conn).Forget();
		}

		private void OnResponse(Reader reader) {
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

		/// <summary>
		/// Call the base on override (registers system messages)!
		/// </summary>
		public virtual void OnInit() {
			server.RegisterMessageHandler(SystemMessages.AUTH_REQUEST, OnRequest);
			client.RegisterMessageHandler(SystemMessages.AUTH_RESPONSE, OnResponse);
		}

		// 7 bytes for 'Accept', 3 for 'Reject'
		#region Accept & Reject
		// 5 bytes
		private static void PutAccept(Writer writer, uint connId) {
			// type
			writer.PutByte(RESPONSE_MESSAGE_TYPE_ACCEPT);
			// connId
			writer.PutUInt(connId);
		}

		private static void PutAccept(Writer writer, uint connId, SerializerDelegate extra) {
			// type & connId
			PutAccept(writer, connId);
			// extra
			extra.Invoke(writer);
		}

		protected void Accept(ConnToClientData conn) {
			conn.authStage = ConnToClientData.AuthStage.Authenticated;
			server.SendMessage(conn.id, SystemMessages.AUTH_RESPONSE, writer => PutAccept(writer, conn.id), DeliveryMethod.Reliable); // 7 bytes
		}

		protected void Accept(ConnToClientData conn, SerializerDelegate extra) {
			conn.authStage = ConnToClientData.AuthStage.Authenticated;
			server.SendMessage(conn.id, SystemMessages.AUTH_RESPONSE, writer => PutAccept(writer, conn.id, extra), DeliveryMethod.Reliable);
		}

		// 1 byte
		private static void PutReject(Writer writer) {
			// type
			writer.PutByte(RESPONSE_MESSAGE_TYPE_REJECT);
		}

		private static void PutReject(Writer writer, SerializerDelegate extra) {
			// type
			writer.PutByte(RESPONSE_MESSAGE_TYPE_REJECT);
			// extra
			extra.Invoke(writer);
		}

		protected void Reject(ConnToClientData conn) {
			server.SendMessage(conn.id, SystemMessages.AUTH_RESPONSE, writer => PutReject(writer), DeliveryMethod.Reliable); // 3 bytes
			server.DelayedDisconnect(conn, DELAY_BEFORE_DISCONN).Forget();
		}

		protected void Reject(ConnToClientData conn, SerializerDelegate extra) {
			server.SendMessage(conn.id, SystemMessages.AUTH_RESPONSE, writer => PutReject(writer, extra), DeliveryMethod.Reliable);
			server.DelayedDisconnect(conn, DELAY_BEFORE_DISCONN).Forget();
		}
		#endregion
	}
}
