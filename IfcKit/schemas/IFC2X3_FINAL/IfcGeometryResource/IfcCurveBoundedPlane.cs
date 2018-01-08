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

using BuildingSmart.IFC.IfcGeometricModelResource;
using BuildingSmart.IFC.IfcMeasureResource;
using BuildingSmart.IFC.IfcPresentationAppearanceResource;
using BuildingSmart.IFC.IfcPresentationOrganizationResource;
using BuildingSmart.IFC.IfcProfileResource;
using BuildingSmart.IFC.IfcRepresentationResource;
using BuildingSmart.IFC.IfcTopologyResource;

namespace BuildingSmart.IFC.IfcGeometryResource
{
	[Guid("e3f2858e-1dfd-4bcb-b2a8-e0c8e64b4e6a")]
	public partial class IfcCurveBoundedPlane : IfcBoundedSurface
	{
		[DataMember(Order=0)] 
		[Required()]
		IfcPlane _BasisSurface;
	
		[DataMember(Order=1)] 
		[Required()]
		IfcCurve _OuterBoundary;
	
		[DataMember(Order=2)] 
		[Required()]
		ISet<IfcCurve> _InnerBoundaries = new HashSet<IfcCurve>();
	
	
		[Description("The surface to be bound.")]
		public IfcPlane BasisSurface { get { return this._BasisSurface; } set { this._BasisSurface = value;} }
	
		[Description("The outer boundary of the surface.")]
		public IfcCurve OuterBoundary { get { return this._OuterBoundary; } set { this._OuterBoundary = value;} }
	
		[Description("An optional set of inner boundaries. They shall not intersect each other or the o" +
	    "uter boundary.")]
		public ISet<IfcCurve> InnerBoundaries { get { return this._InnerBoundaries; } }
	
	
	}
	
}