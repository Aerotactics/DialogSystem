using System;
using System.IO;
using UnityEngine;

public class DialogFactory : MonoBehaviour
{
    private static string s_sequenceDataPath = "/Dialog/Sequences/{0}.json";
    private static string s_clipDataPath = "/Dialog/Clips/{0}.json";

    private void Start()
    {
        // Insert the application path to our folder path
        string applicationDataPath = Application.dataPath;
        s_sequenceDataPath = s_sequenceDataPath.Insert(0, applicationDataPath);
        s_clipDataPath = s_clipDataPath.Insert(0, applicationDataPath);
    }

    // These save and load Dialog files
    public static bool Save<Type>(string name, Type dialogObject) where Type : class
    {
        string file = JsonUtility.ToJson(dialogObject, true);

        //if the file is an empty Json file, it failed somehow
        if (file.Length <= 3)
            return false;

        string pathFormat = typeof(Type) == typeof(SequenceData) ? s_sequenceDataPath : s_clipDataPath;
        string filePath = string.Format(pathFormat, name);
        File.WriteAllText(filePath, file);
        return true;
    }

    public static Type Load<Type>(string name) where Type : class
    {
        string pathFormat = typeof(Type) == typeof(SequenceData) ? s_sequenceDataPath : s_clipDataPath;
        string filePath = string.Format(pathFormat, name);
        
        if (!File.Exists(filePath))
        {
            Console.WriteLine("DialogFactory: File Not Found: " + filePath);
            return null;
        }

        string file = File.ReadAllText(filePath);
        Type dialogObject = JsonUtility.FromJson<Type>(file);

        //If the loaded object is defaulted, we don't want it
        if(dialogObject.Equals(default(Type)))
        {
            Console.WriteLine("DialogFactory: Failed to parse Json: " + filePath);
            return null;
        }

        return dialogObject;
    }
}
