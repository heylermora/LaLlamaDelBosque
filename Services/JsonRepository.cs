using LaLlamaDelBosque.Interfaces;
using LaLlamaDelBosque.Utils;

namespace LaLlamaDelBosque.Services
{
	public sealed class JsonRepository: IJsonRepository
	{
		public T Read<T>(string filename) where T: new()
		{
			return JsonFile.Read(filename, new T());
		}

		public T Read<T>(string filename, T fallback)
		{
			return JsonFile.Read(filename, fallback);
		}

		public T Write<T>(string filename, T value)
		{
			return JsonFile.Write(filename, value);
		}

		public string ReadText(string filename)
		{
			var path = Path.GetFullPath($"Data/{filename}.json");
			return File.ReadAllText(path);
		}
	}
}
