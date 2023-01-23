namespace Albedo {

	public static partial class Utils {

		public static string GetLogText(string tag, string text) {
			return string.Concat('[', tag, "] ", text);
		}
	}
}
