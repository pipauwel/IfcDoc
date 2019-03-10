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
using System.Collections.Concurrent;


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

		private static ConcurrentDictionary<string, ConstructorInfo> mConstructors = new ConcurrentDictionary<string, ConstructorInfo>();
		
		protected void Initialize(object o, Type t)
		{
			IList<PropertyInfo> fields = GetFieldsOrdered(t);
			foreach (PropertyInfo f in fields)
			{
				if (f.GetValue(o) == null)
				{
					Type type = f.PropertyType;

					if (IsEntityCollection(type))
					{
						Type typeCollection = this.GetCollectionInstanceType(type);
						object collection = Activator.CreateInstance(typeCollection);
						f.SetValue(o, collection);
					}
				}
			}
		}

		protected static bool IsEntityCollection(Type type)
		{
			return (type != typeof(string) && type != typeof(byte[]) && typeof(IEnumerable).IsAssignableFrom(type));
		}

		protected static string SerializeBytes(Byte[] vector)
		{
			StringBuilder sb = new StringBuilder(vector.Length * 2 + 1);
			sb.Append("\"");
			byte b;
			int start;

			// only 8-byte multiples supported
			sb.Append("0");
			start = 0;

			for (int i = start; i < vector.Length; i++)
			{
				b = vector[i];
				sb.Append(HexChars[b / 0x10]);
				sb.Append(HexChars[b % 0x10]);
			}

			sb.Append("\"");
			return sb.ToString();
		}

		protected static byte[] ParseBinary(string strval)
		{
			int len = (strval.Length - 3) / 2; // subtract surrounding quotes and modulus character
			byte[] vector = new byte[len];
			int modulo = 0; // not used for IFC -- always byte-aligned

			int offset;
			if (strval.Length % 2 == 0)
			{
				modulo = Convert.ToInt32(strval[1]) + 4;
				offset = 1;

				char ch = strval[2];
				vector[0] = (ch >= 'A' ? (byte)(ch - 'A' + 10) : (byte)ch);
			}
			else
			{
				modulo = Convert.ToInt32((strval[1] - '0')); // [0] is quote; [1] is modulo
				offset = 0;
			}

			for (int i = offset; i < len; i++)
			{
				char hi = strval[i * 2 + 2 - offset];
				char lo = strval[i * 2 + 3 - offset];

				byte val = (byte)(
					((hi >= 'A' ? +(int)(hi - 'A' + 10) : (int)(hi - '0')) << 4) +
					((lo >= 'A' ? +(int)(lo - 'A' + 10) : (int)(lo - '0'))));

				vector[i] = val;
			}

			return vector;
		}
	}
}
