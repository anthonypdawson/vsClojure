﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Clojure.Code.Editing.PartialUpdate;
using Clojure.System.IO.Compression;
using Clojure.VisualStudio.Project.Configuration;
using Clojure.VisualStudio.Project.Hierarchy;
using Clojure.VisualStudio.Workspace.Menus;
using Clojure.VisualStudio.Workspace.Repl;
using Clojure.VisualStudio.Workspace.SolutionExplorer;
using Clojure.VisualStudio.Workspace.TextEditor;
using Clojure.Workspace;
using Clojure.Workspace.Menus;
using Clojure.Workspace.Repl;
using Clojure.Workspace.Repl.Commands;
using Clojure.Workspace.Repl.Presentation;
using Clojure.Workspace.TextEditor;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Project;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.Win32;

namespace Clojure.VisualStudio
{
	[Guid(PackageGuid)]
	[PackageRegistration(UseManagedResourcesOnly = true)]
	[DefaultRegistryRoot("Software\\Microsoft\\VisualStudio\\10.0")]
	[ProvideObject(typeof (GeneralPropertyPage))]
	[ProvideProjectFactory(typeof (ClojureProjectFactory), "Clojure", "Clojure Project Files (*.cljproj);*.cljproj", "cljproj", "cljproj", @"Project\Templates\Projects\Clojure", LanguageVsTemplate = "Clojure", NewProjectRequireNewFolderVsTemplate = false)]
	[ProvideProjectItem(typeof (ClojureProjectFactory), "Clojure Items", @"Project\Templates\ProjectItems\Clojure", 500)]
	[ProvideMenuResource("Menus.ctmenu", 1)]
	[ProvideToolWindow(typeof (ReplToolWindow))]
	[ProvideAutoLoad(UIContextGuids80.NoSolution)]
	public sealed class ClojurePackage : ProjectPackage
	{
		public const string PackageGuid = "7712178c-977f-45ec-adf6-e38108cc7739";
		private DTEEvents _dteEvents;

		private ReplTabControl _replTabControl;
		private ClojureEnvironment _clojureEnvironment;
		private ClojureEditorCollection _editorCollection;
		private ClojureEditorMenuCommandService _menuCommandService;

		protected override void Initialize()
		{
			base.Initialize();
			var dte = (DTE2) GetService(typeof (DTE));
			_dteEvents = dte.Events.DTEEvents;

			_dteEvents.OnStartupComplete +=
				() =>
				{
					_replTabControl = new ReplTabControl();

					_menuCommandService = new ClojureEditorMenuCommandService(this);
					RegisterMenuCommandService(_menuCommandService);

					_editorCollection = new ClojureEditorCollection(dte);
					_editorCollection.AddEditorChangeListener(_menuCommandService);

					var replToolWindow = (ReplToolWindow) FindToolWindow(typeof (ReplToolWindow), 0, true);
					replToolWindow.SetControl(_replTabControl);

					_clojureEnvironment = new ClojureEnvironment();
					_replTabControl.AddReplActivationListener(_clojureEnvironment);

					AppDomain.CurrentDomain.AssemblyResolve += CurrentDomainAssemblyResolve;
					RegisterProjectFactory(new ClojureProjectFactory(this));
					CreateReplMenuCommands();
					EnableTokenizationOfNewClojureBuffers();
					EnableSettingOfRuntimePathForNewClojureProjects();
					UnzipRuntimes();
				};
		}

		private void RegisterMenuCommandService(OleMenuCommandService menuCommandService)
		{
			var commandRegistry = GetService(typeof(SVsRegisterPriorityCommandTarget)) as IVsRegisterPriorityCommandTarget;
			uint cookie = 0;
			commandRegistry.RegisterPriorityCommandTarget(0, menuCommandService, out cookie);
		}

