using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class MobileGraphics:MonoBehaviour
{
    private readonly HashSet<CarController> optimized=new();
    private readonly List<Mesh> generated=new();
    private float nextCheck;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register(){UnityEngine.SceneManagement.SceneManager.sceneLoaded-=OnLoaded;UnityEngine.SceneManagement.SceneManager.sceneLoaded+=OnLoaded;}
    static void OnLoaded(UnityEngine.SceneManagement.Scene scene,UnityEngine.SceneManagement.LoadSceneMode mode)=>Install();
    static void Install()
    {
#if UNITY_ANDROID || NASCAR_MOBILE
        if(!FindAnyObjectByType<CarController>() || FindAnyObjectByType<MobileGraphics>())return;
        new GameObject("Android graphics profile").AddComponent<MobileGraphics>();
#endif
    }
    IEnumerator Start()
    {
        Screen.sleepTimeout=SleepTimeout.NeverSleep;
        GameGraphics.Apply();
        yield return null;
        var track=GameObject.Find("Yoyleland International Speedway");
        if(track)
        {
            var parts=new List<GameObject>();
            foreach(var filter in track.GetComponentsInChildren<MeshFilter>())
                if(filter.sharedMesh && filter.sharedMesh.isReadable && filter.TryGetComponent<MeshRenderer>(out var r) && r.enabled && !r.isPartOfStaticBatch)parts.Add(filter.gameObject);
            if(parts.Count>0)StaticBatchingUtility.Combine(parts.ToArray(),track);
        }
        OptimizeCars();
    }
    void Update(){if(Time.unscaledTime>=nextCheck){nextCheck=Time.unscaledTime+1f;OptimizeCars();}}
    void OptimizeCars()
    {
        foreach(var car in FindObjectsByType<CarController>())
        {
            if(optimized.Contains(car))continue;
            optimized.Add(car);BatchBody(car);
        }
    }
    void BatchBody(CarController car)
    {
        // The player's detailed interior uses independently animated model parts.
        if(!car.IsExternallyDriven)return;
        var moving=new List<Transform>(car.wheelMeshes);
        foreach(var wheel in car.GetComponentsInChildren<SteeringWheelVisual>(true))moving.Add(wheel.transform);
        var groups=new Dictionary<Material,List<CombineInstance>>();
        var originals=new List<MeshRenderer>();
        foreach(var filter in car.GetComponentsInChildren<MeshFilter>(true))
        {
            if(!filter.sharedMesh || !filter.sharedMesh.isReadable || !filter.TryGetComponent<MeshRenderer>(out var renderer) || !renderer.enabled)continue;
            if(filter.name.Contains("Rear-view") || filter.name.Contains("mirror glass"))continue;
            bool animated=false;
            foreach(var item in moving)if(item && (filter.transform==item || filter.transform.IsChildOf(item))){animated=true;break;}
            if(animated || !filter.gameObject.activeInHierarchy)continue;
            var materials=renderer.sharedMaterials;
            if(materials.Length<filter.sharedMesh.subMeshCount)continue;
            for(int i=0;i<filter.sharedMesh.subMeshCount;i++)
            {
                var material=materials[i];if(!material)continue;
                if(!groups.TryGetValue(material,out var instances))groups[material]=instances=new List<CombineInstance>();
                instances.Add(new CombineInstance{mesh=filter.sharedMesh,subMeshIndex=i,transform=car.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix});
            }
            originals.Add(renderer);
        }
        if(groups.Count==0)return;
        foreach(var group in groups)
        {
            var mesh=new Mesh{name="Mobile body batch",indexFormat=IndexFormat.UInt32};
            mesh.CombineMeshes(group.Value.ToArray(),true,true);mesh.RecalculateBounds();mesh.UploadMeshData(true);generated.Add(mesh);
            var part=new GameObject("Mobile body batch",typeof(MeshFilter),typeof(MeshRenderer));part.transform.SetParent(car.transform,false);
            part.GetComponent<MeshFilter>().sharedMesh=mesh;part.GetComponent<MeshRenderer>().sharedMaterial=group.Key;
        }
        foreach(var renderer in originals)renderer.enabled=false;
    }
    void OnDestroy(){foreach(var mesh in generated)if(mesh)Destroy(mesh);}
}
