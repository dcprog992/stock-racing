using UnityEngine;

public static class DrivingSettings
{
    public const string SensitivityKey="StockRacing.SteeringSensitivity";
    public const string SteeringTypeKey="StockRacing.TouchSteering";
    public static float Sensitivity=>Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey,1.25f),.5f,2f);
    public static bool TouchWheel=>PlayerPrefs.GetInt(SteeringTypeKey,0)==1;
    public static void SetSensitivity(float value){PlayerPrefs.SetFloat(SensitivityKey,Mathf.Clamp(value,.5f,2f));PlayerPrefs.Save();}
    public static void SetTouchWheel(bool wheel){PlayerPrefs.SetInt(SteeringTypeKey,wheel?1:0);PlayerPrefs.Save();}
    // AI uses its original calibration. Only the local driver's steering gain changes.
    public static float SteeringAngle(float input,float maxAngle,float speedLimit,bool external)
    {return Mathf.Clamp(input*Mathf.Min(maxAngle,speedLimit)*(external?1:Sensitivity),-maxAngle,maxAngle);}
}
