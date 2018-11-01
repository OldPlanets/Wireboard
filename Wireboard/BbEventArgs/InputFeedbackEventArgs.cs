using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.BbEventArgs
{
    public class InputFeedbackEventArgs : EventArgs
    {
        public InputFeedbackEventArgs(EInputEvent inputEvent, int imeOptions, int inputType, string hint, string packageName, string text, int cursorPos, int nFieldId)
        {
            InputEvent = inputEvent;
            ImeOptions = imeOptions;
            InputType = inputType;
            Hint = hint;
            PackageName = packageName;
            Text = text;
            CursorPos = cursorPos;
            FieldID = nFieldId;
        }

        public InputFeedbackEventArgs(EInputEvent inputEvent, string text, int cursorPos, int lastProcessedInputID)
        {
            LastProcessedInputID = lastProcessedInputID;
            Text = text;
            CursorPos = cursorPos;
            InputEvent = inputEvent;
        }

        public enum EInputEvent { FOCUSCHANGE, TEXTUPDATE }

        public int ImeOptions { get; private set; }
        public int InputType { get; private set; }
        public int FieldID { get; private set; }
        public int LastProcessedInputID { get; private set; }
        public String Hint { get; private set; }
        public String PackageName { get; private set; }
        public String Text { get; private set; }
        public int CursorPos { get; private set; }
        public EInputEvent InputEvent { get; private set; }
    }
}
