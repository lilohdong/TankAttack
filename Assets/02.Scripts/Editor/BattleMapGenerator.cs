using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

//배틀필드 맵 생성기 (에디터 전용)
//메뉴 : Tools → TankAttack → 배틀 맵 생성
//원본 지형(New Terrain)을 복사한 BattleTerrain에 언덕을 만들고, 가운데에 시가지(건물 블록), 곳곳에 바위를 배치
//시드(seed)가 같으면 항상 같은 맵이 만들어짐
public class BattleMapGenerator : ScriptableWizard
{
    const string originalTerrainPath = "Assets/05.Models/New Terrain.asset";  //타이틀 씬도 사용 → 건드리지 않음
    const string battleTerrainPath = "Assets/05.Models/BattleTerrain.asset";
    const string rockModelPath = "Assets/04.Images/Terrain Assets/Rocks/RockMesh.fbx";
    const string materialFolder = "Assets/04.Images/Materials";
    const string obstacleLayerName = "Obstacle";
    const int obstacleLayerIndex = 10;

    public int seed = 1234;

    [Header("플레이 영역 (이 안쪽만 바꿈, 바깥 산은 그대로)")]
    public float playRadius = 210f;

    [Header("언덕")]
    public int hillCount = 80;
    public Vector2 hillRadius = new Vector2(14f, 38f);
    public Vector2 hillHeight = new Vector2(6f, 22f);
    public float noiseHeight = 1.5f;     //잔잔한 울퉁불퉁함

    [Header("시가지 (맵 가운데)")]
    public float townRadius = 75f;
    public int blockGrid = 5;            //5 x 5 블록
    public float blockSize = 26f;        //블록 한 칸 (도로 포함)
    public float streetWidth = 9f;
    public Vector2 buildingHeight = new Vector2(6f, 20f);
    [Range(0f, 1f)] public float emptyLotChance = 0.2f;  //빈 땅 (폐허)
    public int buildingHp = 90;

    [Header("바위")]
    public int rockCount = 60;
    public Vector2 rockSize = new Vector2(5f, 13f);
    public int rockHp = 45;

    [Header("스폰 지점 보호 (BattleManager와 같은 값)")]
    public float spawnRadius = 120f;
    public float spawnClearRadius = 28f;

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

