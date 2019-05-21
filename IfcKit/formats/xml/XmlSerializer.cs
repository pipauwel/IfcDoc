// Name:        XmlSerializer.cs
// Description: XML serializer
// Author:      Tim Chipman
// Origination: Work performed for BuildingSmart by Constructivity.com LLC.
// Copyright:   (c) 2017 BuildingSmart International Ltd.
// License:     http://www.buildingsmart-tech.org/legal

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Xml;

namespace BuildingSmart.Serialization.Xml
{
	public class XmlSerializer : Serializer
	{
		public XmlSerializer(Type type) : base(type)
		{
			// get the XML namespace
		}

		public override object ReadObject(Stream stream)
		{
			if (stream == null)
				throw new ArgumentNullException("stream");

			// pull it into a memory stream so we can make multiple passes (can't assume it's a file; could be web service)
			//...MemoryStream memstream = new MemoryStream();

			Dictionary<string, object> instances = null;
			return ReadObject(stream, out instances);
		}

		/// <summary>
		/// Reads an object graph and provides access to instance identifiers from file.
		/// </summary>
		/// <param name="stream"></param>
		/// <param name="instances"></param>
		/// <returns></returns>
		public object ReadObject(Stream stream, out Dictionary<string, object> instances)
		{
			instances = new Dictionary<string, object>();

			// first pass: read header to verify schema and model view definitions...

			// second pass: read instances
			ReadContent(stream, instances, false);

			// third pass: read fields
			ReadContent(stream, instances, true);

			// stash project in empty string key
			object root = null;
			if (instances.TryGetValue(String.Empty, out root))
			{
				return root;
			}

			return null; // could not find the single project object
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="stream"></param>
		/// <param name="idmap"></param>
		/// <param name="parsefields">True to populate fields; False to load instances only.</param>
		private void ReadContent(Stream stream, Dictionary<string, object> idmap, bool parsefields)
		{
			stream.Position = 0;

			using (XmlReader reader = XmlReader.Create(stream))
			{
				while (reader.Read())
				{
					switch (reader.NodeType)
					{
						case XmlNodeType.Element:
							if (reader.Name == "ex:iso_10303_28")
							{
								//ReadIsoStep(reader, fixups, instances, inversemap);
							}
							else if (reader.LocalName.Equals("ifcXML"))
							{
								//ReadPopulation(reader, fixups, instances, inversemap);

								while (reader.Read())
								{
									switch (reader.NodeType)
									{
										case XmlNodeType.Element:
											ReadEntity(reader, idmap, reader.LocalName, parsefields);
											break;

										case XmlNodeType.EndElement:
											return;
									}
								}

							}
							break;
					}
				}
			}
		}

		private object ReadEntity(XmlReader reader, IDictionary<string, object> instances, string typename, bool parsefields)
		{
			object o = null;
			Type t;
			if (typename == "header")
			{
				t = typeof(header);
			}
			else
			{
				t = this.GetTypeByName(typename);
			}

			if (t != null)
			{
				// map instance id if used later
				string sid = reader.GetAttribute("id");
				if (!String.IsNullOrEmpty(sid) && !instances.TryGetValue(sid, out o))
				{
					o = FormatterServices.GetUninitializedObject(t);
					instances.Add(sid, o);
				}
				else if (o == null)
				{
					o = FormatterServices.GetUninitializedObject(t);
					if (!String.IsNullOrEmpty(sid))
					{
						instances.Add(sid, o);
					}
				}

				// stash project using blank index
				if (this.RootType.IsInstanceOfType(o) && !instances.ContainsKey(String.Empty))
				{
					instances.Add(String.Empty, o);
				}

				if (parsefields)
				{
					// ensure all lists/sets are instantiated
					IList<PropertyInfo> fields = GetFieldsOrdered(t);
					foreach (PropertyInfo f in fields)
					{
						Type type = f.PropertyType;

						if (IsEntityCollection(type))
						{
							Type typeCollection = this.GetCollectionInstanceType(type);
							object collection = Activator.CreateInstance(typeCollection);
							f.SetValue(o, collection);
						}
					}

					// read attribute properties
					for (int i = 0; i < reader.AttributeCount; i++)
					{
						reader.MoveToAttribute(i);
						if (!reader.LocalName.Equals("id"))
						{
							string match = reader.LocalName;
							PropertyInfo f = GetFieldByName(t, match);
							if (f != null)
							{
								ReadValue(reader, o, f, f.PropertyType);
							}
						}
					}
				}

				reader.MoveToElement();

				// now read attributes or end of entity
				if (!reader.IsEmptyElement)
				{
					while (reader.Read())
					{
						switch (reader.NodeType)
						{
							case XmlNodeType.Element:
								ReadAttribute(reader, o, instances);
								break;

							case XmlNodeType.Attribute:
								break;

							case XmlNodeType.EndElement:
								return o;
						}
					}
				}
			}

