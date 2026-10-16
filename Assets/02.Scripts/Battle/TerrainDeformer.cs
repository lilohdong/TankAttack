using UnityEngine;

//포탄이 떨어진 곳의 지형을 둥글게 파냄 (포트리스 스타일)
//Terrain 오브젝트에 붙임
[RequireComponent(typeof(Terrain))]
public class TerrainDeformer : MonoBehaviour
{
    [Tooltip("아래로 팔 수 있는 깊이. 지형 높이가 0에 가까우면 더 팔 수 없어서, 시작할 때 전체를 이만큼 올리고 Terrain을 그만큼 내림 (보이는 모습은 같음)")]
    public float digReserve = 20f;

    Terrain terrain;
    TerrainData data;

    void Awake()
    {
        terrain = GetComponent<Terrain>();
        //원본 TerrainData 에셋을 그대로 바꾸면 Play를 끝내도 지형이 파인 채로 남음
        //→ 복사본을 만들어서 그것만 수정
        data = Instantiate(terrain.terrainData);
        terrain.terrainData = data;
        GetComponent<TerrainCollider>().terrainData = data;
        AddDigReserve();
    }

    void AddDigReserve()
    {
        if (digReserve <= 0f) return;
        int res = data.heightmapResolution;
        float add01 = digReserve / data.size.y;

        float[,] heights = data.GetHeights(0, 0, res, res);
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
                heights[z, x] = Mathf.Min(1f, heights[z, x] + add01);
        data.SetHeights(0, 0, heights);

        //나무도 같이 올림 (나무 높이는 0~1 비율로 저장됨)
        TreeInstance[] trees = data.treeInstances;
        for (int i = 0; i < trees.Length; i++)
            trees[i].position.y = Mathf.Min(1f, trees[i].position.y + add01);
        data.treeInstances = trees;

        terrain.transform.position -= Vector3.up * digReserve;
    }

    //worldPos 주변 radius 안을 depth 만큼 파냄 (가운데가 가장 깊고 가장자리로 갈수록 얕게)
    public void Deform(Vector3 worldPos, float radius, float depth)
    {
        if (radius <= 0f || depth <= 0f) return;

        Vector3 local = worldPos - terrain.transform.position;
        Vector3 size = data.size;
        int res = data.heightmapResolution;

        //월드 좌표 → 높이맵 칸 번호
        int cx = Mathf.RoundToInt(local.x / size.x * (res - 1));
        int cz = Mathf.RoundToInt(local.z / size.z * (res - 1));
        int r = Mathf.CeilToInt(radius / size.x * (res - 1));

        int x0 = Mathf.Clamp(cx - r, 0, res - 1);
        int z0 = Mathf.Clamp(cz - r, 0, res - 1);
        int x1 = Mathf.Clamp(cx + r, 0, res - 1);
        int z1 = Mathf.Clamp(cz + r, 0, res - 1);
        int w = x1 - x0 + 1;
        int h = z1 - z0 + 1;
        if (w <= 1 || h <= 1) return; //지형 밖

        //GetHeights는 [z, x] 순서, 값은 0~1 (실제 높이 / size.y)
        float[,] heights = data.GetHeights(x0, z0, w, h);
        float depth01 = depth / size.y;

        for (int z = 0; z < h; z++)
        {
            for (int x = 0; x < w; x++)
            {
                float wx = (x0 + x) / (float)(res - 1) * size.x;
                float wz = (z0 + z) / (float)(res - 1) * size.z;
                float dist = Vector2.Distance(new Vector2(wx, wz), new Vector2(local.x, local.z));
                if (dist >= radius) continue;

                //cos 곡선 : 가운데 1 → 가장자리 0 으로 부드럽게
                float falloff = (Mathf.Cos(dist / radius * Mathf.PI) + 1f) * 0.5f;
                heights[z, x] = Mathf.Max(0f, heights[z, x] - depth01 * falloff);
            }
        }
        //SetHeights를 하면 TerrainCollider도 같이 바뀜 → 탱크가 파인 곳으로 떨어짐
        data.SetHeights(x0, z0, heights);
    }
}
