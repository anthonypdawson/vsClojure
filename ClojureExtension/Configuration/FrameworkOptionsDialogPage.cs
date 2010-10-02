﻿using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.ClojureExtension.Configuration
{
    [ClassInterface(ClassInterfaceType.AutoDual)]
    [Guid("1E5E2726-EA97-47C8-AF28-80572D4F2021")]
    public class FrameworkOptionsDialogPage : DialogPage
    {
        private readonly FrameworkOptions _frameworkOptionPage;

        public FrameworkOptionsDialogPage() :
            this(new FrameworkOptions(SettingsStoreProvider.Store))
        {
        }

        public FrameworkOptionsDialogPage(FrameworkOptions frameworkOptionPage)
        {
            _frameworkOptionPage = frameworkOptionPage;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        protected override IWin32Window Window
        {
            get { return _frameworkOptionPage; }
        }

        public override void SaveSettingsToStorage()
        {
            SettingsStoreProvider.Store.Save("Frameworks", _frameworkOptionPage.GetFrameworkList());
        }
    }
}