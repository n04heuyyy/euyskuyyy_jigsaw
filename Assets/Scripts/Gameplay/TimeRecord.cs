using System.Collections.Generic;

[System.Serializable]
public class BestTimeRecord
{
    public string gridSize;  
    public string speedMode; 
    public float timeInSeconds;
    public string playerName;
}

[System.Serializable]
public class BestTimeCollection
{
    // FIX CS0426 & CS0029: Tipe data List langsung mengarah ke BestTimeRecord secara lurus
    public List<BestTimeRecord> records = new List<BestTimeRecord>();
}