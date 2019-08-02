// Name:        FormatJAV.cs
// Description: Java Code Generator
// Author:      Tim Chipman
// Origination: Work performed for BuildingSmart by Constructivity.com LLC.
// Copyright:   (c) 2013 BuildingSmart International Ltd.
// License:     http://www.buildingsmart-tech.org/legal

using System;
using System.Collections.Generic;
using System.Text;

using IfcDoc.Schema;
using IfcDoc.Schema.DOC;

using System.Runtime.Serialization.Json;
using System.IO;

namespace IfcDoc.Format.JAV
{
	internal class FormatJAV : IDisposable,
		IFormatExtension
	{
		//+++++++++++++++
		//ATTRIBUTES
		//+++++++++++++++
		string m_filename;
		DocProject m_project;
        DocSchema m_schema;
        DocDefinition m_definition;
        Dictionary<string, DocObject> m_map;

		//+++++++++++++++
		//CONSTRUCTORS
		//+++++++++++++++

		public FormatJAV()
		{
			this.m_filename = null;
		}

		public FormatJAV(string filename)
		{
			this.m_filename = filename;
		}

		//+++++++++++++++
		//METHODS
		//+++++++++++++++

		/// <summary>
		/// Generates folder of definitions
		/// </summary>
		/// <param name="path"></param>
		public static void GenerateCode(DocProject project, string path, Dictionary<string, DocObject> map, DocCodeEnum options)
		{
			string schemaid = project.GetSchemaIdentifier();

			foreach (DocSection docSection in project.Sections)
			{
				foreach (DocSchema docSchema in docSection.Schemas)
				{
					string pathSchema = path + @"\" + docSchema.Name;
					if (!Directory.Exists(pathSchema))
					{
						Directory.CreateDirectory(pathSchema);
					}

					foreach (DocType docType in docSchema.Types)
					{
						string file = docSchema.Name + @"\" + docType.Name + ".java";
						using (FormatJAV format = new FormatJAV(path + @"\" + file))
						{
							format.Instance = project;
							format.Schema = docSchema;
							format.Definition = docType;
							format.Map = map;
							format.Save();
						}
					}

					foreach (DocEntity docType in docSchema.Entities)
					{
						string file = docSchema.Name + @"\" + docType.Name + ".java";
						using (FormatJAV format = new FormatJAV(path + @"\" + file))
						{
							format.Instance = project;
							format.Schema = docSchema;
							format.Definition = docType;
							format.Map = map;
							format.Save();
						}
					}
				}
			}
		}

		public void Save()
		{
			string dirpath = System.IO.Path.GetDirectoryName(this.m_filename);
			if (!System.IO.Directory.Exists(this.m_filename))
			{
				System.IO.Directory.CreateDirectory(dirpath);
			}

			if (this.m_definition == null)
			{
				return;
			}

			//if (!(this.m_definition is DocEntity))
			//{
			//	return;
			//}

			using (System.IO.StreamWriter writer = new System.IO.StreamWriter(this.m_filename))
			{
				writer.WriteLine("// This file was automatically generated from IFCDOC at https://technical.buildingsmart.org/.");
				writer.WriteLine("// IFC content is copyright (C) 1996-2013 BuildingSMART International Ltd.");
				writer.WriteLine("// Author: Pieter Pauwels, Ghent University");
				writer.WriteLine();
				
				if (this.m_definition != null)
				{
					writer.Write("package com.buildingsmart.tech.ifc");
					if (this.m_schema != null)
					{
						writer.Write("." + this.m_schema.Name);
					}
					writer.Write(";");

					writer.WriteLine();
					writer.WriteLine();

					//IMPORTS
					if (!(this.m_definition is DocSelect) && !(this.m_definition is DocEnumeration))
					{
						this.WriteImports(writer);
						writer.WriteLine();
					}

					//CONTENT
					if (this.m_definition is DocDefined)
					{
						//TODO
						//Console.Out.WriteLine("DocDefined found: " + this.m_definition.Name);
						//Console.Out.WriteLine("doing nothing with it.");
						
						//DocDefined docDefined = (DocDefined)this.m_definition;
						//string text = this.Indent(this.FormatDefined(docDefined, this.m_map, null), 1);
						//writer.WriteLine(text);
						// do nothing - don't make a separate type in Java, as that would require extra heap allocation 
					}
					else if (this.m_definition is DocSelect)
					{
						DocSelect docSelect = (DocSelect)this.m_definition;
						string text = this.FormatSelect(docSelect, this.m_map, null);
						writer.Write(text);
					}
					else if (this.m_definition is DocEnumeration)
					{
						DocEnumeration docEnumeration = (DocEnumeration)this.m_definition;
						string text = this.FormatEnumeration(docEnumeration, this.m_map, null);
						writer.WriteLine(text);
					}
					else if (this.m_definition is DocEntity)
					{
						DocEntity docEntity = (DocEntity)this.m_definition;
						string text = this.FormatEntity(docEntity, this.m_map, null);
						writer.WriteLine(text);
					}
				}
				else
				{

#if false // TBD
                    writer.WriteLine("package buildingsmart.ifc.properties");
                    writer.WriteLine("{");
                    writer.WriteLine();

                    Dictionary<string, string[]> mapEnums = new Dictionary<string, string[]>();

                    foreach (DocSection docSection in this.m_project.Sections)
                    {
                        foreach (DocSchema docSchema in docSection.Schemas)
                        {
                            foreach (DocPropertySet docPset in docSchema.PropertySets)
                            {
                                writer.WriteLine("    /// <summary>");
                                writer.WriteLine("    /// " + docPset.Documentation.Replace('\r', ' ').Replace('\n', ' '));
                                writer.WriteLine("    /// </summary>");

                                writer.WriteLine("    public class " + docPset.Name + " : Pset");
                                writer.WriteLine("    {");

                                foreach (DocProperty docProperty in docPset.Properties)
                                {
                                    writer.WriteLine("        /// <summary>");
                                    writer.WriteLine("        /// " + docProperty.Documentation.Replace('\r', ' ').Replace('\n', ' '));
                                    writer.WriteLine("        /// </summary>");

                                    switch (docProperty.PropertyType)
                                    {
                                        case DocPropertyTemplateTypeEnum.P_SINGLEVALUE:
                                            writer.WriteLine("        public " + docProperty.PrimaryDataType + " " + docProperty.Name + " { get { return this.GetValue<" + docProperty.PrimaryDataType + ">(\"" + docProperty.Name + "\"); } set { this.SetValue<" + docProperty.PrimaryDataType + ">(\"" + docProperty.Name + "\", value); } }");
                                            break;

                                        case DocPropertyTemplateTypeEnum.P_ENUMERATEDVALUE:
                                            // record enum for later
                                            {
                                                string[] parts = docProperty.SecondaryDataType.Split(':');
                                                if (parts.Length == 2)
                                                {
                                                    string typename = parts[0];
                                                    if (!mapEnums.ContainsKey(typename))
                                                    {
                                                        string[] enums = parts[1].Split(',');
                                                        mapEnums.Add(typename, enums);

                                                        writer.WriteLine("        public " + typename + " " + docProperty.Name + " { get { return this.GetValue<" + typename + ">(\"" + docProperty.Name + "\"); } set { this.SetValue<" + typename + ">(\"" + docProperty.Name + "\", value); } }");
                                                    }
                                                }
                                            }
                                            break;

                                        case DocPropertyTemplateTypeEnum.P_BOUNDEDVALUE:
                                            writer.WriteLine("        public PBound<" + docProperty.PrimaryDataType + "> " + docProperty.Name + " { get { return this.GetBound<" + docProperty.PrimaryDataType + ">(\"" + docProperty.Name + "\"); } }");
                                            break;

                                        case DocPropertyTemplateTypeEnum.P_LISTVALUE:
                                            break;

                                        case DocPropertyTemplateTypeEnum.P_TABLEVALUE:
                                            writer.WriteLine("        public PTable<" + docProperty.PrimaryDataType + ", " + docProperty.SecondaryDataType + "> " + docProperty.Name + " { get { return this.GetTable<" + docProperty.PrimaryDataType + ", " + docProperty.SecondaryDataType + ">(\"" + docProperty.Name + "\"); } }");
                                            break;

                                        case DocPropertyTemplateTypeEnum.P_REFERENCEVALUE:
                                            if (docProperty.PrimaryDataType.Equals("IfcTimeSeries"))
                                            {
                                                string datatype = docProperty.SecondaryDataType;
                                                if (String.IsNullOrEmpty(datatype))
                                                {
                                                    datatype = "IfcReal";
                                                }
                                                writer.WriteLine("        public PTimeSeries<" + datatype + "> " + docProperty.Name + " { get { return this.GetTimeSeries<" + datatype + ">(\"" + docProperty.Name + "\"); } }");
                                            }
                                            // ... TBD
                                            break;

                                        case DocPropertyTemplateTypeEnum.COMPLEX:
                                            //... TBD
                                            break;
                                    }
                                }

                                writer.WriteLine("    }");
                                writer.WriteLine();
                            }
                        }
                    }

                    // enums
                    foreach (string strEnum in mapEnums.Keys)
                    {
                        string[] enums = mapEnums[strEnum];

                        writer.WriteLine("    /// <summary>");
                        writer.WriteLine("    /// </summary>");
                        writer.WriteLine("    public enum " + strEnum);
                        writer.WriteLine("    {");

                        int counter = 0;
                        foreach (string val in enums)
                        {
                            int num = 0;
                            string id = val.ToUpper().Trim('.').Replace('-', '_');
                            switch (id)
                            {
                                case "OTHER":
                                    num = -1;
                                    break;

                                case "NOTKNOWN":
                                    num = -2;
                                    break;

                                case "UNSET":
                                    num = 0;
                                    break;

                                default:
                                    counter++;
                                    num = counter;
                                    break;
                            }

                            if (id[0] >= '0' && id[0] <= '9')
                            {
                                id = "_" + id; // avoid numbers
                            }

                            writer.WriteLine("        /// <summary></summary>");
                            writer.WriteLine("        " + id + " = " + num + ",");
                        }

                        writer.WriteLine("    }");
                        writer.WriteLine();
                    }

                    writer.WriteLine("}");
#endif
				}
			}
		}

		

		

		private void WriteImports(StreamWriter writer)
		{
			//TODO: check for completeness!!
			writer.WriteLine("import java.util.ArrayList;");
			writer.WriteLine("import java.util.HashMap;");
			writer.WriteLine("import java.util.Map;");
			writer.WriteLine("import java.util.HashSet;");
			writer.WriteLine("import java.util.LinkedList;");
			writer.WriteLine("import java.util.List;");
			writer.WriteLine("import java.util.Set;");
			
			//NOTE PIETER: not included the following method, as I have no clue what it is
			//WriteIncludes(writer);
		}

		private String FindBase(String basedef)
		{
			String based = "";

			//TODO: find schema of basedef string

			//DocDefinition d = new DocDefinition();

			//if (docDef is DocEnumeration)
			//	docSchema = this.m_project.GetSchemaOfDefinition((DocEnumeration)docDef);
			//if (docDef is DocSelect)
			//	docSchema = this.m_project.GetSchemaOfDefinition((DocSelect)docDef);
			//if (docDef is DocDefined)
			//	docSchema = this.m_project.GetSchemaOfDefinition((DocDefined)docDef);
			//if (docDef is DocEntity)
			//	docSchema = this.m_project.GetSchemaOfDefinition((DocEntity)docDef);

			//if (docSchema != null && docSchema != entitySchema)
			//	deftype = "com.buildingsmart.tech.ifc." + docSchema.Name + "." + deftype;

			return based;
		}

		public string FormatEntity(DocEntity docEntity, Dictionary<string, DocObject> map, Dictionary<DocObject, bool> included)
		{
			DocSchema entitySchema = m_project.GetSchemaOfDefinition(docEntity);
			StringBuilder sb = new StringBuilder();

			string basedef = docEntity.BaseDefinition;
			if (String.IsNullOrEmpty(basedef))
				sb.AppendLine("public class " + docEntity.Name);
			else
			{
				basedef = FindBase(basedef);

				sb.AppendLine("public class " + docEntity.Name + " extends " + basedef);



			}
			
			sb.AppendLine("{");

			// fields
			foreach (DocAttribute docAttribute in docEntity.Attributes)
			{
				DocSchema docSchema = null;
				string deftype = docAttribute.DefinedType;

				// if defined type, use raw type (avoiding extra memory allocation)
				DocObject docDef = null;
				if (deftype != null)
				{
					map.TryGetValue(deftype, out docDef);
				}

				if (docDef is DocDefined)
				{
					deftype = ((DocDefined)docDef).DefinedType;

					switch (deftype)
					{
						case "STRING":
							deftype = "String";
							break;

						case "INTEGER":
							deftype = "int";
							break;

						case "REAL":
							deftype = "double";
							break;

						case "BOOLEAN":
							deftype = "boolean";
							break;

						case "LOGICAL":
							deftype = "int";
							break;

						case "BINARY":
							deftype = "byte[]";
							break;
					}
				}
				else
				{
					if (docDef is DocEnumeration)
						docSchema = this.m_project.GetSchemaOfDefinition((DocEnumeration)docDef);
					if (docDef is DocSelect)
						docSchema = this.m_project.GetSchemaOfDefinition((DocSelect)docDef);
					if (docDef is DocDefined)
						docSchema = this.m_project.GetSchemaOfDefinition((DocDefined)docDef);
					if (docDef is DocEntity)
						docSchema = this.m_project.GetSchemaOfDefinition((DocEntity)docDef);

					if (docSchema != null && docSchema != entitySchema)
						deftype = "com.buildingsmart.tech.ifc." + docSchema.Name + "." + deftype;
				}

				switch (docAttribute.GetAggregation())
				{
					case DocAggregationEnum.SET:
						sb.AppendLine("\tprivate " + deftype + "[] " + docAttribute.Name + ";");
						break;

					case DocAggregationEnum.LIST:
						sb.AppendLine("\tprivate " + deftype + "[] " + docAttribute.Name + ";");
						break;

					default:
						sb.AppendLine("\tprivate " + deftype + " " + docAttribute.Name + ";");
						break;
				}
			}

			sb.AppendLine("}");
			return sb.ToString();
		}

		public string FormatEnumeration(DocEnumeration docEnumeration, Dictionary<string, DocObject> map, Dictionary<DocObject, bool> included)
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("public enum " + docEnumeration.Name);
			sb.AppendLine("{");
			foreach (DocConstant docConstant in docEnumeration.Constants)
			{
				sb.AppendLine("\t" + docConstant.Name + ",");
			}
			sb.AppendLine("}");
			return sb.ToString();
		}

		public string FormatSelect(DocSelect docSelect, Dictionary<string, DocObject> map, Dictionary<DocObject, bool> included)
		{
			StringBuilder sb = new StringBuilder();
			//sb.AppendLine("[Guid(\"" + docSelect.Uuid.ToString() + "\")]");
			sb.Append("public interface "+ docSelect.Name);

			FindSelectInheritance(sb, docSelect, map, included, false);

			sb.AppendLine(" {");
			sb.AppendLine();
			sb.AppendLine("}");
			return sb.ToString();
		}

		public string FormatDefined(DocDefined docDefined, Dictionary<string, DocObject> map, Dictionary<DocObject, bool> included)
		{
			// nothing -- java does not support structures
			return "/* " + docDefined.Name + " : " + docDefined.DefinedType + " (Java does not support structures, so usage of defined types are inline for efficiency.) */\r\n";
		}


		//UNSURE WHAT THESE ARE FOR:
		public string FormatDefinitions(DocProject docProject, DocPublication docPublication, Dictionary<string, DocObject> map, Dictionary<DocObject, bool> included)
		{
			StringBuilder sb = new StringBuilder();
			foreach (DocSection docSection in docProject.Sections)
			{
				foreach (DocSchema docSchema in docSection.Schemas)
				{
					foreach (DocType docType in docSchema.Types)
					{
						bool use = true;
						if (included != null)
						{
							use = false;
							included.TryGetValue(docType, out use);
						}

						if (use)
						{
							if (docType is DocDefined)
							{
								DocDefined docDefined = (DocDefined)docType;
								string text = this.Indent(this.FormatDefined(docDefined, map, included), 1);
								sb.AppendLine(text);
							}
							else if (docType is DocSelect)
							{
								DocSelect docSelect = (DocSelect)docType;
								string text = this.Indent(this.FormatSelect(docSelect, map, included), 1);
								sb.AppendLine(text);
							}
							else if (docType is DocEnumeration)
							{
								DocEnumeration docEnumeration = (DocEnumeration)docType;
								string text = this.Indent(this.FormatEnumeration(docEnumeration, map, included), 1);
								sb.AppendLine(text);
							}
							sb.AppendLine();
						}
					}

					foreach (DocEntity docEntity in docSchema.Entities)
					{
						bool use = true;
						if (included != null)
						{
							use = false;
							included.TryGetValue(docEntity, out use);
						}
						if (use)
						{
							string text = this.Indent(this.FormatEntity(docEntity, map, included), 1);
							sb.AppendLine(text);
							sb.AppendLine();
						}
					}
				}

			}

			return sb.ToString();
		}

		public string FormatData(DocPublication docPublication, DocExchangeDefinition docExchange, Dictionary<string, DocObject> map, Dictionary<long, SEntity> instances)
		{
			System.IO.MemoryStream stream = new System.IO.MemoryStream();
			if (instances.Count > 0)
			{
				SEntity rootproject = null;
				foreach (SEntity ent in instances.Values)
				{
					if (ent.GetType().Name.Equals("IfcProject"))
					{
						rootproject = ent;
						break;
					}
				}

				if (rootproject != null)
				{
					Type type = rootproject.GetType();

					DataContractJsonSerializer contract = new DataContractJsonSerializer(type);

					try
					{
						contract.WriteObject(stream, rootproject);
					}
					catch (Exception xx)
					{
						//...
						xx.ToString();
					}
				}
			}

			stream.Position = 0;
			System.IO.TextReader reader = new System.IO.StreamReader(stream);
			string content = reader.ReadToEnd();
			return content;
		}


		//CALLED BY THE MAIN FORMAT METHODS:
		/// <summary>
		/// Makes sure that inheritance of interfaces is collected and serialised into the generated code
		/// </summary>
		/// <param name="sb"></param>
		/// <param name="docEntity"></param>
		/// <param name="map"></param>
		/// <param name="included"></param>
		/// <param name="hasentry">Whether entries already listed; if not, then colon is added</param>
		private void FindSelectInheritance(StringBuilder sb, DocDefinition docEntity, Dictionary<string, DocObject> map, Dictionary<DocObject, bool> included, bool hasentry)
		{
			SortedList<string, DocSelect> listSelects = new SortedList<string, DocSelect>();
			foreach (DocObject obj in map.Values)
			{
				if (obj is DocSelect)
				{
					DocSelect docSelect = (DocSelect)obj;
					foreach (DocSelectItem docItem in docSelect.Selects)
					{
						if (docItem.Name != null && docItem.Name.Equals(docEntity.Name) && !listSelects.ContainsKey(docSelect.Name))
						{
							// found it; add it
							listSelects.Add(docSelect.Name, docSelect);
						}
					}
				}
			}
			DocSchema entitySchema = m_project.GetSchemaOfDefinition(docEntity);

			foreach (DocSelect docSelect in listSelects.Values)
			{
				if (docSelect == listSelects.Values[0] && !hasentry)
				{
					sb.Append(" extends");
				}
				else
				{
					sb.Append(",");
				}

				// fully qualify reference inline (rather than in usings) to differentiate C# select implementation from explicit schema references in data model
				DocSchema docSchema = this.m_project.GetSchemaOfDefinition(docSelect);
				sb.Append(" ");
				if (docSchema != entitySchema)
				{
					sb.Append("com.buildingsmart.tech.ifc.");
					sb.Append(docSchema.Name);
					sb.Append(".");
				}
				sb.Append(docSelect.Name);
			}
		}

		

		//+++++++++++++++
		//HELPER METHODS
		//+++++++++++++++

		public void Dispose()
		{
		}

		/// <summary>
		/// Inserts tabs for each line
		/// </summary>
		/// <param name="text"></param>
		/// <param name="level"></param>
		/// <returns></returns>
		private string Indent(string text, int level)
		{
			for (int i = 0; i < level; i++)
			{
				text = "\t" + text;
				text = text.Replace("\r\n", "\r\n\t");
			}

			return text;
		}


		//+++++++++++++++
		//ACCESSORS
		//+++++++++++++++

		/// <summary>
		/// Optional definition to save, or null for all definitions in project.
		/// </summary>
		private DocDefinition GetDefinition(string def)
		{
			foreach (DocSection docSection in this.m_project.Sections)
			{
				foreach (DocSchema docSchema in docSection.Schemas)
				{
					foreach (DocType docType in docSchema.Types)
					{
						if (docType.Name.Equals(def))
						{
							return docType;
						}
					}

					foreach (DocEntity docType in docSchema.Entities)
					{
						if (docType.Name.Equals(def))
						{
							return docType;
						}
					}
				}
			}

			return null;
		}

		public DocDefinition Definition
		{
			get
			{
				return this.m_definition;
			}
			set
			{
				this.m_definition = value;
			}
		}

		public DocProject Instance
		{
			get
			{
				return this.m_project;
			}
			set
			{
				this.m_project = value;
			}
		}

		public DocSchema Schema
        {
            get
            {
                return this.m_schema;
            }
            set
            {
                this.m_schema = value;
            }
        }

        public Dictionary<string, DocObject> Map
        {
            get
            {
                return this.m_map;
            }
            set
            {
                this.m_map = value;
            }
        }
    }
}
