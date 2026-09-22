using Unity.Mathematics;
using UnityEngine;

namespace RacingSim.Damage
{
    // ====================================================================
    // ENUMS
    // ====================================================================

    public enum DamageZoneType
    {
        Rigid,
        PanelDeform,
        NodeBeam,
        Fracture
    }

    public enum BeamType
    {
        Structural,
        Support,
        Panel,
        Attachment
    }

    // ====================================================================
    // ZONE CONFIGURATION
    // ====================================================================

    [System.Serializable]
    public struct DamageZoneConfig
    {
        public string ZoneName;
        public DamageZoneType Type;

        [Header("Deformation Parameters")]
        public float ElasticLimit;
        public float PlasticThreshold;
        public float BreakThreshold;
        public float Stiffness;
        public float DampingRatio;

        [Header("Visual Parameters")]
        public float MaxDeformDepth;
        public float DeformRadius;
        public float WrinkleIntensity;

        [Header("Fracture (Level 3)")]
        public int FragmentCount;
        public float FragmentMass;

        [Header("Node-Beam (Level 2)")]
        public int NodeCount;
        public int BeamCount;
        public float BeamBreakForce;

        [Header("Attachment")]
        public float AttachmentStrength;
    }

    // ====================================================================
    // NODE-BEAM STRUCTS
    // ====================================================================

    public struct DeformNode
    {
        public float3 Position;
        public float3 OriginalPosition;
        public float3 Velocity;
        public float3 Force;
        public float Mass;

        public float AccumulatedDamage;
        public bool IsBroken;
        public float PlasticOffset;

        public int ZoneIndex;
    }

    public struct DeformBeam
    {
        public int NodeA;
        public int NodeB;
        public float RestLength;
        public float CurrentLength;
        public float Stiffness;
        public float Damping;
        public float BreakForce;
        public float CurrentStress;
        public bool IsBroken;
        public BeamType Type;
    }

    // ====================================================================
    // PANEL CONTROL POINTS
    // ====================================================================

    public struct PanelControlPoint
    {
        public float3 Position;
        public float3 OriginalPosition;
        public float3 Velocity;
        public float Mass;
        public float Stiffness;
        public float Damping;
        public float Damage;
        public bool IsDetached;
        public int AttachedToZone;
    }

    public struct PanelMeshBinding
    {
        public int VertexIndex;
        public int[] ControlPointIndices;
        public float[] Weights;
    }

    // ====================================================================
    // VERTEX BINDING (for NodeBeam skinning)
    // ====================================================================

    public struct VertexNodeBinding
    {
        public int VertexIndex;
        public int NodeIndex0;
        public int NodeIndex1;
        public int NodeIndex2;
        public float Weight0;
        public float Weight1;
        public float Weight2;
        public int Count;
    }

    // ====================================================================
    // IMPACT DATA
    // ====================================================================

    public struct ImpactData
    {
        public float3 ImpactPoint;
        public float3 ImpactNormal;
        public float3 ImpactVelocity;
        public float ImpactSpeed;
        public float ImpactEnergy;
        public float EffectiveMass;
        public int HitZoneIndex;
    }

    // ====================================================================
    // FRACTURE STRUCTS
    // ====================================================================

    public struct AttachmentPoint
    {
        public float3 Position;
        public float BreakForce;
        public bool IsBroken;
        public float BreakTime;
    }

    public struct FragmentData
    {
        public GameObject MeshObject;
        public Rigidbody RigidBody;
        public float Mass;
        public float3 Center;
    }

    // ====================================================================
    // MESH VERTICES BUFFER
    // ====================================================================

    public struct MeshVertex
    {
        public float3 Position;
        public float3 OriginalPosition;
        public float3 DeformationOffset;
        public float3 Normal;
    }
}