			return o;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="reader"></param>
		/// <param name="o"></param>
		/// <param name="instances"></param>
		private void ReadAttribute(XmlReader reader, object o, IDictionary<string, object> instances)
		{
			if (o == null)
				throw new ArgumentNullException("o");

			System.Diagnostics.Debug.WriteLine(">>ReadAttribute: " + o.GetType().Name + "." + reader.LocalName);

			// read attribute of object
			string a = reader.LocalName;

			if (a == "StyledByItem")
			{
				this.ToString();
			}

			string match = a;
			PropertyInfo f = GetFieldByName(o.GetType(), match);

			// inverse
			if (f == null)
			{
				f = GetInverseByName(o.GetType(), match);
			}

			if (f == null)
			{

				//Log("IFCXML: " + o.GetType().Name + "::" + reader.LocalName + " attribute name does not exist.");
				return;
			}

			// read attribute properties
			string reftype = null;

			if (!f.PropertyType.IsGenericType &&
				!f.PropertyType.IsInterface &&
				f.PropertyType != typeof(DateTime) &&
				f.PropertyType != typeof(string))
			{
				reftype = f.PropertyType.Name;
			}

			// interface, e.g. IfcRepresentation.StyledByItem\IfcStyledItem

			string t = reader.GetAttribute("xsi:type");
			string r = reader.GetAttribute("href");
			if (t != null)
			{
				reftype = t;
				if (t.Contains(":"))
				{
					string[] parts = t.Split(':');
					if (parts.Length == 2)
					{
						reftype = parts[1];
					}
				}
			}

			if (reftype != null)
			{
				this.ReadReference(reader, o, f, instances, reftype);
			}
			else
			{
				// now read object(s) of attribute -- multiple indicates list
				if (!reader.IsEmptyElement)
				{
					while (reader.Read())
					{
						switch (reader.NodeType)
						{
							case XmlNodeType.Element:
								ReadReference(reader, o, f, instances, reader.LocalName);
								break;

							case XmlNodeType.Text:
							case XmlNodeType.CDATA:
								ReadValue(reader, o, f, null);
								break;

							case XmlNodeType.EndElement:
								System.Diagnostics.Debug.WriteLine("!!ReadAttribute: " + o.GetType().Name + "." + reader.LocalName);
								return;
						}
					}
				}
			}

			System.Diagnostics.Debug.WriteLine("<<ReadAttribute: " + o.GetType().Name + "." + reader.LocalName);

		}

		/// <summary>
		/// Reads a reference or qualified value.
		/// </summary>
		/// <param name="reader"></param>
		/// <param name="o"></param>
		/// <param name="f"></param>
		private void ReadReference(XmlReader reader, object o, PropertyInfo f, IDictionary<string, object> instances, string typename)
		{
			if (typename.EndsWith("-wrapper"))
			{
				typename = typename.Substring(0, typename.Length - 8);
			}

			Type vt = GetTypeByName(typename);
			if (reader.Name == "ex:double-wrapper")
			{
				// drill in
				if (!reader.IsEmptyElement)
				{
					while (reader.Read())
					{
						switch (reader.NodeType)
						{
							case XmlNodeType.Text:
								ReadValue(reader, o, f, typeof(double));
								break;

							case XmlNodeType.EndElement:
								return;
						}
					}
				}
			}
			else if (vt != null)
			{
				Type et = f.PropertyType;
				if (typeof(IEnumerable).IsAssignableFrom(f.PropertyType))
				{
					et = f.PropertyType.GetGenericArguments()[0];
				}

				if (vt.IsValueType)
				{
					// drill in
					if (!reader.IsEmptyElement)
					{
						bool hasvalue = false;
						while (reader.Read())
						{
							switch (reader.NodeType)
							{
								case XmlNodeType.Text:
								case XmlNodeType.CDATA:
									if (ReadValue(reader, o, f, vt))
										return;
									hasvalue = true;
									break;

								case XmlNodeType.EndElement:
									if (!hasvalue)
									{
										ReadValue(reader, o, f, vt);
									}
									return;
							}
						}
					}
				}
				else
				{
					// reference
					string r = reader.GetAttribute("href");
					if (r != null)
					{
						object value = null;
						if (instances.TryGetValue(r, out value))
						{
							LoadEntityValue(o, f, value);
						}
						else
						{
							//fixups.Add(new Fixup(o, f, r));
						}
					}
					else
					{
						// embedded entity definition
						object v = this.ReadEntity(reader, instances, typename, true);
						LoadEntityValue(o, f, v);
					}
				}
			}
		}

		private void LoadCollectionValue(IEnumerable list, object v)
		{
			if (list == null)
				return;

			Type typeCollection = list.GetType();

			try
			{
				MethodInfo methodAdd = typeCollection.GetMethod("Add");
				methodAdd.Invoke(list, new object[] { v }); // perf!!
			}
			catch (Exception)
			{
				// could be type that changed and is no longer compatible with schema -- try to keep going
			}
		}

