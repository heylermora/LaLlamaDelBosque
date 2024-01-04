using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Newtonsoft.Json;

namespace LaLlamaDelBosque.Utils
{
    public static class TempDataExtensions
    {
        public static bool Put<T>(this ITempDataDictionary tempData, string key, T? value) where T : class
        {
            if(value != null && tempData != null) {
                tempData[key] = JsonConvert.SerializeObject(value) ?? null;
                return true;
            }
            else if(tempData != null && tempData.ContainsKey(key))
                tempData.Remove(key);
            return false;
		}

		public static T? Get<T>(this ITempDataDictionary tempData, string key) where T : class
        {
            if(tempData != null && tempData.ContainsKey(key))
            {
                tempData.TryGetValue(key, out object? o);
                return o == null ? null : JsonConvert.DeserializeObject<T>((string)o);
            }
            return null;
        }
    }
}
