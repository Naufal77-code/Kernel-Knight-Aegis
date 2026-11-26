using UnityEngine;

namespace KernelKnight.Combat
{
    [RequireComponent(typeof(Collider2D))]
    public class PogoObstacle : MonoBehaviour, IPogoable
    {
        [Header("Pogo Reaction")]
        [SerializeField] private bool squishOnBounce = true;
        [SerializeField] private Color flashColor = Color.cyan;

        private SpriteRenderer sr;
        private MeshRenderer mr;
        private Color originalColor = Color.white;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            mr = GetComponent<MeshRenderer>();

            if (sr != null) originalColor = sr.color;
            else if (mr != null && mr.sharedMaterial != null) originalColor = mr.sharedMaterial.color;
        }

        public bool OnPogoHit(Vector2 hitPoint)
        {
            Debug.Log($"<color=cyan><b>[POGO OBSTACLE]</b> Bit successfully bounced on {gameObject.name}!</color>");
            if (squishOnBounce)
            {
                StartCoroutine(SquishFeedbackRoutine());
            }
            return true;
        }

        private System.Collections.IEnumerator SquishFeedbackRoutine()
        {
            Vector3 origScale = transform.localScale;
            transform.localScale = new Vector3(origScale.x * 1.25f, origScale.y * 0.75f, origScale.z);
            SetColor(flashColor);

            yield return new WaitForSeconds(0.12f);

            transform.localScale = origScale;
            SetColor(originalColor);
        }

        private void SetColor(Color c)
        {
            if (sr != null) sr.color = c;
            else if (mr != null && mr.material != null) mr.material.color = c;
        }
    }
}
