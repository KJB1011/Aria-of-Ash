using UnityEngine;

public class ObjectForCutscene : MonoBehaviour
{
    public void DestroyObject()
    {
        gameObject.SetActive(false);
    }

}
