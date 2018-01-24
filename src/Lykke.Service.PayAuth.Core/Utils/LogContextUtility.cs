﻿using System.Collections.Generic;

namespace Lykke.Service.PayAuth.Core.Utils
{
    public static class LogContextUtility
    {
        public static IDictionary<string, string> ToContext(this string value, string key)
        {
            return new Dictionary<string, string> {{key, value}};
        }

        public static IDictionary<string, string> ToContext(this IDictionary<string, string> context, string key, object value)
        {
            context[key] = value?.ToString();
            return context;
        }
    }
}
