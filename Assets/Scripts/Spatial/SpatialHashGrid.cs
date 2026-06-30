using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 空间哈希网格客户端接口，所有需要被空间哈希网格管理的实体需实现此接口
/// </summary>
public interface ISpatialHashGridClient
{
    /// <summary>实体当前位置</summary>
    Vector2 Position { get; }
    /// <summary>实体碰撞体大小（用于判断占用的网格范围）</summary>
    Vector2 Size { get; }
    /// <summary>记录实体在每个网格单元格中的索引（键：单元格索引，值：实体在该单元格列表中的位置）</summary>
    Dictionary<int, int> ListIndexByCellIndex { get; set; }
    /// <summary>查询标识，用于避免同一查询中重复添加实体</summary>
    int QueryID { get; set; }
}

/// <summary>
/// 空间哈希网格管理器，用于高效管理游戏世界中的实体位置，支持快速查询附近实体
/// </summary>
public class SpatialHashGrid
{
    /// <summary>网格单元格数组，每个单元格存储属于该格子的实体列表</summary>
    //一个列表表示一个网格，长*宽就为总网格数量，也就是该数组的容量
    protected List<ISpatialHashGridClient>[] cells;
    /// <summary>网格边界，[0]为左下角，[1]为右上角</summary>
    protected Vector2[] bounds;
    /// <summary>网格的维度（x方向格子数，y方向格子数）</summary>
    protected Vector2Int dimensions;
    /// <summary>查询ID计数器，用于生成唯一查询标识</summary>
    protected int queryIds;

    /// <summary>
    /// 初始化空间哈希网格
    /// </summary>
    /// <param name="bounds">网格边界（左下角和右上角坐标）</param>
    /// <param name="dimensions">网格维度（x和y方向的格子数量）</param>
    public SpatialHashGrid(Vector2[] bounds, Vector2Int dimensions)
    {
        this.bounds = bounds;
        this.dimensions = dimensions;
        this.queryIds = 0;
        // 初始化单元格数组，每个单元格创建一个实体列表
        this.cells = new List<ISpatialHashGridClient>[dimensions.x * dimensions.y];
        //挨个对网格进行初始化
        for (int i = 0; i < cells.Length; i++)
            cells[i] = new List<ISpatialHashGridClient>();
    }

    /// <summary>
    /// 以指定位置为中心重建网格（保持原大小）
    /// </summary>
    //在玩家靠近单元格边缘时会在EntityManager中调用Rebuild
    public void Rebuild(Vector2 position)
    {
        Vector2 size = bounds[1] - bounds[0];
        // 计算新边界（以position为中心，保持原大小）
        Rebuild(new Vector2[] { position - size / 2, position + size / 2 }, dimensions);
    }

    /// <summary>
    /// 重建网格（更新边界和维度）
    /// </summary>
    /// <param name="bounds">新的网格边界</param>
    /// <param name="dimensions">新的网格维度</param>
    public void Rebuild(Vector2[] bounds, Vector2Int dimensions)
    {
        // 提取所有旧实体，
        //使用HashSet，而不使用List<>的原因，一个实体可能跨多个网格单元，因此可能会被多个网格遍历到
        //而HashSet会自动去重，确保每个实体只被保存一次
        HashSet<ISpatialHashGridClient> oldClients = new HashSet<ISpatialHashGridClient>();
        for (int i = 0; i < cells.Length; i++)
        {
            foreach (ISpatialHashGridClient client in cells[i])
            {
                oldClients.Add(client);
            }
        }

        // 重新初始化单元格
        this.bounds = bounds;
        this.dimensions = dimensions;
        this.cells = new List<ISpatialHashGridClient>[dimensions.x * dimensions.y];
        for (int i = 0; i < cells.Length; i++)
            cells[i] = new List<ISpatialHashGridClient>();

        // 将旧实体重新插入新网格
        foreach (ISpatialHashGridClient oldClient in oldClients)
        {
            InsertClient(oldClient);
        }
    }

