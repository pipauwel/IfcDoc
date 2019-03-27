using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using IfcDoc.Schema;
using IfcDoc.Schema.DOC;
using BuildingSmart.Serialization.Step;
using BuildingSmart.Serialization.Xml;


namespace IfcDoc
{
	public static class IfcDocUtils
	{
		public static DocProject LoadFile(string filePath)
		{
			List<object> instances = new List<object>();
			string ext = System.IO.Path.GetExtension(filePath).ToLower();
			string schema = "";
			DocProject project = null;
			switch (ext)
			{
				case ".ifcdoc":
					using (FileStream streamDoc = new FileStream(filePath, FileMode.Open, FileAccess.Read))
					{
						Dictionary<long, object> dictionaryInstances = null;
						StepSerializer formatDoc = new StepSerializer(typeof(DocProject), SchemaDOC.Types);
						project = (DocProject)formatDoc.ReadObject(streamDoc, out dictionaryInstances);
						instances.AddRange(dictionaryInstances.Values);
						schema = formatDoc.Schema;
					}
					break;
				case ".ifcdocxml":
					using (FileStream streamDoc = new FileStream(filePath, FileMode.Open, FileAccess.Read))
					{
						Dictionary<string, object> dictionaryInstances = null;
						XmlSerializer formatDoc = new XmlSerializer(typeof(DocProject));
						project = (DocProject)formatDoc.ReadObject(streamDoc, out dictionaryInstances);
						instances.AddRange(dictionaryInstances.Values);
					}
					break;
				default:
					MessageBox.Show("Unsupported file type " + ext, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
					break;
#if MDB
                    case ".mdb":
                        using (FormatMDB format = new FormatMDB(this.m_file, SchemaDOC.Types, this.m_instances))
                        {
                            format.Load();
                        }
                        break;
#endif
			}
			if (project == null)
				return null;
			

			List<SEntity> listDelete = new List<SEntity>();
			List<DocTemplateDefinition> listTemplate = new List<DocTemplateDefinition>();
			List<DocProperty> enumeratedPropertiesToRedefine = new List<DocProperty>();

			foreach (object o in instances)
			{
				if (o is DocSchema)
				{
					DocSchema docSchema = (DocSchema)o;

					// renumber page references
					foreach (DocPageTarget docTarget in docSchema.PageTargets)
					{
						if (docTarget.Definition != null) // fix it up -- NULL bug from older .ifcdoc files
						{
							int page = docSchema.GetDefinitionPageNumber(docTarget);
							int item = docSchema.GetPageTargetItemNumber(docTarget);
							docTarget.Name = page + "," + item + " " + docTarget.Definition.Name;

							foreach (DocPageSource docSource in docTarget.Sources)
							{
								docSource.Name = docTarget.Name;
							}
						}
					}
				}
				else if (o is DocExchangeDefinition)
				{
					// files before V4.9 had Description field; no longer needed so use regular Documentation field again.
					DocExchangeDefinition docexchange = (DocExchangeDefinition)o;
					if (docexchange._Description != null)
					{
						docexchange.Documentation = docexchange._Description;
						docexchange._Description = null;
					}
				}
				else if (o is DocTemplateDefinition)
				{
					// files before V5.0 had Description field; no longer needed so use regular Documentation field again.
					DocTemplateDefinition doctemplate = (DocTemplateDefinition)o;
					if (doctemplate._Description != null)
					{
						doctemplate.Documentation = doctemplate._Description;
						doctemplate._Description = null;
					}

					listTemplate.Add((DocTemplateDefinition)o);
				}
				else if (o is DocConceptRoot)
				{
					// V12.0: ensure template is defined
					DocConceptRoot docConcRoot = (DocConceptRoot)o;
					if (docConcRoot.ApplicableTemplate == null && docConcRoot.ApplicableEntity != null)
					{
						docConcRoot.ApplicableTemplate = new DocTemplateDefinition();
						docConcRoot.ApplicableTemplate.Type = docConcRoot.ApplicableEntity.Name;
					}
				}
				else if (o is DocTemplateUsage)
				{
					// V12.0: ensure template is defined
					DocTemplateUsage docUsage = (DocTemplateUsage)o;
					if (docUsage.Definition == null)
					{
						docUsage.Definition = new DocTemplateDefinition();
					}
				}

				// ensure all objects have valid guid
				DocObject docObject = o as DocObject;
				if (docObject != null)
				{
					if (docObject.Uuid == Guid.Empty)
					{
						docObject.Uuid = Guid.NewGuid();
					}
					if (!string.IsNullOrEmpty(docObject.Documentation))
						docObject.Documentation = docObject.Documentation.Trim();
					DocProperty docProperty = docObject as DocProperty;
					if (docProperty != null)
					{
						if (string.IsNullOrEmpty(docProperty.SecondaryDataType))
							docProperty.SecondaryDataType = null;
						else if (docProperty.PropertyType == DocPropertyTemplateTypeEnum.P_ENUMERATEDVALUE && docProperty.Enumeration == null)
							enumeratedPropertiesToRedefine.Add(docProperty);
					}
					else
					{
						DocChangeSet docChangeSet = docObject as DocChangeSet;
						if (docChangeSet != null)
							docChangeSet.ChangesEntities.RemoveAll(x => !isUnchanged(x));
					}
				}
			}

			if (project == null)
				return null;

			DocListings docListings = project.Listings;
			if (docListings == null)
			{
				docListings = project.Listings = new DocListings();
				foreach (DocSchema docSchema in project.Sections.SelectMany(x => x.Schemas))
					extractListings(docSchema, docListings);
			}
			foreach (DocProperty docProperty in enumeratedPropertiesToRedefine)
			{
				string[] fields = docProperty.SecondaryDataType.Split(":".ToCharArray());
				if (fields.Length == 2)
				{
					docProperty.SecondaryDataType = null;
					string name = fields[0];
					foreach (DocEnumeration docEnumeration in docListings.Enumerations)
					{
						if (string.Compare(name, docEnumeration.Name) == 0)
						{
							docProperty.Enumeration = docEnumeration;
							break;
						}
					}
					if (docProperty.Enumeration == null)
					{
						docProperty.Enumeration = new DocEnumeration() { Name = name };
						docListings.Enumerations.Add(docProperty.Enumeration = docProperty.Enumeration);
						foreach (string str in fields[1].Split(",".ToCharArray()))
						{
							DocConstant constant = new DocConstant() { Name = str.Trim() };
							docListings.Constants.Add(constant);
							docProperty.Enumeration.Constants.Add(constant);
						}
					}
				}
			}

			if (!string.IsNullOrEmpty(schema))
			{
				string[] fields = schema.Split("_".ToCharArray());
				int ver = 0;
				if (fields.Length > 1 && int.TryParse(fields[1], out ver) && ver < 12)
				{
					foreach (DocEntity entity in project.Listings.Entities)
					{
						entity.ClearDefaultMember();
					}
				}
			}

			foreach (DocModelView docModelView in project.ModelViews)
			{
				// sort alphabetically (V11.3+)
				docModelView.SortConceptRoots();
			}

			// upgrade to Publications (V9.6)
			if (project.Annotations.Count == 4)
			{
				project.Publications.Clear();

				DocAnnotation docCover = project.Annotations[0];
				DocAnnotation docContents = project.Annotations[1];
				DocAnnotation docForeword = project.Annotations[2];
				DocAnnotation docIntro = project.Annotations[3];

				DocPublication docPub = new DocPublication();
				docPub.Name = "Default";
				docPub.Documentation = docCover.Documentation;
				docPub.Owner = docCover.Owner;
				docPub.Author = docCover.Author;
				docPub.Code = docCover.Code;
				docPub.Copyright = docCover.Copyright;
				docPub.Status = docCover.Status;
				docPub.Version = docCover.Version;

				docPub.Annotations.Add(docForeword);
				docPub.Annotations.Add(docIntro);

				project.Publications.Add(docPub);

				docCover.Delete();
				docContents.Delete();
				project.Annotations.Clear();
			}

			// V11.3: sort terms, references
			project.SortTerms();
			project.SortAbbreviations();
			project.SortNormativeReferences();
			project.SortInformativeReferences();
			docListings.SortLists();

			return project;
		}
		private static bool isUnchanged(DocChangeAction docChangeAction)
		{
			docChangeAction.Changes.RemoveAll(x => !isUnchanged(x));
			if (docChangeAction.Changes.Count == 0 && docChangeAction.Action == DocChangeActionEnum.NOCHANGE && !docChangeAction.ImpactXML && !docChangeAction.ImpactSPF)
				return true;
			return false;
		}
		private static void extractListings(DocSchema schema, DocListings listings)
		{
			foreach (DocType t in schema.Types)
			{
				if (t is DocDefined valueType)
					listings.ValueTypes.Add(valueType);
				else if (t is DocEnumeration enumeration)
				{
					listings.Enumerations.Add(enumeration);
					foreach (DocConstant constant in enumeration.Constants)
					{
						if (!listings.Constants.Contains(constant))
							listings.Constants.Add(constant);
					}
				}
				else if (t is DocSelect selectType)
					listings.SelectTypes.Add(selectType);
			}
			foreach (DocEntity entity in schema.Entities)
				listings.Entities.Add(entity);
			foreach (DocFunction function in schema.Functions)
				listings.Functions.Add(function);
			foreach (DocGlobalRule rule in schema.GlobalRules)
				listings.GlobalRules.Add(rule);
			foreach (DocPropertySet pset in schema.PropertySets)
				extractListings(pset, listings);
			foreach (DocQuantitySet qset in schema.QuantitySets)
				extractListings(qset, listings);
		}

		private static void extractListings(DocPropertySet propertySet, DocListings listings)
		{
			listings.PropertySets.Add(propertySet);
			foreach (DocProperty property in propertySet.Properties)
				extractListings(property, listings);

		}

		private static void extractListings(DocProperty property, DocListings listings)
		{
			listings.Properties.Add(property);
			foreach (DocProperty element in property.Elements)
				extractListings(element, listings);
		}

		private static void extractListings(DocQuantitySet quantitySet, DocListings listings)
		{
			listings.QuantitySets.Add(quantitySet);
			foreach (DocQuantity quantity in quantitySet.Quantities)
				listings.Quantities.Add(quantity);

		}
	}
}
