using System;
using UnityEngine;

namespace Vespertine.Save
{
    /// <summary>Generic per-entity snapshot used by quick save / checkpoints.</summary>
    [Serializable]
    public class EntityState
    {
        public string Id;
        public bool Active = true;
        public bool B0, B1, B2, B3;
        public float F0, F1, F2;
        public int I0, I1;
        public string S0, S1;
        public Vector3 P;
        public float Yaw;
    }
}
