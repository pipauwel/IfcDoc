// Name:        XmlSerializer.cs
// Description: XML serializer
// Author:      Tim Chipman
// Origination: Work performed for BuildingSmart by Constructivity.com LLC.
// Copyright:   (c) 2017 BuildingSmart International Ltd.
// License:     http://www.buildingsmart-tech.org/legal

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
	public class XmlFolderSerializer : XmlSerializer
	{
		internal class QueueData
		{
			internal bool SingleObject { get; set; } = false;
			internal bool UniqueIdFolder { get; set; } = false;
			internal string Path { get; set; }
			internal object PayLoad { get; set; }

			internal QueueData(string path, object payload) { Path = path; PayLoad = payload; }

			public override string ToString()
			{
				return PayLoad.ToString() + " " + Path;
			}
		}

		private Dictionary<string, string> m_typeFilePrefix = new Dictionary<string, string>();
		private Dictionary<string, string> m_typeNoFilePrefix = new Dictionary<string, string>();
		private Dictionary<Type, string> m_NominatedTypeFilePrefix = new Dictionary<Type, string>();

		public XmlFolderSerializer(Type type) : base(type)
		{
			// get the XML namespace
			mObjectStore.UseUniqueIdReferences = true;
		}
		public XmlFolderSerializer(Type type, XmlFolderSerializer parent) : this(type)
		{
			mObjectStore = parent.mObjectStore;	
		}
		public void AddFilePrefix(Type type, string prefix)
		{
			m_NominatedTypeFilePrefix[type] = prefix;
			m_typeFilePrefix[type.FullName] = prefix;
		}
		private string hasFilePrefix(Type type)
		{
			string prefix = "", name = type.FullName, str = "";
			if (m_typeFilePrefix.TryGetValue(name, out prefix))
				return prefix;
			if (m_typeNoFilePrefix.TryGetValue(name, out  str))
				return "";
			foreach(KeyValuePair<Type, string> pair in m_NominatedTypeFilePrefix)
			{
				if(type.IsSubclassOf(pair.Key))
				{
					m_typeFilePrefix[name] = pair.Value;
					return pair.Value;
				}
			}
			m_typeNoFilePrefix[name] = name;
			return "";
			
		}
		private char initialChar(string name, string prefix, int prefixLength)
		{
			if (name.StartsWith(prefix))
				return name.Substring(prefixLength)[0];
			return name[0];
		}
		protected override void WriteHeader(StreamWriter writer)
		{
			string header = "<?xml version=\"1.0\" encoding=\"utf-8\"?>";
			writer.WriteLine(header);
		}
		protected override void WriteFooter(StreamWriter writer)
		{
			writer.Write("\r\n");
		}
		/// <summary>
		/// Writes an object graph to a folder hierarchy with local xml files.
		/// </summary>
		/// <param name="stream">The stream to write.</param>
		/// <param name="root">The root object to write (typically IfcProject)</param>
		public void WriteObject(string folderPath, object root)
		{
			if (Directory.Exists(folderPath))
				Directory.CreateDirectory(folderPath);
			if(!Directory.Exists(folderPath))
				throw new ArgumentNullException("Folder not Created");

			if (root == null)
				throw new ArgumentNullException("root");

			Queue<QueueData> queue = new Queue<QueueData>();

			int nextID = 0;
			WriteNestedObject(new QueueData(folderPath, root) { SingleObject = false }, queue, ref nextID);
			while(queue.Count > 0)
				WriteNestedObject(queue.Dequeue(), queue, ref nextID);
		}

		private void WriteNestedObject(QueueData dataObject, Queue<QueueData> queue, ref int nextID)
		{
			object obj = dataObject.PayLoad;

			Type objectType = obj.GetType(), stringType = typeof(String);
			string objectFileName = objectType.Name;
			if (dataObject.UniqueIdFolder)
			{
				PropertyInfo uniqueIdProp = objectType.GetProperty("id", typeof(string));
				if (uniqueIdProp != null)
				{
					object objectName = uniqueIdProp.GetValue(obj);
					if (objectName != null)
					{
						string name = objectName.ToString();
						if (!string.IsNullOrEmpty(name))
							objectFileName = name;
					}
				}
			}
			else
			{
				PropertyInfo nameProp = objectType.GetProperty("Name", typeof(string));
				if (nameProp != null)
				{
					object objectName = nameProp.GetValue(obj);
					if (objectName != null)
					{
						string name = objectName.ToString();
						if (!string.IsNullOrEmpty(name))
							objectFileName = name;
					}
				}
			}
			foreach (char c in Path.GetInvalidFileNameChars())
			{
				objectFileName = objectFileName.Replace(c, '_');
			}
			string path = dataObject.SingleObject ? dataObject.Path : Path.Combine(dataObject.Path, objectFileName);
			HashSet<string> nestedProperties = new HashSet<string>();

			IList<PropertyInfo> fields = this.GetFieldsAll(objectType);
			foreach (PropertyInfo propertyInfo in fields)
			{
				if (propertyInfo == null)
					continue;
				Type propertyType = propertyInfo.PropertyType;
				if (propertyType.IsGenericType && typeof(IEnumerable).IsAssignableFrom(propertyType.GetGenericTypeDefinition()))
				{
					Type genericType = propertyType.GetGenericArguments()[0];
					DataContractAttribute dataContractAttribute = genericType.GetCustomAttribute<DataContractAttribute>(true);
					PropertyInfo nameProperty = genericType.GetProperty("Name", typeof(string));
					PropertyInfo uniqueIdProperty = genericType.GetProperty("id", typeof(string));
					if(uniqueIdProperty == null)
						uniqueIdProperty = genericType.GetProperty("UniqueId", typeof(string));
					if ((nameProperty != null || uniqueIdProperty != null) && (dataContractAttribute == null || dataContractAttribute.IsReference))
					{
						IEnumerable enumerable = propertyInfo.GetValue(obj) as IEnumerable;
						if (enumerable != null)
						{
							bool allSaved = true;
							Dictionary<string, object> uniqueNames = new Dictionary<string, object>();
							int count = 0;
							foreach (object nested in enumerable)
							{
								count++;
								if(string.IsNullOrEmpty(mObjectStore.EncounteredId(nested)))
									allSaved = false;
								if(nameProperty != null)
								{
									object nameObject = nameProperty.GetValue(nested);
									if (nameObject == null)
									{
										uniqueNames.Clear();
										if (uniqueIdProperty == null)
											break;
										continue;
									}
									string name = nameObject.ToString();
									object existingObject = null;
									if (string.IsNullOrEmpty(name) || uniqueNames.TryGetValue(name, out existingObject))
									{
										uniqueNames.Clear();
										if (uniqueIdProperty == null)
											break;
										continue;
									}
									else
										uniqueNames[name] = nested;
								}
							}
							if (!allSaved && (uniqueNames.Count == count || uniqueIdProperty != null))
							{
								Directory.CreateDirectory(path);
								nestedProperties.Add(propertyInfo.Name);
								string nestedPath = Path.Combine(path, propertyInfo.Name);
								Directory.CreateDirectory(nestedPath);
								if(count > 500 && uniqueNames.Count == count)
								{
									string prefix = m_NominatedTypeFilePrefix.Count > 0 ? hasFilePrefix(genericType) : "";
									IEnumerable<IGrouping<char, object>> groups = null;
									if (string.IsNullOrEmpty(prefix))
										groups = enumerable.Cast<object>().GroupBy(x => char.ToLower(nameProperty.GetValue(x).ToString()[0]));
									else
									{
										int prefixLength = prefix.Length;
										groups = enumerable.Cast<object>().GroupBy(x => char.ToLower(initialChar(nameProperty.GetValue(x).ToString(), prefix, prefixLength)));
									}
									if(groups.Count() > 2)
									{
										foreach(IGrouping<char,object> group in groups)
										{
											string alphaPath = Path.Combine(nestedPath, group.Key.ToString());
											foreach (object nested in group)
											{
												mObjectStore.MarkEncountered(nested, ref nextID);
												queue.Enqueue(new QueueData(alphaPath, nested));
											}
										}
										continue;
									}
								}
								foreach (object nested in enumerable)
								{
									mObjectStore.MarkEncountered(nested, ref nextID);
									queue.Enqueue(new QueueData(nestedPath, nested) { UniqueIdFolder = uniqueNames.Count != count });
								}
							}
						}
					}
				}
				else
				{
					object propertyObject = propertyInfo.GetValue(obj);
					if(propertyObject == null)
					{
						nestedProperties.Add(propertyInfo.Name);
					}
					else
					{
						DataTypeAttribute dataTypeAttribute = propertyInfo.GetCustomAttribute(typeof(DataTypeAttribute)) as DataTypeAttribute;
						if (dataTypeAttribute != null)
						{
							if (dataTypeAttribute.DataType == DataType.Html)
							{
								string html = propertyObject.ToString();
								if (!string.IsNullOrEmpty(html))
								{
									Directory.CreateDirectory(path);
									nestedProperties.Add(propertyInfo.Name);
									string htmlPath = Path.Combine(path, propertyInfo.Name + ".html");
									File.WriteAllText(htmlPath, html);
									continue;
								}
							}
							else if (dataTypeAttribute.DataType == DataType.MultilineText)
							{
								string txt = propertyObject.ToString();
								if (!string.IsNullOrEmpty(txt))
								{
									Directory.CreateDirectory(path);
									nestedProperties.Add(propertyInfo.Name);
									string htmlPath = Path.Combine(path, propertyInfo.Name + ".txt");
									File.WriteAllText(htmlPath, txt);
									continue;
								}
							}
						}
						Type propertyObjectType = propertyObject.GetType();
						if (!(propertyObjectType.IsValueType || propertyObjectType == stringType))
						{
							if (string.IsNullOrEmpty(mObjectStore.EncounteredId(obj)))
							{
								DataContractAttribute dataContractAttribute = propertyObjectType.GetCustomAttribute<DataContractAttribute>(true);
								if (dataContractAttribute == null || dataContractAttribute.IsReference)
								{
									queue.Enqueue(new QueueData(Path.Combine(path, propertyInfo.Name), propertyObject) { SingleObject = true });
									nestedProperties.Add(propertyInfo.Name);
									mObjectStore.MarkEncountered(propertyObject, ref nextID);
								}
							}
						}
					}
				}
			}
			if (nestedProperties.Count < fields.Count)
			{
				mObjectStore.RemoveEncountered(obj);
				Directory.CreateDirectory(path);
				using (FileStream fileStream = new FileStream(Path.Combine(path, objectFileName + ".xml"), FileMode.Create, FileAccess.Write))
				{
					XmlFolderSerializer serializer = new XmlFolderSerializer(objectType, this);
					serializer.writeObject(fileStream, obj, nestedProperties, ref nextID);
				}
			}
		}
	}
}


