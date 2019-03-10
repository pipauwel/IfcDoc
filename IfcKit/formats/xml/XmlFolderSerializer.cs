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

		public object ReadObject(string folderPath)
		{
			if (!Directory.Exists(folderPath))
				throw new ArgumentNullException("Folder doesn't exist");

			Dictionary<string, object> instances = new Dictionary<string, object>();
			QueuedObjects queuedObjects = new QueuedObjects();

			return readFolder(folderPath, RootType, instances, queuedObjects);
		}
		private object readFolder(string folderPath, Type nominatedType, Dictionary<string, object> instances, QueuedObjects queuedObjects)
		{
			string[] files = Directory.GetFiles(folderPath, "*.xml", SearchOption.TopDirectoryOnly);
			if (files == null || files.Length == 0)
				return null;

			if (files.Length > 1)
				throw new Exception("Unexpected multiple xml files in folder " + folderPath);

			string filePath = files[0];
			string fileName = Path.GetFileNameWithoutExtension(filePath);
			Type detectedType = GetTypeByName(fileName);
			

			string typeName = detectedType == null ? "" : detectedType.Name;

			object result = null;
			using (FileStream streamSource = new FileStream(filePath, FileMode.Open))
			{
				XmlReaderSettings settings = new XmlReaderSettings { NameTable = new NameTable() };
				XmlNamespaceManager xmlns = new XmlNamespaceManager(settings.NameTable);
				xmlns.AddNamespace("xsi", "http://www.w3.org/2001/XMLSchema-instance");
				XmlParserContext context = new XmlParserContext(null, xmlns, "", XmlSpace.Default);
				using (XmlReader reader = XmlReader.Create(streamSource, settings, context))
				{
					result = ReadEntity(reader, instances, typeName, queuedObjects, string.IsNullOrEmpty(typeName));
				}
			}
			string[] directories = Directory.GetDirectories(folderPath, "*", SearchOption.TopDirectoryOnly);
			foreach(string directory in directories)
			{
				string directoryName = new DirectoryInfo(directory).Name;	
				PropertyInfo f = GetFieldByName(detectedType == null ? nominatedType : detectedType , directoryName);
				if (f == null)
				{

				}
				else
				{
					if (IsEntityCollection(f.PropertyType))
					{
						IEnumerable list = f.GetValue(result) as IEnumerable;
						Type typeCollection = list.GetType();
						MethodInfo methodAdd = typeCollection.GetMethod("Add");
						if (methodAdd == null)
						{
							throw new Exception("Unsupported collection type " + typeCollection.Name);
						}
						Type collectionGeneric = typeCollection.GetGenericArguments()[0];
						List<object> objects = new List<object>();
						string[] subDirectories = Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly);
						foreach(string subDir in subDirectories)
						{
							string[] subfiles = Directory.GetFiles(subDir, "*.xml", SearchOption.TopDirectoryOnly);
							if (subfiles.Length > 0)
							{
								object o = readFolder(subDir, collectionGeneric, instances, queuedObjects);
								if (o != null)
									objects.Add(o);
							}
							else
							{
								string[] subsubDirectories = Directory.GetDirectories(subDir, "*", SearchOption.TopDirectoryOnly);
								foreach(string subsubDir in subsubDirectories)
								{
									object o = readFolder(subsubDir, collectionGeneric, instances, queuedObjects);
									if (o != null)
										objects.Add(o);
								}

							}
						}
						foreach (object o in objects)
						{
							try
							{
								methodAdd.Invoke(list, new object[] { o }); // perf!!
							}
							catch (Exception) { }
						}
					}
					else
					{
						object o = readFolder(directory, f.PropertyType, instances, queuedObjects);
						if (o != null)
							LoadEntityValue(result, f, o);
					}
				}

			}
			return result;
		}
	}
}


