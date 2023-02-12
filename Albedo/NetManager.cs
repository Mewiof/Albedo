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
			authenticator.OnInit();

			OnInit();
		}

		private void Tick() {
			if (transport.IsServer) {
				Server.Tick();
			}
			if (transport.IsClient) {
				Client.Tick();
			}
		}

		#region Start / Stop
		public void ServerStart() {
			Server.Start(port);
		}

		public void ClientStart() {
			Client.Start(address, port);
		}

		public void HostStart() {
			ServerStart();
			Client.Start(LOCAL_ADDRESS, port);
		}

		public void HostStop() {
			if (transport.IsClient) {
				Client.Stop();
			}
			if (transport.IsServer) {
				Server.Stop();
			}
		}
		#endregion

		#region Req & Res
		// void
		/// <summary>
		/// Registers a request handler for server and a response handler for client
		/// </summary>
		public void ServerRegisterRequestHandler<TRequest, TResponse>(ushort uId, RequestHandlerDelegate<TRequest, TResponse> handler)
			where TRequest : struct, INetSerializable
			where TResponse : struct, INetSerializable {

			Server.RegisterRequestHandler(uId, handler);
			Client.RegisterResponseHandler<TRequest, TResponse>(uId);
		}

		// UniTaskVoid
		/// <summary>
		/// Registers a request handler for server and a response handler for client
		/// </summary>
		public void ServerRegisterRequestHandler<TRequest, TResponse>(ushort uId, RequestAltHandlerDelegate<TRequest, TResponse> altHandler)
			where TRequest : struct, INetSerializable
			where TResponse : struct, INetSerializable {

			Server.RegisterRequestHandler(uId, altHandler);
			Client.RegisterResponseHandler<TRequest, TResponse>(uId);
		}

		/// <summary>
		/// Unregisters a request handler for server and client
		/// </summary>
		public void ServerUnregisterRequest(ushort uId) {
			Server.UnregisterRequestHandler(uId);
			Client.UnregisterResponseHandler(uId);
		}
		#endregion

		private void Awake() {
			Init();
		}

		private void Update() {
			Tick();
		}

		private void OnApplicationQuit() {
			HostStop();
		}

		private void OnDestroy() {
			HostStop();
		}
	}
}
