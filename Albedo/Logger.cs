using UnityEngine;

namespace Albedo {

	public sealed class Logger {

		private readonly string _tag;

		public Logger(string tag) {
			_tag = tag;
		}

		public static string GetParamDescText(string param, string desc) {
			return string.Concat('\'', param, "' ", desc);
		}

		public string GetTaggedText(string text) {
			return string.Concat('[', _tag, "] ", text);
		}

		public string GetTaggedParamDescText(string param, string desc) {
			return GetTaggedText(GetParamDescText(param, desc));
		}

		public void Log(string text) {
			Debug.Log(GetTaggedText(text));
		}

		public void Log(string param, string desc) {
			Log(GetParamDescText(param, desc));
		}

		public void LogWarning(string text) {
			Debug.LogWarning(GetTaggedText(text));
		}

		public void LogWarning(string param, string desc) {
			LogWarning(GetParamDescText(param, desc));
		}

		public void LogError(string text) {
			Debug.LogError(GetTaggedText(text));
		}

		public void LogError(string param, string desc) {
			LogError(GetParamDescText(param, desc));
		}
	}
}
