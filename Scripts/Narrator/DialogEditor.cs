using System;
using System.Collections.Generic;
using UnityEditor;

public class DialogEditor : EditorWindow
{
    private string m_sequenceDataName = null;
    private SequenceData m_sequenceDataEdit = null;
    private string m_clipDataName = null;
    private ClipData m_clipDataEdit = null;

    [MenuItem("Tools/Dialog Editor")]
    private static void OnGUI()
    {
        
    }
}
