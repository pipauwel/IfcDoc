// This file was automatically generated from IFCDOC at www.buildingsmart-tech.org.
// IFC content is copyright (C) 1996-2018 BuildingSMART International Ltd.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Xml.Serialization;

using BuildingSmart.IFC.IfcGeometryResource;
using BuildingSmart.IFC.IfcMeasureResource;
using BuildingSmart.IFC.IfcPresentationAppearanceResource;
using BuildingSmart.IFC.IfcProfileResource;
using BuildingSmart.IFC.IfcTopologyResource;

namespace BuildingSmart.IFC.IfcGeometricModelResource
{
	[Guid("f8ed7699-eae0-403b-a7a3-79679b07189f")]
	public partial class IfcTriangulatedFaceSet : IfcTessellatedFaceSet
	{
		[DataMember(Order=0)] 
		[Required()]
		IList<Int64> _CoordIndex = new List<Int64>();
	
		[DataMember(Order=1)] 
		IList<Int64> _NormalIndex = new List<Int64>();
	
	
		[Description(@"<EPM-HTML>
	Two-dimensional list, where the first dimension represents the triangles (from 1 to N) and the second dimension the indices to
	three points defining the vertices (from 1 to 3).
	<blockquote class=""note"">
	NOTE&nbsp; The coordinates of the vertices are provided by the indexed list of <em>SELF\IfcTessellatedFaceSet.Coordinates.CoordList</em>.
	</blockquote>
	</EPM-HTML>")]
		public IList<Int64> CoordIndex { get { return this._CoordIndex; } }
	
		[Description(@"<EPM-HTML>
	Two-dimensional list, where the first dimension represents the triangle (from 1 to N) and the second dimension the indices to
	three normals (from 1 to 3) corresponding to the vertices.
	<blockquote class=""note"">
	The directions of the normals are provided by the indexed list of <em>SELF\IfcTessellatedFaceSet.Normals.DirectionList</em>.
	</blockquote>
	</EPM-HTML>")]
		public IList<Int64> NormalIndex { get { return this._NormalIndex; } }
	
	
	}
	
}