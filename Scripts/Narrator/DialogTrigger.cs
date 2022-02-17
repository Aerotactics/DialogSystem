using UnityEngine;

// This trigger simply tells the Dialog Manager to play a sequence file.
public class DialogTrigger : MonoBehaviour
{
    [SerializeField] private DialogManager m_dialogManager = null;
    [SerializeField] private string m_sequenceFile = null;

    private void OnTriggerEnter(Collider other)
    {
        if(m_sequenceFile != null && other.CompareTag("Player"))
        {
            m_dialogManager.PlaySequence(m_sequenceFile);
        }
    }
}
