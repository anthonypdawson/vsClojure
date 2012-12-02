﻿using System.Collections.Generic;
using Clojure.VisualStudio.Workspace.TextEditor.View;
using Clojure.Workspace.TextEditor;
using Microsoft.VisualStudio.Text.Editor;

namespace Clojure.VisualStudio.Workspace.TextEditor
{
	public class VisualStudioClojureTextEditor : IUserActionSource
	{
		private readonly ITextView _currentWpfTextView;
		private readonly List<IUserActionListener> _actionListeners;
		private readonly List<IClojureViewActionListener> _viewListeners;

		public VisualStudioClojureTextEditor(ITextView view)
		{
			_currentWpfTextView = view;
			_currentWpfTextView.Caret.PositionChanged += CaretPositionChanged;
			_viewListeners = new List<IClojureViewActionListener>();
			_actionListeners = new List<IUserActionListener>();
		}

		private void CaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
		{
			_viewListeners.ForEach(l => l.OnCaretPositionChange(e.NewPosition.BufferPosition.Position));
		}

		public void AddViewListener(IClojureViewActionListener listener)
		{
			_viewListeners.Add(listener);
		}

		public void AddUserActionListener(IUserActionListener listener)
		{
			_actionListeners.Add(listener);
		}

		public void Format()
		{
			_actionListeners.ForEach(l => l.Format());
		}

		public void CommentSelectedLines()
		{
			int startPosition = _currentWpfTextView.Selection.Start.Position.GetContainingLine().Start.Position;
			int endPosition = _currentWpfTextView.Selection.End.Position.GetContainingLine().End.Position;
			_actionListeners.ForEach(l => l.CommentLines(startPosition, endPosition));
		}

		public void UncommentSelectedLines()
		{
			int startPosition = _currentWpfTextView.Selection.Start.Position.GetContainingLine().Start.Position;
			int endPosition = _currentWpfTextView.Selection.End.Position.GetContainingLine().End.Position;
			_actionListeners.ForEach(l => l.UncommentLines(startPosition, endPosition));
		}
	}
}