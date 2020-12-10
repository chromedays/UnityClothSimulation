using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClothGrid : MonoBehaviour
{
    public class Point
    {
        public float Mass = 1;
        public Vector3 Acceleration = new Vector3();
        public Vector3 Pos;
        public Vector3 OldPos;
        public float Damping;
        public Transform TargetSphere;
        public float TargetSphereRadius;
        public float TargetSphereRadiusSquared;
        public bool Movable = true;

        public void Init(Transform targetSphere, float damping, Vector3 initialPosition)
        {
            Damping = damping;
            Pos = initialPosition;
            OldPos = initialPosition;
            TargetSphere = targetSphere;
            TargetSphereRadius = TargetSphere.localScale.x * 0.6f;
            TargetSphereRadiusSquared = TargetSphereRadius * TargetSphereRadius;
        }

        // Update is called once per frame
        public void Update()
        {
            if (!Movable)
                return;
            var temp = Pos;
            Pos += (Pos - OldPos) * (1 - Damping) + Acceleration * Time.deltaTime;
            OldPos = temp;
            Acceleration.Set(0, 0, 0);

            var distance = Pos - TargetSphere.position;
            if (distance.sqrMagnitude < TargetSphereRadiusSquared)
            {
                Pos = TargetSphere.position + distance.normalized * Mathf.Sqrt(TargetSphereRadiusSquared);
            }
        }

        public void AddForce(Vector3 force)
        {
            Acceleration += force / Mass;
        }
    }


    public class Constraint
    {
        public Point A;
        public Point B;
        public float RestDistance;

        public Constraint(Point a, Point b)
        {
            A = a;
            B = b;
            RestDistance = (B.Pos - A.Pos).magnitude;
        }

        public void Update()
        {
            var atob = B.Pos - A.Pos;
            float distance = atob.magnitude;
            var correctionVectorHalf = atob * (1 - RestDistance / distance) * 0.5f;
            if (A.Movable)
            {
                A.Pos = A.Pos + correctionVectorHalf;
            }

            if (B.Movable)
            {
                B.Pos = B.Pos - correctionVectorHalf;
            }
        }
    }

    public int Row;
    public int Col;

    public Point[] Particles;
    public List<Constraint> Constraints = new List<Constraint>();
    public float RestDistance = 3f;
    public float Damping = 0.005f;
    public Transform TargetSphere;
    public Mesh ClothMesh;
    public bool WindEnabled = false;
    public Vector3 WindForce = new Vector3(30, 0, -30);

    void Start()
    {
        var startPos = new Vector3(-(Row - 1) * RestDistance * 0.5f, 0, (Col - 1) * RestDistance * 0.5f);
        Particles = new Point[Row * Col];
        for (int i = 0; i < Particles.Length; ++i)
        {
            Particles[i] = new Point();
            Particles[i].Init(TargetSphere, Damping, transform.position + startPos + new Vector3((i % Col) * RestDistance, 0, (i / Col) * -RestDistance));
            if (i / Col == 0)
            {
                Particles[i].Movable = false;
            }
        }

        for (int i = 0; i < Row; ++i)
        {
            for (int j = 0; j < Col; ++j)
            {
                if (j < Col - 1)
                {
                    Constraints.Add(new Constraint(Particles[i * Col + j], Particles[i * Col + j + 1]));
                }
                if (i < Row - 1)
                {
                    Constraints.Add(new Constraint(Particles[i * Col + j], Particles[(i + 1) * Col + j]));
                }

                if (i < Row - 1 && j < Col - 1)
                {
                    Constraints.Add(new Constraint(Particles[i * Col + j], Particles[(i + 1) * Col + j + 1]));
                    Constraints.Add(new Constraint(Particles[i * Col + j + 1], Particles[(i + 1) * Col + j]));
                }
            }
        }

        for (int i = 0; i < Row; ++i)
        {
            for (int j = 0; j < Col; ++j)
            {
                if (i < Row - 2)
                {
                    Constraints.Add(new Constraint(Particles[i * Col + j], Particles[(i + 2) * Col + j]));
                }

                if (j < Col - 2)
                {
                    Constraints.Add(new Constraint(Particles[i * Col + j], Particles[i * Col + j + 2]));
                }

                if (i < Row - 2 && j < Col - 2)
                {

                    Constraints.Add(new Constraint(Particles[i * Col + j], Particles[(i + 2) * Col + j + 2]));
                    Constraints.Add(new Constraint(Particles[i * Col + j + 2], Particles[(i + 2) * Col + j]));
                }
            }
        }

        ClothMesh = new Mesh();
        UpdateMesh();
        GetComponent<MeshFilter>().mesh = ClothMesh;
    }

    void UpdateMesh()
    {
        var startPos = new Vector3(-(Row - 1) * RestDistance * 0.5f, 0, (Col - 1) * RestDistance * 0.5f);
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var indices = new List<int>();

        for (int i = 0; i < Row; ++i)
        {
            for (int j = 0; j < Col; ++j)
            {
                vertices.Add(Particles[i * Col + j].Pos - transform.position);
                uvs.Add(new Vector2((float)j / Col, (float)i / Row));
                if (i < Row - 1 && j < Col - 1)
                {
                    indices.Add(i * Col + j);
                    indices.Add(i * Col + j + 1);
                    indices.Add((i + 1) * Col + j + 1);
                    indices.Add((i + 1) * Col + j + 1);
                    indices.Add((i + 1) * Col + j);
                    indices.Add(i * Col + j);
                }
            }
        }

        ClothMesh.Clear();
        ClothMesh.vertices = vertices.ToArray();
        ClothMesh.triangles = indices.ToArray();
        ClothMesh.Optimize();
        ClothMesh.RecalculateNormals();
    }

    void applyWind(Point p0, Point p1, Point p2, Vector3 windForce)
    {
        var normal = Vector3.Cross(p2.Pos - p0.Pos, p1.Pos - p0.Pos).normalized;
        var force = normal * Vector3.Dot(normal, windForce);
        p0.AddForce(force);
        p1.AddForce(force);
        p2.AddForce(force);
    }

    // Update is called once per frame
    void Update()
    {
        if (WindEnabled)
        {
            for (int i = 0; i < Row - 1; ++i)
            {
                for (int j = 0; j < Col - 1; ++j)
                {
                    applyWind(Particles[i * Col + j], Particles[i * Col + j + 1], Particles[(i + 1) * Col + j + 1], WindForce * Time.deltaTime);
                    applyWind(Particles[(i + 1) * Col + j + 1], Particles[(i + 1) * Col + j + 1], Particles[i * Col + j], WindForce * Time.deltaTime);
                }
            }
        }

        foreach (var particle in Particles)
        {
            particle.AddForce(new Vector3(0, -9.81f * Time.deltaTime, 0));
            
            particle.Update();
        }

        for (int i = 0; i < 10; ++i)
        {
            foreach (var constraint in Constraints)
            {
                constraint.Update();
            }
        }

        UpdateMesh();
    }

    private void OnGUI()
    {
        WindEnabled = GUILayout.Toggle(WindEnabled, "Toggle wind");
    }
}
