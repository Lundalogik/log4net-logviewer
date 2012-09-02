﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace LogViewer
{
    public static class XmlExtension
    {
        public static string GetOrDefault(this XmlAttributeCollection self, string name)
        {
            if (null == self) { return default(string); }
            var val = self[name];
            if (null == val) { return default(string); }
            return val.Value;
        }
    }

    public class LogEntryParser
    {
        public IEnumerable<LogEntry> Parse(Stream s)
        {
            //todo? http://www.hanselman.com/blog/XmlAndTheNametable.aspx
            var doc = new XmlDocument { XmlResolver = null, };
            var nt = new NameTable();
            var nsmgr = new XmlNamespaceManager(nt);
            nsmgr.AddNamespace("log4j", "http://jakarta.apache.org/log4j/");
            nsmgr.AddNamespace("log4net", "http://logging.apache.org/log4net/");
            // Create the XmlParserContext.
            var context = new XmlParserContext(null, nsmgr, null, XmlSpace.None);

            // Create the reader. 
            var settings = new XmlReaderSettings
            {
                ConformanceLevel = ConformanceLevel.Fragment,
                CheckCharacters = false,
                XmlResolver = null,
                DtdProcessing = DtdProcessing.Ignore
            };
            var encoding = Encoding.UTF8;
            var xmlreader = XmlReader.Create(new StreamReader(s, encoding), settings, context);
#if !yield
            var list = new List<LogEntry>();
#endif
            while (xmlreader.Read())
            {
                LogEntry logentry = ParseElement(doc.ReadNode(xmlreader));
#if !yield
                list.Add(logentry);
#else
                yield return logentry;
#endif
            }
#if !yield
            return list;
#endif
        }
        private readonly DateTime _dt = new DateTime(1970, 1, 1, 0, 0, 0, 0);

        private LogEntry ParseElement(XmlNode xElement)
        {
            var logentry = new LogEntry();
            var timestamp = xElement.Attributes.GetOrDefault("timestamp");
            if (!string.IsNullOrEmpty(timestamp))
            {
                double dSeconds;
                logentry.TimeStamp = Double.TryParse(timestamp, out dSeconds)
                                         ? _dt.AddMilliseconds(dSeconds).ToLocalTime()
                                         : DateTime.Parse(timestamp).ToLocalTime();
            }
            logentry.Thread = xElement.Attributes.GetOrDefault("thread");


            logentry.App = xElement.Attributes.GetOrDefault("domain");

            #region get level

            ////////////////////////////////////////////////////////////////////////////////
            logentry.Level = xElement.Attributes.GetOrDefault("level");
            logentry.Image = ParseImageType(logentry.Level);
            ////////////////////////////////////////////////////////////////////////////////

            #endregion

            #region read xml

            var elements = xElement.ChildNodes;
            foreach (XmlNode element in elements)
            {
                var name = element.Name;
                if (element.NodeType == XmlNodeType.Text)
                {
                    continue;
                }
                var split = name.Split(':');
                if (split.Length <= 1) { throw new Exception("Name: " + name); }
                switch (split[1])
                {
                    case "message":
                        {
                            logentry.Message = element.InnerText;
                            break;
                        }
                    case "properties":
                        {
                            var properties = element.ChildNodes;

                            foreach (XmlNode property in properties)
                                switch (property.Attributes["name"].Value)
                                {
                                    case ("log4jmachinename"):
                                        logentry.MachineName = property.Attributes.GetOrDefault("value");
                                        break;
                                    case ("log4net:HostName"):
                                        logentry.HostName = property.Attributes.GetOrDefault("value");
                                        break;
                                    case ("log4net:UserName"):
                                        logentry.UserName = property.Attributes.GetOrDefault("value");
                                        break;
                                    case ("log4japp"):
                                        logentry.App = property.Attributes.GetOrDefault("value");
                                        break;
                                    default:
                                        throw new NotImplementedException(property.Attributes.GetOrDefault("name"));
                                }

                            break;
                        }

                    case "throwable":
                    case "exception":
                        {
                            logentry.Throwable = element.InnerText;
                            break;
                        }
                    case ("locationInfo"):
                        {
                            logentry.Class = element.Attributes.GetOrDefault("class");
                            logentry.Method = element.Attributes.GetOrDefault("method");
                            logentry.File = element.Attributes.GetOrDefault("file");
                            logentry.Line = element.Attributes.GetOrDefault("line");
                            break;
                        }
                    default:
                        throw new NotImplementedException(element.Name);
                }

            }

            #endregion

            return logentry;
        }



        public static LogEntry.ImageType ParseImageType(string level)
        {
            switch (level)
            {
                case "ERROR":
                    return LogEntry.ImageType.Error;
                case "INFO":
                    return LogEntry.ImageType.Info;
                case "DEBUG":
                    return LogEntry.ImageType.Debug;
                case "WARN":
                    return LogEntry.ImageType.Warn;
                case "FATAL":
                    return LogEntry.ImageType.Fatal;
                default:
                    return LogEntry.ImageType.Custom;
            }
        }
    }
}
