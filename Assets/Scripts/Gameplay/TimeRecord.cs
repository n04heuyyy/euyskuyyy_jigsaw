using System.Collections.Generic;

[System.Serializable]
public class BestTimeRecord // Simpanan data record time
{
    public string gridSize;  
    public string speedMode; 
    public float timeInSeconds;
    public string playerName;
}

[System.Serializable]
public class BestTimeCollection
{
    public List<BestTimeRecord> records = new List<BestTimeRecord>();
}