		private void LoadEntityValue(object o, PropertyInfo f, object v)
		{
			if (v == null)
				return;

			if (!f.PropertyType.IsValueType &&
				typeof(IEnumerable).IsAssignableFrom(f.PropertyType) &&
				f.PropertyType.IsGenericType &&
				f.PropertyType.GetGenericArguments()[0].IsInstanceOfType(v))
			{
				if (f.IsDefined(typeof(InversePropertyAttribute), false))//f.FieldType.GetGenericTypeDefinition() == typeof(SInverse<>))
				{
					InversePropertyAttribute[] attrs = (InversePropertyAttribute[])f.GetCustomAttributes(typeof(InversePropertyAttribute), false);

					// set the direct field, map to inverse
					PropertyInfo fieldDirect = GetFieldByName(v.GetType(), attrs[0].Property);
					if (IsEntityCollection(fieldDirect.PropertyType))
					{
						System.Collections.IEnumerable list = fieldDirect.GetValue(v) as System.Collections.IEnumerable;
						try
						{
							Type typeCollection = this.GetCollectionInstanceType(fieldDirect.PropertyType);
							MethodInfo methodAdd = typeCollection.GetMethod("Add");
							methodAdd.Invoke(list, new object[] { o }); // perf!!
						}
						catch (Exception e)
						{
							// could be type that changed and is no longer compatible with schema -- try to keep going
						}
					}
					else
					{
						// single object
						fieldDirect.SetValue(v, o);
					}

					// also add to inverse

					// allocate collection as needed
					System.Collections.IEnumerable listInv = (System.Collections.IEnumerable)f.GetValue(o);
					if (listInv == null)
					{
						Type typeCollection = this.GetCollectionInstanceType(f.PropertyType);
						listInv = (System.Collections.IEnumerable)System.Activator.CreateInstance(typeCollection);
						f.SetValue(o, listInv);
					}

					// add to inverse collection
					try
					{
						Type typeCollection = this.GetCollectionInstanceType(f.PropertyType);
						MethodInfo methodAdd = typeCollection.GetMethod("Add");
						methodAdd.Invoke(listInv, new object[] { v }); // perf!!
					}
					catch (Exception e)
					{
						// could be type that changed and is no longer compatible with schema -- try to keep going
					}

				}
				else
				{
					// add to set/list of values
					IEnumerable list = f.GetValue(o) as IEnumerable;
					if (list != null)
					{
						try
						{
							Type typeCollection = this.GetCollectionInstanceType(f.PropertyType);
							MethodInfo methodAdd = typeCollection.GetMethod("Add");
							methodAdd.Invoke(list, new object[] { v }); // perf!!
						}
						catch (Exception e)
						{
							// could be type that changed and is no longer compatible with schema -- try to keep going
						}

						UpdateInverse(o, f, v);
					}
				}
			}
			else
			{
				if (!f.PropertyType.IsInstanceOfType(v))
				{
					//this.Log("IFCXML: #" + o.OID + ": " + o.GetType().Name + "." + f.Name + ": '" + v.GetType().Name + "' type is incompatible.");
					return;
				}

				// set value
				f.SetValue(o, v);
				this.UpdateInverse(o, f, v);
			}

		}

		/// <summary>
		/// Reads a value
		/// </summary>
		/// <param name="reader">The xml reader</param>
		/// <param name="o">The entity</param>
		/// <param name="f">The field</param>
		/// <param name="ft">Optional explicit type, or null to use field type.</param>
		private bool ReadValue(XmlReader reader, object o, PropertyInfo f, Type ft)
		{
			bool endelement = false;

			if (ft == null)
			{
				ft = f.PropertyType;
			}

			if (ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(Nullable<>))
			{
				// special case for Nullable types
				ft = ft.GetGenericArguments()[0];
			}

			object v = null;
			if (ft.IsEnum)
			{
				FieldInfo enumfield = ft.GetField(reader.Value, BindingFlags.IgnoreCase | BindingFlags.Public | System.Reflection.BindingFlags.Static);
				if (enumfield != null)
				{
					v = enumfield.GetValue(null);
				}
			}
			else if (
				ft == typeof(DateTime) ||
				ft == typeof(string))
			{
				v = ParsePrimitive(reader.Value, ft);
			}
			else if (ft.IsValueType)
			{
				// defined type -- get the underlying field
				PropertyInfo[] fields = ft.GetProperties(BindingFlags.Instance | BindingFlags.Public); //perf: cache this
				if (fields.Length == 1)
				{
					PropertyInfo fieldValue = fields[0];
					object primval = ParsePrimitive(reader.Value, fieldValue.PropertyType);
					v = Activator.CreateInstance(ft);
					fieldValue.SetValue(v, primval);
				}
			}
			else if (IsEntityCollection(ft))
			{
				// IfcCartesianPoint.Coordinates

				Type typeColl = GetCollectionInstanceType(ft);
				v = System.Activator.CreateInstance(typeColl);

				Type typeElem = ft.GetGenericArguments()[0];
				PropertyInfo propValue = typeElem.GetProperty("Value");

				if (propValue != null)
				{
					string[] elements = reader.Value.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

					IEnumerable list = (IEnumerable)v;
					foreach (string elem in elements)
					{
						object elemv = Activator.CreateInstance(typeElem);
						object primv = ParsePrimitive(elem, propValue.PropertyType);
						propValue.SetValue(elemv, primv);
						LoadCollectionValue(list, elemv);
					}
				}
			}

			LoadEntityValue(o, f, v);

			return endelement;
		}

		private static object ParsePrimitive(string readervalue, Type type)
		{
			object value = null;
			if (typeof(Int64) == type)
			{
				// INTEGER
				value = ParseInteger(readervalue);
			}
			else if (typeof(Int32) == type)
			{
				value = (Int32)ParseInteger(readervalue);
			}
			else if (typeof(Double) == type)
			{
				// REAL
				value = ParseReal(readervalue);
			}
			else if (typeof(Single) == type)
			{
				value = (Single)ParseReal(readervalue);
			}
			else if (typeof(Boolean) == type)
			{
				// BOOLEAN
				value = ParseBoolean(readervalue);
			}
			else if (typeof(String) == type)
			{
				// STRING
				value = readervalue.Trim();
			}
			else if (typeof(DateTime) == type)
			{
				DateTime dtVal;
				if (DateTime.TryParse(readervalue, out dtVal))
				{
					value = dtVal;
				}
			}
			else if (typeof(byte[]) == type)
			{
				// BINARY
				int bytecount = readervalue.Length / 2;
				byte[] bytes = new byte[bytecount];
				//...reader.ReadContentAsBinHex(bytes, 0, bytes.Length);
				value = bytes;

				// modulo not supported for now
				//endelement = true;
			}

