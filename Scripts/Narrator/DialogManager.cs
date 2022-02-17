
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using ClipStackTuple = System.Tuple<SequenceData, ClipData, float>; // Owner, Clip, Time Queued

// TODO: Save DialogManager to file
[System.Serializable]
class DialogManager : MonoBehaviour
{
    // The HUD display to draw the dialog text
    [SerializeField] private TMPro.TMP_Text m_textDisplayElement = null;

    // The audio source component will be used to play our audio clips
    [SerializeField] private AudioSource m_currentAudio = null;
    
    // Clips with an age older than this will be ignored when they
    //  are reached in the queue.
    [SerializeField] private uint m_clipAgeMaximum = 300; // In Seconds

    // If a sound fails to load within this time, attempt only display
    //  the dialog text.
    [SerializeField] private uint m_resourceTimeout = 2; // In Seconds

    // If a resource does timeout, this is the time the text is displayed.
    [SerializeField] private float m_timeoutDisplayTime = 10; // In Seconds

    //Sequence Stack will always have 0 or 1 Sequence, unless another 
    //  sequence is persistent. When the clip queue is empty, we can
    //  clear the stack.
    private Stack<SequenceData> m_sequenceStack = new Stack<SequenceData>();

    // Clip stack will contain the active clips being played as follows:
    //  SequenceData = the Clip's owning Sequence
    //  ClipData = the clip's data itself
    //  uint = the time the clip was queued
    private Stack<ClipStackTuple> m_clipStack = new Stack<ClipStackTuple>();

    // We save a list of previous sequences so that we don't play
    //  them twice. (Would persist upon saving and loading.)
    private List<string> m_previousSequences = new List<string>();

    // We do a lot of file reads here, so let's cache the data path
    private string m_dataPath = null;

    private bool m_showDialog = true;
    private bool m_clipPlaying = false; // This may not be necessary
    private Coroutine m_playAudioRoutine = null;
    private float m_lastSequenceQueueTime = 0;

    public bool showDialog { get { return m_showDialog; } set { m_showDialog = value; } }

    private void Start()
    {
        m_textDisplayElement.text = "";
        m_dataPath = Application.dataPath;

#if UNITY_EDITOR
        //GenerateExampleJsonFiles();
        //PlaySequence("intro");
#endif //UNITY_EDITOR
    }

    private void Update()
    {
        //Demonstration (Ran out of time for a proper demo)
        if(Input.GetKeyDown(KeyCode.Keypad1))
        {
            PlaySequence("intro");
        }

        if (Input.GetKeyDown(KeyCode.Keypad2))
        {
            PlaySequence("about");
        }

        if (Input.GetKeyDown(KeyCode.Keypad3))
        {
            PlaySequence("manager");
        }

        if (Input.GetKeyDown(KeyCode.Keypad4))
        {
            PlaySequence("end");
        }

        if (Input.GetKeyDown(KeyCode.O))
        {
            AbortCurrentSequence();
        }
    }

    private void PlayNextClip()
    {
        // Play first clip on stack if theres a queue
        if(m_clipStack.Count > 0)
        {
            // Loop until we hit a clip that is still within the age maximum
            while (m_clipStack.Peek().Item3 >= m_clipAgeMaximum)
            {
                PopClip();
            }

            PlayClip(m_clipStack.Peek().Item2);
        }
        // otherwise, there's no clips, so clear the sequence queue
        else
        {
            m_sequenceStack.Clear();
        }
    }

    private void PopClip()
    {
        m_clipStack.Pop();

        if (m_clipStack.Count > 0 && m_clipStack.Peek().Item1 != m_sequenceStack.Peek())
        {
            m_sequenceStack.Pop();

            // At this point the clip stack should be in sync with the
            //  sequence stack. If it isn't, there's a big problem.
            if (m_clipStack.Count > 0 && m_clipStack.Peek().Item1 != m_sequenceStack.Peek())
            {
                throw new Exception();
            }
        }
    }

    public void PlaySequence(string name)
    {
        //Check if it's already been played
        if (m_previousSequences.Exists(item => item == name))
            return;

        bool interrupt = false;

        //Load the next sequence
        SequenceData nextSequence = DialogFactory.Load<SequenceData>(name);
        if (nextSequence == null)
            return;

        // We successfully loaded the sequenceData, add it to previously played list
        if (nextSequence.canRepeat == false)
            m_previousSequences.Add(name);

        if(m_sequenceStack.Count > 0)
        {
            // The current sequence cannot be stopped, no matter what.
            //  This sequence is then ignored.
            SequenceData currentSequence = m_sequenceStack.Peek();
            if (currentSequence.unstoppable)
            {
                return;
            }
            // This sequence is persistent, so we will have to return to it, calling
            //   "return" clip list if it exists. (It should exist if you plan on making
            //   the sequence persistent.)
            else if(currentSequence.persistent)
            {
                QueueEventFront(currentSequence, SequenceData.Event.kReturn);
                StopCurrentClip();
            }
            else
            {
                StopAndPopCurrentSequence();
            }

            // Because there was a sequence queued, we interupted it...at the end of queueing
            //  everything because we're using a stack, and so we have to stack it on top at
            //  the end.
            interrupt = true;
        }

        // Now we queue the new dialog
        QueueEventFront(nextSequence, SequenceData.Event.kDefault);

        // Before the default dialog and after a possible interupt, we check to see if this
        //  sequence arrived early or later than it was expected, either by a set time, or
        //  by previous and next sequence options.

        // If the next sequence was already played, OR
        // the time from the last sequence queue time exceeds our defined max time, we're late.
        float time = Time.time;
        if ((nextSequence.nextSequence != null && nextSequence.nextSequence.Length > 0 && m_previousSequences.Exists(item => item == nextSequence.nextSequence)) ||
            (nextSequence.maxTime > 0 && time - m_lastSequenceQueueTime >= nextSequence.maxTime))
        {
            QueueEventFront(nextSequence, SequenceData.Event.kLate);
        }
        // Otherwise, if the previous sequence has not been played, OR
        // the time from the last sequence queue time is under our defined minimum time, we are early.
        else if((nextSequence.previousSequence != null && nextSequence.previousSequence.Length > 0 && m_previousSequences.Exists(item => item == nextSequence.previousSequence) == false) ||
            (nextSequence.minTime > 0 && time - m_lastSequenceQueueTime <= nextSequence.minTime))
        {
            QueueEventFront(nextSequence, SequenceData.Event.kEarly);
        }

        //and if we interrupted, stack the interrupt clips
        if(interrupt == true)
        {
            QueueEventFront(nextSequence, SequenceData.Event.kInterrupt);
        }

        // Finally, we stack the sequence and play the queue
        m_sequenceStack.Push(nextSequence);
        PlayNextClip();
    }

