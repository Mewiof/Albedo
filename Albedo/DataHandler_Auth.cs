using System;

namespace Albedo {

	public abstract partial class DataHandler {

		/// <summary>
		/// Server
		/// <para>Throws an exception to prevent further execution</para>
		/// </summary>
		private void ServerOnDataAuth(ConnToClientData dataSender, ushort messageUId) {
			// auth request?
			if (messageUId == SystemMessages.AUTH_REQUEST) {
				// has been sent before?
				if (dataSender.authStage != ConnToClientData.AuthStage.NotAuthenticated) {
					throw new Exception(logger.GetTaggedText("Received multiple auth requests"));
				}
				dataSender.authStage = ConnToClientData.AuthStage.Requested;
				return;
			}

			if (dataSender.authStage != ConnToClientData.AuthStage.Authenticated) {
				throw new Exception(logger.GetTaggedText("Received an unauthorized message"));
			}
		}

		/* Not all transports can guarantee ordered packet delivery, even with
		 * 'DeliveryMethod.Reliable'. But it is important to us that auth
		 * response packet always arrives first. Sending response message ("Hey, I got the message <3") back to
		 * server would add a lot of confusing code, as well as a lot of potential
		 * bugs. So I decided to solve this problem on client side by adding all but
		 * the right (auth response) message to queue before authorization and processing it afterwards
		 */
		// NOTE: reset on client start
		private readonly System.Collections.Generic.Queue<byte[]> _clientAuthDataQueue = new();

		/// <summary>
		/// Client
		/// </summary>
		private bool TryToQueueDataIfNotAuthorized(ushort messageUId, Reader reader) {
			// not authenticated & not a response message?
			if (((NetClient)this).connId == 0U && messageUId != SystemMessages.AUTH_RESPONSE) {
				// pack & enqueue
				writer.SetPosition(0);
				writer.PutUShort(messageUId);
				writer.PutRaw(reader.GetRemainingDataSegment().ToArray());
				_clientAuthDataQueue.Enqueue(writer.Data.ToArray());
				return true;
			}
			return false;
		}

		internal void DequeueClientAuthDataQueue() {
			while (_clientAuthDataQueue.Count > 0) {
				ClientOnData(_clientAuthDataQueue.Dequeue());
			}
		}

		protected void ClearClientAuthDataQueue() {
			_clientAuthDataQueue.Clear();
		}
	}
}