			return value;
		}

		private static bool ParseBoolean(string strval)
		{
			bool iv;
			if (Boolean.TryParse(strval, out iv))
			{
				return iv;
			}

			return false;
		}

		private static Int64 ParseInteger(string strval)
		{
			long iv;
			if (Int64.TryParse(strval, out iv))
			{
				return iv;
			}

			return 0;
		}

		private static Double ParseReal(string strval)
		{
			double iv;
			if (Double.TryParse(strval, out iv))
			{
				return iv;
			}

			return 0.0;
		}

		/// <summary>
		/// Writes an object graph to a stream in Step Physical File (SPF) format according to ISO 10303-21.
		/// </summary>
		/// <param name="stream">The stream to write.</param>
		/// <param name="root">The root object to write (typically IfcProject)</param>
		public override void WriteObject(Stream stream, object root)
		{
			if (stream == null)
				throw new ArgumentNullException("stream");

			if (root == null)
				throw new ArgumentNullException("root");

			// pass 1: (first time ever encountering for serialization) -- determine which entities require IDs -- use a null stream
			int nextID = 0;
			int indent = 0;
			StreamWriter writer = new StreamWriter(Stream.Null);
			HashSet<object> saved = new HashSet<object>();
			Dictionary<object, long> idmap = new Dictionary<object, long>();
			Queue<object> queue = new Queue<object>();
			queue.Enqueue(root);
			while (queue.Count > 0)
			{
				object ent = queue.Dequeue();
				if (!saved.Contains(ent))
				{
					this.WriteEntity(writer, ref indent, ent, saved, idmap, queue, ref nextID);
				}
			}

			// pass 2: write to file -- clear save map; retain ID map
			saved.Clear();
			indent = 0;
			writer = new StreamWriter(stream);

			this.WriteHeader(writer);

			// writer header info
			header h = new header();
			h.time_stamp = DateTime.UtcNow;
			h.preprocessor_version = this.Preprocessor;
			h.originating_system = this.Application;
			this.WriteEntity(writer, ref indent, h, saved, idmap, queue, ref nextID);

			bool rootdelim = false;
			queue.Enqueue(root);
			while (queue.Count > 0)
			{
				// insert delimeter after first root object
				if (rootdelim)
				{
					this.WriteRootDelimeter(writer);
				}
				rootdelim = true;

				object ent = queue.Dequeue();
				if (!saved.Contains(ent))
				{
					this.WriteEntity(writer, ref indent, ent, saved, idmap, queue, ref nextID);
				}
			}

			this.WriteFooter(writer);

			writer.Flush();
		}

		protected virtual void WriteHeader(StreamWriter writer)
		{
			string header = "<?xml version=\"1.0\" encoding=\"utf-8\"?>";

			string schema = "<ifc:ifcXML xsi:schemaLocation=\"" + this.BaseURI + " " + this.Schema + ".xsd\" " +
				"xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" " +
				"xmlns:ifc=\"" + this.BaseURI + "\" " +
				"xmlns=\"" + this.BaseURI + "\">";

			writer.WriteLine(header);
			writer.WriteLine(schema);
		}

		protected virtual void WriteFooter(StreamWriter writer)
		{
			string footer = "</ifc:ifcXML>";
			writer.WriteLine(footer);
			writer.Write("\r\n\r\n");
		}

		protected virtual void WriteRootDelimeter(StreamWriter writer)
		{
		}

		protected virtual void WriteCollectionStart(StreamWriter writer, ref int indent)
		{
		}

		protected virtual void WriteCollectionDelimiter(StreamWriter writer, int indent)
		{
		}

		protected virtual void WriteCollectionEnd(StreamWriter writer, ref int indent)
		{
		}

		protected virtual void WriteEntityStart(StreamWriter writer, ref int indent)
		{
		}

		protected virtual void WriteEntityEnd(StreamWriter writer, ref int indent)
		{
		}

		private void WriteEntity(StreamWriter writer, ref int indent, object o, HashSet<object> saved, Dictionary<object, long> idmap, Queue<object> queue, ref int nextID)
		{
			// sanity check
			if (indent > 100)
			{
				return;
			}

			if (o == null)
				return;

			Type t = o.GetType();

			this.WriteStartElementEntity(writer, ref indent, t.Name);
			bool close = this.WriteEntityAttributes(writer, ref indent, o, saved, idmap, queue, ref nextID);
			if (close)
			{
				this.WriteEndElementEntity(writer, ref indent, t.Name);
			}
			else
			{
				this.WriteCloseElementEntity(writer, ref indent);
			}
		}

		/// <summary>
		/// Terminates the opening tag, to allow for sub-elements to be written
		/// </summary>
		protected virtual void WriteOpenElement(StreamWriter writer)
		{
			// end opening tag
			writer.WriteLine(">");
		}

