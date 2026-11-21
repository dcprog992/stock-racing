using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public static class GameFlow
{
    public const string MenuScene="MainMenu",RaceScene="YoylelandSpeedway";
    public static bool Pending,Training;
    public static int Laps=1;
    public static bool Loading;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset(){Pending=false;Training=false;Laps=1;Loading=false;SceneManager.sceneLoaded-=Loaded;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register(){SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InitialScene()=>Loaded(SceneManager.GetActiveScene(),LoadSceneMode.Single);
    static void Loaded(Scene scene,LoadSceneMode mode)
    {
        if(Object.FindAnyObjectByType<GameMenu>())return;
        Loading=false;Time.timeScale=1;AudioListener.pause=false;
        if(!Object.FindAnyObjectByType<EventSystem>())new GameObject("UI EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
        new GameObject("Game front end").AddComponent<GameMenu>();
        GameGraphics.Apply();
    }
    public static void Play(bool training,int laps,GameMenu menu)
    {
        if(Loading)return;
        Training=training;Laps=laps==3?3:1;Pending=true;
        menu.StartCoroutine(Load(RaceScene,menu));
    }
    public static void Home(GameMenu menu)
    {
        if(Loading)return;
        Pending=false;menu.StartCoroutine(Load(MenuScene,menu));
    }
    static IEnumerator Load(string scene,GameMenu menu)
    {
        Loading=true;Time.timeScale=1;AudioListener.pause=false;menu.ShowLoading(0);
        yield return null;
        var op=SceneManager.LoadSceneAsync(scene);op.allowSceneActivation=false;
        while(op.progress<.9f){menu.ShowLoading(Mathf.Clamp01(op.progress/.9f));yield return null;}
        menu.ShowLoading(1);yield return null;op.allowSceneActivation=true;
    }
}
