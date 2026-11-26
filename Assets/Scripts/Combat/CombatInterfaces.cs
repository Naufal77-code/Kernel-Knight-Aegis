using UnityEngine;

namespace KernelKnight.Combat
{
    public interface IDamageable
    {
        void TakeDamage(float damage, Vector2 knockbackDirection, float knockbackForce);
    }

    public interface IPogoable
    {
        /// <summary>
        /// Called when Bit lands a Pogo Slash on this target.
        /// Returns true if the pogo bounce was successfully triggered.
        /// </summary>
        bool OnPogoHit(Vector2 hitPoint);
    }

    public interface IParryable
    {
        /// <summary>
        /// Called when Bit parries this attack.
        /// </summary>
        void OnParried(GameObject parrier);
    }
}
