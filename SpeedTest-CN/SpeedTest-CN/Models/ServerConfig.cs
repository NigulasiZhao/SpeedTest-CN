using System.Xml.Serialization;

namespace SpeedTest_CN.Models
{
    [XmlRoot("server-config")]
    public class ServerConfig
    {
        [XmlAttribute("ignoreids")]
        public string IgnoreIds { get; set; }
    }
}