namespace LaLlamaDelBosque.Interfaces
{
	public interface IJsonRepository
	{
		T Read<T>(string filename) where T: new();
		T Read<T>(string filename, T fallback);
		T Write<T>(string filename, T value);
		string ReadText(string filename);
	}
}
