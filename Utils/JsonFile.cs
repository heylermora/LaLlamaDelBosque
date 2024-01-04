using System.Text.Json;

namespace LaLlamaDelBosque.Utils
{
    public class JsonFile
    {
        public static T Read<T>(string filename, T t)
        {
            try
            {
                var path = Path.GetFullPath($"Data/{filename}.json");
                var jsonString = System.IO.File.ReadAllText(path);
                t = JsonSerializer.Deserialize<T>(jsonString) ?? t;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return t;
        }

        public static T Write<T>(string filename, T t)
        {
            try
            {
                var jsonString = JsonSerializer.Serialize(t) ?? "";
                var path = Path.GetFullPath($"Data/{filename}.json");
                System.IO.File.WriteAllText(path, jsonString);
                return t;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                return Activator.CreateInstance<T>();
            }

        }
    }
}
