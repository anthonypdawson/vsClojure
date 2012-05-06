﻿using System.Collections.Generic;
using Clojure.VisualStudio.Utilities;
using EnvDTE80;

namespace Clojure.VisualStudio.Repl.Operations
{
    public class ActiveFileProvider : IProvider<List<string>>
    {
        private readonly DTE2 _dte;

        public ActiveFileProvider(DTE2 dte)
        {
            _dte = dte;
        }

        public List<string> Get()
        {
            return new List<string>() { _dte.ActiveDocument.FullName };
        }
    }
}