    /// <summary>
    /// 查找指定矩形范围内的所有实体
    /// </summary>
    /// <param name="position">矩形中心位置</param>
    /// <param name="size">矩形大小（宽和高）</param>
    /// <returns>范围内的实体列表</returns>
    public List<ISpatialHashGridClient> FindNearby(Vector2 position, Vector2 size)
    {
        // 计算矩形左下角和右上角对应的网格索引
        Vector2Int i1 = GetCellIndex(position.x - size.x / 2.0f, position.y - size.y / 2.0f);
        Vector2Int i2 = GetCellIndex(position.x + size.x / 2.0f, position.y + size.y / 2.0f);

        List<ISpatialHashGridClient> nearbyClients = new List<ISpatialHashGridClient>();
        int queryId = queryIds++; // 生成唯一查询ID

        // 遍历范围内的所有网格单元格
        for (int x = i1.x, xn = i2.x; x <= xn; x++)
        {
            for (int y = i1.y, yn = i2.y; y <= yn; y++)
            {
                int cellIndex = CellToIndex(x, y);
                // 收集单元格内的实体（避免重复）
                foreach (ISpatialHashGridClient client in cells[cellIndex])
                {
                    if (client.QueryID != queryId)
                    {
                        client.QueryID = queryId;
                        nearbyClients.Add(client);
                    }
                }
            }
        }

        return nearbyClients;
    }

