using System;
using Cysharp.Threading.Tasks;

namespace Albedo {

	public partial class NetServer : DataHandler {

		public int maxNumOfConnections = 4;

		public readonly System.Collections.Generic.Dictionary<uint, ConnToClientData> connections = new();

		#region Events
		public delegate void StartedHandler();
		public event StartedHandler OnStarted;

		public delegate void StoppedHandler();
		public event StoppedHandler OnStopped;

		public delegate void ClientConnectedHandler(ConnToClientData conn);
		public event ClientConnectedHandler OnClientConnected;

		public delegate void TransportErrorHandler(Transport.Error error);
		public event TransportErrorHandler OnTransportError;

		public delegate void ClientDisconnectedHandler(ConnToClientData conn, Transport.DisconnInfo disconnInfo);
		public event ClientDisconnectedHandler OnClientDisconnected;
		#endregion

		public NetServer(Transport transport, Logger logger, NetAuthenticator authenticator) : base(transport, logger, authenticator) { }

		public void Tick() {
			transport.ServerTick();
		}

		/// <param name="time">Milliseconds</param>
		public async UniTaskVoid DelayedDisconnect(ConnToClientData conn, int time) {
			await UniTask.Delay(time, ignoreTimeScale: true);
			if (!conn.disconnected) {
				transport.Disconnect(conn.id);
			}
		}

		public void Start(ushort port) {
			if (transport.IsServer) {
				logger.LogWarning("Unable to start server (already active)");
				return;
			}

			// clear connection dict
			connections.Clear();

			// reset req & res
			_requestCallbacks.Clear();
			ResetNextRequestId();

			// reset callbacks
			transport.serverOnClientConnected = (connId, endPoint) => {
				// reached 'maxNumOfConnections'?
				if (connections.Count >= maxNumOfConnections) {
					// disconnect & log
					string endPointStr = endPoint.ToString();
					transport.Disconnect(connId);
					logger.LogWarning(endPointStr, "has been disconnected (reached max num of connections)");
					return;
				}

				// assign connection
				ConnToClientData conn = new(connId, endPoint);
				connections[connId] = conn;

				// callback
				OnClientConnected?.Invoke(conn);

				// auth
				authenticator.ServerOnAuth(conn);
			};
			transport.serverOnData = (connId, data) => {
				try {
					ServerOnData(connections[connId], data);
				} catch (Exception e) {
					// disconnect & log
					string endPointStr = connections[connId].address;
					transport.Disconnect(connId);
					logger.LogWarning(endPointStr, "has been disconnected (caused an exception)\n\n" + e.ToString());
				}
			};
			transport.serverOnError = error => OnTransportError?.Invoke(error);
			transport.serverOnClientDisconnected = (connId, disconnInfo) => {
				ConnToClientData conn = connections[connId];
				// mark disconnected
				conn.disconnected = true;
				// callback
				// NOTE: an exception will cause the following code not to execute, we do not catch it for performance
				OnClientDisconnected?.Invoke(conn, disconnInfo);
				// remove
				_ = connections.Remove(connId);
			};

			// start
			transport.StartServer(port);

			// log
			logger.Log("Server has been started");

			// callback
			OnStarted?.Invoke();
		}

		public void Stop() {
			if (!transport.IsServer) {
				logger.LogWarning("Unable to stop server (inactive)");
				return;
			}

			// stop
			transport.StopServer();

			// log
			logger.Log("Server has been stopped");

			// callback
			OnStopped?.Invoke();
		}

		public void Disconnect(uint connId) {
			transport.Disconnect(connId);
		}

		#region Send
		public void SendMessage(uint connId, ushort messageUId, DeliveryMethod deliveryMethod = DeliveryMethod.Reliable) {
			SetMessage(messageUId);
			transport.ServerSend(connId, writer.Data, deliveryMethod);
		}

		public void SendMessage(uint connId, ushort messageUId, SerializerDelegate extra, DeliveryMethod deliveryMethod = DeliveryMethod.Reliable) {
			SetMessage(messageUId, extra);
			transport.ServerSend(connId, writer.Data, deliveryMethod);
		}
		#endregion
	}
}