		private void UnzipRuntimes()
		{
			try
			{
				var runtimeBasePath = Path.Combine(GetDirectoryOfDeployedContents(), "Runtimes");
				Directory.GetFiles(runtimeBasePath, "*.zip").ToList().ForEach(CompressionExtensions.ExtractZipToFreshSubDirectoryAndDelete);
			}
			catch (Exception e)
			{
				var errorMessage = new StringBuilder();
				errorMessage.AppendLine("Failed to extract ClojureCLR runtime(s).  You may need to reinstall vsClojure.");
				errorMessage.AppendLine(e.Message);
			}
		}

		private string GetDirectoryOfDeployedContents()
		{
			string codebaseRegistryLocation = ApplicationRegistryRoot + "\\Packages\\{" + PackageGuid + "}";
			return Path.GetDirectoryName(Registry.GetValue(codebaseRegistryLocation, "CodeBase", "").ToString());
		}

		private void EnableSettingOfRuntimePathForNewClojureProjects()
		{
			string codebaseRegistryLocation = ApplicationRegistryRoot + "\\Packages\\{" + PackageGuid + "}";
			string runtimePath = Registry.GetValue(codebaseRegistryLocation, "CodeBase", "").ToString();
			runtimePath = Path.GetDirectoryName(runtimePath) + "\\Runtimes\\";

			if (Environment.GetEnvironmentVariable("VSCLOJURE_RUNTIMES_DIR", EnvironmentVariableTarget.User) != runtimePath)
			{
				Environment.SetEnvironmentVariable("VSCLOJURE_RUNTIMES_DIR", runtimePath, EnvironmentVariableTarget.User);
				MessageBox.Show("Setup of vsClojure complete.  Please restart Visual Studio.", "vsClojure Setup");
			}
		}

		private IMenuCommand CreateVisualStudioMenuCommand(CommandID commandId, IExternalClickListener clickListener)
		{
			var menuCommandService = (OleMenuCommandService) GetService(typeof (IMenuCommandService));
			var menuCommandAdapterReference = new MenuCommandAdapterReference();
			var internalMenuCommand = new MenuCommand((o, e) => menuCommandAdapterReference.Adapter.OnClick(), commandId);
			var menuCommandAdapter = new VisualStudioClojureMenuCommandAdapter(internalMenuCommand);
			menuCommandAdapterReference.Adapter = menuCommandAdapter;
			menuCommandService.AddCommand(internalMenuCommand);
			menuCommandAdapter.AddClickListener(clickListener);
			return menuCommandAdapter;
		}

		private void EnableTokenizationOfNewClojureBuffers()
		{
			var componentModel = (IComponentModel) GetService(typeof (SComponentModel));
			var documentFactoryService = componentModel.GetService<ITextDocumentFactoryService>();
			var editorFactoryService = componentModel.GetService<ITextEditorFactoryService>();

			documentFactoryService.TextDocumentDisposed += (o, e) => { };

			documentFactoryService.TextDocumentCreated +=
				(o, e) =>
				{
					if (!e.TextDocument.FilePath.EndsWith(".clj")) return;
					var vsClojureTextBuffer = new VisualStudioClojureTextBuffer(e.TextDocument.TextBuffer);
					vsClojureTextBuffer.InvalidateTokens();
				};

			editorFactoryService.TextViewCreated +=
				(o, e) =>
				{
					if (e.TextView.TextSnapshot.ContentType.TypeName.ToLower() != "clojure") return;

					var vsTextBuffer = e.TextView.TextBuffer;
					var clojureTextBuffer = vsTextBuffer.Properties.GetProperty<VisualStudioClojureTextBuffer>(typeof(VisualStudioClojureTextBuffer));
					var filePath = vsTextBuffer.Properties.GetProperty<ITextDocument>(typeof (ITextDocument)).FilePath;

					var editor = new VisualStudioClojureTextView(e.TextView);
					editor.AddUserActionListener(clojureTextBuffer);
					_editorCollection.EditorAdded(filePath, editor);

					IEditorOptions editorOptions = componentModel.GetService<IEditorOptionsFactoryService>().GetOptions(e.TextView);
					editorOptions.SetOptionValue(new ConvertTabsToSpaces().Key, true);
					editorOptions.SetOptionValue(new IndentSize().Key, 2);
				};

			var routingTextEditor = new RoutingTextView();
			_editorCollection.AddEditorChangeListener(routingTextEditor);
			_menuCommandService.Add(new MenuCommand((o, e) => routingTextEditor.Format(), CommandIDs.FormatDocument));
			_menuCommandService.Add(new MenuCommand((o, e) => routingTextEditor.CommentSelectedLines(), CommandIDs.BlockComment));
			_menuCommandService.Add(new MenuCommand((o, e) => routingTextEditor.UncommentSelectedLines(), CommandIDs.BlockUncomment));
		}

