using UnityEngine;

namespace Vespertine.Level
{
    /// <summary>Base for anything spawned from a map entity line.</summary>
    public class Entity : MonoBehaviour
    {
        public string Id;
        public string Group;
        public EntitySpec Spec;

        public virtual void Init(EntitySpec spec)
        {
            Spec = spec;
            Id = spec.Id;
            Group = spec.Group;
        }

        /// <summary>Called after all entities are spawned (cross references resolved here).</summary>
        public virtual void Link() { }
    }
}