		/// <summary>
		/// Terminates the opening tag, with no subelements
		/// </summary>
		protected virtual void WriteCloseElementEntity(StreamWriter writer, ref int indent)
		{
			writer.WriteLine(" />");
			indent--;
		}

		protected virtual void WriteCloseElementAttribute(StreamWriter writer, ref int indent)
		{
			this.WriteCloseElementEntity(writer, ref indent);
		}

		protected virtual void WriteStartElementEntity(StreamWriter writer, ref int indent, string name)
		{
			this.WriteIndent(writer, indent);
			writer.Write("<" + name);
			indent++;
		}

		protected virtual void WriteStartElementAttribute(StreamWriter writer, ref int indent, string name)
		{
			this.WriteStartElementEntity(writer, ref indent, name);
		}

		protected virtual void WriteEndElementEntity(StreamWriter writer, ref int indent, string name)
		{
			indent--;

			this.WriteIndent(writer, indent);
			writer.Write("</");
			writer.Write(name);
			writer.WriteLine(">");
		}

		protected virtual void WriteEndElementAttribute(StreamWriter writer, ref int indent, string name)
		{
			WriteEndElementEntity(writer, ref indent, name);
		}

		protected virtual void WriteIdentifier(StreamWriter writer, int indent, long oid)
		{
			// record id, and continue to write out all attributes (works properly on second pass)
			writer.Write(" id=\"i");
			writer.Write(oid);
			writer.Write("\"");
		}

		protected virtual void WriteReference(StreamWriter writer, int indent, long oid)
		{
			writer.Write(" xsi:nil=\"true\" href=\"i");
			writer.Write(oid);
			writer.Write("\"");
		}

		protected virtual void WriteType(StreamWriter writer, int indent, string type)
		{
			writer.Write(" xsi:type=\"");
			writer.Write(type);
			writer.Write("\"");
		}

		protected virtual void WriteTypedValue(StreamWriter writer, ref int indent, string type, string encodedvalue)
		{
			this.WriteIndent(writer, indent);
			writer.WriteLine("<" + type + "-wrapper>" + encodedvalue + "</" + type + "-wrapper>");
		}

		protected virtual void WriteStartAttribute(StreamWriter writer, int indent, string name)
		{
			writer.Write(" ");
			writer.Write(name);
			writer.Write("=\"");
		}

		protected virtual void WriteEndAttribute(StreamWriter writer)
		{
			writer.Write("\"");
		}

		protected virtual void WriteAttributeDelimiter(StreamWriter writer)
		{
		}

		protected virtual void WriteAttributeTerminator(StreamWriter writer)
		{
		}

		private static bool IsEntityCollection(Type type)
		{
			return (type != typeof(string) && type != typeof(byte[]) && typeof(IEnumerable).IsAssignableFrom(type));
		}

		private static bool IsValueCollection(Type t)
		{
			return t.IsGenericType &&
				typeof(IEnumerable).IsAssignableFrom(t.GetGenericTypeDefinition()) &&
				t.GetGenericArguments()[0].IsValueType;
		}