		private void CreateReplMenuCommands()
		{
			var dte = (DTE2) GetService(typeof (DTE));
			var replToolWindow = (ReplToolWindow) FindToolWindow(typeof (ReplToolWindow), 0, true);

			var replPortfolio = new ReplPortfolio();
			replPortfolio.AddPortfolioListener(_replTabControl);
			replPortfolio.AddPortfolioListener(replToolWindow);

			var replLauncher = new ReplLauncher(replPortfolio);
			CreateVisualStudioMenuCommand(ProjectMenuCommand.LaunchReplCommandId, new ProjectMenuCommand(dte.ToolWindows.SolutionExplorer, replLauncher));

			var explorer = new VisualStudioExplorer(dte);
			var repl = new ReplCommandRouter();
			_replTabControl.AddReplActivationListener(repl);

			var loadSelectedProjectCommand = new LoadSelectedProjectCommand(explorer, repl);
			explorer.AddSelectionListener(loadSelectedProjectCommand);

			var loadSelectedFilesCommand = new LoadSelectedFilesCommand(repl);
			explorer.AddSelectionListener(loadSelectedFilesCommand);

			var explorerMenuCommandCollection = new MenuCommandCollection(MenuCommandCollection.VisibleEditorStates);
			explorerMenuCommandCollection.Add(CreateVisualStudioMenuCommand(new CommandID(Guids.GuidClojureExtensionCmdSet, 11), loadSelectedProjectCommand));
			explorerMenuCommandCollection.Add(CreateVisualStudioMenuCommand(new CommandID(Guids.GuidClojureExtensionCmdSet, 12), loadSelectedFilesCommand));
			_clojureEnvironment.AddActivationListener(explorerMenuCommandCollection);

			var loadActiveFileCommand = new LoadActiveFileCommand(repl);
			//_textEditor.AddStateChangeListener(loadActiveFileCommand);

			var changeNamespaceCommand = new ChangeNamespaceCommand(repl);
			//_textEditor.AddStateChangeListener(changeNamespaceCommand);

			var loadSelectionCommand = new LoadSelectionCommand(repl);
			//_textEditor.AddStateChangeListener(loadSelectionCommand);

			var editorMenuCommandCollection = new MenuCommandCollection(MenuCommandCollection.VisibleEditorStates);
			editorMenuCommandCollection.Add(CreateVisualStudioMenuCommand(new CommandID(Guids.GuidClojureExtensionCmdSet, 13), loadActiveFileCommand));
			editorMenuCommandCollection.Add(CreateVisualStudioMenuCommand(new CommandID(Guids.GuidClojureExtensionCmdSet, 14), changeNamespaceCommand));
			editorMenuCommandCollection.Add(CreateVisualStudioMenuCommand(new CommandID(Guids.GuidClojureExtensionCmdSet, 15), loadSelectionCommand));
			_clojureEnvironment.AddActivationListener(editorMenuCommandCollection);
		}

		public override string ProductUserContext
		{
			get { return "ClojureProj"; }
		}

		private static Assembly CurrentDomainAssemblyResolve(object sender, ResolveEventArgs args)
		{
			return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly => assembly.FullName == args.Name);
		}
	}
}