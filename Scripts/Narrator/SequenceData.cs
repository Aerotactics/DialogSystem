using UnityEngine;
using System.Collections.Generic;

// In order for this to be de-serializeable, I've had to make the key a string.
using ClipDictionary = System.Collections.Generic.Dictionary<SequenceData.Event, System.Collections.Generic.List<string>>;

[System.Serializable]
public class SequenceData
{
    public enum Event
    {
        kDefault,       // This is the main sequence intended to be played
        kInterrupt,     // This sequence successfully interupted another
        kAbort,         // This sequence was aborted 2
        kEarly,         // Sequence triggered early (or before another required sequence)
        kLate,          // Sequence triggered after a later sequence, or length of time
        kReturn,        // Sequence continued after it was interrupted (only called when persistent=true)

        kCount
    }

    // A collection of all the clips in this sequence
    private ClipDictionary m_clipDictionary = new ClipDictionary();

    //Dictionaries cannot be json serialized in Unity (Big F), so each list will have to be individually
    //  serialized. We still use the dictionary for easy indexing.
    [SerializeField] private List<string> m_eventDefault = null;
    [SerializeField] private List<string> m_eventInterrupt = null;
    [SerializeField] private List<string> m_eventAbort = null;
    [SerializeField] private List<string> m_eventEarly = null;
    [SerializeField] private List<string> m_eventLate = null;
    [SerializeField] private List<string> m_eventReturn = null;

    // Cannot be stopped or interupted (Other sequence is ignored completely)
    //  Useful for main quest or important dialog that never is interupted
    [SerializeField] private bool m_unstoppable = false;

    // Keep this sequence queued when interupted. (Quest dialog for example)
    [SerializeField] private bool m_persistent = false;

    // If set to true, a sequence can be triggered multiple times. (It's not cached in .)
    [SerializeField] private bool m_canRepeat = false;

    // Set these if you want to detect a skipped sequence. kEarly or kLate will be called accordingly.
    [SerializeField] private string m_previousSequence = null;
    [SerializeField] private string m_nextSequence = null;

    // Set these if you want to detect the time from the last sequence to the current one.
    //  if this sequence plays before minTime, kEarly will play. If it starts after maxTime,
    //  kLate will play. Then the kDefault is queued as normal.
    [SerializeField] private float m_minTime = 0;
    [SerializeField] private float m_maxTime = 0;

    public ClipDictionary clipDictionary => m_clipDictionary;
    public bool unstoppable => m_unstoppable;
    public bool persistent => m_persistent;
    public string previousSequence => m_previousSequence;
    public string nextSequence => m_nextSequence;
    public float minTime => m_minTime;
    public float maxTime => m_maxTime;
    public bool canRepeat => m_canRepeat;

    public SequenceData()
    {
        m_eventDefault = new List<string>();
        m_eventInterrupt = new List<string>();
        m_eventAbort = new List<string>();
        m_eventEarly = new List<string>();
        m_eventLate = new List<string>();
        m_eventReturn = new List<string>();

        m_clipDictionary[Event.kDefault] = m_eventDefault;
        m_clipDictionary[Event.kInterrupt] = m_eventInterrupt;
        m_clipDictionary[Event.kAbort] = m_eventAbort;
        m_clipDictionary[Event.kEarly] = m_eventEarly;
        m_clipDictionary[Event.kLate] = m_eventLate;
        m_clipDictionary[Event.kReturn] = m_eventReturn;
    }

#if UNITY_EDITOR
    public void GenerateJsonExample()
    {
        m_clipDictionary[Event.kDefault].Add("SomeFileName");
        m_clipDictionary[Event.kInterrupt].Add("SomeInterruptName");
        m_clipDictionary[Event.kAbort].Add("SomeAbortName");
    }
#endif //UNITY_EDITOR
}