		/// <summary>
		/// Returns true if any elements written (requiring closing tag); or false if not
		/// </summary>
		/// <param name="o"></param>
		/// <returns></returns>
		private bool WriteEntityAttributes(StreamWriter writer, ref int indent, object o, HashSet<object> saved, Dictionary<object, long> idmap, Queue<object> queue, ref int nextID)
		{
			Type t = o.GetType();

			long oid = 0;
			if (saved.Contains(o))
			{
				// give it an ID if needed (first pass)
				if (!idmap.TryGetValue(o, out oid))
				{
					nextID++;
					idmap[o] = nextID;
				}

				// reference existing; return
				this.WriteReference(writer, indent, oid);
				return false;
			}

			// mark as saved
			saved.Add(o);

			if (idmap.TryGetValue(o, out oid))
			{
				this.WriteIdentifier(writer, indent, oid);
			}

			bool previousattribute = false;

			// write fields as attributes
			bool haselements = false;
			IList<PropertyInfo> fields = this.GetFieldsAll(t);
			foreach (PropertyInfo f in fields)
			{
				if (f != null) // derived fields are null
				{
					DocXsdFormatEnum? xsdformat = this.GetXsdFormat(f);

					if (f.IsDefined(typeof(DataMemberAttribute)) && (xsdformat == null || (xsdformat != DocXsdFormatEnum.Element && xsdformat != DocXsdFormatEnum.Attribute)))
					{
						// direct field

						Type ft = f.PropertyType;

						bool isvaluelist = IsValueCollection(ft);
						bool isvaluelistlist = ft.IsGenericType && // e.g. IfcTriangulatedFaceSet.Normals
							typeof(System.Collections.IEnumerable).IsAssignableFrom(ft.GetGenericTypeDefinition()) &&
							IsValueCollection(ft.GetGenericArguments()[0]);

						if (isvaluelistlist || isvaluelist || ft.IsValueType)
						{
							object v = f.GetValue(o);
							if (v != null)
							{
								if (previousattribute)
								{
									this.WriteAttributeDelimiter(writer);
								}

								previousattribute = true;
								this.WriteStartAttribute(writer, indent, f.Name);

								if (isvaluelistlist)
								{
									ft = ft.GetGenericArguments()[0].GetGenericArguments()[0];
									PropertyInfo fieldValue = ft.GetProperty("Value");
									if (fieldValue != null)
									{
										System.Collections.IList list = (System.Collections.IList)v;
										for (int i = 0; i < list.Count; i++)
										{
											System.Collections.IList listInner = (System.Collections.IList)list[i];
											for (int j = 0; j < listInner.Count; j++)
											{
												if (i > 0 || j > 0)
												{
													writer.Write(" ");
												}

												object elem = listInner[j];
												if (elem != null) // should never be null, but be safe
												{
													elem = fieldValue.GetValue(elem);
													string encodedvalue = System.Security.SecurityElement.Escape(elem.ToString());
													writer.Write(encodedvalue);
												}
											}
										}
									}
									else
									{
										this.ToString();
									}
								}
								else if (isvaluelist)
								{
									ft = ft.GetGenericArguments()[0];
									PropertyInfo fieldValue = ft.GetProperty("Value");

									IEnumerable list = (IEnumerable)v;
									int i = 0;
									foreach (object e in list)
									{
										if (i > 0)
										{
											writer.Write(" ");
										}

										if (e != null) // should never be null, but be safe
										{
											object elem = e;
											if (fieldValue != null)
											{
												elem = fieldValue.GetValue(e);
											}

											if (elem is byte[])
											{
												// IfcPixelTexture.Pixels
												byte[] bytes = (byte[])elem;

												char[] s_hexchar = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };
												StringBuilder sb = new StringBuilder(bytes.Length * 2);
												for (int z = 0; z < bytes.Length; z++)
												{
													byte b = bytes[z];
													sb.Append(s_hexchar[b / 0x10]);
													sb.Append(s_hexchar[b % 0x10]);
												}
												v = sb.ToString();
												writer.Write(v);
											}
											else
											{
												string encodedvalue = System.Security.SecurityElement.Escape(elem.ToString());
												writer.Write(encodedvalue);
											}
										}

										i++;
									}

								}
								else
								{
									if (ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(Nullable<>))
									{
										// special case for Nullable types
										ft = ft.GetGenericArguments()[0];
									}

									Type typewrap = null;
									while (ft.IsValueType && !ft.IsPrimitive)
									{
										PropertyInfo fieldValue = ft.GetProperty("Value");
										if (fieldValue != null)
										{
											v = fieldValue.GetValue(v);
											if (typewrap == null)
											{
												typewrap = ft;
											}
											ft = fieldValue.PropertyType;
										}
										else
										{
											break;
										}
									}

									if (ft.IsEnum || ft == typeof(bool))
									{
										v = v.ToString().ToLowerInvariant();
									}

									if (v is System.Collections.IList)
									{
										// IfcCompoundPlaneAngleMeasure
										System.Collections.IList list = (System.Collections.IList)v;
										for (int i = 0; i < list.Count; i++)
										{
											if (i > 0)
											{
												writer.Write(" ");
											}

											object elem = list[i];
											if (elem != null) // should never be null, but be safe
											{
												string encodedvalue = System.Security.SecurityElement.Escape(elem.ToString());
												writer.Write(encodedvalue);
											}
										}
									}
									else if (v != null)
									{
										string encodedvalue = System.Security.SecurityElement.Escape(v.ToString());
										writer.Write(encodedvalue);
									}
								}

								this.WriteEndAttribute(writer);
							}
						}
						else
						{
							haselements = true;
						}
					}
					else
					{
						haselements = true;
					}
				}
			}

			if (haselements)
			{
				bool open = false;

				// write direct object references and lists
				foreach (PropertyInfo f in fields)
				{
					if (f != null) // derived attributes are null
					{
						DocXsdFormatEnum? format = GetXsdFormat(f);
						if (f.IsDefined(typeof(DataMemberAttribute)) && (format == null || (format != DocXsdFormatEnum.Element && format != DocXsdFormatEnum.Attribute)))
						{
							Type ft = f.PropertyType;
							bool isvaluelist = IsValueCollection(ft);
							bool isvaluelistlist = ft.IsGenericType && // e.g. IfcTriangulatedFaceSet.Normals
								typeof(IEnumerable).IsAssignableFrom(ft.GetGenericTypeDefinition()) &&
								IsValueCollection(ft.GetGenericArguments()[0]);

							// hide fields where inverse attribute used instead
							if (!f.PropertyType.IsValueType && !isvaluelist && !isvaluelistlist &&
								(format == null || (format != DocXsdFormatEnum.Hidden)))
							{
								object value = f.GetValue(o);
								if (value != null)
								{
									bool showit = true;

									if (!f.IsDefined(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), false) && value is IEnumerable)
									{
										showit = false;
										IEnumerable en = (IEnumerable)value;
										foreach (object sub in en)
										{
											showit = true;
											break;
										}
									}

									if (showit)
									{
										if (!open)
										{
											WriteOpenElement(writer);
											open = true;
										}

										if (previousattribute)
										{
											this.WriteAttributeDelimiter(writer);
										}
										previousattribute = true;

										WriteAttribute(writer, ref indent, o, f, saved, idmap, queue, ref nextID);
									}
								}
							}
						}
						else if (format != null && (format == DocXsdFormatEnum.Element || format == DocXsdFormatEnum.Attribute))
						{
							object value = f.GetValue(o);
							if (value != null)
							{
								// for collection is must be non-zero (e.g. IfcProject.IsNestedBy)
								bool showit = true; //...check: always include tag if Attribute (even if zero); hide if Element 
								if (value is IEnumerable) // what about IfcProject.RepresentationContexts if empty? include???
								{
									showit = false;
									IEnumerable enumerate = (IEnumerable)value;
									foreach (object check in enumerate)
									{
										showit = true; // has at least one element
										break;
									}
								}

								if (showit)
								{
									if (!open)
									{
										WriteOpenElement(writer);
										open = true;
									}

									if (previousattribute)
									{
										this.WriteAttributeDelimiter(writer);
									}
									previousattribute = true;

									WriteAttribute(writer, ref indent, o, f, saved, idmap, queue, ref nextID);
								}
							}
						}
						else
						{
							object value = f.GetValue(o);

							// inverse
							// record it for downstream serialization
							if (value is IEnumerable)
							{
								IEnumerable invlist = (IEnumerable)value;
								foreach (object invobj in invlist)
								{
									if (!saved.Contains(invobj))
									{
										queue.Enqueue(invobj);
									}
								}
							}
						}
					}
				}

