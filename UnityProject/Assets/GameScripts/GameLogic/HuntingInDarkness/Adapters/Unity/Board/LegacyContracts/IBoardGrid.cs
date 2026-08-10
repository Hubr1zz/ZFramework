using System.Collections.Generic;
using UnityEngine;

namespace GameplayBase.Board
{
    public interface IBoardGrid
    {
        bool IsValidCoord(Vector2Int coord);
        List<Vector2Int> GetNeighbors(Vector2Int coord);
        int GetDistance(Vector2Int a, Vector2Int b);
        List<Vector2Int> GetInRange(Vector2Int center, int range);
        List<Vector2Int> GetAllCoords();
        Vector3 CoordToWorld(Vector2Int coord, float cellSize);
    }
}
