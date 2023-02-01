using System;
using UnityEngine;

namespace Albedo {

	[DefaultExecutionOrder(-1000)]
	public class NetManager : MonoBehaviour {

		public const string LOCAL_ADDRESS = "127.0.0.1";

		public Logger Logger { get; private set; }

		public NetServer Server { get; private set; }
		public NetClient Client { get; private set; }

		public string address = LOCAL_ADDRESS;
		public ushort port = 25500;
		public Transport transport;
		public NetAuthenticator authenticator;

		/// <summary>Override. Called on server and client during manager initialization</summary>
		protected virtual void OnInit() { }

		private void Init() {
			Logger = new(name);

			if (transport == null) {
				throw new Exception(Logger.GetTaggedParamDescText(nameof(transport), "is null"));
			}

			if (authenticator == null) {
				throw new Exception(Logger.GetTaggedParamDescText(nameof(authenticator), "is null"));
			}

			Server = new(transport, Logger, authenticator);
			Client = new(transport, Logger, authenticator);

			authenticator.server = Server;
			authenticator.client = Client;
			authenticator.RegisterMessageHandlers();

			OnInit();
		}

		private void Tick(float delta) {
			if (transport.IsServer) {
				Server.Tick(ref delta);
			}
			if (transport.IsClient) {
				Client.Tick();
			}
		}

		#region Start / Stop
		/// <summary>
		/// Same as 'Server.Start(<paramref name="port"/>)'
		/// </summary>
		public void ServerStart(ushort port) {
			Server.Start(port);
		}

		public void ServerStart() {
			ServerStart(port);
		}

		/// <summary>
		/// Same as 'Server.Stop()'
		/// </summary>
		public void ServerStop() {
			Server.Stop();
		}

		/// <summary>
		/// Same as 'Client.Start(<paramref name="address"/>, <paramref name="port"/>)'
		/// </summary>
		public void ClientStart(string address, ushort port) {
			Client.Start(address, port);
		}

		public void ClientStart() {
			ClientStart(address, port);
		}

		/// <summary>
		/// Same as 'Client.Stop()'
		/// </summary>
		public void ClientStop() {
			Client.Stop();
		}

		public void HostStart() {
			ServerStart();
			ClientStart(LOCAL_ADDRESS, port);
		}

		public void HostStop() {
			if (transport.IsClient) {
				ClientStop();
			}
			if (transport.IsServer) {
				ServerStop();
			}
		}
		#endregion

		#region Req & Res
		public void ServerRegisterRequest<TRequest, TResponse>(ushort requestUId, RequestHandlerDelegate<TRequest, TResponse> requestHandler, ResponseHandlerDelegate<TResponse> responseHandler = null)
			where TRequest : struct, INetSerializable
			where TResponse : struct, INetSerializable {

			Server.RegisterRequestHandler(requestUId, requestHandler);
			Client.RegisterResponseHandler<TRequest, TResponse>(requestUId, responseHandler);
		}

		public void ServerUnregisterRequest(ushort requestUId) {
			Server.UnregisterRequestHandler(requestUId);
			Client.UnregisterResponseHandler(requestUId);
		}
		#endregion

		private void Awake() {
			Init();
		}

		private void Update() {
			Tick(Time.unscaledDeltaTime);
		}

		private void OnApplicationQuit() {
			HostStop();
		}

		private void OnDestroy() {
			HostStop();
		}
	}
}
