using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class BakeSceneMapaVRNavMesh
{
    private const string ScenePath = "Assets/Scenes/SceneMapaVR.unity";
    private const string SurfaceName = "SceneMapaVR_NavMeshSurface";

    [MenuItem("Tools/Carpincho Smasher/Bake SceneMapaVR NavMesh")]
    public static void Bake()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);

        GameObject surfaceObject = GameObject.Find(SurfaceName);
        if (surfaceObject == null)
        {
            surfaceObject = new GameObject(SurfaceName);
        }

        var surface = surfaceObject.GetComponent<NavMeshSurface>();
        if (surface == null)
        {
            surface = surfaceObject.AddComponent<NavMeshSurface>();
        }

        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~0;
        surface.defaultArea = 0;
        surface.ignoreNavMeshAgent = true;
        surface.ignoreNavMeshObstacle = true;
        surface.minRegionArea = 2f;

        surface.BuildNavMesh();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        string navMeshFolder = Path.Combine("Assets", "Scenes", "SceneMapaVR");
        Debug.Log($"[BakeSceneMapaVRNavMesh] NavMesh baked for {ScenePath}. Assets folder: {navMeshFolder}");
    }
}