    /// <summary>
    /// 查找指定圆形范围内的所有实体
    /// </summary>
    /// <param name="position">圆心位置</param>
    /// <param name="radius">圆半径</param>
    /// <returns>范围内的实体列表</returns>
    public List<ISpatialHashGridClient> FindNearbyInRadius(Vector2 position, float radius)
    {
        // 计算圆边界对应的网格索引范围
        int xMin = GetCellXIndex(position.x - radius);
        int xMax = GetCellXIndex(position.x + radius);
        int yMin = GetCellYIndex(position.y - radius);
        int yMax = GetCellYIndex(position.y + radius);

        List<ISpatialHashGridClient> nearbyClients = new List<ISpatialHashGridClient>();
        int queryId = queryIds++; // 生成唯一查询ID

        // 遍历矩形范围内的所有网格单元格
        for (int x = xMin; x <= xMax; x++)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                int cellIndex = CellToIndex(x, y);
                // 收集单元格内且实际在圆范围内的实体（避免重复）
                foreach (ISpatialHashGridClient client in cells[cellIndex])
                {
                    //增加额外条件，来判断是否在圈外
                    if (client.QueryID != queryId && Vector2.Distance(client.Position, position) < radius)
                    {
                        client.QueryID = queryId;
                        nearbyClients.Add(client);
                    }
                }
            }
        }

        return nearbyClients;
    }

    /// <summary>
    /// 查找指定圆形范围内最近的实体
    /// </summary>
    /// <param name="position">圆心位置</param>
    /// <param name="radius">圆半径</param>
    /// <returns>最近的实体（无则返回null）</returns>
    public ISpatialHashGridClient FindClosestInRadius(Vector2 position, float radius)
    {
        List<ISpatialHashGridClient> nearby = FindNearbyInRadius(position, radius);
        if (nearby.Count == 0) return null;

        // 查找最近实体
        int closestIdx = 0;
        float minDistance = Vector2.Distance(nearby[closestIdx].Position, position);
        for (int i = 1; i < nearby.Count; i++)
        {
            float distance = Vector2.Distance(nearby[i].Position, position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestIdx = i;
            }
        }
        return nearby[closestIdx];
    }

    /// <summary>
    /// 将实体插入网格（根据实体位置和大小分配到对应单元格）
    /// </summary>
    /// <param name="client">要插入的实体</param>
    public void InsertClient(ISpatialHashGridClient client)
    {
        // 计算实体边界对应的网格索引范围
        // GetCellIndex函数是根据坐标计算出的该坐标所在网格的新索引值
        //计算client的左下角和右上角坐标，并带入GetCellIndex得到各坐标对应的网格坐标型索引值
        Vector2Int i1 = GetCellIndex(client.Position.x - client.Size.x / 2.0f, client.Position.y - client.Size.y / 2.0f);
        Vector2Int i2 = GetCellIndex(client.Position.x + client.Size.x / 2.0f, client.Position.y + client.Size.y / 2.0f);

        client.ListIndexByCellIndex = new Dictionary<int, int>();

        // 将实体添加到所有覆盖的单元格
        for (int x = i1.x, xn = i2.x; x <= xn; x++)
        {
            for (int y = i1.y, yn = i2.y; y <= yn; y++)
            {
                //转化为单元格数组索引
                int cellIndex = CellToIndex(x, y);
                cells[cellIndex].Add(client);
                // 记录实体在该单元格中的索引
                client.ListIndexByCellIndex[cellIndex] = cells[cellIndex].Count - 1;
            }
        }
    }

    /// <summary>
    /// 更新实体在网格中的位置（如果实体移动超出原单元格范围，则重新分配）
    /// </summary>
    /// <param name="client">要更新的实体</param>
    public void UpdateClient(ISpatialHashGridClient client)
    {
        // 计算实体当前边界对应的网格索引范围
        Vector2Int i1 = GetCellIndex(client.Position.x - client.Size.x / 2.0f, client.Position.y - client.Size.y / 2.0f);
        Vector2Int i2 = GetCellIndex(client.Position.x + client.Size.x / 2.0f, client.Position.y + client.Size.y / 2.0f);

        int cellIndexMin = CellToIndex(i1.x, i1.y);
        int cellIndexMax = CellToIndex(i2.x, i2.y);

        // 如果实体仍在原单元格范围内，则无需更新
        if (client.ListIndexByCellIndex.ContainsKey(cellIndexMin) && client.ListIndexByCellIndex.ContainsKey(cellIndexMax))
            return;

        // 否则移除后重新插入
        RemoveClient(client);
        InsertClient(client);
    }

    /// <summary>
    /// 从网格中移除实体
    /// </summary>
    /// <param name="client">要移除的实体</param>
    public void RemoveClient(ISpatialHashGridClient client)
    {
        // 遍历实体所在的所有单元格
        foreach (var indices in client.ListIndexByCellIndex)
        {
            int cellIndex = indices.Key;
            int entityIndexInCell = indices.Value;
            int lastIndexInCell = cells[cellIndex].Count - 1;

            // 优化移除：用最后一个元素覆盖当前元素，再删除最后一个（避免列表移位）
            ISpatialHashGridClient lastEntityInCell = cells[cellIndex][lastIndexInCell];
            if (lastEntityInCell != client)
            {
                lastEntityInCell.ListIndexByCellIndex[cellIndex] = entityIndexInCell;
                cells[cellIndex][entityIndexInCell] = lastEntityInCell;
            }
            // 移除最后一个元素
            cells[cellIndex].RemoveAt(lastIndexInCell);
        }
    }

    /// <summary>
    /// 判断实体是否靠近网格边缘
    /// </summary>
    /// <param name="client">要判断的实体</param>
    /// <returns>是否靠近边缘</returns>
    public bool CloseToEdge(ISpatialHashGridClient client)
    {
        Vector2Int i1 = GetCellIndex(client.Position.x - client.Size.x / 2.0f, client.Position.y - client.Size.y / 2.0f);
        Vector2Int i2 = GetCellIndex(client.Position.x + client.Size.x / 2.0f, client.Position.y + client.Size.y / 2.0f);
        // 检查是否触达网格的四个边缘
        return i1.x == 0 || i1.y == 0 || i2.x == dimensions.x - 1 || i2.y == dimensions.y - 1;
    }

    /// <summary>
    /// 根据世界坐标计算对应的网格索引（x和y）
    /// </summary>
    /// <param name="xPosition">世界x坐标</param>
    /// <param name="yPosition">世界y坐标</param>
    /// <returns>网格索引（x,y）</returns>
    private Vector2Int GetCellIndex(float xPosition, float yPosition)
    {
        // 将坐标归一化到[0,1]范围（相对于网格边界）
        float u = Mathf.Clamp01((xPosition - bounds[0][0]) / (bounds[1][0] - bounds[0][0]));
        float v = Mathf.Clamp01((yPosition - bounds[0][1]) / (bounds[1][1] - bounds[0][1]));

        // 计算网格索引（限制在维度范围内）
        //向下取整
        int xIndex = Mathf.FloorToInt(u * (dimensions.x - 1));
        int yIndex = Mathf.FloorToInt(v * (dimensions.y - 1));

        return new Vector2Int(xIndex, yIndex);
    }

    /// <summary>
    /// 根据世界x坐标计算对应的网格x索引
    /// </summary>
    private int GetCellXIndex(float xPosition)
    {
        //bounds[0]表示边界左下角坐标，bounds[0][0]表示左下角坐标的x值
        //Clamp01将输入值限制在 [0, 1] 范围内
        float u = Mathf.Clamp01((xPosition - bounds[0][0]) / (bounds[1][0] - bounds[0][0]));
        int xIndex = Mathf.FloorToInt(u * (dimensions.x - 1));
        return xIndex;
    }

    /// <summary>
    /// 根据世界y坐标计算对应的网格y索引
    /// </summary>
    private int GetCellYIndex(float yPosition)
    {
        float v = Mathf.Clamp01((yPosition - bounds[0][1]) / (bounds[1][1] - bounds[0][1]));
        int yIndex = Mathf.FloorToInt(v * (dimensions.y - 1));
        return yIndex;
    }

    /// <summary>
    /// 将网格（x,y）索引转换为单元格数组的索引
    /// </summary>
    private int CellToIndex(int x, int y)
    {
        return x + y * dimensions.x;
    }
}