using UnityEngine;

public static class SpeedUnits
{
    const string Key="StockRacing.SpeedUnits";
    public static bool Miles=>PlayerPrefs.GetInt(Key,0)==1;
    public static string Label=>Miles?"MPH":"КМ/Ч";
    public static float Display(float kmh)=>Miles?kmh*.6213712f:kmh;
    public static void SetMiles(bool value){PlayerPrefs.SetInt(Key,value?1:0);PlayerPrefs.Save();}
}
