﻿namespace ServiceStack.Configuration.Consul
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using Configuration;
    using DTO;
    using Logging;

    public class ConsulAppSettings : IAppSettings
    {
        private readonly string keyValueEndpoint;
        private readonly string consulUri;
        private readonly ILog log = LogManager.GetLogger(typeof(ConsulAppSettings));

        // TODO Handle consul not being available - circuit breaker?
        public ConsulAppSettings(string consulUri)
        {
            consulUri.ThrowIfNullOrEmpty("consulUri");
            
            if (consulUri.EndsWith("/"))
                consulUri = consulUri.Substring(0, consulUri.Length - 1);

            this.consulUri = consulUri;
            keyValueEndpoint = $"{consulUri}/v1/kv/";
        }

        // Default this to localhost/default port
        public ConsulAppSettings() : this("http://127.0.0.1:8500/")
        {
        }

        public Dictionary<string, string> GetAll()
        {
            var allkeys = GetAllKeys();

            var all = new Dictionary<string, string>();
            foreach (var key in allkeys)
            {
                all.Add(key, GetString(key));
            }

            return all;
        }

        public List<string> GetAllKeys()
        {
            // GET ?keys []
            try
            {
                return $"{keyValueEndpoint}?keys".GetJsonFromUrl().FromJson<List<string>>();
            }
            catch (Exception ex)
            {
                log.Error("Error getting all keys from Consul", ex);
                return null;
            }
        }

        public bool Exists(string key)
        {
            // 404 returned if not found
            var result = GetValue<string>(key, null);
            return result != null;
        }

        public void Set<T>(string key, T value)
        {
            // PUT. Throw if != true
            var keyVal = KeyValue.Create(key, value);
            var result = consulUri.CombineWith(keyVal.ToPutUrl()).PutJsonToUrl(keyVal.Value);

            bool success;
            if (!bool.TryParse(result, out success) || !success)
            {
                // TODO throw a wee error
            }
        }

        public string GetString(string name)
        {
            return GetValue<string>(name, null);
        }

        public IList<string> GetList(string key)
        {
            // GET /{name}
            return GetValue<List<string>>(key, null);
        }

        public IDictionary<string, string> GetDictionary(string key)
        {
            // NOTE Is this enough?
            return GetValue<Dictionary<string, string>>(key, null);
        }

        public T Get<T>(string name)
        {
            return GetValue(name, default(T));
        }

        public T Get<T>(string name, T defaultValue)
        {
            return GetValue(name, defaultValue);
        }

        private T GetValue<T>(string name, T defaultValue)
        {
            name.ThrowIfNullOrEmpty("name");

            try
            {
                // IF serialising to string then it just gets the base-64 string. Need to cast???;
                var keyValues = GetKeyValue(name);
                var value = keyValues.GetValue<T>();

                if (log.IsDebugEnabled)
                {
                    log.Debug($"Got config value {value} for key {name}");
                }

                return value;
            }
            catch (WebException ex) when (ex.ToStatusCode() == 404)
            {
                log.Error($"Unable to find config value with key {name}", ex);

                // throw KeyNotFoundException here
                return defaultValue;
            }
            catch (NotSupportedException ex)
            {
                log.Error($"Unable to deserialise config value with key {name}", ex);

                // throw something else or KNF???
                return defaultValue;
            }
            catch (Exception ex)
            {
                log.Error($"Error getting string value for config key {name}", ex);

                // throw something else or KNF???
                return defaultValue;
            }
        }

        // Catch with invalid type serialize?
        private KeyValue GetKeyValue(string name)
        {
            var keyVal = KeyValue.Create(name);
            var jsonFromUrl = consulUri.CombineWith(keyVal.ToGetUrl()).GetJsonFromUrl();
            var keyValues = jsonFromUrl.FromJson<List<KeyValue>>();
            var value = keyValues.First();
            return value;
        }
    }
}