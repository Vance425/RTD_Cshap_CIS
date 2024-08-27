using System;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace RTDWebAPI.Commons.Method.Log
{
    public class LogManagement
    {
        public static void LogTrace(string[] args)
        {
            CreateLogger();

            Logger logger = LogManager.GetCurrentClassLogger();

            logger.Trace("Trace");
            logger.Debug("Debug");
            logger.Info("Info");
            logger.Warn("Warn");
            logger.Error("Error");
            logger.Fatal("Fatal");


            Console.ReadLine();
        }

        public static void CreateLogger()
        {
            var config = new LoggingConfiguration();
            var fileTarget = new FileTarget
            {
                FileName = "${basedir}/logs/${shortdate}.log",
                Layout = "${date:format=yyyy-MM-dd HH\\:mm\\:ss} [${uppercase:${level}}] ${message}",
            };
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, fileTarget);
            LogManager.Configuration = config;
        }
    }
}
