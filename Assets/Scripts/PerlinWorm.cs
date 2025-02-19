using System;
using UnityEngine;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections;
using UnityEditor;

namespace WormsDemo
{
    [RequireComponent(typeof(UILineRenderer))]
    public class PerlinWorm : MonoBehaviour
    {
        [Min(1)] public int segments = 10;
        [Min(0.01f)] public float segmentLength = 0.1f;
        public float moveSpeed = 0.1f;
        public float squirmSpeed = 0.1f;
        [Range(0, 1)] public float twistiness = 0.35f;

        private UILineRenderer uiLineRenderer;
        private float3 head;
        private float3[] wormPoints;
        private float2[] linePoints;

        private float3 moveVector;
        private float3 squirmVector;
        private float2 right = new float2(1.0f, 0.0f);

        private void OnValidate()
        {
            if (EditorApplication.isPlaying)
                CreatePoints();

            moveVector = new float3(moveSpeed * 0.001f, 0, 0);
            squirmVector = new float3(0, 0, squirmSpeed);
        }

        private void Awake()
        {
            uiLineRenderer = GetComponent<UILineRenderer>();
            head = new float3(0.5f, 0.5f, 0);
        }

        private void CreatePoints()
        {
            wormPoints = new float3[segments + 1];
            linePoints = new float2[segments + 1];
        }

        private void Start()
        {
            CreatePoints();
        }
        
        private static float2 Rotate(float2 v, float angle) 
        {
            float sin = math.sin(angle);
            float cos = math.cos(angle);
            float tx = v.x;
            float ty = v.y;
            return new float2(cos * tx - sin * ty, sin * tx + cos * ty);
        }

        private void FixedUpdate()
        {
            head += moveVector;
            head += squirmVector;
            wormPoints[0] = head;
            linePoints[0] = head.xy;
            float angle = noise.cnoise(head) * 2 * math.PI * twistiness;
            
            for (int i = 1; i <= segments; i++)
            {
                float3 prev = wormPoints[i - 1];
                float2 pos = prev.xy + Rotate(right * segmentLength, angle);
                angle += noise.cnoise(prev) * 2 * math.PI * twistiness;
                linePoints[i] = pos;
                wormPoints[i] = new float3(pos, prev.z);
            }
            
            uiLineRenderer.SetPositions(linePoints);
        }
    }
}