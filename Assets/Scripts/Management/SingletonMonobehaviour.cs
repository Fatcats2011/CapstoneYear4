/* Script Created by: Alex Fischer
 * Year: 2023
 * Purpose: To act as a universal singleton option to derrive from. To be used in any project 
 */
using UnityEngine;

public abstract class SingletonMonobehaviour<T> : MonoBehaviour where T : MonoBehaviour
{
    internal static T instance;
    [Space(10)]
    public bool dontDestroyOnLoad;

    public static T Instance
    {
        get { return instance; }
    }

    protected virtual void Awake()
    {
        // Normal singleton reference
        if (instance == null)
        {
            instance = this as T;
        }
        else
        {
            if (this)
            {
                Destroy(gameObject);
            }
        }

        // If true, allow for scene loading
        if (dontDestroyOnLoad)
        {
            T[] objs = FindObjectsOfType<T>();
            if (objs.Length > 1)
            {
                if (this)
                {
                    Destroy(gameObject);
                }
            }


            DontDestroyOnLoad(this.gameObject);
        }
    }
}
