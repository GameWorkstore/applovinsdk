using System;
using System.Diagnostics;

namespace log4net
{
    public static class LogManager
    {
        public enum LogLevel
        {
            Error = 0,
            Warning = 1,
            Info = 2,
            Debug = 3,
        }
        public static LogLevel CurrentLogLevel { get; set; } = LogLevel.Error;
        
        public static ILog GetLogger(Type type)
        {
            return new ILog(type);
        }
    }

    // ReSharper disable once InconsistentNaming
    public class ILog
    {
        // ReSharper disable once InconsistentNaming
        private readonly string _logFormat;
        
        public ILog(Type type)
        {
            _logFormat = "[" + type.FullName + "] {0}";
        }
        
        public void DebugFormat(string format, params object[] logObjects)
        {
            if(LogManager.CurrentLogLevel < LogManager.LogLevel.Debug) return;
            var final = string.Format(format, logObjects);
            UnityEngine.Debug.Log(string.Format(_logFormat,final));
        }

        public void Debug(string message)
        { 
            if(LogManager.CurrentLogLevel < LogManager.LogLevel.Debug) return;
            UnityEngine.Debug.Log(string.Format(_logFormat,message));
        }
        
        public void Info(string message)
        {
            if(LogManager.CurrentLogLevel < LogManager.LogLevel.Info) return;
            UnityEngine.Debug.Log(string.Format(_logFormat,message));
        }

        public void InfoFormat(string format, params object[] logObjects)
        {
            if(LogManager.CurrentLogLevel < LogManager.LogLevel.Info) return;
            var final = string.Format(format, logObjects);
            UnityEngine.Debug.Log(string.Format(_logFormat,final));
        }

        public void Warn(string message)
        {
            if(LogManager.CurrentLogLevel < LogManager.LogLevel.Warning) return;
            UnityEngine.Debug.LogWarning(string.Format(_logFormat,message));
        }
        
        public void WarnFormat(string format, params object[] logObjects)
        {
            if(LogManager.CurrentLogLevel < LogManager.LogLevel.Warning) return;
            var final = string.Format(format, logObjects);
            UnityEngine.Debug.LogWarning(string.Format(_logFormat,final));
        }

        public void ErrorFormat(string format, params object[] logObjects)
        {
            var final = string.Format(format, logObjects);
            UnityEngine.Debug.LogError(string.Format(_logFormat,final));
        }

        public void Error(string message, Exception exception)
        {
            UnityEngine.Debug.LogError(string.Format(_logFormat,message) + " err:" + exception);
        }
    }
}
