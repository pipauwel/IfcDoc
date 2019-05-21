using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace BuildingSmart.Serialization.Xml
{
	public class header
	{
		[DataMember(Order = 0)]
		[XmlElement]
		public string name { get; set; }

		[DataMember(Order = 1)]
		[XmlElement]
		public DateTime time_stamp { get; set; }

		[DataMember(Order = 2)]
		[XmlElement]
		public string author { get; set; }

		[DataMember(Order = 3)]
		[XmlElement]
		public string organization { get; set; }

		[DataMember(Order = 4)]
		[XmlElement]
		public string preprocessor_version { get; set; }

		[DataMember(Order = 5)]
		[XmlElement]
		public string originating_system { get; set; }

		[DataMember(Order = 6)]
		[XmlElement]
		public string authorization { get; set; }
	}
}
