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
        private static string ifcPath = "com.buildingsmart.tech.ifc";
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

					foreach (DocEntity docEntity in docSchema.Entities)
					{
						string file = docSchema.Name + @"\" + docEntity.Name + ".java";
						using (FormatJAV format = new FormatJAV(path + @"\" + file))
						{
							format.Instance = project;
							format.Schema = docSchema;
							format.Definition = docEntity;
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

			using (System.IO.StreamWriter writer = new System.IO.StreamWriter(this.m_filename))
			{
				writer.WriteLine("// This file was automatically generated from IFCDOC at https://technical.buildingsmart.org/.");
				writer.WriteLine("// IFC content is copyright (C) 1996-2013 BuildingSMART International Ltd.");
				writer.WriteLine("// Author: Pieter Pauwels, Eindhoven University of Technology");
				writer.WriteLine();
				
				if (this.m_definition != null)
				{
					writer.Write("package " + ifcPath);
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
						DocDefined docDefined = (DocDefined)this.m_definition;
						string text = this.FormatDefined(docDefined, this.m_map, null);
						writer.WriteLine(text);
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
                    //TODO: potentially include property sets here - see FormatCSC for example
				}
			}
		}
        
		private void WriteImports(StreamWriter writer)
		{
			writer.WriteLine("import java.util.ArrayList;");
            writer.WriteLine("import java.util.Arrays;");
            writer.WriteLine("import java.util.HashMap;");
			writer.WriteLine("import java.util.Map;");
			writer.WriteLine("import java.util.HashSet;");
			writer.WriteLine("import java.util.LinkedList;");
			writer.WriteLine("import java.util.List;");
			writer.WriteLine("import java.util.Set;");

            writer.WriteLine("import com.fasterxml.jackson.annotation.JsonIgnore;");
            writer.WriteLine("import com.fasterxml.jackson.dataformat.xml.annotation.JacksonXmlProperty;"); 

             writer.WriteLine("import com.buildingsmart.tech.annotations.*;");
		}

		public string FormatEntity(DocEntity docEntity, Dictionary<string, DocObject> map, Dictionary<DocObject, bool> included)
		{
			StringBuilder sb = new StringBuilder();

			sb.AppendLine("@Guid(\"" + docEntity.Uuid.ToString() + "\")");
            sb.AppendLine("@JsonIgnoreProperties(ignoreUnknown=true)");

			sb.Append("public ");
			if (docEntity.IsAbstract)
			{
				sb.Append("abstract ");
			}
			sb.Append("class " + docEntity.Name);
            
            if (!String.IsNullOrEmpty(docEntity.BaseDefinition))
			{
				sb.Append(" extends");

                // fully qualify reference inline (rather than in usings) to differentiate C# select implementation from explicit schema references in data model
                DocSchema entitySchema = this.m_project.GetSchemaOfDefinition(docEntity);
                DocSchema docSchema = this.m_project.GetSchemaOfDefinition(this.m_project.GetDefinition(docEntity.BaseDefinition) as DocEntity);

                sb.Append(" ");
                if (docSchema != entitySchema)
                {
                    sb.Append(ifcPath + ".");
                    sb.Append(docSchema.Name);
                    sb.Append(".");
                }
                sb.Append(docEntity.BaseDefinition);
            }

			// implement any selects
			FindImplements(sb, docEntity, map, included);

            // class body
			sb.AppendLine();
			sb.AppendLine("{");

			// fields
			int order = 0;
			StringBuilder sbFields = new StringBuilder();
			StringBuilder sbAccessors = new StringBuilder();
			StringBuilder sbConstructor = new StringBuilder(); // constructor parameters
			StringBuilder sbAssignment = new StringBuilder(); // constructor assignment of fields
			StringBuilder sbElemConstructor = new StringBuilder(); // default constructor

			// attributes / fields
			foreach (DocAttribute docAttribute in docEntity.Attributes)
			{
                // find domain of type so it can be imported
				string type = FormatIdentifier(docAttribute.DefinedType);
                DocSchema docSchema = this.m_project.GetSchemaOfDefinition(this.m_project.GetDefinition(type) as DocType);
                string domain = "";
                if (docSchema != null)
                {
                    domain = ifcPath + "." + docSchema.Name + ".";
                }
                else
                {
                    docSchema = this.m_project.GetSchemaOfDefinition(this.m_project.GetDefinition(type) as DocEntity);
                    if (docSchema != null)
                    {
                        domain = ifcPath + "." + docSchema.Name + ".";
                    }
                }

                //replace Int64 with java long type
                if (string.Compare(type, "Int64") == 0)
                    type = "long";

                DocObject docRef = null;
				if (docAttribute.DefinedType != null)
				{
					map.TryGetValue(docAttribute.DefinedType, out docRef);
				}

				if (docAttribute.Derived != null)
				{
					// export as "new" property that hides base
					switch (docAttribute.GetAggregation())
					{
						case DocAggregationEnum.SET:
                            //Used to be ISet in C#
                            sbAccessors.AppendLine("\tpublic Set<" + domain + type + "> get" + docAttribute.Name + "() {");
							sbAccessors.AppendLine("\t\treturn null;");
							sbAccessors.AppendLine("\t}");
							break;

						case DocAggregationEnum.LIST:
                            //Used to be IList in C#
                            sbAccessors.AppendLine("\tpublic List<" + domain + type + "> get" + docAttribute.Name + "() {");
							sbAccessors.AppendLine("\t\treturn null;");
							sbAccessors.AppendLine("\t}");
							break;

						default:
							if (docRef is DocDefined)
							{
								sbAccessors.AppendLine("\tpublic " + domain + type + " get" + docAttribute.Name + "() {");
								sbAccessors.AppendLine("\t\treturn new " + domain + type + "();");
								sbAccessors.AppendLine("}");
							}
							else
							{
								if (string.Compare(type, "long") == 0 || string.Compare(type, "Int64") == 0 || string.Compare(type, "Double") == 0)
								{
									sbAccessors.AppendLine("\tpublic " + domain + type + " get" + docAttribute.Name + "() {");
									sbAccessors.AppendLine("\t\treturn 0;");
									sbAccessors.AppendLine("}");
								}
								else
								{
									sbAccessors.AppendLine("\tpublic " + domain + type + " get" + docAttribute.Name + "() {");
									sbAccessors.AppendLine("\t\treturn null;");
									sbAccessors.AppendLine("\t}");
								}
							}
							break;
					}
					sbAccessors.AppendLine();
				}
				else
				{
					bool inscope = false;

					if (included != null)
					{
						included.TryGetValue(docAttribute, out inscope);
					}
					else
					{
						inscope = true;
					}

					if (docAttribute.Inverse == null)
					{
						// System.Runtime.Serialization -- used by Windows Communication Foundation formatters to indicate data serialization inclusion and order
						//sbFields.AppendLine("\t[DataMember(Order = " + order + ")] ");
						order++;
					}
					else if (inscope)
					{
						// System.ComponentModel.DataAnnotations for capturing inverse properties -- EntityFramework navigation properties
						//sbFields.AppendLine("\t[InverseProperty(\"" + docAttribute.Inverse + "\")] ");
					}

					// xml configuration
					if (docAttribute.AggregationAttribute == null && (docRef is DocDefined || docRef is DocEnumeration))
					{
						sbFields.AppendLine("\t@JacksonXmlProperty(isAttribute=true)");
					}
					else
					{
						switch (docAttribute.XsdFormat)
						{
							case DocXsdFormatEnum.Attribute: // e.g. IfcRoot.OwnerHistory -- only attribute has tag; element data type does not
															 //sbFields.AppendLine("\t[XmlElement]");
								break;

							case DocXsdFormatEnum.Element: // attribute has tag and referenced object instance(s) have tags
														   //sbFields.AppendLine("\t[XmlElement(\"" + docAttribute.DefinedType + "\")]"); // same as .Element, but skip attribute name (NOT XmlAttribute)
								break;

							case DocXsdFormatEnum.Hidden:
								sbFields.AppendLine("\t@JsonIgnore");
								//sbFields.AppendLine("\t[XmlIgnore]");
								break;
						}
					}

					if (docAttribute.Inverse == null || inscope)
					{
						// documentation
						if (!String.IsNullOrEmpty(docAttribute.Documentation))
						{
							sbFields.Append("\t@Description(\""); // keep descriptions on one line
							string encodedoc = docAttribute.Documentation.Replace("\\", "\\\\"); // backslashes used for notes that relate to EXPRESS syntax
							encodedoc = encodedoc.Replace("\"", "\\\""); // escape any quotes
							encodedoc = encodedoc.Replace("\r", " "); // remove any return characters
							encodedoc = encodedoc.Replace("\n", " "); // remove any return characters
							sbFields.Append(encodedoc);

							//prov.GenerateCodeFromExpression(new CodePrimitiveExpression(docAttribute.Documentation), new StringWriter(sbFields), null); //... do this directly to avoid line splitting...
							sbFields.AppendLine("\")");
						}

						if (docAttribute.Inverse == null && !docAttribute.IsOptional)
						{
							sbFields.AppendLine("\t@Required()");
						}

						int lower = 0;
						if (docAttribute.AggregationLower != null && Int32.TryParse(docAttribute.AggregationLower, out lower) && lower != 0)
						{
							sbFields.AppendLine("\t@MinLength(" + lower + ")");
						}

						int upper = 0;
						if (docAttribute.AggregationUpper != null && Int32.TryParse(docAttribute.AggregationUpper, out upper) && upper != 0)
						{
							sbFields.AppendLine("\t@MaxLength(" + upper + ")");
						}

						sbFields.AppendLine("\t@Guid(\"" + docAttribute.Uuid.ToString() + "\")");
                        
                        switch (docAttribute.GetAggregation())
						{
							case DocAggregationEnum.SET:
								if (docAttribute.Inverse == null && !docAttribute.IsOptional)
								{
                                    sbAssignment.AppendLine("\t\tthis." + toLowerCamelCase(docAttribute.Name) + " = new HashSet<>(Arrays.asList(" + formatAttributeName(docAttribute) + "));");
                                }
                                sbFields.AppendLine("\tprivate Set<" + domain + type + "> " + toLowerCamelCase(docAttribute.Name) + " = new HashSet<" + domain + type + ">();");
								sbAccessors.AppendLine("\tpublic Set<" + domain + type + "> get" + docAttribute.Name + "() {");
								sbAccessors.AppendLine("\t\treturn this." + toLowerCamelCase(docAttribute.Name) + ";");
								sbAccessors.AppendLine("\t}");
								break;

							case DocAggregationEnum.LIST:
                                if (docAttribute.Inverse == null && !docAttribute.IsOptional)
                                {
                                    sbAssignment.AppendLine("\t\tthis." + toLowerCamelCase(docAttribute.Name) + " = new ArrayList<>(Arrays.asList(" + formatAttributeName(docAttribute) + "));");
								}
								sbFields.AppendLine("\tprivate List<" + domain + type + "> " + toLowerCamelCase(docAttribute.Name) + " = new ArrayList<" + domain + type + ">();");
                                sbAccessors.AppendLine("\tpublic List<" + domain + type + "> get" + docAttribute.Name + "() {");
								sbAccessors.AppendLine("\t\treturn this." + toLowerCamelCase(docAttribute.Name) + ";");
								sbAccessors.AppendLine("\t}");
								break;

							case DocAggregationEnum.ARRAY:
								if (docAttribute.Inverse == null && !docAttribute.IsOptional)
								{
									sbAssignment.AppendLine("\t\tthis." + toLowerCamelCase(docAttribute.Name) + " = " + formatAttributeName(docAttribute) + ";");
								}
								sbFields.AppendLine("\tprivate " + domain + type + "[] " + toLowerCamelCase(docAttribute.Name) + ";");
								sbAccessors.AppendLine("\tpublic " + domain + type + "[] get" + docAttribute.Name + "() {");
								sbAccessors.AppendLine("\t\treturn this." + toLowerCamelCase(docAttribute.Name) + ";");
								sbAccessors.AppendLine("\t}");
								break;

							default:
								if (docAttribute.Inverse == null && !docAttribute.IsOptional)
								{
									sbAssignment.AppendLine("\t\tthis." + toLowerCamelCase(docAttribute.Name) + " = " + formatAttributeName(docAttribute) + ";");
								}
								sbFields.AppendLine("\tprivate " + domain + type + " " + toLowerCamelCase(docAttribute.Name) + ";");
								sbAccessors.AppendLine("\tpublic " + domain + type + " get" + docAttribute.Name + "() {");
								sbAccessors.AppendLine("\t\treturn this." + toLowerCamelCase(docAttribute.Name) + ";");
								sbAccessors.AppendLine("\t}");
								sbAccessors.AppendLine();
								sbAccessors.AppendLine("\tpublic void set" + docAttribute.Name + "(" + domain + type + " " + toLowerCamelCase(docAttribute.Name) + ") {");
								sbAccessors.AppendLine("\t\tthis." + toLowerCamelCase(docAttribute.Name) + " = " + toLowerCamelCase(docAttribute.Name) + ";");
								sbAccessors.AppendLine("\t}");
								break;
						}
						
						sbFields.AppendLine();
						sbAccessors.AppendLine();
					}
				}
			}

			sb.Append(sbFields.ToString());
			sb.AppendLine();

			// constructors

			// parameters for base constructor
			List<DocAttribute> listAttr = new List<DocAttribute>();
			BuildAttributeList(docEntity, map, listAttr);

			List<DocAttribute> listBase = new List<DocAttribute>();
			if (docEntity.BaseDefinition != null)
			{
				DocEntity docBase = (DocEntity)map[docEntity.BaseDefinition];
				BuildAttributeList(docBase, map, listBase);
			}

            // If there are attributes listed in the constructor, then we need to add a base constructor that is needed for (de-)serialising XML and JSON data 
            if(listAttr.Count != 0)
            {
                // default constructor
                sb.AppendLine("\tpublic " + docEntity.Name + "()");
                sb.AppendLine("\t{");
                sb.AppendLine("\t}");
                sb.AppendLine();
            }

            // helper constructor -- expand fixed lists into separate parameters -- e.g. IfcCartesianPoint(IfcLengthMeasure, IfcLengthMeasure, IfcLengthMeasure)
            sb.Append("\tpublic " + docEntity.Name + "(");
			foreach (DocAttribute docAttr in listAttr)
			{
				if (docAttr != listAttr[0])
				{
					sb.Append(", ");
				}
                
                // find domain of type so it can be imported
                string type = FormatIdentifier(docAttr.DefinedType);
                DocSchema docSchema = this.m_project.GetSchemaOfDefinition(this.m_project.GetDefinition(type) as DocType);
                string domain = "";
                if (docSchema != null)
                {
                    domain = ifcPath + "." + docSchema.Name + ".";
                }
                else
                {
                    docSchema = this.m_project.GetSchemaOfDefinition(this.m_project.GetDefinition(type) as DocEntity);
                    if (docSchema != null)
                    {
                        domain = ifcPath + "." + docSchema.Name + ".";
                    }
                }

                //replace Int64 with java long type
                if (string.Compare(type, "Int64") == 0)
                    type = "long";

                sb.Append(domain + type);

				DocObject docRef = null;
				if (docAttr.DefinedType != null)
				{
					map.TryGetValue(docAttr.DefinedType, out docRef);
				}

				if (docAttr.GetAggregation() != DocAggregationEnum.NONE)
				{
					sb.Append("[]");
				}

				sb.Append(" " + formatAttributeName(docAttr));

			}
			sb.AppendLine(sbConstructor.ToString() + ")");
			sb.AppendLine("\t{");

            //call upper constructor(s)
            if (listBase.Count > 0)
            {
                sb.Append("\t\tsuper(");
                foreach (DocAttribute docAttr in listBase)
                {
                    if (docAttr != listBase[0])
                    {
                        sb.Append(", ");
                    }

                    sb.Append(formatAttributeName(docAttr));
                }

                sb.AppendLine(");");
            }

            sb.Append(sbAssignment.ToString());
			sb.AppendLine("\t}");
			sb.AppendLine();
			
			// if only a single list attribute, then expand x, y, z (include IfcCartesianPoint, NOT IfcSurfaceReinforcementArea)
			if (sbElemConstructor.Length > 0 && order == 1 && listBase.Count == 0)
			{
				sb.AppendLine(sbElemConstructor.ToString());
			}

			sb.Append(sbAccessors.ToString());
			sb.AppendLine();
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
            sb.AppendLine("import com.buildingsmart.tech.annotations.Guid;");
            sb.AppendLine();

            sb.AppendLine("@Guid(\"" + docSelect.Uuid.ToString() + "\")");
            sb.Append("public interface "+ docSelect.Name);

            FindExtends(sb, docSelect, map, included);

			sb.AppendLine(" {");
			sb.AppendLine();
			sb.AppendLine("}");
			return sb.ToString();
		}

		public string FormatDefined(DocDefined docDefined, Dictionary<string, DocObject> map, Dictionary<DocObject, bool> included)
		{
            // find domain of type so it can be imported
            string type = FormatIdentifier(docDefined.DefinedType);
            DocSchema docSchema = this.m_project.GetSchemaOfDefinition(this.m_project.GetDefinition(type) as DocType);
            string domain = "";
            if (docSchema != null)
            {
                domain = ifcPath + "." + docSchema.Name + ".";
            }
            else
            {
                //The defined type is a base type: Double, String, Int64, Byte[], Boolean, Decimal

                //replace datatypes with corresponding datatypes
                if (string.Compare(type, "Int64") == 0)
                {
                    domain = "";
                    type = "long";
                }
                if (string.Compare(type, "Decimal") == 0)
                {
                    domain = "";
                    type = "double";
                }
                if (string.Compare(type, "Byte[]") == 0)
                {
                    domain = "";
                    type = "byte[]";
                }
            }            

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("@Guid(\"" + docDefined.Uuid.ToString() + "\")");
            sb.AppendLine("@JsonIgnoreProperties(ignoreUnknown=true)");
            sb.Append("public class " + docDefined.Name);

            FindExtends(sb, docDefined, map, included);

            sb.AppendLine(" {");

            if (docDefined.Length != 0)
            {
                sb.AppendLine("\t@MaxLength(" + docDefined.Length + ")");
            }
            sb.AppendLine("\tpublic " + domain + type + " value;");
            sb.AppendLine();

            // empty constructor
            sb.AppendLine("\tpublic " + docDefined.Name + "() {");
            sb.AppendLine("\t}");
            sb.AppendLine();

            // direct constructor for all types
            sb.AppendLine("\tpublic " + docDefined.Name + "(" + domain + type + " value) {");
            sb.AppendLine("\t\tthis();");
            sb.AppendLine("\t\tthis.value = value;");
            sb.AppendLine("\t}");
            sb.AppendLine();

            //Accessors
            sb.AppendLine("\tpublic " + domain + type + " getValue() {");
            sb.AppendLine("\t\treturn this.value;");
            sb.AppendLine("\t}");
            sb.AppendLine();
            sb.AppendLine("\tpublic void setValue(" + domain + type + " value) {");
            sb.AppendLine("\t\tthis.value = value;");
            sb.AppendLine("\t}");

            sb.AppendLine("}");
            return sb.ToString();
        }

		private string formatAttributeName(DocAttribute docAttribute)
		{
			if (string.Compare(docAttribute.Name, "Operator", true) == 0)
				return "_operator";
			return Char.ToLowerInvariant(docAttribute.Name[0]) + docAttribute.Name.Substring(1);
		}

        private string toLowerCamelCase(string input)
        {
            return Char.ToLowerInvariant(input[0]) + input.Substring(1);
        }

		//CALLED BY THE MAIN FORMAT METHODS:

		/// <summary>
		/// Converts any native types into .NET types
		/// TODO: need to change this so that it matches for JAVA
		/// </summary>
		/// <param name="identifier"></param>
		/// <returns></returns>
		private static string FormatIdentifier(string identifier)
		{
			Type typeNative = GetNativeType(identifier);
			if (typeNative != null)
			{
				if (typeNative.IsGenericType && typeNative.GetGenericTypeDefinition() == typeof(Nullable<>))
				{
					return typeNative.GetGenericArguments()[0].Name;
				}

				return typeNative.Name;
			}

			return identifier;
		}

		private static void BuildAttributeList(DocEntity docEntity, Dictionary<string, DocObject> map, List<DocAttribute> listAttr)
		{
			// recurse upwards -- base first
			DocObject docBase = null;
			if (docEntity.BaseDefinition != null && map.TryGetValue(docEntity.BaseDefinition, out docBase) && docBase is DocEntity)
			{
				DocEntity docBaseEntity = (DocEntity)docBase;
				BuildAttributeList(docBaseEntity, map, listAttr);
			}

			foreach (DocAttribute docAttr in docEntity.Attributes)
			{
				if (docAttr.Inverse == null && docAttr.Derived == null && !docAttr.IsOptional)
				{
					listAttr.Add(docAttr);
				}
			}
		}

		/// <summary>
		/// Returns the native .NET type to use for a given EXPRESS type.
		/// </summary>
		/// <param name="expresstype"></param>
		/// <returns></returns>
		public static Type GetNativeType(string expresstype)
		{
			switch (expresstype)
			{
				case "STRING":
					return typeof(string);

				case "INTEGER":
					return typeof(long);

				case "REAL":
					return typeof(double);

				case "NUMBER":
					return typeof(decimal);

				case "LOGICAL":
					return typeof(bool?);

				case "BOOLEAN":
					return typeof(bool);

				case "BINARY":
				case "BINARY (32)":
					return typeof(byte[]);

			}

			return null;
		}

		/// <summary>
		/// Find all interfaces that the DocEntity implements and append them
		/// </summary>
		/// <param name="sb"></param>
		/// <param name="docEntity"></param>
		/// <param name="map"></param>
		/// <param name="included"></param>
		private void FindImplements(StringBuilder sb, DocEntity docEntity, Dictionary<string, DocObject> map, Dictionary<DocObject, bool> included)
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
							listSelects.Add(docSelect.Name, docSelect);
						}
					}
				}
			}
			DocSchema entitySchema = m_project.GetSchemaOfDefinition(docEntity);

			foreach (DocSelect docSelect in listSelects.Values)
			{
				if (docSelect == listSelects.Values[0])
				{
					sb.Append(" implements");
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
					sb.Append(ifcPath + ".");
					sb.Append(docSchema.Name);
					sb.Append(".");
				}
				sb.Append(docSelect.Name);
			}
		}
                
        /// <summary>
		/// Find all superinterfaces and add them through 'extends'
		/// </summary>
		/// <param name="sb"></param>
		/// <param name="docEntity"></param>
		/// <param name="map"></param>
		/// <param name="included"></param>
		private void FindExtends(StringBuilder sb, DocDefined docDefined, Dictionary<string, DocObject> map, Dictionary<DocObject, bool> included)
        {
            SortedList<string, DocSelect> listSelects = new SortedList<string, DocSelect>();
            foreach (DocObject obj in map.Values)
            {
                if (obj is DocSelect)
                {
                    DocSelect docSelectUpper = (DocSelect)obj;
                    foreach (DocSelectItem docItem in docSelectUpper.Selects)
                    {
                        if (docItem.Name != null && docItem.Name.Equals(docDefined.Name) && !listSelects.ContainsKey(docSelectUpper.Name))
                        {
                            listSelects.Add(docSelectUpper.Name, docSelectUpper);
                        }
                    }
                }
            }
            DocSchema entitySchema = m_project.GetSchemaOfDefinition(docDefined);

            foreach (DocSelect docSelectUpper in listSelects.Values)
            {
                if (docSelectUpper == listSelects.Values[0])
                {
                    sb.Append(" implements");
                }
                else
                {
                    sb.Append(",");
                }

                // fully qualify reference inline (rather than in usings) to differentiate C# select implementation from explicit schema references in data model
                DocSchema docSchema = this.m_project.GetSchemaOfDefinition(docSelectUpper);
                sb.Append(" ");
                if (docSchema != entitySchema)
                {
                    sb.Append(ifcPath + ".");
                    sb.Append(docSchema.Name);
                    sb.Append(".");
                }
                sb.Append(docSelectUpper.Name);
            }
        }

        /// <summary>
		/// Find all superinterfaces and add them through 'extends'
		/// </summary>
		/// <param name="sb"></param>
		/// <param name="docEntity"></param>
		/// <param name="map"></param>
		/// <param name="included"></param>
		private void FindExtends(StringBuilder sb, DocSelect docSelect, Dictionary<string, DocObject> map, Dictionary<DocObject, bool> included)
        {
            SortedList<string, DocSelect> listSelects = new SortedList<string, DocSelect>();
            foreach (DocObject obj in map.Values)
            {
                if (obj is DocSelect)
                {
                    DocSelect docSelectUpper = (DocSelect)obj;
                    foreach (DocSelectItem docItem in docSelectUpper.Selects)
                    {
                        if (docItem.Name != null && docItem.Name.Equals(docSelect.Name) && !listSelects.ContainsKey(docSelectUpper.Name))
                        {
                            listSelects.Add(docSelectUpper.Name, docSelectUpper);
                        }
                    }
                }
            }
            DocSchema entitySchema = m_project.GetSchemaOfDefinition(docSelect);

            foreach (DocSelect docSelectUpper in listSelects.Values)
            {
                if (docSelectUpper == listSelects.Values[0])
                {
                    sb.Append(" extends");
                }
                else
                {
                    sb.Append(",");
                }

                // fully qualify reference inline (rather than in usings) to differentiate C# select implementation from explicit schema references in data model
                DocSchema docSchema = this.m_project.GetSchemaOfDefinition(docSelectUpper);
                sb.Append(" ");
                if (docSchema != entitySchema)
                {
                    sb.Append(ifcPath + ".");
                    sb.Append(docSchema.Name);
                    sb.Append(".");
                }
                sb.Append(docSelectUpper.Name);
            }
        }

        /// <summary>
		/// Makes sure that inheritance of interfaces is collected and serialised into the generated code
		/// </summary>
		/// <param name="sb"></param>
		/// <param name="docEntity"></param>
		/// <param name="map"></param>
		/// <param name="included"></param>
		private void FindSelectInheritanceBackup(StringBuilder sb, DocDefinition docEntity, Dictionary<string, DocObject> map, Dictionary<DocObject, bool> included)
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
                            listSelects.Add(docSelect.Name, docSelect);
                        }
                    }
                }
            }
            DocSchema entitySchema = m_project.GetSchemaOfDefinition(docEntity);

            foreach (DocSelect docSelect in listSelects.Values)
            {
                if (docSelect == listSelects.Values[0])
                {
                    sb.Append(" implements");
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
                    sb.Append(ifcPath + ".");
                    sb.Append(docSchema.Name);
                    sb.Append(".");
                }
                sb.Append(docSelect.Name);
            }
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
