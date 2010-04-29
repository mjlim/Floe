﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;

namespace Floe.UI
{
	public partial class ChatPresenter : ChatBoxBase, IScrollInfo
	{
		private class CustomTextRunProperties : TextRunProperties
		{
			private Typeface _typeface;
			private double _fontSize;
			private Brush _foreground;
			private Brush _background;

			public override double FontHintingEmSize { get { return 0.0; } }
			public override TextDecorationCollection TextDecorations { get { return null; } }
			public override TextEffectCollection TextEffects { get { return null; } }
			public override CultureInfo CultureInfo { get { return CultureInfo.InvariantCulture; } }
			public override Typeface Typeface { get { return _typeface; } }
			public override double FontRenderingEmSize { get { return _fontSize; } }
			public override Brush BackgroundBrush { get { return _background; } }
			public override Brush ForegroundBrush { get { return _foreground; } }

			public void SetForeground(Brush foreground)
			{
				_foreground = foreground;
			}

			public CustomTextRunProperties(Typeface typeface, double fontSize, Brush foreground, Brush background)
			{
				_typeface = typeface;
				_fontSize = fontSize;
				_foreground = foreground;
				_background = background;
			}
		}

		private class CustomParagraphProperties : TextParagraphProperties
		{
			private TextRunProperties _defaultProperties;
			private double _indent;

			public override FlowDirection FlowDirection { get { return FlowDirection.LeftToRight; } }
			public override TextAlignment TextAlignment { get { return TextAlignment.Left; } }
			public override double LineHeight { get { return 0.0; } }
			public override bool FirstLineInParagraph { get { return false; } }
			public override TextWrapping TextWrapping { get { return TextWrapping.Wrap; } }
			public override TextMarkerProperties TextMarkerProperties { get { return null; } }
			public override TextRunProperties DefaultTextRunProperties { get { return _defaultProperties; } }
			public override double Indent { get { return _indent; } }

			public void SetIndent(double indent)
			{
				_indent = indent;
			}

			public CustomParagraphProperties(TextRunProperties defaultTextRunProperties)
			{
				_defaultProperties = defaultTextRunProperties;
			}
		}

		private class LineSource : TextSource
		{
			private TextRunProperties _defaultProperties;
			private CustomParagraphProperties _paraProperties;
			private int _lineIdx, _charIdx;
			private string[] _lines;

			public LineSource(TextRunProperties defaultProperties, CustomParagraphProperties paraProperties,
				IEnumerable<string> lines)
			{
				_defaultProperties = defaultProperties;
				_paraProperties = paraProperties;
				_lines = lines.ToArray();
			}

			public bool HasMore { get { return _lineIdx < _lines.Length; } }
			public int BaseIndex { get { return _charIdx; } }

			private void Next()
			{
				_charIdx += _lines[_lineIdx].Length + 1;
				_lineIdx++;
			}

			private void Prev()
			{
				_lineIdx--;
				_charIdx -= _lines[_lineIdx].Length + 1;
			}

			private void MoveToCharIndex(int charIdx)
			{
				int idx = charIdx - _charIdx;
				while (idx < 0)
				{
					this.Prev();
					idx = charIdx - _charIdx;
				}
				while (idx > _lines[_lineIdx].Length)
				{
					this.Next();
					idx = charIdx - _charIdx;
				}
			}

			public override TextRun GetTextRun(int charIdx)
			{
				this.MoveToCharIndex(charIdx);
				int idx = charIdx - _charIdx;
				string text = _lines[_lineIdx];

				TextRun result;

				if (idx == 0)
				{
					_paraProperties.SetIndent(20.0);
				}

				if (idx >= text.Length)
				{
					this.Next();
					result = new TextEndOfLine(1);
					_paraProperties.SetIndent(0.0);
				}
				else
				{
					result = new TextCharacters(text, idx, text.Length - idx, _defaultProperties);
				}

				return result;
			}

			public override TextSpan<CultureSpecificCharacterBufferRange> GetPrecedingText(int textSourceCharacterIndexLimit)
			{
				throw new NotImplementedException();
			}

			public override int GetTextEffectCharacterIndexFromTextSourceCharacterIndex(int textSourceCharacterIndex)
			{
				throw new NotImplementedException();
			}
		}

		private List<TextLine> _output = new List<TextLine>();
		private TextLine _lastLine;
		private int _lastVisibleLineIdx = -1;

		private Typeface Typeface
		{
			get
			{
				return new Typeface(this.FontFamily, this.FontStyle, this.FontWeight, this.FontStretch);
			}
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);

			drawingContext.DrawRectangle(this.Background, null, new Rect(new Size(this.ActualWidth, this.ActualHeight)));

			_isAutoScrolling = true;

			if (_lastLine == null)
			{
				return;
			}

			double height = 0.0, minHeight = this.ExtentHeight - (this.VerticalOffset + this.ActualHeight);
			double vPos = this.ActualHeight;

			TextFormatter formatter = null;
			LineSource source = null;
			CustomParagraphProperties paraProperties = null;
			if (this.IsSelecting)
			{
				this.CreateFormatter(SystemColors.HighlightTextBrush, out formatter, out source, out paraProperties);
			}

			_lastVisibleLineIdx = -1;
			for (int i = _output.Count - 1; i >= 0 && vPos >= 0.0; --i)
			{
				if ((height += _output[i].Height) < minHeight)
				{
					_isAutoScrolling = false;
					continue;
				}
				if (_lastVisibleLineIdx < 0)
				{
					_lastVisibleLineIdx = i;
				}
				vPos -= _output[i].Height;
				_output[i].Draw(drawingContext, new Point(0.0, vPos), InvertAxes.None);
				if (this.IsSelecting)
				{
					this.DrawSelectedLine(drawingContext, vPos, i, formatter, source, paraProperties);
				}
			}
		}

		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
		{
			this.FormatText();
		}

		private void CreateFormatter(Brush foreground, out TextFormatter formatter, out LineSource source,
			out CustomParagraphProperties paraProperties)
		{
			formatter = TextFormatter.Create(TextFormattingMode.Display);
			var textRunProperties = new CustomTextRunProperties(
				this.Typeface, this.FontSize, this.Foreground, Brushes.Transparent);
			paraProperties = new CustomParagraphProperties(textRunProperties);
			source = new LineSource(textRunProperties, paraProperties, _lines);
		}

		private void CreateFormatter(out TextFormatter formatter, out LineSource source, out CustomParagraphProperties paraProperties)
		{
			this.CreateFormatter(this.Foreground, out formatter, out source, out paraProperties);
		}

		private void FormatText()
		{
			TextFormatter formatter;
			LineSource source;
			CustomParagraphProperties paraProperties;
			this.CreateFormatter(out formatter, out source, out paraProperties);

			_extentHeight = 0.0;
			_output.Clear();
			int idx = 0;
			while (source.HasMore)
			{
				var textLine = formatter.FormatLine(source, idx, this.ActualWidth, paraProperties, null);
				idx += textLine.Length;
				_extentHeight += textLine.Height;
				_output.Add(textLine);
			}

			_lastLine = _output.Count > 0 ? _output[_output.Count - 1] : null;
			_extentHeight += this.ActualHeight;

			this.InvalidateVisual();
			_viewer.InvalidateScrollInfo();
		}
	}
}