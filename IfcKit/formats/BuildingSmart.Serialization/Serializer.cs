// Name:        Serializer.cs
// Description: Base class for serializers
// Author:      Tim Chipman
// Origination: Work performed for BuildingSmart by Constructivity.com LLC.
// Copyright:   (c) 2017 BuildingSmart International Ltd.
// License:     http://www.buildingsmart-tech.org/legal

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Runtime.Serialization;
using System.ComponentModel.DataAnnotations.Schema;


namespace BuildingSmart.Serialization
{
	public abstract class Serializer : Inspector
	{
		protected static char[] HexChars = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

		/// <summary>
		/// Empty constructor if unknown schema
		/// </summary>
		public Serializer()
			: base(null, null, null, null, null)
		{
		}

		/// <summary>
		/// Creates serializer accepting all types within assembly.
		/// </summary>
		/// <param name="typeProject">Type of the root object to load</param>
		public Serializer(Type typeProject)
			: base(typeProject, null, null, null, null)
		{
		}

		public Serializer(Type typeProject, Type[] loadtypes)
			: base(typeProject, loadtypes, null, null, null)
		{
		}

		public Serializer(Type typeProject, Type[] types, string schema, string release, string application)
			: base(typeProject, types, schema, release, application)
		{
		}

		/// <summary>
		/// Reads header information (without reading entire file) to retrieve schema version, application, and exchanges.
		/// Schema identifier may be used to resolve source schema and automatically convert to target schema.
		/// Application identifier may be used for validation purposes to provide user instructions for fixing missing data requirements.
		/// Exchange identifiers may be used for validation purposes or for automatically converting to tabular formats (e.g. COBie).
		/// </summary>
		/// <param name="stream">Stream to read, which must be seekable; i.e. if web service, then must be cached as MemoryStream</param>
		/// <returns>Header data about file.</returns>
		//public abstract Header ReadHeader(Stream stream);

		/// <summary>
		/// Reads object from stream.
		/// </summary>
		/// <param name="stream"></param>
		/// <returns></returns>
		public abstract object ReadObject(Stream stream);

		/// <summary>
		/// Writes object to stream.
		/// </summary>
		/// <param name="stream"></param>
		/// <param name="graph"></param>
		public abstract void WriteObject(Stream stream, object graph);
	}


}
