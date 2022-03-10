using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections;

[ExecuteAlways]
public class UILineRenderer : Graphic
{
    [Min(0.001f)] public float thickness = .015f;
    public bool endCaps = true;
    public bool scale = true;

    private int resolution;
    private float2[] positions;
    private Vector2 startPoint;
    private Vector2 endPoint;
    private int endCapResolution = 16;
    private float2[] vertexPositions;

    protected override void OnValidate() => CalculateVertexPositions();
    
    // Doesn't ever get called. Doesn't work with RectTransform? Bug?
    // protected override void OnTransformParentChanged()
    // {
    //     if (scale)
    //         CalculateVertexPositions();
    // }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float2 Rotate90(float2 vector)
    {
        float theta = math.acos(math.dot(vector, new float2(1, 0)));
        
        if (vector.y < 0)
            return new float2(math.sin(theta), math.cos(theta));
        else
            return new float2(-math.sin(theta), math.cos(theta));
    }
    
    [BurstCompile]
    private struct ScaleJob : IJobParallelFor
    {
        [ReadOnly] public float2 scale;
        [ReadOnly] public NativeArray<float2> points;
        public NativeArray<float2> result;

        public void Execute(int index)
        {
            result[index] = points[index] * scale;
        }
    }
    
    [BurstCompile]
    private struct NormalsJob : IJobParallelFor
    {
        [ReadOnly] public int resolution;
        [ReadOnly] public NativeArray<float2> points;
        public NativeArray<float2> result;

        public void Execute(int index)
        {
            int last = math.max(index - 1, 0);
            float2 lastTan = math.normalize(points[last + 1] - points[last]);
            float2 lastNormal = Rotate90(lastTan);
            int next = math.min(index + 1, resolution - 1);
            float2 nextTan = math.normalize(points[next] - points[next - 1]);
            float2 nextNormal = Rotate90(nextTan);
            result[index] = (lastNormal + nextNormal) * 0.5f;
        }
    }
    
    [BurstCompile]
    private struct VertPositionsJob : IJobParallelFor
    {
        [ReadOnly] public float width;
        [ReadOnly] public NativeArray<float2> points;
        [ReadOnly] public NativeArray<float2> normals;
        [NativeDisableParallelForRestriction] public NativeArray<float2> result;

        public void Execute(int index)
        {
            result[index * 2] = points[index] + normals[index] * width;
            result[index * 2 + 1] = points[index] - normals[index] * width; 
        }
    }
    
    private struct Vector2ToFloat2Job : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Vector2> vector2s;
        public NativeArray<float2> result;

