using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

//배틀필드 맵 생성기 (에디터 전용)
//메뉴 : Tools → TankAttack → 배틀 맵 생성
//원본 지형(New Terrain)을 복사한 BattleTerrain에 산과 언덕을 만들고, 경사에 맞게 텍스처를 칠하고, 바위를 배치
//가운데에 큰 산이 있어서 스폰 지점끼리 직선으로 보이지 않음 → 포물선으로 넘겨 쏘는 포트리스 스타일
//시드(seed)가 같으면 항상 같은 맵이 만들어짐
public class BattleMapGenerator : ScriptableWizard
{
    const string originalTerrainPath = "Assets/05.Models/New Terrain.asset";  //타이틀 씬도 사용 → 건드리지 않음
    const string battleTerrainPath = "Assets/05.Models/BattleTerrain.asset";
    const string rockModelPath = "Assets/04.Images/Terrain Assets/Rocks/RockMesh.fbx";
    const string obstacleLayerName = "Obstacle";
    const int obstacleLayerIndex = 10;

    //지형 텍스처 레이어 번호 (New Terrain 기준)
    const int layerGrass = 0, layerGrassRock = 1, layerDirt = 2, layerRockPath = 3, layerCliff = 4;

    public int seed = 1234;

    [Header("플레이 영역 (이 안쪽만 바꿈, 바깥 산은 그대로)")]
    public float playRadius = 215f;

    [Header("가운데 큰 산")]
    public float centerMountainRadius = 60f;
    public float centerMountainHeight = 42f;

    [Header("산")]
    public int mountainCount = 7;
    public Vector2 mountainRadius = new Vector2(32f, 55f);
    public Vector2 mountainHeight = new Vector2(22f, 40f);
    public float ridgeStrength = 0.35f;  //능선/바위산 느낌 (0이면 매끈한 산)

    [Header("언덕")]
    public int hillCount = 60;
    public Vector2 hillRadius = new Vector2(12f, 30f);
    public Vector2 hillHeight = new Vector2(4f, 14f);
    public float noiseHeight = 1.5f;     //잔잔한 울퉁불퉁함

    [Header("바위")]
    public int rockCount = 90;
    public Vector2 rockSize = new Vector2(4f, 12f);
    public int rockHp = 45;

    [Header("스폰 지점 보호 (BattleManager와 같은 값)")]
    public float spawnRadius = 120f;
    public float spawnClearRadius = 30f;

    [MenuItem("Tools/TankAttack/배틀 맵 생성")]
    static void Open()
    {
        DisplayWizard<BattleMapGenerator>("배틀 맵 생성", "생성", "원래 지형으로 되돌리기");
    }

    void OnWizardCreate()
    {
        if (!OpenBattleScene()) return;
        Generate();
    }

    void OnWizardOtherButton()
    {
        if (!OpenBattleScene()) return;
        Terrain terrain = Object.FindAnyObjectByType<Terrain>();
        TerrainData original = AssetDatabase.LoadAssetAtPath<TerrainData>(originalTerrainPath);
        Undo.RecordObjects(new Object[] { terrain, terrain.GetComponent<TerrainCollider>() }, "Reset Terrain");
        terrain.terrainData = original;
        terrain.GetComponent<TerrainCollider>().terrainData = original;
        GameObject old = GameObject.Find("MapObjects");
        if (old != null) Undo.DestroyObjectImmediate(old);
        EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        EditorSceneManager.SaveScene(terrain.gameObject.scene);
        Debug.Log("[MapGenerator] 원래 지형으로 되돌림");
    }

