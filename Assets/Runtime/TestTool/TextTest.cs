using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TextTest : MonoBehaviour
{

    public DialogueSeriesSO dialogueSeries;

    void OnEnable()
    {
        Invoke("PlayDialogue", 3f);
    }

    void PlayDialogue()
    {
        if(dialogueSeries && DialogueManager.Instance)
            DialogueManager.Instance.PlaySeries(dialogueSeries);
    }
}