    private void StopAndPopCurrentSequence()
    {
        if(m_sequenceStack.Count > 0)
        {
            StopCurrentClip();
            ClearCurrentSequenceClips();
            m_sequenceStack.Pop();
        }
    }

    private void ClearCurrentSequenceClips()
    {
        if (m_sequenceStack.Count > 0)
        {
            SequenceData currentSequence = m_sequenceStack.Peek();

            while (m_clipStack.Count > 0 && m_clipStack.Peek().Item1 == currentSequence)
            {
                m_clipStack.Pop();
            }
        }
    }

    //Stops the current sequence, calling the "Abort" clip list immediately.
    //  This safely waits for the abort clips to finish before stopping the
    //  sequence
    public void AbortCurrentSequence()
    {
        if (m_sequenceStack.Count > 0)
        {
            StopCurrentClip();
            ClearCurrentSequenceClips();
            QueueEventFront(m_sequenceStack.Peek(), SequenceData.Event.kAbort);
            PlayNextClip();
        }
    }

    private void PlayClip(ClipData clip)
    {
        if (!m_clipPlaying)
        {
            m_playAudioRoutine = StartCoroutine(ClipRoutine(clip));
            return;
        }
    }

    private void StopCurrentClip()
    {
        if (m_currentAudio.isPlaying)
            m_currentAudio.Stop();

        m_clipPlaying = false;
        StopCoroutine(m_playAudioRoutine);
        m_textDisplayElement.text = "";
    }

    // An odd name for a function, but does exactly what it says.
    private void QueueEventFront(SequenceData sequence, SequenceData.Event eventType)
    {
        if(sequence.clipDictionary.ContainsKey(eventType))
        {
            // First we grab the list of clips for the given event type,
            //  then we load each clipData's file from JSON to a clipData
            //  object. Then that clip is added to the clip stack, along
            //  with its owner, and the current time.
            //
            // Because a stack works in the FIFO manner, We need to stack
            //  the clips in reverse order.
            List<string> clipList = sequence.clipDictionary[eventType];
            float time = Time.time;
            for (int i = clipList.Count - 1; i >= 0; --i)
            {
                ClipData clip = DialogFactory.Load<ClipData>(clipList[i]);
                
                if (clip == null)
                    continue;
                
                m_clipStack.Push(new ClipStackTuple(sequence, clip, time));    
            }
            return;
        }
        // Currently dont have a way to grab the Sequence name
        Debug.Log($"Sequence doesn't contain event: {eventType}");
    }

    private IEnumerator ClipRoutine(ClipData clip)
    {
        m_clipPlaying = true;

        // I thought I may need a timeout here in case something stalls.
        float timeRequested = Time.time;
        ResourceRequest request = Resources.LoadAsync(clip.audioFile);
        yield return new WaitUntil(() => { return request.isDone == true || Time.time - timeRequested >= m_resourceTimeout; });

        if(m_showDialog)
            m_textDisplayElement.text = clip.dialogText;
        
        // If the resource was loaded, play it, and display it for the
        //  duration of the clip.
        float playTime = 0f;
        if(request != null && request.isDone && request.asset != null)
        {
            m_currentAudio.clip = request.asset as AudioClip;
            playTime = m_currentAudio.clip.length;
            m_currentAudio.Play();
        }
        // Otherwise, display the text for a set time so that it won't break
        //  the immersion too much.
        else
        {
            playTime = m_timeoutDisplayTime;
        }
        yield return new WaitForSeconds(playTime);

        //Force stop the clip when its done.
        if(m_currentAudio.isPlaying)
            m_currentAudio.Stop();

        PopClip();

        Resources.UnloadUnusedAssets();

        m_textDisplayElement.text = "";
        m_clipPlaying = false;

        PlayNextClip();

        yield return null;
    }

#if UNITY_EDITOR

    // In this example, I attempted to serialize basic objects into their
    //  expected folders. As it turns out, after some research, I found
    //  that serializing a dictionary with empty lists fails for some reason.
    //  https://stackoverflow.com/questions/18426801/parse-json-to-a-dictionarystring-liststring
    public void GenerateExampleJsonFiles()
    {
        ClipData clipData = new ClipData();
        clipData.audioFile = "someAudio";
        clipData.dialogText = "Some Text.";

        DialogFactory.Save("Generated", clipData);

        SequenceData sequenceData = new SequenceData();
        sequenceData.GenerateJsonExample();

        DialogFactory.Save("Generated", sequenceData);
    }
#endif //UNITY_EDITOR
}
