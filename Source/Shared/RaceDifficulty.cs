using UnityEngine;

public static class RaceDifficulty
{
    public enum Level { Easy, Medium, Hard, Hardcore }
    public const string Key="StockRacing.OpponentDifficulty";
    public static Level Current=>(Level)Mathf.Clamp(PlayerPrefs.GetInt(Key,1),0,3);
    public static void Set(Level level){PlayerPrefs.SetInt(Key,Mathf.Clamp((int)level,0,3));PlayerPrefs.Save();}
    public static string Name(Level level)=>level switch{Level.Easy=>"ЛЁГКИЙ",Level.Hard=>"СЛОЖНЫЙ",Level.Hardcore=>"ХАРДКОР",_=>"СРЕДНИЙ"};
    public static string Description(Level level)=>level switch
    {
        Level.Easy=>"Спокойнее разгон и темп. Соперники оставляют больше дистанции.",
        Level.Hard=>"Двигатель мощнее на 14%. Плотнее борьба и раньше обгоны.",
        Level.Hardcore=>"Двигатель мощнее на 28%. Максимальный темп и плотная борьба.",
        _=>"Мощность как у твоей машины. Сбалансированная дистанция."
    };
    public readonly struct Profile
    {
        public readonly float Power,Speed,Throttle,Headway,PassClosing,Clearance,Reaction;
        public Profile(float power,float speed,float throttle,float headway,float passClosing,float clearance,float reaction)
        {Power=power;Speed=speed;Throttle=throttle;Headway=headway;PassClosing=passClosing;Clearance=clearance;Reaction=reaction;}
    }
    public static Profile For(Level level)=>level switch
    {
        Level.Easy=>new(.82f,.92f,1f,.30f,4f,7f,.55f),
        Level.Hard=>new(1.14f,1.04f,1f,.18f,2f,7.5f,0f),
        Level.Hardcore=>new(1.28f,1.065f,1f,.15f,1.7f,7.5f,0f),
        // Medium uses the same engine and gearbox as the player.
        _=>new(1f,1f,1f,.20f,2.5f,7f,0f)
    };
}
