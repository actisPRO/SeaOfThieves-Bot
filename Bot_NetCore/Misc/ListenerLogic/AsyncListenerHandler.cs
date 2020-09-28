﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DSharpPlus;

namespace Bot_NetCore.Misc
{
    internal static class AsyncListenerHandler
    {
        public static IEnumerable<ListenerMethod> ListenerMethods { get; private set; }

        public static void InstallListeners(DiscordClient client, Bot bot)
        {
            // find all methods from ModCore with AsyncListener attr
            ListenerMethods =
                from a in AppDomain.CurrentDomain.GetAssemblies()
                from t in a.GetTypes()
                    //where t.Namespace.StartsWith("ModCore")
                from m in t.GetMethods()
                let attribute = m.GetCustomAttribute(typeof(AsyncListenerAttribute), true)
                where attribute != null
                select new ListenerMethod { Method = m, Attribute = attribute as AsyncListenerAttribute };

            foreach (var listener in ListenerMethods)
            {
                listener.Attribute.Register(bot, client, listener.Method);

                client.DebugLogger.LogMessage(LogLevel.Debug, "SoT", $"{listener.Method.DeclaringType.Name}.{listener.Method.Name} installed as {listener.Attribute.Target} event", DateTime.Now);
            }
        }
    }

    internal class ListenerMethod
    {
        public MethodInfo Method { get; internal set; }
        public AsyncListenerAttribute Attribute { get; internal set; }
    }
}
