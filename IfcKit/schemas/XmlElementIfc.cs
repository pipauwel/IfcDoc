using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Xml.Serialization;

using BuildingSmart.IFC.IfcKernel;

namespace BuildingSmart.IFC
{
	[XmlType("ifcXML")]
	public class XmlElementIfc : List<object>
	{
		[DataMember(Name = "header", Order = 0)] [XmlElement] public XmlHeader Header { get; set; }

		public XmlElementIfc(XmlHeader header, IfcContext context)
		{
			Header = header;
			base.Add(context);
		}
		
		public const string NameSpace = "http://www.buildingsmart-tech.org/ifcXML/IFC4/Add1";
		public const string SchemaLocation = "http://www.buildingsmart-tech.org/ifcXML/IFC4/Add1/IFC4_ADD1.xsd"; 
	}
}
