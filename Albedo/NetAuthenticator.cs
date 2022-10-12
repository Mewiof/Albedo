namespace Albedo {

	public abstract class NetAuthenticator {

		public const ushort
			REQUEST_MESSAGE_UNIQUE_ID = ushort.MaxValue,
			RESPONSE_MESSAGE_UNIQUE_ID = ushort.MaxValue - 1;

		public const byte
			RESPONSE_MESSAGE_TYPE_REJECT = 0,
			RESPONSE_MESSAGE_TYPE_ACCEPT = 1;

		/// <summary>
		/// When disconnecting client, it is important to wait some
		/// time for response message to reach it
		/// </summary>
		public const float DELAY_BEFORE_DISCONN = 1f;

		public readonly NetManager manager;

		/// <summary>
		/// Time for client to send auth request before disconn
		/// <para>(4 seconds by default)</para>
		/// </summary>
		public float timeout = 4f;

		public NetAuthenticator(NetManager manager) {
			this.manager = manager;
		}

		#region Abstract

		// Server

		/// <summary>
		/// Server
		/// <para>(use 'Accept()' / 'Reject()' methods)</para>
		/// </summary>
		protected abstract void OnRequestMessage(ConnToClientData conn, Reader reader);

		// Client

		/// <summary>Client</summary>
		protected abstract void ClientOnAccepted(uint connId);

		/// <summary>Client</summary>
		protected abstract void ClientOnRejected(Reader reader);

		/// <summary>
		/// Called on CLIENT at the beginning of auth process
		/// <para>(use to send custom auth request)</para>
		/// </summary>
		public abstract void ClientOnAuth();

		#endregion

		/// <summary>
		/// Called on SERVER at the beginning of auth process
		/// <para>(by default starts a countdown to timeout)</para>
		/// </summary>
		public virtual void ServerOnAuth(ConnToClientData conn) {
			conn.AddTask("auth_timeout", timeout, () => {
				if (conn.authStage != ConnToClientData.AuthStage.Authenticated) {
					manager.server.transport.Disconnect(conn.id);
				}
			});
		}

		private void Internal_OnResponseMessage(Reader reader) {
			byte type = reader.GetByte();

			switch (type) {
				case RESPONSE_MESSAGE_TYPE_ACCEPT:
					ClientOnAccepted(reader.GetUInt());
					return;

				case RESPONSE_MESSAGE_TYPE_REJECT:
					ClientOnRejected(reader);
					break;
			}

			manager.StopClient();
		}

		public void RegisterMessageHandlers() {
			manager.server.RegisterMessageHandler(REQUEST_MESSAGE_UNIQUE_ID, OnRequestMessage);
			manager.client.RegisterMessageHandler(RESPONSE_MESSAGE_UNIQUE_ID, Internal_OnResponseMessage);
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

		protected void Accept(ConnToClientData conn) {
			conn.authStage = ConnToClientData.AuthStage.Authenticated;
			manager.server.SendMessage(conn.id, RESPONSE_MESSAGE_UNIQUE_ID, writer => PutAccept(writer, conn.id), DeliveryMethod.Reliable); // 7 bytes
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
				manager.server.transport.Disconnect(conn.id));
		}

		protected void Reject(ConnToClientData conn) {
			manager.server.SendMessage(conn.id, RESPONSE_MESSAGE_UNIQUE_ID, writer => PutReject(writer), DeliveryMethod.Reliable); // 3 bytes
			DelayedDisconnect(conn);
		}

		protected void Reject(ConnToClientData conn, SerializerDelegate serializerDelegate) {
			manager.server.SendMessage(conn.id, RESPONSE_MESSAGE_UNIQUE_ID, writer => PutReject(writer, serializerDelegate), DeliveryMethod.Reliable); // 3 bytes + custom data
			DelayedDisconnect(conn);
		}

		#endregion
	}
}
