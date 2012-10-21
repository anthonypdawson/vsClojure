﻿/***************************************************************************

Copyright (c) Microsoft Corporation. All rights reserved.
This code is licensed under the Visual Studio SDK license terms.
THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

***************************************************************************/

using System.Runtime.InteropServices;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Project;
using Microsoft.VisualStudio.Shell;

namespace Clojure.VisualStudio.Project
{
	/// <summary>
	/// Represent the methods for creating projects within the solution.
	/// </summary>
    [Guid("985F20FF-87AE-45F6-86E0-1DBBF0224EB9")]
	public class ClojureProjectFactory : ProjectFactory
	{
		#region Fields
		private Package package;
		#endregion

		#region Constructors
		/// <summary>
		/// Explicit default constructor.
		/// </summary>
		/// <param name="package">Value of the project package for initialize internal package field.</param>
		public ClojureProjectFactory(Package package)
			: base(package)
		{
			this.package = package;
		}
		#endregion

		#region Overriden implementation
		/// <summary>
		/// Creates a new project by cloning an existing template project.
		/// </summary>
		/// <returns></returns>
		protected override ProjectNode CreateProject()
		{
			var project = new ClojureProjectNode(this.package);
			var packageServiceProvider = (global::System.IServiceProvider) package;
			var provider = packageServiceProvider.GetService(typeof (IServiceProvider)) as IServiceProvider;
			project.SetSite(provider);
			return project;
		}
		#endregion
	}
}