        public void Execute(int index)
        {
            result[index] = vector2s[index];
        }
    }
    
    public void SetPoints(Vector2[] points)
    {
        int count = points.Length;
        if (count < 2)
        {
            Debug.LogWarning("Positions array must have at least 2 elements.");
            return;
        }
        
        resolution = count;

        NativeArray<Vector2> vector2s = new NativeArray<Vector2>(points, Allocator.TempJob);
        NativeArray<float2> float2s = new NativeArray<float2>(resolution, Allocator.TempJob);
        var vector2ToFloat2Job = new Vector2ToFloat2Job();
        vector2ToFloat2Job.vector2s = vector2s;
        vector2ToFloat2Job.result = float2s;
        vector2ToFloat2Job.Schedule(resolution, 32).Complete();
        this.positions = float2s.ToArray();
        vector2s.Dispose();
        float2s.Dispose();
        CalculateVertexPositions();
    }
    
    public void SetPoints(float2[] points)
    {
        int count = points.Length;
        if (count < 2)
        {
            Debug.LogWarning("Positions array must have at least 2 elements.");
            return;
        }
        
        resolution = count;
        this.positions = points;
        CalculateVertexPositions();
    }
    
    // adding a context menu option because OnTransformParentChanged event doesn't trigger
    [ContextMenu("Calculate Vertex Positions")]
    private void CalculateVertexPositions()
    {
        if (positions == null)
            return;

        if (positions.Length == 0)
            return;

        NativeArray<float2> points;
        var normals = new NativeArray<float2>(resolution, Allocator.TempJob);
        var vertPositions = new NativeArray<float2>(resolution * 2, Allocator.TempJob);
        
        if (scale)
        {
            NativeArray<float2> pos = new NativeArray<float2>(positions, Allocator.TempJob);
            points = new NativeArray<float2>(resolution, Allocator.TempJob);
            var scaleJob = new ScaleJob();
            scaleJob.scale = rectTransform.rect.size;
            scaleJob.points = pos;
            scaleJob.result = points;
            scaleJob.Schedule(resolution, 32).Complete();
            pos.Dispose();
        }
        else
        {
            points = new NativeArray<float2>(positions, Allocator.TempJob);
        }

        var normalsJob = new NormalsJob();
        normalsJob.resolution = resolution;
        normalsJob.points = points;
        normalsJob.result = normals;
        var normalsHandle = normalsJob.Schedule(resolution, 32);

        var vertPositionsJob = new VertPositionsJob();
        vertPositionsJob.width = thickness;
        vertPositionsJob.points = points;
        vertPositionsJob.normals = normals;
        vertPositionsJob.result = vertPositions;
        var vertPositionsHandle = vertPositionsJob.Schedule(resolution, 32, normalsHandle);

        vertPositionsHandle.Complete();
        startPoint = points[0];
        endPoint = points[resolution - 1];
        vertexPositions = vertPositions.ToArray();

        points.Dispose();
        normals.Dispose();
        vertPositions.Dispose();

        SetVerticesDirty();
    }

    private Vector2 OffsetFromPivot(Vector2 pos, bool scaleOffset = false)
    {
        return scaleOffset ? pos - (rectTransform.pivot * rectTransform.rect.size) : pos - rectTransform.pivot;
    }

    private Vector2 Scale(Vector2 pos)
    {
        return Vector2.Scale(pos, rectTransform.rect.size);
    }
    
    private static Vector2 Rotate(Vector2 v, float angle) 
    {
        float sin = Mathf.Sin(angle);
        float cos = Mathf.Cos(angle);
        float tx = v.x;
        float ty = v.y;
        return new Vector2(cos * tx - sin * ty, sin * tx + cos * ty);
    }
    
    // Draws a half-circle in a clockwise direction. edgeStartPoint is a point on the circumference
    private void DoEndCap(ref VertexHelper vh, int startIndex, Vector2 center, Vector2 edgeStartPoint)
    {
        var vert = new UIVertex();
        vert = UIVertex.simpleVert;
        vert.position = OffsetFromPivot(center, true);
        vert.color = color;
        vh.AddVert(vert);
        Vector2 r = edgeStartPoint - center;
        float delta = -1.0f / (endCapResolution - 1.0f) * Mathf.PI;

        for (int i = 0; i < endCapResolution; i++)
        {
            float theta = i * delta;
            vert.position = OffsetFromPivot(center + Rotate(r, theta), true);
            vert.color = color;
            vh.AddVert(vert);
        }
        
        for (int i = 0; i < endCapResolution - 1; i++)
        {
            int j = startIndex + i;
            vh.AddTriangle(startIndex, j + 1, j + 2);
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        
        if (vertexPositions == null)
            return;

        if (vertexPositions.Length == 0)
            return;
        
        var vert = new UIVertex();
        vert = UIVertex.simpleVert;
        
        if (endCaps)
            DoEndCap(ref vh, 0, startPoint, vertexPositions[1]);

        for (int i = 0; i < resolution * 2; i++)
        {
            vert.position = OffsetFromPivot(vertexPositions[i], true);
            vert.color = color;
            vh.AddVert(vert);
        }

        int start = endCaps ? endCapResolution + 1 : 0;
        for (int i = 0; i < resolution - 1; i++)
        {
            int j = start + i * 2;
            vh.AddTriangle(j, j + 2, j + 3);
            vh.AddTriangle(j, j + 3, j + 1);
        }
        
        if (endCaps)
            DoEndCap(ref vh, endCapResolution + 1 + resolution * 2, endPoint, vertexPositions[resolution * 2 - 2]);
    }
}