        //2. 높이 수정
        int res = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, res, res);
        float[,] meters = new float[res, res];
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
                meters[z, x] = heights[z, x] * size.y;

        Vector2 CellToWorld(int x, int z) =>
            new Vector2(origin.x + x / (float)(res - 1) * size.x, origin.z + z / (float)(res - 1) * size.z);

        //2-1. 시가지 땅 평평하게 (가장자리는 부드럽게 이어짐)
        float townGround = AverageHeight(meters, res, center, townRadius, CellToWorld);
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float d = Vector2.Distance(CellToWorld(x, z), center);
                float edge = townRadius + 20f;
                if (d >= edge) continue;
                float t = d <= townRadius ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (d - townRadius) / 20f);
                meters[z, x] = Mathf.Lerp(meters[z, x], townGround, t);
            }
        }

        //2-2. 언덕 (타원 모양, 방향 랜덤 → 능선처럼 길쭉한 것도 생김)
        int made = 0;
        for (int tries = 0; made < hillCount && tries < hillCount * 30; tries++)
        {
            float r = Mathf.Lerp(hillRadius.x, hillRadius.y, (float)rng.NextDouble());
            Vector2 p = center + RandomInDisk(rng, playRadius - r);
            if (Vector2.Distance(p, center) < townRadius + r + 8f) continue;   //시가지 침범 금지
            if (NearSpawn(p, spawns, spawnClearRadius + r)) continue;          //스폰 지점 보호
            float height = Mathf.Lerp(hillHeight.x, hillHeight.y, (float)rng.NextDouble());
            float aspect = Mathf.Lerp(1f, 2.4f, (float)rng.NextDouble());
            float angle = (float)rng.NextDouble() * Mathf.PI;
            AddHill(meters, res, p, r, aspect, angle, height, CellToWorld);
            made++;
        }

        //2-3. 잔잔한 노이즈 (시가지 밖)
        float ox = (float)rng.NextDouble() * 1000f, oz = (float)rng.NextDouble() * 1000f;
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                Vector2 w = CellToWorld(x, z);
                float d = Vector2.Distance(w, center);
                if (d > playRadius || d < townRadius + 20f) continue;
                float fade = Mathf.Clamp01((playRadius - d) / 30f);
                meters[z, x] += (Mathf.PerlinNoise(ox + w.x * 0.03f, oz + w.y * 0.03f) - 0.5f) * 2f * noiseHeight * fade;
            }
        }

        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
                heights[z, x] = Mathf.Clamp01(meters[z, x] / size.y);
        data.SetHeights(0, 0, heights);

        //3. 나무 : 시가지/스폰 근처는 지우고, 나머지는 새 높이에 맞춤
        List<TreeInstance> trees = new List<TreeInstance>();
        foreach (TreeInstance tree in data.treeInstances)
        {
            Vector2 w = new Vector2(origin.x + tree.position.x * size.x, origin.z + tree.position.z * size.z);
            if (Vector2.Distance(w, center) < townRadius + 15f) continue;
            if (NearSpawn(w, spawns, spawnClearRadius)) continue;
            TreeInstance t = tree;
            t.position.y = data.GetInterpolatedHeight(tree.position.x, tree.position.z) / size.y;
            trees.Add(t);
        }
        data.treeInstances = trees.ToArray();
        EditorUtility.SetDirty(data);

        //4. 오브젝트 배치
        EnsureObstacleLayer();
        GameObject oldRoot = GameObject.Find("MapObjects");
        if (oldRoot != null) Object.DestroyImmediate(oldRoot);
        GameObject root = new GameObject("MapObjects");

        int buildings = PlaceTown(rng, terrain, root.transform, center);
        Physics.SyncTransforms(); //바위가 건물과 겹치는지 검사하려면 필요
        int rocks = PlaceRocks(rng, terrain, root.transform, center, spawns);

        //5. 미니맵에 엄폐물도 보이게
        GameObject mm = GameObject.Find("MinimapCamera");
        if (mm != null) mm.GetComponent<Camera>().cullingMask |= 1 << obstacleLayerIndex;

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        EditorSceneManager.SaveScene(terrain.gameObject.scene);
        Debug.Log($"[MapGenerator] seed {seed} : 언덕 {made}개, 건물 {buildings}개, 바위 {rocks}개, 나무 {trees.Count}그루");
    }

    #region 지형
    static void AddHill(float[,] meters, int res, Vector2 p, float radius, float aspect, float angle,
                        float height, System.Func<int, int, Vector2> cellToWorld)
    {
        //영향 범위의 칸만 계산
        Vector2 a = cellToWorld(0, 0), b = cellToWorld(res - 1, res - 1);
        float cellSize = (b.x - a.x) / (res - 1);
        float reach = radius * aspect;
        int cx = Mathf.RoundToInt((p.x - a.x) / cellSize), cz = Mathf.RoundToInt((p.y - a.y) / cellSize);
        int rc = Mathf.CeilToInt(reach / cellSize) + 1;
        float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);

        for (int z = Mathf.Max(0, cz - rc); z <= Mathf.Min(res - 1, cz + rc); z++)
        {
            for (int x = Mathf.Max(0, cx - rc); x <= Mathf.Min(res - 1, cx + rc); x++)
            {
                Vector2 d = cellToWorld(x, z) - p;
                //회전한 타원 좌표계에서의 거리 (1 = 가장자리)
                float u = (d.x * cos + d.y * sin) / (radius * aspect);
                float v = (-d.x * sin + d.y * cos) / radius;
                float dist = Mathf.Sqrt(u * u + v * v);
                if (dist >= 1f) continue;
                float falloff = (Mathf.Cos(dist * Mathf.PI) + 1f) * 0.5f; //가운데 1 → 가장자리 0
                meters[z, x] += height * falloff;
            }
        }
    }

    static float AverageHeight(float[,] meters, int res, Vector2 center, float radius, System.Func<int, int, Vector2> cellToWorld)
    {
        float sum = 0f;
        int count = 0;
        for (int z = 0; z < res; z += 2)
        {
            for (int x = 0; x < res; x += 2)
            {
                if (Vector2.Distance(cellToWorld(x, z), center) > radius) continue;
                sum += meters[z, x];
                count++;
            }
        }
        return count > 0 ? sum / count : 0f;
    }
    #endregion

    #region 오브젝트
    int PlaceTown(System.Random rng, Terrain terrain, Transform root, Vector2 center)
    {
        Transform town = new GameObject("Town").transform;
        town.SetParent(root, false);
        Material[] mats = BuildingMaterials();

        int count = 0;
        float half = (blockGrid - 1) * 0.5f;
        for (int i = 0; i < blockGrid; i++)
        {
            for (int j = 0; j < blockGrid; j++)
            {
                Vector2 blockCenter = center + new Vector2((i - half) * blockSize, (j - half) * blockSize);
                if (Vector2.Distance(blockCenter, center) > townRadius - blockSize * 0.4f) continue;
                if (i == Mathf.RoundToInt(half) && j == Mathf.RoundToInt(half)) continue; //가운데는 광장
                if (rng.NextDouble() < emptyLotChance) continue;

                //한 블록에 건물 1~2개
                float lot = blockSize - streetWidth;
                int n = rng.NextDouble() < 0.5 ? 1 : 2;
                for (int k = 0; k < n; k++)
                {
                    float w = n == 1 ? Mathf.Lerp(lot * 0.6f, lot, (float)rng.NextDouble()) : lot * 0.45f;
                    float d = Mathf.Lerp(lot * 0.5f, lot, (float)rng.NextDouble());
                    float h = Mathf.Lerp(buildingHeight.x, buildingHeight.y, (float)rng.NextDouble());
                    float offset = n == 1 ? 0f : (k == 0 ? -lot * 0.27f : lot * 0.27f);
                    Vector2 p = blockCenter + new Vector2(offset, 0f);

                    GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    b.name = "Building";
                    b.layer = obstacleLayerIndex;
                    b.transform.SetParent(town, false);
                    float ground = terrain.SampleHeight(new Vector3(p.x, 0f, p.y)) + terrain.transform.position.y;
                    b.transform.position = new Vector3(p.x, ground + h * 0.5f - 0.5f, p.y);
                    b.transform.localScale = new Vector3(w, h, d);
                    b.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 6f - 3f, 0f); //살짝 비뚤게
                    b.GetComponent<Renderer>().sharedMaterial = mats[rng.Next(mats.Length)];
                    b.AddComponent<Destructible>().hp = buildingHp + Mathf.RoundToInt(h * 2f); //높을수록 튼튼
                    count++;
                }
            }
        }
        return count;
    }

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

        int placed = 0;
        for (int tries = 0; placed < rockCount && tries < rockCount * 30; tries++)
        {
            Vector2 p = center + RandomInDisk(rng, playRadius - 10f);
            if (NearSpawn(p, spawns, spawnClearRadius)) continue;
            //시가지 안에서는 도로 위에만 (건물과 겹치지 않게) 작은 잔해로
            bool inTown = Vector2.Distance(p, center) < townRadius;
            float sizeM = Mathf.Lerp(rockSize.x, rockSize.y, (float)rng.NextDouble()) * (inTown ? 0.5f : 1f);
            if (inTown && Physics.CheckSphere(GroundPoint(terrain, p) + Vector3.up * 2f, sizeM, 1 << obstacleLayerIndex)) continue;

            GameObject rock = (GameObject)PrefabUtility.InstantiatePrefab(model, rocksRoot);
            rock.name = "Rock";
            SetLayer(rock, obstacleLayerIndex);
            rock.transform.localScale = Vector3.one * (sizeM / modelSize) *
                                        Mathf.Lerp(0.8f, 1.2f, (float)rng.NextDouble());
            rock.transform.rotation = Quaternion.Euler((float)rng.NextDouble() * 20f - 10f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 20f - 10f);
            rock.transform.position = GroundPoint(terrain, p) - Vector3.up * sizeM * 0.25f; //땅에 살짝 묻기

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
            Physics.SyncTransforms(); //다음 바위의 CheckSphere가 방금 놓은 것도 알도록
            placed++;
        }
        return placed;
    }

    Material[] BuildingMaterials()
    {
        Color[] colors =
        {
            new Color(0.62f, 0.60f, 0.56f), //콘크리트
            new Color(0.50f, 0.48f, 0.45f), //어두운 콘크리트
            new Color(0.70f, 0.64f, 0.52f), //베이지
            new Color(0.55f, 0.42f, 0.36f), //벽돌
        };
        Material[] mats = new Material[colors.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            string path = $"{materialFolder}/Building{i + 1}.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = colors[i];
            m.SetFloat("_Glossiness", 0.1f);
            EditorUtility.SetDirty(m);
            mats[i] = m;
        }
        return mats;
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
