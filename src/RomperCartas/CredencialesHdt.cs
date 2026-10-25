using System;
using System.Reflection;
using System.Threading.Tasks;

namespace RomperCartas
{
	/// <summary>Obtiene de HDT las credenciales con las que consulta HSReplay. HDT no las expone a los plugins,
	/// así que se leen por reflexión: ApiWrapper.Client (HsReplayClient._apiKey), ApiWrapper.GetUploadToken()
	/// (crea el token de esta instalación si todavía no existe) y Helper.GetUserAgent().
	/// Sin el User-Agent de HDT, HSReplay responde con la verificación anti-bots de Cloudflare.</summary>
	public static class CredencialesHdt
	{
		const BindingFlags Estatico = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

		public static async Task<CredencialesHsreplay> Obtener()
		{
			var hdt = typeof(Hearthstone_Deck_Tracker.API.Core).Assembly;
			var wrapper = Tipo(hdt, "Hearthstone_Deck_Tracker.HsReplay.ApiWrapper");

			var cliente = Campo(wrapper, "Client", Estatico).GetValue(null)
				?? throw new InvalidOperationException("ApiWrapper.Client es null");
			var clave = Campo(cliente.GetType(), "_apiKey", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(cliente) as string;

			var obtenerToken = wrapper.GetMethod("GetUploadToken", Estatico)
				?? throw new MissingMethodException("ApiWrapper", "GetUploadToken");
			var token = await (Task<string>)obtenerToken.Invoke(null, null);

			var helper = Tipo(hdt, "Hearthstone_Deck_Tracker.Helper");
			var agente = (helper.GetMethod("GetUserAgent", Estatico) ?? throw new MissingMethodException("Helper", "GetUserAgent"))
				.Invoke(null, null) as string;

			if (string.IsNullOrEmpty(clave) || string.IsNullOrEmpty(token) || string.IsNullOrEmpty(agente))
				throw new InvalidOperationException($"Credenciales incompletas (clave: {!string.IsNullOrEmpty(clave)}, " +
					$"token: {!string.IsNullOrEmpty(token)}, User-Agent: {!string.IsNullOrEmpty(agente)})");
			return new CredencialesHsreplay(clave, token, agente);
		}

		static Type Tipo(Assembly a, string nombre) =>
			a.GetType(nombre) ?? throw new TypeLoadException($"No existe {nombre} en esta versión de HDT");

		static FieldInfo Campo(Type t, string nombre, BindingFlags flags) =>
			t.GetField(nombre, flags) ?? throw new MissingFieldException(t.FullName, nombre);
	}
}
