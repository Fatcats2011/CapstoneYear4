using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using UnityEngine.InputSystem;
using System.Linq;
using System.Net;

public class QAManager : SingletonMonobehaviour<QAManager>
{
    [Tooltip("Toggle for recording data in the text document.")]
    private bool recordData = true;

    private List<QAHandler> handlers = new List<QAHandler>();

    private string fileName = "QAData.csv";

    static string dataDirectory;

    /// <summary>
    /// Folder the playtest CSV is written to. Never StreamingAssets, which ships inside builds.
    /// </summary>
    public static string DataDirectory
    {
        get { return dataDirectory ?? Path.Combine(Application.persistentDataPath, "Playtest"); }
        set { dataDirectory = value; }
    }

    private bool generateHeatmap = true;
    public bool GenerateHeatmap { get { return generateHeatmap; } }

    private string[] columns = { 
        "DateTime", 
        "Name", 
        "Placement", 
        "Score", 
        "#Easy", 
        "#Medium", 
        "#Hard", 
        "#Gold", 
        "Gold $$", 
        "#Deaths", 
        "#Boosts",
        "#Steals",
        "#GoldSteals"
    };

    // heatmap stuff
    [Tooltip("Each player needs their own trail so the colors are different. Set up each prefab here.")]
    [SerializeField] private GameObject[] trailObjects;

    private void Start()
    {
        GameManager.Instance.OnSwapResults += SendData;
        GameManager.Instance.OnSwapBegin += ResetHandlers;
    }

    private void OnDisable()
    {
        GameManager.Instance.OnSwapResults -= SendData;
        GameManager.Instance.OnSwapBegin -= ResetHandlers;
    }

    /// <summary>
    /// Adds a QAHandler to the list.
    /// </summary>
    /// <param name="inHandler">Handler to be added</param>
    public void AddHandler(QAHandler inHandler)
    {
        if (!handlers.Contains(inHandler))
        {
            handlers.Add(inHandler);
            inHandler.SetTrailObj(trailObjects[handlers.Count - 1]);
        }
    }

    /// <summary>
    /// Clears the QAHandler list and reinits it. For when a player un-readies up.
    /// </summary>
    /// <param name="playerInputs">Active players</param>
    public void UpdateQAHandlers(PlayerInput[] playerInputs)
    {
        handlers.Clear();
        foreach (PlayerInput player in playerInputs)
        {
            if (player != null)
            {
                handlers.Add(player.GetComponentInChildren<QAHandler>());
            }
        }
    }

    /// <summary>
    /// Gathers data from handlers to write to CSV.
    /// </summary>
    private void SendData()
    {
        // Playtest data is only recorded in the editor and development builds
        if (!DevTools.Enabled) { return; }

#if UNITY_EDITOR
        if (!recordData) { return; }
#endif
        DateTime dt = DateTime.Now;
        foreach(QAHandler handler in handlers)
        {
            string[] handlerData = handler.GetData();
            string[] sheetData = new string[handlerData.Length + 1];
            sheetData[0] = dt.ToString();
            for(int i=0;i<handlerData.Length; i++)
            {
                sheetData[i+1] = handlerData[i];
            }
            WriteCSV(fileName, sheetData);
        }
        WriteEmptyLine(fileName);
    }

    /// <summary>
    /// Writes to CSV file if exists, otherwise creates a CSV and writes to it.
    /// </summary>
    /// <param name="fileName">Name of the CSV file.</param>
    /// <param name="data">Array of data to write. Each element is a new column.</param>
    private void WriteCSV(string fileName, string[] data)
    {
        string filePath = Path.Combine(DataDirectory, fileName);

        try
        {
            Directory.CreateDirectory(DataDirectory);

            if (!File.Exists(filePath))
            {
                File.WriteAllLines(filePath, new[] { string.Join(",", columns) });
            }
            if(File.ReadLines(filePath).First<string>() != string.Join(",", columns))
            {
                File.WriteAllLines(filePath, new[] { string.Join(",", columns) });
            }

            File.AppendAllLines(filePath, new[] { string.Join(",", data) });

        }
        catch(IOException e) // mainly occurs when the file is open somewhere else
        {

            return;
        }
    }

    /// <summary>
    /// Writes an empty line in the CSV file.
    /// </summary>
    /// <param name="fileName">Name of CSV file.</param>
    private void WriteEmptyLine(string fileName)
    {
        string filePath = Path.Combine(DataDirectory, fileName);

        try
        {
            Directory.CreateDirectory(DataDirectory);

            if (!File.Exists(filePath))
            {
                File.WriteAllLines(filePath, new[] { string.Join(",", columns) });
            }

            File.AppendAllText(filePath, Environment.NewLine);
        }
        catch(IOException e) // mainly occurs when the file is open somewhere else
        {

            return;
        }
    }

    /// <summary>
    /// Resets the stats of the handlers.
    /// </summary>
    private void ResetHandlers()
    {
        foreach(QAHandler handler in handlers)
        {
            handler.ResetQA();
        }
    }
}
