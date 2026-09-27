using System;
using System.Linq;
using System.Reflection;

namespace JoinLeaveAlerts
{
    public static class OptionalEventSubscription
    {
        public static bool TrySubscribe(string assemblyName, string typeName, string eventName, MethodInfo handler, out Delegate eventHandler)
        {
            eventHandler = null;
            if (handler == null) return false;

            try
            {
                Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(item => String.Equals(item.GetName().Name, assemblyName, StringComparison.Ordinal));
                if (assembly == null) assembly = Assembly.Load(assemblyName);
                Type type = assembly.GetType(typeName, false);
                if (type == null) return false;

                EventInfo eventInfo = type.GetEvent(eventName, BindingFlags.Public | BindingFlags.Static);
                if (eventInfo == null || eventInfo.EventHandlerType == null) return false;

                eventHandler = Delegate.CreateDelegate(eventInfo.EventHandlerType, handler);
                eventInfo.AddEventHandler(null, eventHandler);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
