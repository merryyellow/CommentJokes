/***************************************************************************

based on vssdk sample typing speed meter
https://github.com/microsoft/VSSDK-Extensibility-Samples/blob/master/Typing_Speed_Meter/C%23/CommandFilter.cs

***************************************************************************/

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace CommentJokes
{

    [Export(typeof(IVsTextViewCreationListener))]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [ContentType("text")]
    internal sealed class VsTextViewListener : IVsTextViewCreationListener
    {
        [Import]
        internal IVsEditorAdaptersFactoryService AdapterService = null;

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            var textView = AdapterService.GetWpfTextView(textViewAdapter);
            if (textView == null)
                return;

            var extension = GetExtension(textView);
            
            var isBoth = both.Contains(extension);
            var isSingle = isBoth || single.Contains(extension);
            var isBlock = isBoth || block.Contains(extension);
            var isXml = xml.Contains(extension);

            //var adornment = textView.Properties.GetProperty<TypingSpeedMeter>(typeof(TypingSpeedMeter));

            textView.Properties.GetOrCreateSingletonProperty(
                () => new TypeCharFilter(textViewAdapter, textView,
                isSingle, isBlock, isXml));
        }

        public static string GetExtension(IWpfTextView textView)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            textView.TextBuffer.Properties.TryGetProperty(typeof(IVsTextBuffer), out IVsTextBuffer bufferAdapter);
            var persistFileFormat = bufferAdapter as Microsoft.VisualStudio.Shell.Interop.IPersistFileFormat;

            if (persistFileFormat == null)
            {
                return null;
            }
            persistFileFormat.GetCurFile(out string filePath, out _);
            var extension = System.IO.Path.GetExtension(filePath);
            //return filePath;
            return extension.ToLower();
        }

        // both single & block
        HashSet<string> both = new HashSet<string>(
            new string[]{
                ".cs", ".csx",
                ".c", ".cc",  ".cpp", ".cxx", ".c++", ".h", ".hh", "hpp", ".hxx", ".h++", ".m", ".mm",
                ".ts", ".tsx",
                ".js", ".cjs", ".mjs",
                ".go",
                ".java", ".class", ".jmod", ".jar",
                ".rs", ".rlib",
                ".groovy", ".gvy", ".gy", ".gsh",
                ".less",
                ".swift",
            });
        HashSet<string> single = new HashSet<string>(
            new string[]
            {
                ".fs", ".fsi", ".fsx", ".fsscript",
                ".jade",
            });
        HashSet<string> block = new HashSet<string>(
            new string[]
            {
                ".css",
            });
        HashSet<string> xml = new HashSet<string>(
            new string[]{
                ".cs",
            });
    }


    internal sealed class TypeCharFilter : IOleCommandTarget
    {
        IOleCommandTarget nextCommandHandler;
        ITextView textView;
        internal int typedChars { get; set; }

        bool leadingCharacters = false;
        bool trailingCharacters = false;

        bool blockComment, lineComment, xmlComment;

        /// <summary>
        /// Add this filter to the chain of Command Filters
        /// </summary>
        internal TypeCharFilter(IVsTextView adapter, ITextView textView,
            bool blockComment, bool lineComment, bool xmlComment)
        {
            this.textView = textView;

            this.blockComment = blockComment;
            this.lineComment = lineComment;
            this.xmlComment = xmlComment;

            adapter.AddCommandFilter(this, out nextCommandHandler);
        }

        /// <summary>
        /// Get user input and update Typing Speed meter. Also provides public access to
        /// IOleCommandTarget.Exec() function
        /// </summary>
        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            //int hr = VSConstants.S_OK;
            int hr = nextCommandHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);

            if (hr != VSConstants.S_OK)
                return hr;

            char typedChar;
            if (TryGetTypedChar(pguidCmdGroup, nCmdID, pvaIn, out typedChar))
            {
                //adornment.UpdateBar(typedChars++);
                TellAJoke(typedChar);
            }

            return hr;
        }

        private void TellAJoke(char typedChar)
        {
            if (!(typedChar == '/' && (lineComment || xmlComment)) &&
                !(typedChar == '*' && blockComment))
                return;

            var caretPosition = textView.Caret.Position.BufferPosition;
            var lineRaw = textView.TextBuffer.CurrentSnapshot.GetLineFromPosition(caretPosition);
            var line = lineRaw.Extent;

            // if there are trailing characters
            if (!trailingCharacters && caretPosition != line.End)
                return;

            var isXml = xmlComment && typedChar == '/' && isXmlFunc();

            if (!isXml)
            {
                // check previous char
                if (!line.Contains(caretPosition - 2) || line.Snapshot[caretPosition - 2] != '/')
                    return;

                // there are leading characters
                if (!leadingCharacters && (int)caretPosition - FirstIndexOfNonWhiteSpace(line) != 2)
                    return;
            }

            var joke = Joker.TellAJoke();

            // insert joke
            var span = new Span(caretPosition, 0);
            var snapshot = textView.TextBuffer.Replace(span, joke);

            // select joke
            textView.Selection.Select(new SnapshotSpan(snapshot, span.Start, joke.Length), false);

            bool isXmlFunc()
            {
                // check previous char
                if (!line.Contains(caretPosition - 3) || line.Snapshot[caretPosition - 3] != '/' ||
                    line.Snapshot[caretPosition - 2] != '/' || line.Snapshot[caretPosition - 1] != ' ')
                    return false;
                // there are leading characters
                if (/*!leadingCharacters && */(int)caretPosition - FirstIndexOfNonWhiteSpace(line) != 4)
                    return false;

                var previousLineNumber = lineRaw.LineNumber - 1;
                var previousLineText = textView.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(previousLineNumber)?.Extent.GetText().TrimStart();

                if (previousLineText != "/// <summary>")
                    return false;
                
                return true;
            }
        }

        /// <summary>
        /// Public access to IOleCommandTarget.QueryStatus() function
        /// </summary>
        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            return nextCommandHandler.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        /// <summary>
        /// Try to get the keypress value. Returns 0 if attempt fails
        /// </summary>
        /// <param name="typedChar">Outputs the value of the typed char</param>
        /// <returns>Boolean reporting success or failure of operation</returns>
        bool TryGetTypedChar(Guid cmdGroup, uint nCmdID, IntPtr pvaIn, out char typedChar)
        {
            typedChar = char.MinValue;

            if (cmdGroup != VSConstants.VSStd2K || nCmdID != (uint)VSConstants.VSStd2KCmdID.TYPECHAR)
                return false;

            typedChar = (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
            return true;
        }

        public static int FirstIndexOfNonWhiteSpace(SnapshotSpan text)
        {
            var start = (int)text.Start;
            var end = (int)text.End;
            for (var i = start; i < end; i++)
            {
                if (!char.IsWhiteSpace(text.Snapshot[i]))
                {
                    return i;
                }
            }

            return -1;
        }

    }
}
