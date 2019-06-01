// Name:        FormSelectPropertyEnum.cs
// Description: Dialog box for selecting property enumeration
// Author:      Tim Chipman
// Origination: Work performed for BuildingSmart by Constructivity.com LLC.
// Copyright:   (c) 2013 BuildingSmart International Ltd.
// License:     http://www.buildingsmart-tech.org/legal

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

using IfcDoc.Schema.DOC;

namespace IfcDoc
{
	public partial class FormSelectPropertyEnum : Form
	{
		DocProject m_project;

		public FormSelectPropertyEnum()
		{
			InitializeComponent();
		}

		public FormSelectPropertyEnum(DocProject project, DocPropertyEnumeration selection) : this()
		{
			this.m_project = project;

			SortedList<string, DocPropertyEnumeration> list = new SortedList<string, DocPropertyEnumeration>();
			
			foreach (DocPropertyEnumeration enumeration in project.PropertyEnumerations)
			{
				list.Add(enumeration.Name, enumeration);
			}

			foreach (string s in list.Keys)
			{
				DocPropertyEnumeration enumeration = list[s];
				ListViewItem lvi = new ListViewItem();
				lvi.Tag = enumeration;
				lvi.Text = enumeration.Name;
				lvi.ImageIndex = 0;
				this.listView.Items.Add(lvi);

				if (selection == enumeration)
				{
					lvi.Selected = true;
				}
			}
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);

			if (this.listView.SelectedItems.Count == 1)
			{
				this.listView.SelectedItems[0].EnsureVisible();
			}
		}

		public DocPropertyEnumeration Selection
		{
			get
			{
				if (this.listView.SelectedItems.Count == 1)
				{
					return this.listView.SelectedItems[0].Tag as DocPropertyEnumeration;
				}
				return null;
			}
		}
	}
}