				this.WriteAttributeTerminator(writer);
				return open;
			}
			else
			{
				this.WriteAttributeTerminator(writer);
				return false;
			}
		}

		private void WriteAttribute(StreamWriter writer, ref int indent, object o, PropertyInfo f, HashSet<object> saved, Dictionary<object, long> idmap, Queue<object> queue, ref int nextID)
		{
			object v = f.GetValue(o);
			if (v == null)
				return;

			this.WriteStartElementAttribute(writer, ref indent, f.Name);

			Type ft = f.PropertyType;
			PropertyInfo fieldValue = null;
			if (ft.IsValueType)
			{
				fieldValue = ft.GetProperty("Value"); // if it exists for value type
			}

			DocXsdFormatEnum? format = GetXsdFormat(f);
			if (format == null || format != DocXsdFormatEnum.Attribute || f.Name.Equals("InnerCoordIndices")) //hackhack -- need to resolve...
			{
				this.WriteOpenElement(writer);
			}

			if (IsEntityCollection(ft))
			{
				IEnumerable list = (IEnumerable)v;

				// for nested lists, flatten; e.g. IfcBSplineSurfaceWithKnots.ControlPointList
				if (typeof(IEnumerable).IsAssignableFrom(ft.GetGenericArguments()[0]))
				{
					// special case
					if (f.Name.Equals("InnerCoordIndices")) //hack
					{
						foreach (System.Collections.IEnumerable innerlist in list)
						{
							string entname = "Seq-IfcPositiveInteger-wrapper"; // hack
							this.WriteStartElementEntity(writer, ref indent, entname);
							this.WriteOpenElement(writer);
							foreach (object e in innerlist)
							{
								object ev = e.GetType().GetField("Value").GetValue(e);

								writer.Write(ev.ToString());
								writer.Write(" ");
							}
							writer.WriteLine();
							this.WriteEndElementEntity(writer, ref indent, entname);
						}
						WriteEndElementAttribute(writer, ref indent, f.Name);
						return;
					}

					ArrayList flatlist = new ArrayList();
					foreach (IEnumerable innerlist in list)
					{
						foreach (object e in innerlist)
						{
							flatlist.Add(e);
						}
					}

					list = flatlist;
				}

				// required if stated or if populated.....

				foreach (object e in list)
				{
					// if collection is non-zero and contains entity instances
					if (e != null && !e.GetType().IsValueType && !(e is string) && !(e is System.Collections.IEnumerable))
					{
						this.WriteCollectionStart(writer, ref indent);
					}
					break;
				}

				bool needdelim = false;
				foreach (object e in list)
				{
					if (e != null) // could be null if buggy file -- not matching schema
					{
						if (e is IEnumerable)
						{
							IEnumerable listInner = (IEnumerable)e;
							foreach (object oinner in listInner)//j = 0; j < listInner.Count; j++)
							{
								object oi = oinner;//listInner[j];

								Type et = oi.GetType();
								while (et.IsValueType && !et.IsPrimitive)
								{
									PropertyInfo fieldColValue = et.GetProperty("Value");
									if (fieldColValue != null)
									{
										oi = fieldColValue.GetValue(oi);
										et = fieldColValue.PropertyType;
									}
									else
									{
										break;
									}
								}

								// write each value in sequence with spaces delimiting
								string sval = oi.ToString();
								writer.Write(sval);
								writer.Write(" ");
							}
						}
						else if (!e.GetType().IsValueType && !(e is string)) // presumes an entity
						{
							if (needdelim)
							{
								this.WriteCollectionDelimiter(writer, indent);
							}

							if (format != null && format == DocXsdFormatEnum.Attribute)
							{
								// only one item, e.g. StyledByItem\IfcStyledItem
								this.WriteEntityStart(writer, ref indent);
								bool closeelem = this.WriteEntityAttributes(writer, ref indent, e, saved, idmap, queue, ref nextID);
								if (!closeelem)
								{
									this.WriteCloseElementAttribute(writer, ref indent);
									/////?????return;//TWC:20180624
								}
								else
								{
									this.WriteEntityEnd(writer, ref indent);
								}
								break; // if more items, skip them -- buggy input data; no way to encode
							}
							else
							{
								this.WriteEntity(writer, ref indent, e, saved, idmap, queue, ref nextID);
							}

							needdelim = true;
						}
						else
						{
							// if flat-list (e.g. structural load Locations) or list of strings (e.g. IfcPostalAddress.AddressLines), must wrap
							this.WriteValueWrapper(writer, ref indent, e);
						}
					}
				}

				foreach (object e in list)
				{
					if (e != null && !e.GetType().IsValueType && !(e is string))
					{
						this.WriteCollectionEnd(writer, ref indent);
					}
					break;
				}
			} // otherwise if not collection...
			else if (ft.IsInterface && v is ValueType)
			{
				this.WriteValueWrapper(writer, ref indent, v);
			}
			else if (ft == typeof(DateTime)) // header datetime
			{
				this.WriteOpenElement(writer);
				DateTime datetime = (DateTime)v;
				string datetimeiso8601 = datetime.ToString("s", System.Globalization.CultureInfo.InvariantCulture);
				writer.Write(datetimeiso8601);
			}
			else if (ft == typeof(string))
			{
				this.WriteOpenElement(writer);
				string strval = (string)(v);
				writer.Write(strval);
			}
			else if (fieldValue != null) // must be IfcBinary -- but not DateTime or other raw primitives
			{
				v = fieldValue.GetValue(v);
				if (v is byte[])
				{
					this.WriteOpenElement(writer);

					// binary data type - we don't support anything other than 8-bit aligned, though IFC doesn't either so no point in supporting extraBits
					byte[] bytes = (byte[])v;

					StringBuilder sb = new StringBuilder(bytes.Length * 2);
					for (int i = 0; i < bytes.Length; i++)
					{
						byte b = bytes[i];
						sb.Append(HexChars[b / 0x10]);
						sb.Append(HexChars[b % 0x10]);
					}
					v = sb.ToString();
					writer.WriteLine(v);
				}
			}
			else
			{
				if (format != null && format == DocXsdFormatEnum.Attribute)
				{
					this.WriteEntityStart(writer, ref indent);

					Type vt = v.GetType();
					if (ft != vt)
					{
						this.WriteType(writer, indent, vt.Name);
					}

					bool closeelem = this.WriteEntityAttributes(writer, ref indent, v, saved, idmap, queue, ref nextID);

					if (!closeelem)
					{
						this.WriteCloseElementEntity(writer, ref indent);
						return;
					}

					this.WriteEntityEnd(writer, ref indent);
				}
				else
				{
					// if rooted, then check if we need to use reference; otherwise embed
					this.WriteEntity(writer, ref indent, v, saved, idmap, queue, ref nextID);
				}
			}

			WriteEndElementAttribute(writer, ref indent, f.Name);
		}

