﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Clojure.Code.Repl
{
	public interface IReplWriteCompleteListener
	{
		void OnReplWriteComplete();
	}
}