    static bool OpenBattleScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path.EndsWith("scBattleField.unity")) return true;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        EditorSceneManager.OpenScene("Assets/01.Scenes/scBattleField.unity");
        return true;
    }

    public void Generate()
    {
        System.Random rng = new System.Random(seed);
        Terrain terrain = Object.FindAnyObjectByType<Terrain>();
        if (terrain == null) { Debug.LogError("[MapGenerator] Terrain이 없습니다"); return; }

        //1. 원본 지형을 복사해서 배틀 전용 지형 만들기 (매번 원본에서 새로 시작)
        AssetDatabase.DeleteAsset(battleTerrainPath);
        AssetDatabase.CopyAsset(originalTerrainPath, battleTerrainPath);
        TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(battleTerrainPath);
        terrain.terrainData = data;
        terrain.GetComponent<TerrainCollider>().terrainData = data;

        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;
        Vector2 center = new Vector2(origin.x + size.x * 0.5f, origin.z + size.z * 0.5f);
        List<Vector2> spawns = SpawnPoints(center);

        //2. 높이 (미터 단위로 계산한 뒤 마지막에 0~1로 바꿈)
        int res = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, res, res);
        float[,] meters = new float[res, res];
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
                meters[z, x] = heights[z, x] * size.y;

        Vector2 CellToWorld(int x, int z) =>
            new Vector2(origin.x + x / (float)(res - 1) * size.x, origin.z + z / (float)(res - 1) * size.z);

        float ridgeOffset = (float)rng.NextDouble() * 1000f;
        List<Vector3> peaks = new List<Vector3>(); //x, z, 반경 (겹치지 않게 배치하려고 기억)

        //2-1. 가운데 큰 산
        AddMountain(meters, res, center, centerMountainRadius, 1.2f, (float)rng.NextDouble() * Mathf.PI,
                    centerMountainHeight, ridgeOffset, CellToWorld);
        peaks.Add(new Vector3(center.x, center.y, centerMountainRadius));

        //2-2. 산 (스폰 지점과 다른 산에서 떨어진 곳)
        int mountains = 0;
        for (int tries = 0; mountains < mountainCount && tries < mountainCount * 60; tries++)
        {
            float r = Mathf.Lerp(mountainRadius.x, mountainRadius.y, (float)rng.NextDouble());
            Vector2 p = center + RandomInDisk(rng, playRadius - r * 0.5f);
            if (NearSpawn(p, spawns, spawnClearRadius + r)) continue;
            if (OverlapsPeak(p, r, peaks, 0.6f)) continue;
            float h = Mathf.Lerp(mountainHeight.x, mountainHeight.y, (float)rng.NextDouble());
            float aspect = Mathf.Lerp(1f, 1.8f, (float)rng.NextDouble());
            AddMountain(meters, res, p, r, aspect, (float)rng.NextDouble() * Mathf.PI, h, ridgeOffset, CellToWorld);
            peaks.Add(new Vector3(p.x, p.y, r));
            mountains++;
        }

        //2-3. 언덕 (산 사이를 채움, 산과는 조금 겹쳐도 됨 → 산기슭이 자연스러워짐)
        int hills = 0;
        for (int tries = 0; hills < hillCount && tries < hillCount * 40; tries++)
        {
            float r = Mathf.Lerp(hillRadius.x, hillRadius.y, (float)rng.NextDouble());
            Vector2 p = center + RandomInDisk(rng, playRadius - r);
            if (NearSpawn(p, spawns, spawnClearRadius + r)) continue;
            float h = Mathf.Lerp(hillHeight.x, hillHeight.y, (float)rng.NextDouble());
            float aspect = Mathf.Lerp(1f, 2.4f, (float)rng.NextDouble());
            AddHill(meters, res, p, r, aspect, (float)rng.NextDouble() * Mathf.PI, h, CellToWorld);
            hills++;
        }

        //2-4. 잔잔한 노이즈 (스폰 지점 근처는 평평하게 둠)
        float ox = (float)rng.NextDouble() * 1000f, oz = (float)rng.NextDouble() * 1000f;
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                Vector2 w = CellToWorld(x, z);
                float d = Vector2.Distance(w, center);
                if (d > playRadius || NearSpawn(w, spawns, spawnClearRadius * 0.7f)) continue;
                float fade = Mathf.Clamp01((playRadius - d) / 30f);
                meters[z, x] += (Mathf.PerlinNoise(ox + w.x * 0.03f, oz + w.y * 0.03f) - 0.5f) * 2f * noiseHeight * fade;
            }
        }

        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
                heights[z, x] = Mathf.Clamp01(meters[z, x] / size.y);
        data.SetHeights(0, 0, heights);

        //3. 경사에 맞게 텍스처 칠하기 (가파르면 절벽, 중간은 풀+바위, 완만하면 풀)
        PaintBySlope(data, origin, center, size);

        //4. 나무 : 스폰 근처, 가파른 곳은 지우고 나머지는 새 높이에 맞춤
        List<TreeInstance> trees = new List<TreeInstance>();
        foreach (TreeInstance tree in data.treeInstances)
        {
            Vector2 w = new Vector2(origin.x + tree.position.x * size.x, origin.z + tree.position.z * size.z);
            if (NearSpawn(w, spawns, spawnClearRadius)) continue;
            if (data.GetSteepness(tree.position.x, tree.position.z) > 35f) continue;
            TreeInstance t = tree;
            t.position.y = data.GetInterpolatedHeight(tree.position.x, tree.position.z) / size.y;
            trees.Add(t);
        }
        data.treeInstances = trees.ToArray();
        EditorUtility.SetDirty(data);

        //5. 바위 배치
        EnsureObstacleLayer();
        GameObject oldRoot = GameObject.Find("MapObjects");
        if (oldRoot != null) Object.DestroyImmediate(oldRoot);
        GameObject root = new GameObject("MapObjects");
        int rocks = PlaceRocks(rng, terrain, root.transform, center, spawns);

        //6. 미니맵에 바위도 보이게
        GameObject mm = GameObject.Find("MinimapCamera");
        if (mm != null) mm.GetComponent<Camera>().cullingMask |= 1 << obstacleLayerIndex;

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        EditorSceneManager.SaveScene(terrain.gameObject.scene);
        Debug.Log($"[MapGenerator] seed {seed} : 산 {mountains + 1}개, 언덕 {hills}개, 바위 {rocks}개, 나무 {trees.Count}그루");
    }

    #region 지형
    //산 : 언덕보다 크고 높고, 능선 노이즈로 울퉁불퉁 (바위산 느낌)
    static void AddMountain(float[,] meters, int res, Vector2 p, float radius, float aspect, float angle,
                            float height, float ridgeOffset, System.Func<int, int, Vector2> cellToWorld)
    {
        ForEachCellInEllipse(res, p, radius, aspect, angle, cellToWorld, (x, z, w, dist) =>
        {
            //가운데가 뾰족하도록 falloff를 제곱에 가깝게
            float falloff = Mathf.Pow((Mathf.Cos(dist * Mathf.PI) + 1f) * 0.5f, 1.4f);
            //ridged noise : |노이즈| 를 뒤집으면 날카로운 능선이 생김
            float n = 1f - Mathf.Abs(Mathf.PerlinNoise(ridgeOffset + w.x * 0.045f, ridgeOffset + w.y * 0.045f) * 2f - 1f);
            float ridge = Mathf.Lerp(1f, n, 0.35f + 0.65f * dist); //꼭대기보다 산기슭에 골짜기가 많이
            meters[z, x] += height * falloff * Mathf.Lerp(1f, ridge, 0.35f);
        });
    }

    static void AddHill(float[,] meters, int res, Vector2 p, float radius, float aspect, float angle,
                        float height, System.Func<int, int, Vector2> cellToWorld)
    {
        ForEachCellInEllipse(res, p, radius, aspect, angle, cellToWorld, (x, z, w, dist) =>
        {
            meters[z, x] += height * (Mathf.Cos(dist * Mathf.PI) + 1f) * 0.5f; //가운데 1 → 가장자리 0
        });
    }

    //회전한 타원 안의 높이맵 칸마다 action(x, z, 월드좌표, 0~1 거리) 실행
    static void ForEachCellInEllipse(int res, Vector2 p, float radius, float aspect, float angle,
                                     System.Func<int, int, Vector2> cellToWorld,
                                     System.Action<int, int, Vector2, float> action)
    {
        Vector2 a = cellToWorld(0, 0), b = cellToWorld(res - 1, res - 1);
        float cellSize = (b.x - a.x) / (res - 1);
        int cx = Mathf.RoundToInt((p.x - a.x) / cellSize), cz = Mathf.RoundToInt((p.y - a.y) / cellSize);
        int rc = Mathf.CeilToInt(radius * aspect / cellSize) + 1;
        float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);

        for (int z = Mathf.Max(0, cz - rc); z <= Mathf.Min(res - 1, cz + rc); z++)
        {
            for (int x = Mathf.Max(0, cx - rc); x <= Mathf.Min(res - 1, cx + rc); x++)
            {
                Vector2 w = cellToWorld(x, z);
                Vector2 d = w - p;
                float u = (d.x * cos + d.y * sin) / (radius * aspect);
                float v = (-d.x * sin + d.y * cos) / radius;
                float dist = Mathf.Sqrt(u * u + v * v);
                if (dist < 1f) action(x, z, w, dist);
            }
        }
    }

    void PaintBySlope(TerrainData data, Vector3 origin, Vector2 center, Vector3 size)
    {
        int ar = data.alphamapResolution;
        int layers = data.alphamapLayers;
        if (layers <= layerCliff) { Debug.LogWarning("[MapGenerator] 지형 텍스처 레이어가 부족해서 칠하기를 건너뜀"); return; }

        float[,,] alpha = data.GetAlphamaps(0, 0, ar, ar);
        for (int z = 0; z < ar; z++)
        {
            for (int x = 0; x < ar; x++)
            {
                float nx = (x + 0.5f) / ar, nz = (z + 0.5f) / ar;
                Vector2 w = new Vector2(origin.x + nx * size.x, origin.z + nz * size.z);
                float d = Vector2.Distance(w, center);
                if (d > playRadius + 15f) continue; //바깥 산은 원래 칠한 대로
                float blend = Mathf.Clamp01((playRadius + 15f - d) / 15f);

                float steep = data.GetSteepness(nx, nz);              //0 ~ 90도
                float height = data.GetInterpolatedHeight(nx, nz);    //미터

                float cliff = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(28f, 42f, steep));
                float rocky = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(14f, 28f, steep)) * (1f - cliff);
                float peak = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(22f, 38f, height)) * (1f - cliff) * 0.6f; //높은 곳은 바위길
                float grass = Mathf.Max(0f, 1f - cliff - rocky - peak);

                float[] target = new float[layers];
                target[layerGrass] = grass;
                target[layerGrassRock] = rocky;
                target[layerRockPath] = peak;
                target[layerCliff] = cliff;

                for (int l = 0; l < layers; l++)
                    alpha[z, x, l] = Mathf.Lerp(alpha[z, x, l], target[l], blend);
            }
        }
        data.SetAlphamaps(0, 0, alpha);
    }
    #endregion

    #region 바위
    int PlaceRocks(System.Random rng, Terrain terrain, Transform root, Vector2 center, List<Vector2> spawns)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(rockModelPath);
        if (model == null) { Debug.LogWarning("[MapGenerator] 바위 모델이 없습니다 : " + rockModelPath); return 0; }
        Transform rocksRoot = new GameObject("Rocks").transform;
        rocksRoot.SetParent(root, false);

        //모델 원래 크기 (가장 긴 변) → 원하는 크기로 맞추기 위해
        GameObject probe = (GameObject)PrefabUtility.InstantiatePrefab(model);
        Bounds mb = new Bounds(probe.transform.position, Vector3.zero);
        foreach (Renderer r in probe.GetComponentsInChildren<Renderer>()) mb.Encapsulate(r.bounds);
        float modelSize = Mathf.Max(mb.size.x, mb.size.y, mb.size.z, 0.01f);
        Object.DestroyImmediate(probe);

        TerrainData data = terrain.terrainData;
        Vector3 origin = terrain.transform.position;
        int placed = 0;
        for (int tries = 0; placed < rockCount && tries < rockCount * 30; tries++)
        {
            Vector2 p = center + RandomInDisk(rng, playRadius - 10f);
            if (NearSpawn(p, spawns, spawnClearRadius)) continue;
            //절벽처럼 너무 가파른 곳에는 놓지 않음 (떠 보임)
            float nx = (p.x - origin.x) / data.size.x, nz = (p.y - origin.z) / data.size.z;
            float steep = data.GetSteepness(nx, nz);
            if (steep > 32f) continue;
            float sizeM = Mathf.Lerp(rockSize.x, rockSize.y, (float)rng.NextDouble());

            GameObject rock = (GameObject)PrefabUtility.InstantiatePrefab(model, rocksRoot);
            rock.name = "Rock";
            SetLayer(rock, obstacleLayerIndex);
            rock.transform.localScale = new Vector3(
                Mathf.Lerp(0.8f, 1.3f, (float)rng.NextDouble()),
                Mathf.Lerp(0.6f, 1.1f, (float)rng.NextDouble()),
                Mathf.Lerp(0.8f, 1.3f, (float)rng.NextDouble())) * (sizeM / modelSize);
            //경사면에서는 땅에 맞춰 살짝 기울임
            Vector3 normal = data.GetInterpolatedNormal(nx, nz);
            rock.transform.rotation = Quaternion.FromToRotation(Vector3.up, Vector3.Lerp(Vector3.up, normal, 0.6f)) *
                                      Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            rock.transform.position = GroundPoint(terrain, p) - Vector3.up * sizeM * 0.3f; //땅에 묻기

            MeshFilter mf = rock.GetComponentInChildren<MeshFilter>();
            GameObject colGo = mf != null ? mf.gameObject : rock;
            if (colGo.GetComponent<Collider>() == null)
            {
                MeshCollider mc = colGo.AddComponent<MeshCollider>();
                mc.convex = true; //ClosestPoint(거리 계산)는 convex 콜라이더에서만 동작
            }
            //Destructible은 루트에 붙임 → 부서지면 바위 전체가 사라짐
            Destructible dst = rock.AddComponent<Destructible>();
            dst.hp = rockHp;
            dst.effectScale = 1.2f;
            placed++;
        }
        return placed;
    }
    #endregion

    #region 도우미
    List<Vector2> SpawnPoints(Vector2 center)
    {
        //BattleManager.SpawnMyTank 과 같은 계산 (45도부터 90도 간격)
        List<Vector2> list = new List<Vector2>();
        for (int i = 0; i < 4; i++)
        {
            float a = (45f + 90f * i) * Mathf.Deg2Rad;
            list.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * spawnRadius);
        }
        return list;
    }

    static bool NearSpawn(Vector2 p, List<Vector2> spawns, float dist)
    {
        foreach (Vector2 s in spawns)
            if (Vector2.Distance(p, s) < dist) return true;
        return false;
    }

    //다른 산과 너무 많이 겹치는지 (overlap 1이면 반경 합만큼 떨어져야 함)
    static bool OverlapsPeak(Vector2 p, float r, List<Vector3> peaks, float overlap)
    {
        foreach (Vector3 pk in peaks)
            if (Vector2.Distance(p, new Vector2(pk.x, pk.y)) < (r + pk.z) * overlap) return true;
        return false;
    }

    static Vector2 RandomInDisk(System.Random rng, float radius)
    {
        float a = (float)rng.NextDouble() * Mathf.PI * 2f;
        float r = Mathf.Sqrt((float)rng.NextDouble()) * radius; //sqrt : 가운데에 몰리지 않게
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
    }

    static Vector3 GroundPoint(Terrain t, Vector2 p)
    {
        Vector3 w = new Vector3(p.x, 0f, p.y);
        w.y = t.SampleHeight(w) + t.transform.position.y;
        return w;
    }

    static void SetLayer(GameObject go, int layer)
    {
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }

    static void EnsureObstacleLayer()
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layer = tagManager.FindProperty("layers").GetArrayElementAtIndex(obstacleLayerIndex);
        if (string.IsNullOrEmpty(layer.stringValue))
        {
            layer.stringValue = obstacleLayerName;
            tagManager.ApplyModifiedProperties();
        }
    }
    #endregion
}