		private void WriteValueWrapper(StreamWriter writer, ref int indent, object v)
		{
			Type vt = v.GetType();
			PropertyInfo fieldValue = vt.GetProperty("Value");
			while (fieldValue != null)
			{
				v = fieldValue.GetValue(v);
				if (v != null)
				{
					Type wt = v.GetType();
					if (wt.IsEnum || wt == typeof(bool))
					{
						v = v.ToString().ToLowerInvariant();
					}

					fieldValue = wt.GetProperty("Value");
				}
				else
				{
					fieldValue = null;
				}
			}

			string encodedvalue = String.Empty;
			if (v is IEnumerable && !(v is string))
			{
				// IfcIndexedPolyCurve.Segments
				IEnumerable list = (IEnumerable)v;
				StringBuilder sb = new StringBuilder();
				foreach (object o in list)
				{
					if (sb.Length > 0)
					{
						sb.Append(" ");
					}

					PropertyInfo fieldValueInner = o.GetType().GetProperty("Value");
					if (fieldValueInner != null)
					{
						//...todo: recurse for multiple levels of indirection, e.g. 
						object vInner = fieldValueInner.GetValue(o);
						sb.Append(vInner.ToString());
					}
					else
					{
						sb.Append(o.ToString());
					}
				}

				encodedvalue = sb.ToString();
			}
			else if (v != null)
			{
				encodedvalue = System.Security.SecurityElement.Escape(v.ToString());
			}

			this.WriteTypedValue(writer, ref indent, vt.Name, encodedvalue);
		}

		private DocXsdFormatEnum? GetXsdFormat(PropertyInfo field)
		{
			// direct fields marked ignore are ignored
			if (field.IsDefined(typeof(XmlIgnoreAttribute)))
				return DocXsdFormatEnum.Hidden;

			if (field.IsDefined(typeof(XmlAttributeAttribute)))
				return null;

			XmlElementAttribute attrElement = field.GetCustomAttribute<XmlElementAttribute>();
			if (attrElement != null)
			{
				if (!String.IsNullOrEmpty(attrElement.ElementName))
				{
					return DocXsdFormatEnum.Element; // tag according to attribute AND element name
				}
				else
				{
					return DocXsdFormatEnum.Attribute; // tag according to attribute name
				}
			}

			// inverse fields not marked with XmlElement are ignored
			if (attrElement == null && field.IsDefined(typeof(InversePropertyAttribute)))
				return DocXsdFormatEnum.Hidden;

			return null; //?
		}

		private enum DocXsdFormatEnum
		{
			Hidden = 1,//IfcDoc.Schema.CNF.exp_attribute.no_tag,    // for direct attribute, don't include as inverse is defined instead
			Attribute = 2,//IfcDoc.Schema.CNF.exp_attribute.attribute_tag, // represent as attribute
			Element = 3,//IfcDoc.Schema.CNF.exp_attribute.double_tag,   // represent as element
		}
	}

}


