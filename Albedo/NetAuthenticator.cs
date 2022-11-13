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

		/// <summary>
		/// Client
		/// <para>'manager.client.connId' is now set, which can be used for identification</para>
		/// </summary>
		protected abstract void ClientOnAccepted(Reader reader);

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

		/* Not all transports can guarantee ordered packet delivery, even with
		 * 'DeliveryMethod.Reliable'. But it is important to us that auth
		 * response packet always arrives first. Sending response message ("Hey, I got the message <3") back to
		 * server would add a lot of confusing code, as well as a lot of potential
		 * bugs. So I decided to solve this problem on client side by adding all but
		 * the right (auth response) message to queue before authorization and processing it afterwards
		 * 
		 * [!] Clear when starting client
		 */
		public readonly System.Collections.Generic.Queue<byte[]> dataQueue = new();

		private void Internal_OnResponseMessage(Reader reader) {
			byte type = reader.GetByte();

			switch (type) {
				case RESPONSE_MESSAGE_TYPE_ACCEPT:
					// get 'connId'
					manager.client.connId = reader.GetUInt();
					// callback
					ClientOnAccepted(reader);
					// process all unprocessed data
					while (dataQueue.Count > 0) {
						byte[] data = dataQueue.Dequeue();
						manager.client.ClientOnData(new(data, 0, data.Length));
					}
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

		private static void PutAccept(Writer writer, uint connId, SerializerDelegate serializerDelegate) {
			// type & connId
			PutAccept(writer, connId);
			// additional data
			serializerDelegate.Invoke(writer);
		}

		protected void Accept(ConnToClientData conn) {
			conn.authStage = ConnToClientData.AuthStage.Authenticated;
			manager.server.SendMessage(conn.id, RESPONSE_MESSAGE_UNIQUE_ID, writer => PutAccept(writer, conn.id), DeliveryMethod.Reliable); // 7 bytes
		}

		/// <param name="serializerDelegate">Additional data</param>
		protected void Accept(ConnToClientData conn, SerializerDelegate serializerDelegate) {
			conn.authStage = ConnToClientData.AuthStage.Authenticated;
			manager.server.SendMessage(conn.id, RESPONSE_MESSAGE_UNIQUE_ID, writer => PutAccept(writer, conn.id, serializerDelegate), DeliveryMethod.Reliable);
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

		/// <param name="serializerDelegate">Additional data</param>
		protected void Reject(ConnToClientData conn, SerializerDelegate serializerDelegate) {
			manager.server.SendMessage(conn.id, RESPONSE_MESSAGE_UNIQUE_ID, writer => PutReject(writer, serializerDelegate), DeliveryMethod.Reliable); // 3 bytes + custom data
			DelayedDisconnect(conn);
		}

		#endregion
	}
}
