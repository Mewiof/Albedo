using UnityEngine;

namespace Albedo {

	[DisallowMultipleComponent]
	internal class NetManagerGUI : MonoBehaviour {

		[SerializeField] private NetManager _manager;

		private static GUILayoutOption _minWidth64;

		private void OnEnable() {
			_minWidth64 = GUILayout.MinWidth(64f);
		}

		private static T DrawLabeledField<T>(string label, System.Func<T> callback) {
			GUILayout.BeginHorizontal();
			GUILayout.Label(label);
			GUILayout.FlexibleSpace();
			T result = callback.Invoke();
			GUILayout.EndHorizontal();
			return result;
		}

		private static string DrawLabeledInputField(string label, string value) {
			return DrawLabeledField(label, () => GUILayout.TextField(value, _minWidth64));
		}

		private static ushort DrawLabeledUShortInputField(string label, ushort value) {
			return DrawLabeledField(label, () => ushort.TryParse(GUILayout.TextField(value.ToString(), _minWidth64), out ushort result) ? result : ushort.MinValue);
		}

		private void OnGUI() {
			if (_manager == null) {
				return;
			}

			GUILayout.BeginVertical("box");
			GUILayout.Space(8f);
			{
				_manager.address = DrawLabeledInputField("IP:", _manager.address);
				_manager.port = DrawLabeledUShortInputField("Port:", _manager.port);
				GUILayout.Space(8f);
				if (_manager.transport.IsServer && _manager.transport.IsClient) {
					if (GUILayout.Button("Stop Host")) {
						_manager.StopHost();
					}
				} else if (_manager.transport.IsServer) {
					if (GUILayout.Button("Stop Server")) {
						_manager.StopServer();
					}
				} else if (_manager.transport.IsClient) {
					GUILayout.Label("Auth: " + (_manager.Client.connId != 0U));
					GUILayout.Space(8f);
					if (GUILayout.Button("Stop Client")) {
						_manager.StopClient();
					}
				} else {
					if (GUILayout.Button("Start Server")) {
						_manager.StartServer();
					}
					if (GUILayout.Button("Start Client")) {
						_manager.StartClient();
					}
					if (GUILayout.Button("Start Host")) {
						_manager.StartHost();
					}
				}
			}
			GUILayout.Space(8f);
			GUILayout.EndVertical();
		}
	}
}
